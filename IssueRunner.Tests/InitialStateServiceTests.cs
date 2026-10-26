using IssueRunner.Models;
using IssueRunner.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text;

namespace IssueRunner.Tests;

[TestFixture]
public class InitialStateServiceTests
{
    private IIssueDiscoveryService _issueDiscovery = null!;
    private IProjectAnalyzerService _projectAnalyzer = null!;
    private IProcessExecutor _processExecutor = null!;
    private ILogger<InitialStateService> _logger = null!;
    private string _testRoot = null!;
    private string _issueFolder = null!;

    [SetUp]
    public void SetUp()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _issueFolder = Path.Combine(_testRoot, "Issue228");
        Directory.CreateDirectory(_issueFolder);

        _issueDiscovery = Substitute.For<IIssueDiscoveryService>();
        _projectAnalyzer = Substitute.For<IProjectAnalyzerService>();
        _processExecutor = Substitute.For<IProcessExecutor>();
        _logger = Substitute.For<ILogger<InitialStateService>>();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, true);
        }
    }

    [Test]
    public async Task ProcessIssueAsync_ReturnsOk_WhenNoProjectFiles()
    {
        _projectAnalyzer.FindProjectFiles(Arg.Any<string>()).Returns(new List<string>());
        var service = new InitialStateService(_issueDiscovery, _projectAnalyzer, _processExecutor, _logger);

        var result = await service.ProcessIssueAsync(_testRoot, _issueFolder, 228, false, CancellationToken.None);

        Assert.That(result.Number, Is.EqualTo(228));
        Assert.That(result.Status, Is.EqualTo(InitialStateStatus.Ok));
        Assert.That(result.Messages, Has.Some.Contains("No project files"));
    }

    [Test]
    public async Task ProcessIssueAsync_ReconstructsFromGit_WhenNoInitialState()
    {
        var csprojPath = Path.Combine(_issueFolder, "Issue228.csproj");
        await File.WriteAllTextAsync(csprojPath, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""NUnit"" Version=""4.0.0"" />
  </ItemGroup>
</Project>");

        _projectAnalyzer.FindProjectFiles(Arg.Any<string>()).Returns(new List<string> { csprojPath });
        var gitOutput = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""NUnit"" Version=""3.14.0"" />
  </ItemGroup>
</Project>";
        _processExecutor.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((0, gitOutput, ""));
        _projectAnalyzer.ParseProjectContent(Arg.Any<string>())
            .Returns((new List<string> { "net8.0" }, new List<PackageInfo> { new PackageInfo { Name = "NUnit", Version = "3.14.0" } }));

        var service = new InitialStateService(_issueDiscovery, _projectAnalyzer, _processExecutor, _logger);

        var result = await service.ProcessIssueAsync(_testRoot, _issueFolder, 228, false, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(InitialStateStatus.Reconstructed));
        Assert.That(result.ProjectsUpdated, Has.Count.EqualTo(1));

        var statePath = Path.Combine(_issueFolder, "issue_initialstate.json");
        Assert.That(File.Exists(statePath), Is.True);
        var json = await File.ReadAllTextAsync(statePath);
        Assert.That(json, Does.Contain("net8.0"));
        Assert.That(json, Does.Contain("3.14.0"));
    }

    [Test]
    public async Task ProcessIssueAsync_ReturnsOk_WhenInitialStateExistsAndCoversAllProjects()
    {
        var csprojPath = Path.Combine(_issueFolder, "Issue228.csproj");
        await File.WriteAllTextAsync(csprojPath, "<Project/>");
        var statePath = Path.Combine(_issueFolder, "issue_initialstate.json");
        var existingState = new IssueInitialState
        {
            Number = 228,
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Projects =
            [
                new ProjectInitialState
                {
                    ProjectPath = "Issue228.csproj",
                    TargetFrameworks = ["net10.0"],
                    Packages = []
                }
            ]
        };
        await File.WriteAllTextAsync(statePath, System.Text.Json.JsonSerializer.Serialize(existingState, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        _projectAnalyzer.FindProjectFiles(Arg.Any<string>()).Returns(new List<string> { csprojPath });
        var service = new InitialStateService(_issueDiscovery, _projectAnalyzer, _processExecutor, _logger);

        var result = await service.ProcessIssueAsync(_testRoot, _issueFolder, 228, forceUpdate: false, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(InitialStateStatus.Ok));
        Assert.That(result.ProjectsUpdated, Is.Empty);
    }
}

using IssueRunner.Commands;
using IssueRunner.Models;
using IssueRunner.Services;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;

namespace IssueRunner.Tests;

[TestFixture]
public class ProjectFileDetectionTests
{
    private IIssueDiscoveryService? _issueDiscovery;
    private IProjectAnalyzerService? _projectAnalyzer;
    private IFrameworkUpgradeService? _frameworkUpgrade;
    private IPackageUpdateService? _packageUpdate;
    private ITestExecutionService? _testExecution;
    private ILogger<RunTestsCommand>? _logger;
    private ILoggerFactory? _loggerFactory;
    private IEnvironmentService? _environmentService;
    private INuGetPackageVersionService? _nugetVersions;
    private IMarkerService? _markerService;
    private RunTestsCommand? _command;
    private string? _tempDir;
    private string? _issueFolderPath;

    [SetUp]
    public void Setup()
    {
        _issueDiscovery = Substitute.For<IIssueDiscoveryService>();
        _projectAnalyzer = Substitute.For<IProjectAnalyzerService>();
        _frameworkUpgrade = Substitute.For<IFrameworkUpgradeService>();
        _packageUpdate = Substitute.For<IPackageUpdateService>();
        _testExecution = Substitute.For<ITestExecutionService>();
        _logger = Substitute.For<ILogger<RunTestsCommand>>();
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _environmentService = Substitute.For<IEnvironmentService>();
        _nugetVersions = Substitute.For<INuGetPackageVersionService>();
        _markerService = Substitute.For<IMarkerService>();

        _command = new RunTestsCommand(
            _issueDiscovery,
            _projectAnalyzer,
            _frameworkUpgrade,
            _packageUpdate,
            _testExecution,
            _logger,
            _loggerFactory,
            _nugetVersions,
            _environmentService,
            _markerService);

        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _issueFolderPath = Path.Combine(_tempDir, "Issue343");
        Directory.CreateDirectory(_issueFolderPath);

        // Default mock setup
        _markerService.ShouldSkipIssue(Arg.Any<string>()).Returns(false);
        _projectAnalyzer.FindProjectFiles(Arg.Any<string>()).Returns(new List<string>());
    }

    [TearDown]
    public void TearDown()
    {
        if (_tempDir != null && Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Test]
    public void GetProjectFilesForIssue_WithMetadata_ReturnsProjectFile()
    {
        // Arrange
        var projectPath = Path.Combine(_issueFolderPath!, "TestProject", "Test.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        File.WriteAllText(projectPath, "<Project></Project>");

        var metadata = new[]
        {
            new { number = 343, project_path = "TestProject/Test.csproj" }
        };
        var metadataJson = JsonSerializer.Serialize(metadata);
        var metadataPath = Path.Combine(_issueFolderPath!, "issue_metadata.json");
        File.WriteAllText(metadataPath, metadataJson);

        // Act - Use reflection to call private method
        var method = typeof(RunTestsCommand).GetMethod("GetProjectFilesForIssue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method!.Invoke(_command!, new object[] { _issueFolderPath!, 343 }) as List<string>;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(1));
        Assert.That(result[0], Is.EqualTo(projectPath).IgnoreCase);
    }

    [Test]
    public void GetProjectFilesForIssue_WithCaseMismatch_ReturnsProjectFileViaCaseInsensitiveSearch()
    {
        // Arrange - Metadata has uppercase, file has lowercase
        var projectPath = Path.Combine(_issueFolderPath!, "testproject", "test.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        File.WriteAllText(projectPath, "<Project></Project>");

        var metadata = new[]
        {
            new { number = 343, project_path = "TestProject/Test.csproj" } // Uppercase in metadata
        };
        var metadataJson = JsonSerializer.Serialize(metadata);
        var metadataPath = Path.Combine(_issueFolderPath!, "issue_metadata.json");
        File.WriteAllText(metadataPath, metadataJson);

        // Act
        var method = typeof(RunTestsCommand).GetMethod("GetProjectFilesForIssue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method!.Invoke(_command!, new object[] { _issueFolderPath!, 343 }) as List<string>;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(1));
        Assert.That(result[0], Is.EqualTo(projectPath).IgnoreCase);
    }

    [Test]
    public void GetProjectFilesForIssue_WithMetadataButFileMissing_FallsBackToRecursiveSearch()
    {
        // Arrange
        var projectPath = Path.Combine(_issueFolderPath!, "SubFolder", "Test.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        File.WriteAllText(projectPath, "<Project></Project>");

        var metadata = new[]
        {
            new { number = 343, project_path = "NonExistent/Test.csproj" } // Wrong path
        };
        var metadataJson = JsonSerializer.Serialize(metadata);
        var metadataPath = Path.Combine(_issueFolderPath!, "issue_metadata.json");
        File.WriteAllText(metadataPath, metadataJson);

        _projectAnalyzer!.FindProjectFiles(_issueFolderPath!).Returns(new List<string> { projectPath });

        // Act
        var method = typeof(RunTestsCommand).GetMethod("GetProjectFilesForIssue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method!.Invoke(_command!, new object[] { _issueFolderPath!, 343 }) as List<string>;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(1));
        Assert.That(result[0], Is.EqualTo(projectPath));
    }

    [Test]
    public void GetProjectFilesForIssue_WithoutMetadata_FallsBackToRecursiveSearch()
    {
        // Arrange
        var projectPath = Path.Combine(_issueFolderPath!, "Test.csproj");
        File.WriteAllText(projectPath, "<Project></Project>");

        _projectAnalyzer!.FindProjectFiles(_issueFolderPath!).Returns(new List<string> { projectPath });

        // Act
        var method = typeof(RunTestsCommand).GetMethod("GetProjectFilesForIssue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method!.Invoke(_command!, new object[] { _issueFolderPath!, 343 }) as List<string>;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(1));
        Assert.That(result[0], Is.EqualTo(projectPath));
    }

    [Test]
    public void GetProjectFilesForIssue_WithMultipleProjectsInMetadata_ReturnsAll()
    {
        // Arrange
        var project1Path = Path.Combine(_issueFolderPath!, "Project1", "Test1.csproj");
        var project2Path = Path.Combine(_issueFolderPath!, "Project2", "Test2.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(project1Path)!);
        Directory.CreateDirectory(Path.GetDirectoryName(project2Path)!);
        File.WriteAllText(project1Path, "<Project></Project>");
        File.WriteAllText(project2Path, "<Project></Project>");

        var metadata = new[]
        {
            new { number = 343, project_path = "Project1/Test1.csproj" },
            new { number = 343, project_path = "Project2/Test2.csproj" }
        };
        var metadataJson = JsonSerializer.Serialize(metadata);
        var metadataPath = Path.Combine(_issueFolderPath!, "issue_metadata.json");
        File.WriteAllText(metadataPath, metadataJson);

        // Act
        var method = typeof(RunTestsCommand).GetMethod("GetProjectFilesForIssue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method!.Invoke(_command!, new object[] { _issueFolderPath!, 343 }) as List<string>;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(2));
        Assert.That(result.Any(r => string.Equals(r, project1Path, StringComparison.OrdinalIgnoreCase)), Is.True);
        Assert.That(result.Any(r => string.Equals(r, project2Path, StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public void GetProjectFilesForIssue_WithWrongIssueNumber_ReturnsEmpty()
    {
        // Arrange
        var projectPath = Path.Combine(_issueFolderPath!, "Test.csproj");
        File.WriteAllText(projectPath, "<Project></Project>");

        var metadata = new[]
        {
            new { number = 999, project_path = "Test.csproj" } // Wrong issue number
        };
        var metadataJson = JsonSerializer.Serialize(metadata);
        var metadataPath = Path.Combine(_issueFolderPath!, "issue_metadata.json");
        File.WriteAllText(metadataPath, metadataJson);

        // Act
        var method = typeof(RunTestsCommand).GetMethod("GetProjectFilesForIssue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method!.Invoke(_command!, new object[] { _issueFolderPath!, 343 }) as List<string>;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(0));
    }

    [Test]
    public void GetProjectFilesForIssue_WithInvalidMetadata_ReturnsEmpty()
    {
        // Arrange
        var metadataPath = Path.Combine(_issueFolderPath!, "issue_metadata.json");
        File.WriteAllText(metadataPath, "invalid json {");

        // Act
        var method = typeof(RunTestsCommand).GetMethod("GetProjectFilesForIssue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method!.Invoke(_command!, new object[] { _issueFolderPath!, 343 }) as List<string>;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(0));
    }
}

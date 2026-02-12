using IssueRunner.Services;
using Microsoft.Extensions.Logging;
using System.IO;

namespace IssueRunner.Tests;

[TestFixture]
public class ScriptDetectionTests
{
    private IProcessExecutor? _processExecutor;
    private IProjectAnalyzerService? _projectAnalyzer;
    private ILogger<TestExecutionService>? _logger;
    private TestExecutionService? _service;
    private string? _tempDir;
    private string? _issueFolderPath;

    [SetUp]
    public void Setup()
    {
        _processExecutor = Substitute.For<IProcessExecutor>();
        _projectAnalyzer = Substitute.For<IProjectAnalyzerService>();
        _logger = Substitute.For<ILogger<TestExecutionService>>();
        _service = new TestExecutionService(_processExecutor, _projectAnalyzer, _logger);

        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _issueFolderPath = Path.Combine(_tempDir, "Issue343");
        Directory.CreateDirectory(_issueFolderPath);
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
    public void HasCustomRunners_WithRunTestCmd_ReturnsTrue()
    {
        // Arrange
        var scriptPath = Path.Combine(_issueFolderPath!, "run_test.cmd");
        File.WriteAllText(scriptPath, "@echo off\necho test");

        // Act
        var result = _service!.HasCustomRunners(_issueFolderPath!);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void HasCustomRunners_WithRunTestSh_ReturnsTrue()
    {
        // Arrange
        var scriptPath = Path.Combine(_issueFolderPath!, "run_test.sh");
        File.WriteAllText(scriptPath, "#!/bin/bash\necho test");

        // Act
        var result = _service!.HasCustomRunners(_issueFolderPath!);

        // Assert
        // On Windows, .sh files won't match, on Linux they will
        if (OperatingSystem.IsWindows())
        {
            Assert.That(result, Is.False);
        }
        else
        {
            Assert.That(result, Is.True);
        }
    }

    [Test]
    public void HasCustomRunners_WithRunTest0Cmd_ReturnsTrue()
    {
        // Arrange
        var scriptPath = Path.Combine(_issueFolderPath!, "run_test_0.cmd");
        File.WriteAllText(scriptPath, "@echo off\necho test");

        // Act
        var result = _service!.HasCustomRunners(_issueFolderPath!);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void HasCustomRunners_WithMultipleScripts_ReturnsTrue()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_issueFolderPath!, "run_test_0.cmd"), "@echo off");
        File.WriteAllText(Path.Combine(_issueFolderPath!, "run_test_1.cmd"), "@echo off");

        // Act
        var result = _service!.HasCustomRunners(_issueFolderPath!);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void HasCustomRunners_WithCaseMismatch_ReturnsTrue()
    {
        // Arrange - File has different case than pattern
        var scriptPath = Path.Combine(_issueFolderPath!, "RUN_TEST.CMD"); // All uppercase
        File.WriteAllText(scriptPath, "@echo off\necho test");

        // Act
        var result = _service!.HasCustomRunners(_issueFolderPath!);

        // Assert
        Assert.That(result, Is.True, "Case-insensitive search should find the script");
    }

    [Test]
    public void HasCustomRunners_WithRunButNoUnderscore_ReturnsFalse()
    {
        // Arrange - File starts with "run" but doesn't match pattern "run_*"
        var scriptPath = Path.Combine(_issueFolderPath!, "run.cmd"); // Missing underscore
        File.WriteAllText(scriptPath, "@echo off\necho test");

        // Act
        var result = _service!.HasCustomRunners(_issueFolderPath!);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void HasCustomRunners_WithRunButWrongExtension_ReturnsFalse()
    {
        // Arrange - File matches "run_*" but wrong extension
        var scriptPath = Path.Combine(_issueFolderPath!, "run_test.bat"); // .bat instead of .cmd
        File.WriteAllText(scriptPath, "@echo off\necho test");

        // Act
        var result = _service!.HasCustomRunners(_issueFolderPath!);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void HasCustomRunners_WithNoScripts_ReturnsFalse()
    {
        // Arrange - Empty folder
        // (no files created)

        // Act
        var result = _service!.HasCustomRunners(_issueFolderPath!);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void HasCustomRunners_WithNonScriptFiles_ReturnsFalse()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_issueFolderPath!, "test.csproj"), "<Project></Project>");
        File.WriteAllText(Path.Combine(_issueFolderPath!, "readme.txt"), "Readme");

        // Act
        var result = _service!.HasCustomRunners(_issueFolderPath!);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void HasCustomRunners_WithNonexistentFolder_ReturnsFalse()
    {
        // Arrange
        var nonexistentPath = Path.Combine(_tempDir!, "Nonexistent");

        // Act
        var result = _service!.HasCustomRunners(nonexistentPath);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void HasCustomRunners_WithScriptInSubdirectory_ReturnsFalse()
    {
        // Arrange - Script in subdirectory (should only search top level)
        var subDir = Path.Combine(_issueFolderPath!, "SubFolder");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "run_test.cmd"), "@echo off");

        // Act
        var result = _service!.HasCustomRunners(_issueFolderPath!);

        // Assert
        Assert.That(result, Is.False, "Scripts should only be found in the issue folder root, not subdirectories");
    }

    [Test]
    public void HasCustomRunners_WithMixedCaseScriptName_ReturnsTrue()
    {
        // Arrange - Mixed case script name
        var scriptPath = Path.Combine(_issueFolderPath!, "Run_Test_0.Cmd"); // Mixed case
        File.WriteAllText(scriptPath, "@echo off\necho test");

        // Act
        var result = _service!.HasCustomRunners(_issueFolderPath!);

        // Assert
        Assert.That(result, Is.True, "Case-insensitive search should find mixed case script");
    }

    [Test]
    public void HasCustomRunners_WithScriptAndProjectFiles_ReturnsTrue()
    {
        // Arrange - Both script and project file present
        File.WriteAllText(Path.Combine(_issueFolderPath!, "run_test.cmd"), "@echo off");
        File.WriteAllText(Path.Combine(_issueFolderPath!, "test.csproj"), "<Project></Project>");

        // Act
        var result = _service!.HasCustomRunners(_issueFolderPath!);

        // Assert
        Assert.That(result, Is.True, "Script detection should work even when project files are present");
    }
}

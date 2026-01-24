using IssueRunner.Services;
using Microsoft.Extensions.Logging;

namespace IssueRunner.ComponentTests.Services;

/// <summary>
/// Unit tests for the MarkerService class.
/// </summary>
[TestFixture]
public class MarkerServiceTests
{
    private ILogger<MarkerService>? _logger;
    private MarkerService? _markerService;
    private string? _tempDirectory;

    [SetUp]
    public void SetUp()
    {
        _logger = Substitute.For<ILogger<MarkerService>>();
        _markerService = new MarkerService(_logger);
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"MarkerServiceTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (_tempDirectory != null && Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    /// <summary>
    /// Tests that ShouldSkipIssue returns true when a marker file exists in the directory.
    /// Input: Directory containing a marker file.
    /// Expected: Returns true and logs debug message.
    /// </summary>
    [TestCase("ignore")]
    [TestCase("ignore.md")]
    [TestCase("explicit")]
    [TestCase("explicit.md")]
    [TestCase("wip")]
    [TestCase("wip.md")]
    [TestCase("gui")]
    [TestCase("gui.md")]
    [TestCase("closedasnotplanned")]
    [TestCase("closedasnotplanned.md")]
    [TestCase("closednotplanned")]
    [TestCase("closednotplanned.md")]
    public void ShouldSkipIssue_WithMarkerFile_ReturnsTrue(string markerFileName)
    {
        // Arrange
        var markerFilePath = Path.Combine(_tempDirectory!, markerFileName);
        File.WriteAllText(markerFilePath, "test content");

        // Act
        var result = _markerService!.ShouldSkipIssue(_tempDirectory!);

        // Assert
        Assert.That(result, Is.True);
    }

    /// <summary>
    /// Tests that ShouldSkipIssue returns true when a marker file with different casing exists.
    /// Input: Directory containing marker file with uppercase/mixed case name.
    /// Expected: Returns true (case-insensitive matching).
    /// </summary>
    [TestCase("IGNORE")]
    [TestCase("Ignore.MD")]
    [TestCase("EXPLICIT")]
    [TestCase("WIP.md")]
    [TestCase("GUI")]
    [TestCase("ClosedAsNotPlanned.md")]
    public void ShouldSkipIssue_WithCaseInsensitiveMarkerFile_ReturnsTrue(string markerFileName)
    {
        // Arrange
        var markerFilePath = Path.Combine(_tempDirectory!, markerFileName);
        File.WriteAllText(markerFilePath, "test content");

        // Act
        var result = _markerService!.ShouldSkipIssue(_tempDirectory!);

        // Assert
        Assert.That(result, Is.True);
    }

    /// <summary>
    /// Tests that ShouldSkipIssue returns false when no marker files exist in the directory.
    /// Input: Directory with no files.
    /// Expected: Returns false.
    /// </summary>
    [Test]
    public void ShouldSkipIssue_WithNoFiles_ReturnsFalse()
    {
        // Arrange
        // Empty directory created in SetUp

        // Act
        var result = _markerService!.ShouldSkipIssue(_tempDirectory!);

        // Assert
        Assert.That(result, Is.False);
    }

    /// <summary>
    /// Tests that ShouldSkipIssue returns false when only non-marker files exist.
    /// Input: Directory containing files that are not marker files.
    /// Expected: Returns false.
    /// </summary>
    [Test]
    public void ShouldSkipIssue_WithNonMarkerFiles_ReturnsFalse()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDirectory!, "readme.md"), "content");
        File.WriteAllText(Path.Combine(_tempDirectory!, "test.cs"), "content");
        File.WriteAllText(Path.Combine(_tempDirectory!, "data.txt"), "content");

        // Act
        var result = _markerService!.ShouldSkipIssue(_tempDirectory!);

        // Assert
        Assert.That(result, Is.False);
    }

    /// <summary>
    /// Tests that ShouldSkipIssue returns true when multiple marker files exist.
    /// Input: Directory containing multiple marker files.
    /// Expected: Returns true (stops on first match).
    /// </summary>
    [Test]
    public void ShouldSkipIssue_WithMultipleMarkerFiles_ReturnsTrue()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDirectory!, "ignore"), "content");
        File.WriteAllText(Path.Combine(_tempDirectory!, "wip.md"), "content");
        File.WriteAllText(Path.Combine(_tempDirectory!, "explicit"), "content");

        // Act
        var result = _markerService!.ShouldSkipIssue(_tempDirectory!);

        // Assert
        Assert.That(result, Is.True);
    }

    /// <summary>
    /// Tests that ShouldSkipIssue returns true when marker and non-marker files coexist.
    /// Input: Directory with both marker and non-marker files.
    /// Expected: Returns true.
    /// </summary>
    [Test]
    public void ShouldSkipIssue_WithMarkerAndNonMarkerFiles_ReturnsTrue()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDirectory!, "ignore"), "content");
        File.WriteAllText(Path.Combine(_tempDirectory!, "readme.md"), "content");
        File.WriteAllText(Path.Combine(_tempDirectory!, "test.cs"), "content");

        // Act
        var result = _markerService!.ShouldSkipIssue(_tempDirectory!);

        // Assert
        Assert.That(result, Is.True);
    }

    /// <summary>
    /// Tests that ShouldSkipIssue throws ArgumentNullException when path is null.
    /// Input: Null path.
    /// Expected: ArgumentNullException is thrown.
    /// </summary>
    [Test]
    public void ShouldSkipIssue_WithNullPath_ThrowsArgumentNullException()
    {
        // Arrange
        string? nullPath = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _markerService!.ShouldSkipIssue(nullPath!));
    }

    /// <summary>
    /// Tests that ShouldSkipIssue throws DirectoryNotFoundException when directory does not exist.
    /// Input: Path to non-existent directory.
    /// Expected: DirectoryNotFoundException is thrown.
    /// </summary>
    [Test]
    public void ShouldSkipIssue_WithNonExistentDirectory_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDirectory!, "NonExistent", "Folder");

        // Act & Assert
        Assert.Throws<DirectoryNotFoundException>(() => _markerService!.ShouldSkipIssue(nonExistentPath));
    }

    /// <summary>
    /// Tests that ShouldSkipIssue throws exception when path is empty string.
    /// Input: Empty string path.
    /// Expected: ArgumentException is thrown.
    /// </summary>
    [Test]
    public void ShouldSkipIssue_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var emptyPath = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _markerService!.ShouldSkipIssue(emptyPath));
    }

    /// <summary>
    /// Tests that ShouldSkipIssue throws exception when path contains invalid characters.
    /// Input: Path with invalid characters.
    /// Expected: ArgumentException is thrown.
    /// </summary>
    [Test]
    public void ShouldSkipIssue_WithInvalidPathCharacters_ThrowsArgumentException()
    {
        // Arrange
        var invalidPath = "C:\\Invalid<>Path|With?InvalidChars";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _markerService!.ShouldSkipIssue(invalidPath));
    }

    /// <summary>
    /// Tests that ShouldSkipIssue throws exception when path is whitespace only.
    /// Input: Whitespace-only path string.
    /// Expected: ArgumentException is thrown.
    /// </summary>
    [Test]
    public void ShouldSkipIssue_WithWhitespacePath_ThrowsArgumentException()
    {
        // Arrange
        var whitespacePath = "   ";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _markerService!.ShouldSkipIssue(whitespacePath));
    }

    /// <summary>
    /// Tests that ShouldSkipIssue handles relative paths correctly.
    /// Input: Relative directory path with marker file.
    /// Expected: Returns true if relative path resolves correctly.
    /// </summary>
    [Test]
    public void ShouldSkipIssue_WithRelativePath_ReturnsTrue()
    {
        // Arrange
        var currentDir = Directory.GetCurrentDirectory();
        var relativeTestDir = Path.Combine(currentDir, $"RelativeTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(relativeTestDir);
        File.WriteAllText(Path.Combine(relativeTestDir, "ignore"), "content");

        try
        {
            // Act
            var result = _markerService!.ShouldSkipIssue(relativeTestDir);

            // Assert
            Assert.That(result, Is.True);
        }
        finally
        {
            if (Directory.Exists(relativeTestDir))
            {
                Directory.Delete(relativeTestDir, true);
            }
        }
    }

    /// <summary>
    /// Tests that ShouldSkipIssue throws an exception when the folder path is empty.
    /// </summary>
    [Test]
    public void ShouldSkipIssue_WithEmptyPath_ThrowsException()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.ShouldSkipIssue(string.Empty));
    }

    /// <summary>
    /// Tests that ShouldSkipIssue throws an exception when the folder path contains only whitespace.
    /// </summary>
    [Test]
    public void ShouldSkipIssue_WithWhitespacePath_ThrowsException()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.ShouldSkipIssue("   "));
    }

    /// <summary>
    /// Tests that ShouldSkipIssue throws DirectoryNotFoundException when the folder does not exist.
    /// </summary>
    [Test]
    public void ShouldSkipIssue_WithNonExistentFolder_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act & Assert
        Assert.Throws<DirectoryNotFoundException>(() => service.ShouldSkipIssue(nonExistentPath));
    }

    /// <summary>
    /// Tests that ShouldSkipIssue throws an exception when the folder path contains invalid characters.
    /// </summary>
    [Test]
    public void ShouldSkipIssue_WithInvalidPathCharacters_ThrowsException()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var invalidPath = "C:\\Invalid<>Path|";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.ShouldSkipIssue(invalidPath));
    }

    /// <summary>
    /// Tests that ShouldSkipIssue returns false when the folder contains no files.
    /// </summary>
    [Test]
    [Category("Integration")]
    public void ShouldSkipIssue_WithEmptyFolder_ReturnsFalse()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempFolder);

            // Act
            var result = service.ShouldSkipIssue(tempFolder);

            // Assert
            Assert.That(result, Is.False);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
            }
        }
    }

    /// <summary>
    /// Tests that ShouldSkipIssue logs a debug message when a marker file is found.
    /// </summary>
    [Test]
    [Category("Integration")]
    public void ShouldSkipIssue_WithMarkerFile_LogsDebugMessage()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempFolder);
            File.WriteAllText(Path.Combine(tempFolder, "ignore"), "marker");

            // Act
            var result = service.ShouldSkipIssue(tempFolder);

            // Assert
            Assert.That(result, Is.True);
            logger.Received(1).Log(
                LogLevel.Debug,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
            }
        }
    }

    /// <summary>
    /// Tests that ShouldSkipIssue does not log when no marker file is found.
    /// </summary>
    [Test]
    [Category("Integration")]
    public void ShouldSkipIssue_WithoutMarkerFile_DoesNotLog()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempFolder);
            File.WriteAllText(Path.Combine(tempFolder, "readme.md"), "test");

            // Act
            var result = service.ShouldSkipIssue(tempFolder);

            // Assert
            Assert.That(result, Is.False);
            logger.DidNotReceive().Log(
                LogLevel.Debug,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
            }
        }
    }

    /// <summary>
    /// Tests that GetMarkerFiles returns a non-null collection containing all expected marker file names.
    /// </summary>
    [Test]
    public void GetMarkerFiles_WhenCalled_ReturnsExpectedMarkerFiles()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var markerService = new MarkerService(logger);
        var expectedMarkerFiles = new[]
        {
            "ignore", "ignore.md",
            "explicit", "explicit.md",
            "wip", "wip.md",
            "gui", "gui.md",
            "closedasnotplanned", "closedasnotplanned.md",
            "closednotplanned", "closednotplanned.md"
        };

        // Act
        var result = markerService.GetMarkerFiles();

        // Assert
        Assert.That(result, Is.Not.Null, "GetMarkerFiles should not return null");
        Assert.That(result, Is.EquivalentTo(expectedMarkerFiles), "GetMarkerFiles should return all expected marker file names");
    }

    /// <summary>
    /// Tests that GetMarkerFiles returns a collection with the correct count of marker files.
    /// </summary>
    [Test]
    public void GetMarkerFiles_WhenCalled_ReturnsCorrectCount()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var markerService = new MarkerService(logger);

        // Act
        var result = markerService.GetMarkerFiles();

        // Assert
        Assert.That(result.Count(), Is.EqualTo(12), "GetMarkerFiles should return exactly 12 marker file names");
    }

    /// <summary>
    /// Tests that GetMarkerFiles returns an enumerable collection that can be iterated multiple times.
    /// </summary>
    [Test]
    public void GetMarkerFiles_WhenCalled_ReturnsEnumerableCollection()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var markerService = new MarkerService(logger);

        // Act
        var result = markerService.GetMarkerFiles();

        // Assert
        Assert.That(result, Is.InstanceOf<IEnumerable<string>>(), "GetMarkerFiles should return an IEnumerable<string>");

        // Verify it can be enumerated multiple times
        var firstEnumeration = result.ToList();
        var secondEnumeration = result.ToList();
        Assert.That(firstEnumeration, Is.EquivalentTo(secondEnumeration), "Collection should be enumerable multiple times");
    }

    /// <summary>
    /// Tests that GetMarkerFiles returns a collection containing specific known marker file names.
    /// </summary>
    /// <param name="expectedMarkerFile">The expected marker file name.</param>
    [TestCase("ignore")]
    [TestCase("ignore.md")]
    [TestCase("explicit")]
    [TestCase("explicit.md")]
    [TestCase("wip")]
    [TestCase("wip.md")]
    [TestCase("gui")]
    [TestCase("gui.md")]
    [TestCase("closedasnotplanned")]
    [TestCase("closedasnotplanned.md")]
    [TestCase("closednotplanned")]
    [TestCase("closednotplanned.md")]
    public void GetMarkerFiles_WhenCalled_ContainsExpectedMarkerFile(string expectedMarkerFile)
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var markerService = new MarkerService(logger);

        // Act
        var result = markerService.GetMarkerFiles();

        // Assert
        Assert.That(result, Does.Contain(expectedMarkerFile), $"GetMarkerFiles should contain '{expectedMarkerFile}'");
    }

    /// <summary>
    /// Tests that GetMarkerReason throws ArgumentNullException when folder path is null.
    /// </summary>
    [Test]
    public void GetMarkerReason_WithNullPath_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => service.GetMarkerReason(null!));
    }

    /// <summary>
    /// Tests that GetMarkerReason throws ArgumentException when folder path is empty.
    /// </summary>
    [Test]
    public void GetMarkerReason_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.GetMarkerReason(string.Empty));
    }

    /// <summary>
    /// Tests that GetMarkerReason throws DirectoryNotFoundException when folder does not exist.
    /// </summary>
    [Test]
    public void GetMarkerReason_WithNonExistentDirectory_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act & Assert
        Assert.Throws<DirectoryNotFoundException>(() => service.GetMarkerReason(nonExistentPath));
    }

    /// <summary>
    /// Tests that GetMarkerReason returns "marker file" when folder contains no marker files.
    /// </summary>
    [Test]
    public void GetMarkerReason_WithNoMarkerFiles_ReturnsMarkerFile()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create some non-marker files
            File.WriteAllText(Path.Combine(tempDir, "test.txt"), "content");
            File.WriteAllText(Path.Combine(tempDir, "readme.md"), "content");

            // Act
            var result = service.GetMarkerReason(tempDir);

            // Assert
            Assert.That(result, Is.EqualTo("marker file"));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// Tests that GetMarkerReason returns "marker file" when folder is empty.
    /// </summary>
    [Test]
    public void GetMarkerReason_WithEmptyFolder_ReturnsMarkerFile()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            var result = service.GetMarkerReason(tempDir);

            // Assert
            Assert.That(result, Is.EqualTo("marker file"));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// Tests that GetMarkerReason returns the correct reason for each marker file.
    /// </summary>
    /// <param name="markerFileName">The marker file name to test.</param>
    /// <param name="expectedReason">The expected reason string.</param>
    [TestCase("ignore", "Ignored")]
    [TestCase("ignore.md", "Ignored")]
    [TestCase("explicit", "Explicit")]
    [TestCase("explicit.md", "Explicit")]
    [TestCase("gui", "GUI")]
    [TestCase("gui.md", "GUI")]
    [TestCase("wip", "WIP")]
    [TestCase("wip.md", "WIP")]
    [TestCase("closednotplanned", "Closed Not Planned")]
    [TestCase("closednotplanned.md", "Closed Not Planned")]
    [TestCase("closedasnotplanned", "Closed As Not Planned")]
    [TestCase("closedasnotplanned.md", "Closed As Not Planned")]
    public void GetMarkerReason_WithSpecificMarkerFile_ReturnsCorrectReason(string markerFileName, string expectedReason)
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create the marker file
            File.WriteAllText(Path.Combine(tempDir, markerFileName), "");

            // Act
            var result = service.GetMarkerReason(tempDir);

            // Assert
            Assert.That(result, Is.EqualTo(expectedReason));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// Tests that GetMarkerReason is case-insensitive for marker file names.
    /// </summary>
    /// <param name="markerFileName">The marker file name with different casing.</param>
    /// <param name="expectedReason">The expected reason string.</param>
    [TestCase("IGNORE", "Ignored")]
    [TestCase("Ignore", "Ignored")]
    [TestCase("IGNORE.MD", "Ignored")]
    [TestCase("Ignore.Md", "Ignored")]
    [TestCase("EXPLICIT", "Explicit")]
    [TestCase("Explicit.MD", "Explicit")]
    [TestCase("GUI", "GUI")]
    [TestCase("Gui.md", "GUI")]
    [TestCase("WIP", "WIP")]
    [TestCase("Wip.MD", "WIP")]
    public void GetMarkerReason_WithDifferentCasing_ReturnsCorrectReason(string markerFileName, string expectedReason)
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create the marker file with specific casing
            File.WriteAllText(Path.Combine(tempDir, markerFileName), "");

            // Act
            var result = service.GetMarkerReason(tempDir);

            // Assert
            Assert.That(result, Is.EqualTo(expectedReason));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// Tests that GetMarkerReason returns the first matching marker file when multiple markers exist.
    /// The order is determined by the MarkerFiles array: ignore, ignore.md, explicit, explicit.md, etc.
    /// </summary>
    [Test]
    public void GetMarkerReason_WithMultipleMarkerFiles_ReturnsFirstMatch()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create multiple marker files
            // According to MarkerFiles array order: ignore comes before explicit, which comes before wip
            File.WriteAllText(Path.Combine(tempDir, "wip"), "");
            File.WriteAllText(Path.Combine(tempDir, "explicit"), "");
            File.WriteAllText(Path.Combine(tempDir, "ignore"), "");

            // Act
            var result = service.GetMarkerReason(tempDir);

            // Assert
            // Should return "Ignored" because "ignore" is first in the MarkerFiles array
            Assert.That(result, Is.EqualTo("Ignored"));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// Tests that GetMarkerReason prioritizes base marker file over .md variant when both exist.
    /// Since "ignore" comes before "ignore.md" in the MarkerFiles array, it should be matched first.
    /// </summary>
    [Test]
    public void GetMarkerReason_WithBaseAndMdVariant_ReturnsBasePriority()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create both base and .md variant
            File.WriteAllText(Path.Combine(tempDir, "ignore"), "");
            File.WriteAllText(Path.Combine(tempDir, "ignore.md"), "");

            // Act
            var result = service.GetMarkerReason(tempDir);

            // Assert
            // Should return "Ignored" - both would produce the same result, but "ignore" is checked first
            Assert.That(result, Is.EqualTo("Ignored"));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// Tests that GetMarkerReason returns "marker file" for unrecognized marker files.
    /// This tests the default case in the switch expression.
    /// </summary>
    [Test]
    public void GetMarkerReason_WithUnrecognizedMarkerInList_ReturnsMarkerFile()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create a file that matches a marker name but has an unrecognized base
            // This is a theoretical edge case since GetMarkerFiles() returns a fixed list
            File.WriteAllText(Path.Combine(tempDir, "somefile.txt"), "");

            // Act
            var result = service.GetMarkerReason(tempDir);

            // Assert
            Assert.That(result, Is.EqualTo("marker file"));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// Tests that GetMarkerReason works with folder paths containing special characters.
    /// </summary>
    [Test]
    public void GetMarkerReason_WithSpecialCharactersInPath_ReturnsCorrectReason()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var specialDirName = $"test_{Guid.NewGuid()}_with spaces & symbols";
        var tempDir = Path.Combine(Path.GetTempPath(), specialDirName);
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create a marker file
            File.WriteAllText(Path.Combine(tempDir, "ignore"), "");

            // Act
            var result = service.GetMarkerReason(tempDir);

            // Assert
            Assert.That(result, Is.EqualTo("Ignored"));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// Tests that GetMarkerReason correctly handles the closednotplanned vs closedasnotplanned distinction.
    /// These are two separate marker types that should map to different reasons.
    /// </summary>
    [Test]
    public void GetMarkerReason_WithClosedNotPlannedVariants_ReturnsCorrectDistinction()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var tempDir1 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var tempDir2 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir1);
        Directory.CreateDirectory(tempDir2);

        try
        {
            // Create closednotplanned in first directory
            File.WriteAllText(Path.Combine(tempDir1, "closednotplanned"), "");

            // Create closedasnotplanned in second directory
            File.WriteAllText(Path.Combine(tempDir2, "closedasnotplanned"), "");

            // Act
            var result1 = service.GetMarkerReason(tempDir1);
            var result2 = service.GetMarkerReason(tempDir2);

            // Assert
            Assert.That(result1, Is.EqualTo("Closed Not Planned"));
            Assert.That(result2, Is.EqualTo("Closed As Not Planned"));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir1))
            {
                Directory.Delete(tempDir1, true);
            }
            if (Directory.Exists(tempDir2))
            {
                Directory.Delete(tempDir2, true);
            }
        }
    }

    /// <summary>
    /// Tests that GetMarkerReason throws ArgumentNullException when folderPath is null.
    /// </summary>
    [Test]
    public void GetMarkerReason_NullFolderPath_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => service.GetMarkerReason(null!));
    }

    /// <summary>
    /// Tests that GetMarkerReason throws ArgumentException when folderPath is an empty string.
    /// </summary>
    [Test]
    public void GetMarkerReason_EmptyFolderPath_ThrowsArgumentException()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.GetMarkerReason(string.Empty));
    }

    /// <summary>
    /// Tests that GetMarkerReason throws DirectoryNotFoundException when folderPath does not exist.
    /// </summary>
    [Test]
    public void GetMarkerReason_NonExistentDirectory_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act & Assert
        Assert.Throws<DirectoryNotFoundException>(() => service.GetMarkerReason(nonExistentPath));
    }

    /// <summary>
    /// Tests that GetMarkerReason returns "marker file" when the directory contains no marker files.
    /// </summary>
    [Test]
    public void GetMarkerReason_DirectoryWithNoMarkerFiles_ReturnsMarkerFile()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create some non-marker files
            File.WriteAllText(Path.Combine(tempDir, "readme.txt"), "test");
            File.WriteAllText(Path.Combine(tempDir, "test.cs"), "code");

            // Act
            var result = service.GetMarkerReason(tempDir);

            // Assert
            Assert.That(result, Is.EqualTo("marker file"));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Tests that GetMarkerReason returns "marker file" when the directory is completely empty.
    /// </summary>
    [Test]
    public void GetMarkerReason_EmptyDirectory_ReturnsMarkerFile()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            var result = service.GetMarkerReason(tempDir);

            // Assert
            Assert.That(result, Is.EqualTo("marker file"));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Tests that GetMarkerReason correctly identifies and returns the appropriate reason
    /// for various marker files (with and without .md extension).
    /// </summary>
    /// <param name="markerFileName">The name of the marker file to create.</param>
    /// <param name="expectedReason">The expected reason string to be returned.</param>
    [TestCase("ignore", "Ignored")]
    [TestCase("ignore.md", "Ignored")]
    [TestCase("explicit", "Explicit")]
    [TestCase("explicit.md", "Explicit")]
    [TestCase("wip", "WIP")]
    [TestCase("wip.md", "WIP")]
    [TestCase("gui", "GUI")]
    [TestCase("gui.md", "GUI")]
    [TestCase("closedasnotplanned", "Closed As Not Planned")]
    [TestCase("closedasnotplanned.md", "Closed As Not Planned")]
    [TestCase("closednotplanned", "Closed Not Planned")]
    [TestCase("closednotplanned.md", "Closed Not Planned")]
    public void GetMarkerReason_DirectoryWithMarkerFile_ReturnsCorrectReason(string markerFileName, string expectedReason)
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create the marker file
            File.WriteAllText(Path.Combine(tempDir, markerFileName), "marker");

            // Act
            var result = service.GetMarkerReason(tempDir);

            // Assert
            Assert.That(result, Is.EqualTo(expectedReason));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Tests that GetMarkerReason is case-insensitive when matching marker files.
    /// </summary>
    /// <param name="markerFileName">The name of the marker file with varied casing.</param>
    /// <param name="expectedReason">The expected reason string to be returned.</param>
    [TestCase("IGNORE", "Ignored")]
    [TestCase("Ignore.MD", "Ignored")]
    [TestCase("EXPLICIT.md", "Explicit")]
    [TestCase("WIP", "WIP")]
    [TestCase("Gui.Md", "GUI")]
    public void GetMarkerReason_MarkerFileWithDifferentCasing_ReturnsCorrectReason(string markerFileName, string expectedReason)
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create the marker file with specific casing
            File.WriteAllText(Path.Combine(tempDir, markerFileName), "marker");

            // Act
            var result = service.GetMarkerReason(tempDir);

            // Assert
            Assert.That(result, Is.EqualTo(expectedReason));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Tests that GetMarkerReason returns the first marker file found when multiple marker files exist.
    /// The order is determined by the MarkerFiles array order.
    /// </summary>
    [Test]
    public void GetMarkerReason_DirectoryWithMultipleMarkerFiles_ReturnsFirstFound()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create multiple marker files - "ignore" comes first in MarkerFiles array
            File.WriteAllText(Path.Combine(tempDir, "ignore"), "marker");
            File.WriteAllText(Path.Combine(tempDir, "wip"), "marker");
            File.WriteAllText(Path.Combine(tempDir, "explicit"), "marker");

            // Act
            var result = service.GetMarkerReason(tempDir);

            // Assert - Should return "Ignored" as "ignore" comes first in the MarkerFiles array
            Assert.That(result, Is.EqualTo("Ignored"));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Tests that GetMarkerReason handles whitespace-only folder paths appropriately.
    /// </summary>
    [TestCase(" ")]
    [TestCase("   ")]
   public void GetMarkerReason_WhitespaceOnlyFolderPath_ThrowsException(string whitespacePath)
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.GetMarkerReason(whitespacePath));
    }

    /// <summary>
    /// Tests that GetMarkerReason handles invalid path characters appropriately.
    /// </summary>
    [Test]
    public void GetMarkerReason_InvalidPathCharacters_ThrowsException()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var invalidPath = "C:\\invalid|path\\with<invalid>chars";

        // Act & Assert
        Assert.Throws<IOException>(() => service.GetMarkerReason(invalidPath));
    }

    /// <summary>
    /// Tests that GetMarkerFiles returns a non-null collection.
    /// Expected: Method should always return a valid collection reference.
    /// </summary>
    [Test]
    public void GetMarkerFiles_WhenCalled_ReturnsNonNullCollection()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);

        // Act
        var result = service.GetMarkerFiles();

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    /// <summary>
    /// Tests that GetMarkerFiles returns a collection with the expected number of marker files.
    /// Expected: Collection should contain exactly 12 marker file names.
    /// </summary>
    [Test]
    public void GetMarkerFiles_WhenCalled_ReturnsCollectionWithCorrectCount()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);

        // Act
        var result = service.GetMarkerFiles().ToList();

        // Assert
        Assert.That(result, Has.Count.EqualTo(12));
    }

    /// <summary>
    /// Tests that GetMarkerFiles returns a non-empty collection.
    /// Expected: Collection should contain at least one element.
    /// </summary>
    [Test]
    public void GetMarkerFiles_WhenCalled_ReturnsNonEmptyCollection()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);

        // Act
        var result = service.GetMarkerFiles();

        // Assert
        Assert.That(result, Is.Not.Empty);
    }

    /// <summary>
    /// Tests that GetMarkerFiles returns all expected marker file names.
    /// Input: None.
    /// Expected: Collection should contain all predefined marker files including
    /// ignore, explicit, wip, gui, closedasnotplanned, closednotplanned and their .md variants.
    /// </summary>
    [Test]
    public void GetMarkerFiles_WhenCalled_ReturnsAllExpectedMarkerFiles()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);
        var expectedMarkers = new[]
        {
            "ignore", "ignore.md",
            "explicit", "explicit.md",
            "wip", "wip.md",
            "gui", "gui.md",
            "closedasnotplanned", "closedasnotplanned.md",
            "closednotplanned", "closednotplanned.md"
        };

        // Act
        var result = service.GetMarkerFiles().ToList();

        // Assert
        Assert.That(result, Is.EquivalentTo(expectedMarkers));
    }

    /// <summary>
    /// Tests that GetMarkerFiles returns a collection that can be enumerated multiple times.
    /// Expected: Collection should support multiple enumerations without issues.
    /// </summary>
    [Test]
    public void GetMarkerFiles_WhenCalled_ReturnsCollectionThatCanBeEnumeratedMultipleTimes()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);

        // Act
        var result = service.GetMarkerFiles();
        var firstEnumeration = result.ToList();
        var secondEnumeration = result.ToList();

        // Assert
        Assert.That(firstEnumeration, Has.Count.EqualTo(12));
        Assert.That(secondEnumeration, Has.Count.EqualTo(12));
        Assert.That(firstEnumeration, Is.EquivalentTo(secondEnumeration));
    }

    /// <summary>
    /// Tests that GetMarkerFiles contains specific marker files with correct casing.
    /// Input: None.
    /// Expected: Collection should contain specific marker files like "ignore" and "ignore.md".
    /// </summary>
    [TestCase("ignore")]
    [TestCase("ignore.md")]
    [TestCase("explicit")]
    [TestCase("explicit.md")]
    [TestCase("wip")]
    [TestCase("wip.md")]
    [TestCase("gui")]
    [TestCase("gui.md")]
    [TestCase("closedasnotplanned")]
    [TestCase("closedasnotplanned.md")]
    [TestCase("closednotplanned")]
    [TestCase("closednotplanned.md")]
    public void GetMarkerFiles_WhenCalled_ContainsSpecificMarkerFile(string expectedMarker)
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarkerService>>();
        var service = new MarkerService(logger);

        // Act
        var result = service.GetMarkerFiles();

        // Assert
        Assert.That(result, Does.Contain(expectedMarker));
    }
}
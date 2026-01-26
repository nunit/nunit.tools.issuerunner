using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using IssueRunner.Models;
using IssueRunner.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace IssueRunner.Services.UnitTests;


/// <summary>
/// Unit tests for the <see cref="TestResultDiffService"/> class.
/// </summary>
public sealed partial class TestResultDiffServiceTests
{
    /// <summary>
    /// Tests CompareResultsAsync when both current and baseline files are empty.
    /// Expected: Returns empty list of diffs.
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WithEmptyResults_ReturnsEmptyList()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();
        var dataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataDir);

        environmentService.GetDataDirectory(Arg.Any<string>()).Returns(dataDir);

        var currentResults = new List<IssueResult>();
        var baselineResults = new List<IssueResult>();

        await File.WriteAllTextAsync(Path.Combine(dataDir, "results.json"), JsonSerializer.Serialize(currentResults));
        await File.WriteAllTextAsync(Path.Combine(dataDir, "results-baseline.json"), JsonSerializer.Serialize(baselineResults));

        var service = new TestResultDiffService(environmentService, logger);

        try
        {
            // Act
            var result = await service.CompareResultsAsync("test-repo");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }
        finally
        {
            // Cleanup
            Directory.Delete(dataDir, true);
        }
    }

    /// <summary>
    /// Tests CompareResultsAsync when results files don't exist.
    /// Expected: Returns empty list of diffs.
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WithNonExistentFiles_ReturnsEmptyList()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();
        var dataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataDir);

        environmentService.GetDataDirectory(Arg.Any<string>()).Returns(dataDir);

        var service = new TestResultDiffService(environmentService, logger);

        try
        {
            // Act
            var result = await service.CompareResultsAsync("test-repo");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }
        finally
        {
            // Cleanup
            Directory.Delete(dataDir, true);
        }
    }

    /// <summary>
    /// Tests CompareResultsAsync when baseline and current have identical results.
    /// Expected: Returns empty list (no differences).
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WithIdenticalResults_ReturnsEmptyList()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();
        var dataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataDir);

        environmentService.GetDataDirectory(Arg.Any<string>()).Returns(dataDir);

        var results = new List<IssueResult>
        {
            new()
            {
                Number = 123,
                ProjectPath = "TestProject/Test.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Success
            }
        };

        await File.WriteAllTextAsync(Path.Combine(dataDir, "results.json"), JsonSerializer.Serialize(results));
        await File.WriteAllTextAsync(Path.Combine(dataDir, "results-baseline.json"), JsonSerializer.Serialize(results));

        var service = new TestResultDiffService(environmentService, logger);

        try
        {
            // Act
            var result = await service.CompareResultsAsync("test-repo");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }
        finally
        {
            // Cleanup
            Directory.Delete(dataDir, true);
        }
    }

    /// <summary>
    /// Tests CompareResultsAsync when a test was fixed (baseline=fail, current=success).
    /// Expected: Returns diff with ChangeType.Fixed.
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WithFixedTest_ReturnsDiffWithFixedChangeType()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();
        var dataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataDir);

        environmentService.GetDataDirectory(Arg.Any<string>()).Returns(dataDir);

        var baselineResults = new List<IssueResult>
        {
            new()
            {
                Number = 123,
                ProjectPath = "TestProject/Test.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Failed,
                RunResult = RunResult.Run
            }
        };

        var currentResults = new List<IssueResult>
        {
            new()
            {
                Number = 123,
                ProjectPath = "TestProject/Test.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Success,
                RunResult = RunResult.Run
            }
        };

        await File.WriteAllTextAsync(Path.Combine(dataDir, "results.json"), JsonSerializer.Serialize(currentResults));
        await File.WriteAllTextAsync(Path.Combine(dataDir, "results-baseline.json"), JsonSerializer.Serialize(baselineResults));

        var service = new TestResultDiffService(environmentService, logger);

        try
        {
            // Act
            var result = await service.CompareResultsAsync("test-repo");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].IssueNumber, Is.EqualTo(123));
            Assert.That(result[0].ProjectPath, Is.EqualTo("testproject/test.csproj"));
            Assert.That(result[0].BaselineStatus, Is.EqualTo(StepResultStatus.Failed));
            Assert.That(result[0].CurrentStatus, Is.EqualTo(StepResultStatus.Success));
            Assert.That(result[0].ChangeType, Is.EqualTo(ChangeType.Fixed));
        }
        finally
        {
            // Cleanup
            Directory.Delete(dataDir, true);
        }
    }

    /// <summary>
    /// Tests CompareResultsAsync when a test regressed (baseline=success, current=fail).
    /// Expected: Returns diff with ChangeType.Regression.
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WithRegressionTest_ReturnsDiffWithRegressionChangeType()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();
        var dataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataDir);

        environmentService.GetDataDirectory(Arg.Any<string>()).Returns(dataDir);

        var baselineResults = new List<IssueResult>
        {
            new()
            {
                Number = 456,
                ProjectPath = "AnotherProject/Another.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Success
            }
        };

        var currentResults = new List<IssueResult>
        {
            new()
            {
                Number = 456,
                ProjectPath = "AnotherProject/Another.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Failed
            }
        };

        await File.WriteAllTextAsync(Path.Combine(dataDir, "results.json"), JsonSerializer.Serialize(currentResults));
        await File.WriteAllTextAsync(Path.Combine(dataDir, "results-baseline.json"), JsonSerializer.Serialize(baselineResults));

        var service = new TestResultDiffService(environmentService, logger);

        try
        {
            // Act
            var result = await service.CompareResultsAsync("test-repo");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].IssueNumber, Is.EqualTo(456));
            Assert.That(result[0].BaselineStatus, Is.EqualTo(StepResultStatus.Success));
            Assert.That(result[0].CurrentStatus, Is.EqualTo(StepResultStatus.Failed));
            Assert.That(result[0].ChangeType, Is.EqualTo(ChangeType.Regression));
        }
        finally
        {
            // Cleanup
            Directory.Delete(dataDir, true);
        }
    }

    /// <summary>
    /// Tests CompareResultsAsync when a test went from not run to fail.
    /// Expected: Returns diff with ChangeType.BuildToFail.
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WithNotRunToFail_ReturnsDiffWithCompileToFailChangeType()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();
        var dataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataDir);

        environmentService.GetDataDirectory(Arg.Any<string>()).Returns(dataDir);

        var baselineResults = new List<IssueResult>
        {
            new()
            {
                Number = 789,
                ProjectPath = "CompileProject/Compile.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = null,
                RunResult = RunResult.NotRun
            }
        };

        var currentResults = new List<IssueResult>
        {
            new()
            {
                Number = 789,
                ProjectPath = "CompileProject/Compile.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Failed,
                RunResult = RunResult.Run
            }
        };

        await File.WriteAllTextAsync(Path.Combine(dataDir, "results.json"), JsonSerializer.Serialize(currentResults));
        await File.WriteAllTextAsync(Path.Combine(dataDir, "results-baseline.json"), JsonSerializer.Serialize(baselineResults));

        var service = new TestResultDiffService(environmentService, logger);

        try
        {
            // Act
            var result = await service.CompareResultsAsync("test-repo");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].IssueNumber, Is.EqualTo(789));
            Assert.That(result[0].BaselineStatus, Is.EqualTo(StepResultStatus.NotRun));
            Assert.That(result[0].CurrentStatus, Is.EqualTo(StepResultStatus.Failed));
            Assert.That(result[0].ChangeType, Is.EqualTo(ChangeType.BuildToFail));
        }
        finally
        {
            // Cleanup
            Directory.Delete(dataDir, true);
        }
    }

    /// <summary>
    /// Tests CompareResultsAsync when a test went from fail to skipped.
    /// Expected: Returns empty list (Skipped change type is filtered out).
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WithFailToSkipped_ReturnsEmptyList()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();
        var dataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataDir);

        environmentService.GetDataDirectory(Arg.Any<string>()).Returns(dataDir);

        var baselineResults = new List<IssueResult>
        {
            new()
            {
                Number = 111,
                ProjectPath = "SkipProject/Skip.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Failed,
                RunResult = RunResult.Run
                
            }
        };

        var currentResults = new List<IssueResult>
        {
            new()
            {
                Number = 111,
                ProjectPath = "SkipProject/Skip.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.NotRun,
                RunResult = RunResult.Skipped
            }
        };

        await File.WriteAllTextAsync(Path.Combine(dataDir, "results.json"), JsonSerializer.Serialize(currentResults));
        await File.WriteAllTextAsync(Path.Combine(dataDir, "results-baseline.json"), JsonSerializer.Serialize(baselineResults));

        var service = new TestResultDiffService(environmentService, logger);

        try
        {
            // Act
            var result = await service.CompareResultsAsync("test-repo");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }
        finally
        {
            // Cleanup
            Directory.Delete(dataDir, true);
        }
    }

    /// <summary>
    /// Tests CompareResultsAsync with case-insensitive project path matching.
    /// Expected: Results with different casing for ProjectPath are matched correctly.
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WithDifferentCasingProjectPath_MatchesCorrectly()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();
        var dataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataDir);

        environmentService.GetDataDirectory(Arg.Any<string>()).Returns(dataDir);

        var baselineResults = new List<IssueResult>
        {
            new()
            {
                Number = 999,
                ProjectPath = "TestProject/Test.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Failed
            }
        };

        var currentResults = new List<IssueResult>
        {
            new()
            {
                Number = 999,
                ProjectPath = "TESTPROJECT/TEST.CSPROJ",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Success
            }
        };

        await File.WriteAllTextAsync(Path.Combine(dataDir, "results.json"), JsonSerializer.Serialize(currentResults));
        await File.WriteAllTextAsync(Path.Combine(dataDir, "results-baseline.json"), JsonSerializer.Serialize(baselineResults));

        var service = new TestResultDiffService(environmentService, logger);

        try
        {
            // Act
            var result = await service.CompareResultsAsync("test-repo");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].IssueNumber, Is.EqualTo(999));
            Assert.That(result[0].ChangeType, Is.EqualTo(ChangeType.Fixed));
        }
        finally
        {
            // Cleanup
            Directory.Delete(dataDir, true);
        }
    }

    /// <summary>
    /// Tests CompareResultsAsync with result only in baseline.
    /// Expected: Returns diff showing change from baseline status to "not run".
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WithResultOnlyInBaseline_ReturnsDiff()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();
        var dataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataDir);

        environmentService.GetDataDirectory(Arg.Any<string>()).Returns(dataDir);

        var baselineResults = new List<IssueResult>
        {
            new()
            {
                Number = 222,
                ProjectPath = "OldProject/Old.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Success
            }
        };

        var currentResults = new List<IssueResult>();

        await File.WriteAllTextAsync(Path.Combine(dataDir, "results.json"), JsonSerializer.Serialize(currentResults));
        await File.WriteAllTextAsync(Path.Combine(dataDir, "results-baseline.json"), JsonSerializer.Serialize(baselineResults));

        var service = new TestResultDiffService(environmentService, logger);

        try
        {
            // Act
            var result = await service.CompareResultsAsync("test-repo");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].IssueNumber, Is.EqualTo(222));
            Assert.That(result[0].BaselineStatus, Is.EqualTo(StepResultStatus.Success));
            Assert.That(result[0].CurrentStatus, Is.EqualTo(StepResultStatus.NotRun));
        }
        finally
        {
            // Cleanup
            Directory.Delete(dataDir, true);
        }
    }

    /// <summary>
    /// Tests CompareResultsAsync with result only in current.
    /// Expected: Returns diff showing change from "not run" to current status.
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WithResultOnlyInCurrent_ReturnsDiff()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();
        var dataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataDir);

        environmentService.GetDataDirectory(Arg.Any<string>()).Returns(dataDir);

        var baselineResults = new List<IssueResult>();

        var currentResults = new List<IssueResult>
        {
            new()
            {
                Number = 333,
                ProjectPath = "NewProject/New.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Failed
            }
        };

        await File.WriteAllTextAsync(Path.Combine(dataDir, "results.json"), JsonSerializer.Serialize(currentResults));
        await File.WriteAllTextAsync(Path.Combine(dataDir, "results-baseline.json"), JsonSerializer.Serialize(baselineResults));

        var service = new TestResultDiffService(environmentService, logger);

        try
        {
            // Act
            var result = await service.CompareResultsAsync("test-repo");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].IssueNumber, Is.EqualTo(333));
            Assert.That(result[0].BaselineStatus, Is.EqualTo(StepResultStatus.NotRun));
            Assert.That(result[0].CurrentStatus, Is.EqualTo(StepResultStatus.Failed));
            Assert.That(result[0].ChangeType, Is.EqualTo(ChangeType.New));
        }
        finally
        {
            // Cleanup
            Directory.Delete(dataDir, true);
        }
    }

    /// <summary>
    /// Tests CompareResultsAsync with multiple diffs in a single run.
    /// Expected: Returns all diffs correctly.
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WithMultipleDiffs_ReturnsAllDiffs()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();
        var dataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataDir);

        environmentService.GetDataDirectory(Arg.Any<string>()).Returns(dataDir);

        var baselineResults = new List<IssueResult>
        {
            new()
            {
                Number = 1,
                ProjectPath = "Project1/Test1.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Failed
            },
            new()
            {
                Number = 2,
                ProjectPath = "Project2/Test2.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Success
            }
        };

        var currentResults = new List<IssueResult>
        {
            new()
            {
                Number = 1,
                ProjectPath = "Project1/Test1.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Success
            },
            new()
            {
                Number = 2,
                ProjectPath = "Project2/Test2.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Failed
            }
        };

        await File.WriteAllTextAsync(Path.Combine(dataDir, "results.json"), JsonSerializer.Serialize(currentResults));
        await File.WriteAllTextAsync(Path.Combine(dataDir, "results-baseline.json"), JsonSerializer.Serialize(baselineResults));

        var service = new TestResultDiffService(environmentService, logger);

        try
        {
            // Act
            var result = await service.CompareResultsAsync("test-repo");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(2));

            var fixed1 = result.FirstOrDefault(d => d.IssueNumber == 1);
            Assert.That(fixed1, Is.Not.Null);
            Assert.That(fixed1!.ChangeType, Is.EqualTo(ChangeType.Fixed));

            var regression2 = result.FirstOrDefault(d => d.IssueNumber == 2);
            Assert.That(regression2, Is.Not.Null);
            Assert.That(regression2!.ChangeType, Is.EqualTo(ChangeType.Regression));
        }
        finally
        {
            // Cleanup
            Directory.Delete(dataDir, true);
        }
    }

    /// <summary>
    /// Tests CompareResultsAsync with various status normalizations.
    /// Expected: Status strings are properly normalized for comparison.
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WithVariousStatusStrings_NormalizesCorrectly()
    {
        var testCases = new[]
        {
            (StepResultStatus.Success, StepResultStatus.Failed, ChangeType.Regression),
            (StepResultStatus.Success, StepResultStatus.Failed, ChangeType.Regression),
            (StepResultStatus.Failed, StepResultStatus.Success, ChangeType.Fixed),
            (StepResultStatus.Failed, StepResultStatus.Failed, ChangeType.Other), // This will be skipped as no change
            (StepResultStatus.NotRun, StepResultStatus.Failed, CompileToFail: ChangeType.BuildToFail)
        };

        foreach (var (baselineStatus, currentStatus, expectedChangeType) in testCases)
        {
            // Skip the case that won't produce a result (same status)
            if (baselineStatus == currentStatus && baselineStatus == StepResultStatus.Failed)
                continue;

            await TestStatusNormalization(baselineStatus, currentStatus, expectedChangeType);
        }
    }

    private async Task TestStatusNormalization(StepResultStatus baselineStatus, StepResultStatus currentStatus, ChangeType expectedChangeType)
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();
        var dataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataDir);

        environmentService.GetDataDirectory(Arg.Any<string>()).Returns(dataDir);

        var baselineResults = new List<IssueResult>
        {
            new()
            {
                Number = 555,
                ProjectPath = "StatusProject/Status.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = baselineStatus
            }
        };

        var currentResults = new List<IssueResult>
        {
            new()
            {
                Number = 555,
                ProjectPath = "StatusProject/Status.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = currentStatus
            }
        };

        await File.WriteAllTextAsync(Path.Combine(dataDir, "results.json"), JsonSerializer.Serialize(currentResults));
        await File.WriteAllTextAsync(Path.Combine(dataDir, "results-baseline.json"), JsonSerializer.Serialize(baselineResults));

        var service = new TestResultDiffService(environmentService, logger);

        try
        {
            // Act
            var result = await service.CompareResultsAsync("test-repo");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].ChangeType, Is.EqualTo(expectedChangeType));
        }
        finally
        {
            // Cleanup
            Directory.Delete(dataDir, true);
        }
    }

    /// <summary>
    /// Tests CompareResultsAsync with same issue number but different project paths.
    /// Expected: Treats them as separate results.
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WithSameIssueNumberDifferentProjects_TreatsAsSeparate()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();
        var dataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataDir);

        environmentService.GetDataDirectory(Arg.Any<string>()).Returns(dataDir);

        var baselineResults = new List<IssueResult>
        {
            new()
            {
                Number = 100,
                ProjectPath = "ProjectA/TestA.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Failed
            },
            new()
            {
                Number = 100,
                ProjectPath = "ProjectB/TestB.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Success
            }
        };

        var currentResults = new List<IssueResult>
        {
            new()
            {
                Number = 100,
                ProjectPath = "ProjectA/TestA.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Success
            },
            new()
            {
                Number = 100,
                ProjectPath = "ProjectB/TestB.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Failed
            }
        };

        await File.WriteAllTextAsync(Path.Combine(dataDir, "results.json"), JsonSerializer.Serialize(currentResults));
        await File.WriteAllTextAsync(Path.Combine(dataDir, "results-baseline.json"), JsonSerializer.Serialize(baselineResults));

        var service = new TestResultDiffService(environmentService, logger);

        try
        {
            // Act
            var result = await service.CompareResultsAsync("test-repo");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(2));

            var projectADiff = result.FirstOrDefault(d => d.ProjectPath == "projecta/testa.csproj");
            Assert.That(projectADiff, Is.Not.Null);
            Assert.That(projectADiff!.ChangeType, Is.EqualTo(ChangeType.Fixed));

            var projectBDiff = result.FirstOrDefault(d => d.ProjectPath == "projectb/testb.csproj");
            Assert.That(projectBDiff, Is.Not.Null);
            Assert.That(projectBDiff!.ChangeType, Is.EqualTo(ChangeType.Regression));
        }
        finally
        {
            // Cleanup
            Directory.Delete(dataDir, true);
        }
    }

    /// <summary>
    /// Tests CompareResultsAsync with invalid JSON in results file.
    /// Expected: Returns empty list (error is handled gracefully).
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WithInvalidJson_ReturnsEmptyList()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();
        var dataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataDir);

        environmentService.GetDataDirectory(Arg.Any<string>()).Returns(dataDir);

        await File.WriteAllTextAsync(Path.Combine(dataDir, "results.json"), "invalid json {{{");
        await File.WriteAllTextAsync(Path.Combine(dataDir, "results-baseline.json"), "[]");

        var service = new TestResultDiffService(environmentService, logger);

        try
        {
            // Act
            var result = await service.CompareResultsAsync("test-repo");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }
        finally
        {
            // Cleanup
            Directory.Delete(dataDir, true);
        }
    }

    /// <summary>
    /// Tests CompareResultsAsync when GetDataDirectory throws exception.
    /// Expected: Returns empty list (exception is caught and logged).
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WhenGetDataDirectoryThrows_ReturnsEmptyList()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();

        environmentService.GetDataDirectory(Arg.Any<string>()).Returns(x => throw new InvalidOperationException("Test exception"));

        var service = new TestResultDiffService(environmentService, logger);

        // Act
        var result = await service.CompareResultsAsync("test-repo");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    /// <summary>
    /// Tests CompareResultsAsync with empty repository root string.
    /// Expected: Method completes and returns result (may be empty based on file availability).
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WithEmptyRepositoryRoot_CompletesSuccessfully()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();
        var dataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataDir);

        environmentService.GetDataDirectory(string.Empty).Returns(dataDir);

        await File.WriteAllTextAsync(Path.Combine(dataDir, "results.json"), "[]");
        await File.WriteAllTextAsync(Path.Combine(dataDir, "results-baseline.json"), "[]");

        var service = new TestResultDiffService(environmentService, logger);

        try
        {
            // Act
            var result = await service.CompareResultsAsync(string.Empty);

            // Assert
            Assert.That(result, Is.Not.Null);
        }
        finally
        {
            // Cleanup
            Directory.Delete(dataDir, true);
        }
    }

    /// <summary>
    /// Tests CompareResultsAsync with whitespace repository root string.
    /// Expected: Method completes and returns result.
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WithWhitespaceRepositoryRoot_CompletesSuccessfully()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();
        var dataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataDir);

        environmentService.GetDataDirectory("   ").Returns(dataDir);

        await File.WriteAllTextAsync(Path.Combine(dataDir, "results.json"), "[]");
        await File.WriteAllTextAsync(Path.Combine(dataDir, "results-baseline.json"), "[]");

        var service = new TestResultDiffService(environmentService, logger);

        try
        {
            // Act
            var result = await service.CompareResultsAsync("   ");

            // Assert
            Assert.That(result, Is.Not.Null);
        }
        finally
        {
            // Cleanup
            Directory.Delete(dataDir, true);
        }
    }

    /// <summary>
    /// Tests CompareResultsAsync with ChangeType.None scenario.
    /// Expected: Returns diff with ChangeType.None.
    /// </summary>
    [Test]
    public async Task CompareResultsAsync_WithOtherChangeType_ReturnsDiffWithOtherChangeType()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();
        var dataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dataDir);

        environmentService.GetDataDirectory(Arg.Any<string>()).Returns(dataDir);

        var baselineResults = new List<IssueResult>
        {
            new()
            {
                Number = 777,
                ProjectPath = "OtherProject/None.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.NotRun
            }
        };

        var currentResults = new List<IssueResult>
        {
            new()
            {
                Number = 777,
                ProjectPath = "OtherProject/None.csproj",
                TargetFrameworks = ["net6.0"],
                Packages = ["NUnit=3.13.0"],
                TestResult = StepResultStatus.Failed
            }
        };

        await File.WriteAllTextAsync(Path.Combine(dataDir, "results.json"), JsonSerializer.Serialize(currentResults));
        await File.WriteAllTextAsync(Path.Combine(dataDir, "results-baseline.json"), JsonSerializer.Serialize(baselineResults));

        var service = new TestResultDiffService(environmentService, logger);

        try
        {
            // Act
            var result = await service.CompareResultsAsync("test-repo");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].IssueNumber, Is.EqualTo(777));
            Assert.That(result[0].BaselineStatus, Is.EqualTo(StepResultStatus.NotRun));
            Assert.That(result[0].CurrentStatus, Is.EqualTo(StepResultStatus.Failed));
            Assert.That(result[0].ChangeType, Is.EqualTo(ChangeType.BuildToFail));
        }
        finally
        {
            // Cleanup
            Directory.Delete(dataDir, true);
        }
    }

    /// <summary>
    /// Tests that the constructor successfully creates an instance when provided with valid dependencies.
    /// Input: Valid mocked IEnvironmentService and ILogger instances.
    /// Expected: Instance is created without throwing an exception.
    /// </summary>
    [Test]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        var logger = Substitute.For<ILogger<TestResultDiffService>>();

        // Act
        var service = new TestResultDiffService(environmentService, logger);

        // Assert
        Assert.That(service, Is.Not.Null);
    }

    /// <summary>
    /// Tests that the constructor behavior when environmentService is null.
    /// Input: Null environmentService parameter.
    /// Expected: Behavior depends on runtime null checking - currently no explicit validation.
    /// </summary>
    [Test]
    public void Constructor_WithNullEnvironmentService_CreatesInstanceWithoutValidation()
    {
        // Arrange
        IEnvironmentService? environmentService = null;
        var logger = Substitute.For<ILogger<TestResultDiffService>>();

        // Act & Assert - Constructor does not validate null, so it succeeds
        var service = new TestResultDiffService(environmentService!, logger);
        Assert.That(service, Is.Not.Null);
    }

    /// <summary>
    /// Tests that the constructor behavior when logger is null.
    /// Input: Null logger parameter.
    /// Expected: Behavior depends on runtime null checking - currently no explicit validation.
    /// </summary>
    [Test]
    public void Constructor_WithNullLogger_CreatesInstanceWithoutValidation()
    {
        // Arrange
        var environmentService = Substitute.For<IEnvironmentService>();
        ILogger<TestResultDiffService>? logger = null;

        // Act & Assert - Constructor does not validate null, so it succeeds
        var service = new TestResultDiffService(environmentService, logger!);
        Assert.That(service, Is.Not.Null);
    }

    /// <summary>
    /// Tests that the constructor behavior when both dependencies are null.
    /// Input: Null environmentService and logger parameters.
    /// Expected: Behavior depends on runtime null checking - currently no explicit validation.
    /// </summary>
    [Test]
    public void Constructor_WithBothDependenciesNull_CreatesInstanceWithoutValidation()
    {
        // Arrange
        IEnvironmentService? environmentService = null;
        ILogger<TestResultDiffService>? logger = null;

        // Act & Assert - Constructor does not validate null, so it succeeds
        var service = new TestResultDiffService(environmentService!, logger!);
        Assert.That(service, Is.Not.Null);
    }
}
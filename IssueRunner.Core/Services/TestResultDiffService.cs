using IssueRunner.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IssueRunner.Services;

/// <summary>
/// Service for comparing test results between baseline and current.
/// </summary>
public sealed class TestResultDiffService : ITestResultDiffService
{
    private readonly IEnvironmentService _environmentService;
    private readonly ILogger<TestResultDiffService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestResultDiffService"/> class.
    /// </summary>
    public TestResultDiffService(
        IEnvironmentService environmentService,
        ILogger<TestResultDiffService> logger)
    {
        _environmentService = environmentService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<TestResultDiff>> CompareResultsAsync(string repositoryRoot)
    {
        var diffs = new List<TestResultDiff>();
       
        try
        {
            var dataDir = _environmentService.GetDataDirectory(repositoryRoot);
            // Load current results from results.json
            var currentResults = await LoadResultsAsync(dataDir, "results.json");

            // Load baseline results from results-baseline.json
            var baselineResults = await LoadResultsAsync(dataDir, "results-baseline.json");

            // Create dictionaries for easy lookup (key: "Number|ProjectPath")
            // Normalize project path to lowercase for case-insensitive matching
            var currentDict = currentResults
                .ToDictionary(r => $"{r.Number}|{r.ProjectPath.ToLowerInvariant()}", r => r, StringComparer.OrdinalIgnoreCase);

            var baselineDict = baselineResults
                .ToDictionary(r => $"{r.Number}|{r.ProjectPath.ToLowerInvariant()}", r => r, StringComparer.OrdinalIgnoreCase);

            // Get all unique keys from both current and baseline
            var allKeys = currentDict.Keys
                .Union(baselineDict.Keys)
                .Distinct()
                .ToList();

            foreach (var key in allKeys)
            {
                var parts = key.Split('|');
                if (parts.Length != 2)
                {
                    continue;
                }

                if (!int.TryParse(parts[0], out var issueNumber))
                {
                    continue;
                }

                // Project path is already normalized to lowercase in the dictionary key
                var projectPath = parts[1];

                // Determine baseline status from IssueResult.TestResult

                baselineDict.TryGetValue(key, out var baselineResult);


                // Determine current status from IssueResult.TestResult
                currentDict.TryGetValue(key, out var currentResult);

                // Skip if no change
                if ((baselineResult == null && currentResult == null)
                    || Equals(baselineResult, currentResult)
                    || baselineResult?.RunResult == RunResult.Skipped
                    || currentResult?.RunResult == RunResult.Skipped)
                {
                    continue;
                }

                // Determine change type
                var changeType = DetermineChangeType(baselineResult, currentResult);

                // Skip if change type is Skipped (fail -> skipped) - but this shouldn't happen with RunResult
                if (changeType == ChangeType.Skipped)
                {
                    continue;
                }

                diffs.Add(new TestResultDiff
                {
                    IssueNumber = issueNumber,
                    ProjectPath = baselineResult?.ProjectPath ?? currentResult?.ProjectPath ?? projectPath,
                    BaselineStatus = baselineResult?.TestResult ?? StepResultStatus.NotRun,
                    CurrentStatus = currentResult?.TestResult ?? StepResultStatus.NotRun,
                    ChangeType = changeType,
                    BaselineResult = baselineResult,
                    CurrentResult = currentResult
                });
            }

            return diffs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing test results");
            return diffs;
        }
    }

    private static ChangeType DetermineChangeType(IssueResult? baselineResult, IssueResult? currentResult)
    {
        if (baselineResult == null && currentResult == null)
        {
            // (should be filtered out earlier)
            return ChangeType.None;
        }

        if (baselineResult == null && currentResult != null)
        {
            // New: Did not exist before, now exists (Blue)
            return ChangeType.New;
        }

        if (baselineResult != null && currentResult == null)
        {
            // Deleted: Existed before, now does not exist (Dark Grey)
            return ChangeType.Deleted;
        }



        // Fixed: Was non-success, now success (Green)
        if (baselineResult!.TestResult != StepResultStatus.Success && currentResult!.TestResult == StepResultStatus.Success)
        {
            return ChangeType.Fixed;
        }

        // Regression: Was success, now fail (Red) 
        if (baselineResult.TestResult == StepResultStatus.Success && currentResult!.TestResult == StepResultStatus.Failed)
            return ChangeType.Regression;
        // BuildToFail: Was not run, now fail (Orange)
        if ((baselineResult.TestResult == StepResultStatus.NotRun || baselineResult.RunResult== RunResult.NotRun) && currentResult!.TestResult == StepResultStatus.Failed)
            return ChangeType.BuildToFail;
        return ChangeType.Other;
    }

    /// <summary>
    /// Loads IssueResult list from results.json or results-baseline.json.
    /// </summary>
    private async Task<List<IssueResult>> LoadResultsAsync(string dataDir, string fileName)
    {
        var filePath = Path.Combine(dataDir, fileName);
        if (!File.Exists(filePath))
        {
            return new List<IssueResult>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            };
            var results = JsonSerializer.Deserialize<List<IssueResult>>(json, options);
            return results ?? new List<IssueResult>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load results from {Path}", filePath);
            return new List<IssueResult>();
        }
    }
}


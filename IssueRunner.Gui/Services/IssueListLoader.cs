using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IssueRunner.Gui.ViewModels;
using IssueRunner.Models;
using IssueRunner.Services;

namespace IssueRunner.Gui.Services;

/// <summary>
/// Default implementation of <see cref="IIssueListLoader"/> that builds issue list items
/// from metadata, results, diffs, and marker files.
/// </summary>
public sealed class IssueListLoader(
    IEnvironmentService environmentService,
    ITestExecutionService testExecutionService,
    IProjectAnalyzerService projectAnalyzerService,
    ITestResultDiffService diffService,
    IMarkerService markerService)
    : IIssueListLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public async Task<IssueListLoadResult> LoadIssuesAsync(
        string repositoryRoot,
        Dictionary<int, string> folders,
        IssueViewMode viewMode = IssueViewMode.Current,
        Action<string>? log = null)
    {
        var issues = new List<IssueListItem>();

        // Get repository config for GitHub URL generation
        var repoConfig = environmentService.RepositoryConfig;
        var baseUrl = repoConfig != null && !string.IsNullOrEmpty(repoConfig.Owner) && !string.IsNullOrEmpty(repoConfig.Name)
            ? $"https://github.com/{repoConfig.Owner}/{repoConfig.Name}/issues/"
            : "";

        // Load metadata
        var dataDir = environmentService.GetDataDirectory(repositoryRoot);
        var metadataPath = Path.Combine(dataDir, "issues_metadata.json");
        var metadataDict = new Dictionary<int, IssueMetadata>();

        log?.Invoke(
            $"Debug: Checking metadata at {metadataPath}, RepositoryPath={repositoryRoot}, DataDir={dataDir}, Exists={File.Exists(metadataPath)}");

        if (File.Exists(metadataPath))
        {
            try
            {
                var metadataJson = await File.ReadAllTextAsync(metadataPath);
                var metadata = JsonSerializer.Deserialize<List<IssueMetadata>>(metadataJson, JsonOptions) ?? [];

                metadataDict = metadata.ToDictionary(m => m.Number, m => m);

            }
            catch (Exception ex)
            {
                log?.Invoke($"Warning: Could not load metadata: {ex.Message}");
            }
        }
        else
        {
            log?.Invoke($"Warning: Metadata file not found at {metadataPath}");
        }

        // Load test results - use baseline when viewMode is Baseline, otherwise use current
        var resultsPath = viewMode == IssueViewMode.Baseline
            ? Path.Combine(dataDir, "results-baseline.json")
            : Path.Combine(dataDir, "results.json");
        var baselineResultsPath = Path.Combine(dataDir, "results-baseline.json");
        var resultsByIssue = new Dictionary<int, List<IssueResult>>(); // Store full result lists
        var resultsByIssueForComparison = new Dictionary<int, (string Result, string LastRun)>(); // For baseline comparison
        var baselineResultsByIssue = new Dictionary<int, (string Result, string LastRun)>();
        var failedRestores = new HashSet<int>(); // Track issues with restore failures from results.json
        var failedBuilds = new HashSet<int>(); // Track issues with build failures from results.json
        var restoreErrors = new Dictionary<int, string>(); // Track restore error messages by issue number

        // If Baseline view is selected but baseline file doesn't exist, show empty list
        if (viewMode == IssueViewMode.Baseline && !File.Exists(resultsPath))
        {
            log?.Invoke("Baseline results file not found. Baseline view will be empty.");
            return new IssueListLoadResult
            {
                Issues = new List<IssueListItem>(),
                IssueChanges = new Dictionary<string, ChangeType>(),
                RepositoryBaseUrl = baseUrl
            };
        }

        // Always load baseline results when available for ChangeType calculation
        if (File.Exists(baselineResultsPath) && viewMode != IssueViewMode.Baseline)
        {
            try
            {
                var baselineResultsJson = await File.ReadAllTextAsync(baselineResultsPath);
                var allBaselineResults = JsonSerializer.Deserialize<List<IssueResult>>(baselineResultsJson, JsonOptions);
                if (allBaselineResults != null)
                {
                    var baselineResultsByIssueNumber = allBaselineResults
                        .GroupBy(r => r.Number)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    foreach (var kvp in baselineResultsByIssueNumber)
                    {
                        var issueNum = kvp.Key;
                        var issueResults = kvp.Value;
                        var (worstStatus, lastRun) = IssueListItem.DetermineWorstResult(issueResults);
                        baselineResultsByIssue[issueNum] = (worstStatus, lastRun);
                    }
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Warning: Could not load baseline results: {ex.Message}");
            }
        }

        if (File.Exists(resultsPath))
        {
            try
            {
                var resultsJson = await File.ReadAllTextAsync(resultsPath);
                var allResults = JsonSerializer.Deserialize<List<IssueResult>>(resultsJson, JsonOptions);
                if (allResults != null)
                {
                    // Group results by issue number
                    var resultsByIssueNumber = allResults
                        .GroupBy(r => r.Number)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    // For each issue, store the full result list and determine the worst result for comparison
                    foreach (var kvp in resultsByIssueNumber)
                    {
                        var issueNum = kvp.Key;
                        var issueResults = kvp.Value;

                        // Store the full result list
                        resultsByIssue[issueNum] = issueResults;

                        // Check if this issue has any restore failures
                        // A restore failure is indicated by RestoreResult == Failed OR RestoreError having content
                        var restoreFailure = issueResults.FirstOrDefault(r =>
                            r.RestoreResult == StepResultStatus.Failed ||
                            !string.IsNullOrWhiteSpace(r.RestoreError));
                        if (restoreFailure != null)
                        {
                            failedRestores.Add(issueNum);
                            // Store the restore error message if available
                            if (!string.IsNullOrWhiteSpace(restoreFailure.RestoreError))
                            {
                                restoreErrors[issueNum] = restoreFailure.RestoreError;
                            }
                        }
                        // Check for build failures (only if restore didn't fail)
                        else if (issueResults.Any(r => r.BuildResult == StepResultStatus.Failed))
                        {
                            failedBuilds.Add(issueNum);
                        }

                        var (worstStatus, lastRun) = IssueListItem.DetermineWorstResult(issueResults);
                        resultsByIssueForComparison[issueNum] = (worstStatus, lastRun);
                    }
                }
            }
            catch
            {
                // Ignore result loading errors for now
            }
        }

        // Load diff data
        var diffs = await diffService.CompareResultsAsync(repositoryRoot);
        var issueChanges = new Dictionary<string, ChangeType>();
        var issueStatusDisplay = new Dictionary<int, string>();
        
        // Group diffs by issue number
        var diffsByIssue = diffs
            .GroupBy(d => d.IssueNumber)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var diff in diffs)
        {
            var key = $"Issue{diff.IssueNumber}|{diff.ProjectPath}";
            issueChanges[key] = diff.ChangeType;

            issueStatusDisplay[diff.IssueNumber] = diff.ChangeType switch
            {
                // Set StatusDisplay for regressions (show "=> fail")
                ChangeType.Regression => "=> fail",
                ChangeType.Fixed or ChangeType.CompileToFail or ChangeType.Other => diff.CurrentStatus,
                _ => issueStatusDisplay[diff.IssueNumber]
            };
        }

        log?.Invoke($"Discovered {folders.Count} issue folders to load");

        if (folders.Count == 0)
        {
            log?.Invoke("Warning: No issue folders found. The IssueListView will be empty.");
        }

        // Build issue list
        foreach (var kvp in folders.OrderBy(k => k.Key))
        {
            var issueNum = kvp.Key;
            var folderPath = kvp.Value;
            var metadata = metadataDict.GetValueOrDefault(issueNum);
            
            // Get results list for this issue
            var issueResults = resultsByIssue.TryGetValue(issueNum, out var results) ? results : null;
            
            // Get diffs list for this issue
            var issueDiffs = diffsByIssue.TryGetValue(issueNum, out var issueDiffsList) ? issueDiffsList : null;
            
            // Get worst result for comparison with baseline
            var result = resultsByIssueForComparison.TryGetValue(issueNum, out var r) ? r : (Result: "Not tested", LastRun: "");

            // Determine TestTypes based on whether issue has custom scripts
            var hasCustomScripts = testExecutionService.HasCustomRunners(folderPath);
            // Column display values: "Scripts" or "DotNet test"
            var testTypes = hasCustomScripts ? "Scripts" : "DotNet test";

            // Determine Framework type (.Net or .Net Framework)
            var framework = "";
            try
            {
                var projectFiles = projectAnalyzerService.FindProjectFiles(folderPath);
                var hasNetFx = false;
                var hasNet = false;

                foreach (var projectFile in projectFiles)
                {
                    var (targetFrameworks, _) = projectAnalyzerService.ParseProjectFile(projectFile);
                    foreach (var tfm in targetFrameworks)
                    {
                        // Check if it's .NET Framework (net35, net40, net45, net451, net452, net46, net461, net462, net47, net471, net472, net48, net481)
                        if (tfm.StartsWith("net", StringComparison.OrdinalIgnoreCase) &&
                            !tfm.StartsWith("netcoreapp", StringComparison.OrdinalIgnoreCase) &&
                            !tfm.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
                        {
                            // Check if it's a numeric framework (net35, net40, etc.) or net4xx
                            var tfmLower = tfm.ToLowerInvariant();
                            if (tfmLower == "net35" || tfmLower == "net40" ||
                                tfmLower.StartsWith("net4") || tfmLower.StartsWith("net3"))
                            {
                                hasNetFx = true;
                            }
                            else if (tfmLower.StartsWith("net5") || tfmLower.StartsWith("net6") ||
                                     tfmLower.StartsWith("net7") || tfmLower.StartsWith("net8") ||
                                     tfmLower.StartsWith("net9") || tfmLower.StartsWith("net10"))
                            {
                                hasNet = true;
                            }
                        }
                    }
                }

                // Prioritize .NET Framework if both are present
                if (hasNetFx)
                {
                    framework = ".Net Framework";
                }
                else if (hasNet)
                {
                    framework = ".Net";
                }
                // If no projects found or no frameworks detected, leave empty (will show in "All")
            }
            catch
            {
                // If framework detection fails, leave empty
            }

            // Determine state value and detailed state
            // State logic: New (metadata only, just arrived from GitHub), Synced (has metadata and folder),
            // Failed restore/compile (from results.json build_result or restore_result),
            // Runnable (ready to run), Skipped (has marker file)
            IssueState stateValue;
            var detailedState = metadata?.State.ToString() ?? "Unknown";
            string? notTestedReason = null;

            if (markerService.ShouldSkipIssue(folderPath))
            {
                var markerReason = markerService.GetMarkerReason(folderPath);
                detailedState = "skipped";
                stateValue = IssueState.Skipped;
                // Always show marker reason for skipped issues, regardless of test result
                notTestedReason = markerReason;
            }
            else if (failedRestores.Contains(issueNum))
            {
                // failedRestores is populated from results.json (checks restore_result == "fail" or RestoreError)
                stateValue = IssueState.FailedRestore;
                detailedState = "not restored";
                // Include restore error message if available
                notTestedReason = restoreErrors.TryGetValue(issueNum, out var restoreError) 
                    ? $"Restore failed: {restoreError.Trim()}" 
                    : "Restore failed";
            }
            else if (failedBuilds.Contains(issueNum))
            {
                // failedBuilds is populated from results.json (checks build_result == "fail")
                stateValue = IssueState.FailedCompile;
                detailedState = "not compiling";
                if (result.Result == "Not tested" || result.Result == "NotRun" || string.IsNullOrEmpty(result.Result))
                {
                    notTestedReason = "Not compiling";
                }
            }
            else if (metadata != null)
            {
                // No test results - could be New (just synced) or Synced (ready to process)
                // For now, if it has metadata, consider it Synced (has been synced with GitHub and folders)
                // Has metadata - determine state based on test results
                // New: just synced from GitHub, no test results yet
                // Synced: has metadata and folder, ready to process
                // Runnable: has been tested (has test result)
                stateValue = string.IsNullOrEmpty(result.Result) || result.Result == "Not tested" || result.Result == "NotRun"
                    ? IssueState.Synced
                    : IssueState.Runnable;
            }
            else
            {
                // No metadata - created before, synced with GitHub and folders
                stateValue = IssueState.Synced;
            }

            // Check if this issue has a change
            var changeType = ChangeType.None;
            string? statusDisplay = null; // Null by default, will use TestResult via TargetNullValue, or set if there's a change
            string? changeTooltip = null;

            // Calculate ChangeType when baseline exists (for coloring in Current view and filtering in Diff view)
            var baselineExists = baselineResultsByIssue.TryGetValue(issueNum, out var baselineResult);
            var currentExists = resultsByIssueForComparison.TryGetValue(issueNum, out var currentResultTuple);
            
            // Only set ChangeType if issue exists in BOTH baseline and current with different status
            // New issues (only in current) or removed issues (only in baseline) should not show in Diff view
            if (baselineExists && currentExists)
            {
                // Both exist - compare them
                // Parse enum strings from DetermineWorstResult
                var baselineStatusEnum = Enum.TryParse<StepResultStatus>(baselineResult.Result, true, out var bs) ? bs : StepResultStatus.NotRun;
                var currentStatusEnum = Enum.TryParse<StepResultStatus>(currentResultTuple.Result, true, out var cs) ? cs : StepResultStatus.NotRun;
                
                if (baselineStatusEnum != currentStatusEnum)
                {
                    changeType = DetermineChangeType(baselineStatusEnum, currentStatusEnum);
                    changeTooltip = FormatChangeTooltip(baselineStatusEnum.ToString(), currentStatusEnum.ToString());
                    
                    statusDisplay = changeType switch
                    {
                        ChangeType.Regression => "=> fail",
                        ChangeType.Fixed or ChangeType.CompileToFail or ChangeType.Other => currentStatusEnum.ToString(),
                        _ => statusDisplay
                    };
                }
                // If baselineStatus == currentStatus, changeType remains None (no change)
            }
            // If issue only exists in one (new or removed), changeType remains None (don't show in Diff view)

            issues.Add(new IssueListItem
            {
                Metadata = metadata,
                Results = issueResults,
                Diffs = issueDiffs,
                StateValue = stateValue,
                DetailedState = detailedState,
                NotTestedReason = notTestedReason,
                TestTypes = testTypes,
                GitHubUrl = !string.IsNullOrEmpty(baseUrl) ? $"{baseUrl}{issueNum}" : "",
                Framework = framework,
                ChangeType = changeType,
                StatusDisplay = statusDisplay,
                ChangeTooltip = changeTooltip
            });
        }

        return new IssueListLoadResult
        {
            Issues = issues,
            IssueChanges = issueChanges,
            RepositoryBaseUrl = baseUrl
        };
    }

    private static ChangeType DetermineChangeType(StepResultStatus baselineStatus, StepResultStatus currentStatus)
    {
        // Fixed: Was non-success, now success (Green)
        if (baselineStatus != StepResultStatus.Success && currentStatus == StepResultStatus.Success)
        {
            return ChangeType.Fixed;
        }

        // Regression: Was success, now fail (Red)
        if (baselineStatus == StepResultStatus.Success && currentStatus == StepResultStatus.Failed)
        {
            return ChangeType.Regression;
        }

        // CompileToFail: Was not run, now fail (Orange)
        if (baselineStatus == StepResultStatus.NotRun && currentStatus == StepResultStatus.Failed)
        {
            return ChangeType.CompileToFail;
        }

        // Other: Any other status change
        if (baselineStatus != currentStatus)
        {
            return ChangeType.Other;
        }

        return ChangeType.None;
    }

    private static string FormatChangeTooltip(string baselineStatus, string currentStatus)
    {
        var baselineDisplay = FormatStatusForTooltip(baselineStatus);
        var currentDisplay = FormatStatusForTooltip(currentStatus);
        return $"{baselineDisplay} -> {currentDisplay}";
    }

    private static string FormatStatusForTooltip(string status)
    {
        return status switch
        {
            "Success" or "success" => "Test Succeeds",
            "Failed" or "fail" => "Test Fails",
            "NotRun" or "not run" => "Not Run",
            "not compile" => "Not Compiling",
            "Skipped" or "skipped" => "Skipped",
            _ => status
        };
    }
}


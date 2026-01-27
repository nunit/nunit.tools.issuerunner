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
                
                // Handle empty or whitespace files
                if (string.IsNullOrWhiteSpace(metadataJson))
                {
                    log?.Invoke("Warning: Metadata file is empty or contains only whitespace");
                }
                else
                {
                    var metadata = JsonSerializer.Deserialize<List<IssueMetadata>>(metadataJson, JsonOptions) ?? [];
                    
                    // Filter out entries with invalid Number values before creating dictionary
                    var validMetadata = metadata.Where(m => m.Number > 0).ToList();
                    
                    if (validMetadata.Count != metadata.Count)
                    {
                        log?.Invoke($"Warning: Filtered out {metadata.Count - validMetadata.Count} metadata entries with invalid numbers");
                    }
                    
                    metadataDict = validMetadata.ToDictionary(m => m.Number, m => m);
                }
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

            if (diff.ChangeType ==
                // Set StatusDisplay for regressions (show "=> fail")
                ChangeType.Regression)
                issueStatusDisplay[diff.IssueNumber] = "=> fail";
            else if (diff.ChangeType is ChangeType.Fixed or ChangeType.BuildToFail or ChangeType.Other)
                issueStatusDisplay[diff.IssueNumber] = diff.CurrentStatus.ToString();
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
            
            try
            {
                var metadata = metadataDict.GetValueOrDefault(issueNum);

                // Get results list for this issue
                var issueResults = resultsByIssue.TryGetValue(issueNum, out var results) ? results : null;

                // Get diffs list for this issue
                var issueDiffs = diffsByIssue.TryGetValue(issueNum, out var issueDiffsList) ? issueDiffsList : null;

                // Get worst result for comparison with baseline
                var result = resultsByIssueForComparison.TryGetValue(issueNum, out var r) ? r : (Result: "Not tested", LastRun: "");

                // Determine RunType based on whether issue has custom scripts
                var hasCustomScripts = testExecutionService.HasCustomRunners(folderPath);
                // Column display values: "Scripts" or "DotNet test"
                var runType = hasCustomScripts ? RunType.Script : RunType.DotNet;

                // Early check for csproj files
                var projectFiles = projectAnalyzerService.FindProjectFiles(folderPath);
                
                // Determine Framework type (.Net or .Net Framework)
                var framework = Frameworks.None;
                try
                {
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
                        framework = Frameworks.DotNetFramework;
                    }
                    else if (hasNet)
                    {
                        framework = Frameworks.DotNet;
                    }
                    // If no projects found or no frameworks detected, leave empty (will show in "All")
                }
                catch
                {
                    // If framework detection fails, leave empty
                }

                // Determine state value and detailed state
                // State logic: Marker file → No csproj → Missing metadata/initial_state → Failed restore → Failed build → Normal processing
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
                else if (projectFiles.Count == 0)
                {
                    // No csproj files found - nothing to run
                    stateValue = IssueState.NothingToRun;
                    detailedState = "no projects";
                    notTestedReason = "No project files found";
                }
                else
                {
                    // Check for metadata file in issue folder
                    var issueMetadataPath = Path.Combine(folderPath, "issue_metadata.json");
                    var hasIssueMetadata = File.Exists(issueMetadataPath);
                    if (hasIssueMetadata)
                    {
                        try
                        {
                            var issueMetadataJson = await File.ReadAllTextAsync(issueMetadataPath);
                            // Check if file is empty or contains only whitespace
                            hasIssueMetadata = !string.IsNullOrWhiteSpace(issueMetadataJson);
                        }
                        catch
                        {
                            // If reading fails, consider it invalid/empty
                            hasIssueMetadata = false;
                        }
                    }

                    // Check for initial_state file
                    var initialStatePath = Path.Combine(folderPath, "issue_initialstate.json");
                    var hasInitialState = File.Exists(initialStatePath);

                    // If missing metadata or initial_state, mark as NotSynced
                    if (!hasIssueMetadata || !hasInitialState)
                    {
                        stateValue = IssueState.NotSynced;
                        detailedState = "not synced";
                        if (!hasIssueMetadata && !hasInitialState)
                        {
                            notTestedReason = "Missing metadata and initial state files";
                        }
                        else if (!hasIssueMetadata)
                        {
                            notTestedReason = "Missing metadata file";
                        }
                        else
                        {
                            notTestedReason = "Missing initial state file";
                        }
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
                }

                // Check if this issue has a change based on detailed diffs from TestResultDiffService
                // Any underlying diff (including restore/build/skip transitions) should surface in Diff view.
                var changeType = ChangeType.None;
                string? statusDisplay = null; // Null by default, will use TestResult via TargetNullValue, or set if there's a change
                string? changeTooltip = null;

                if (issueDiffs is { Count: > 0 })
                {
                    // Choose a representative ChangeType for the issue.
                    // Prefer more severe/interesting changes (Regression > BuildToFail > Fixed > Other > New/Deleted/None).
                    static int GetSeverity(ChangeType ct) => ct switch
                    {
                        ChangeType.Regression => 5,
                        ChangeType.BuildToFail => 4,
                        ChangeType.Fixed => 3,
                        ChangeType.Other => 2,
                        ChangeType.New or ChangeType.Deleted => 1,
                        _ => 0
                    };

                    var representativeDiff = issueDiffs
                        .OrderByDescending(d => GetSeverity(d.ChangeType))
                        .First();

                    changeType = representativeDiff.ChangeType;

                    // StatusDisplay: reuse the per-issue status display computed from diffs when available
                    issueStatusDisplay.TryGetValue(issueNum, out statusDisplay);

                    // Tooltip: show baseline -> current status for the representative diff
                    changeTooltip = FormatChangeTooltip(
                        representativeDiff.BaselineStatus.ToString(),
                        representativeDiff.CurrentStatus.ToString());
                }

                issues.Add(new IssueListItem
                {
                    Metadata = metadata,
                    IssueNumber = issueNum, // Set issue number from folder name (used when metadata is missing)
                    Results = issueResults,
                    Diffs = issueDiffs,
                    StateValue = stateValue,
                    DetailedState = detailedState,
                    NotTestedReason = notTestedReason,
                    RunType = runType,
                    GitHubUrl = !string.IsNullOrEmpty(baseUrl) ? $"{baseUrl}{issueNum}" : "",
                    Framework = framework,
                    ChangeType = changeType,
                    StatusDisplay = statusDisplay,
                    ChangeTooltip = changeTooltip
                });
            }
            catch (Exception ex)
            {
                // Log error but continue processing other issues
                log?.Invoke($"Warning: Failed to load issue {issueNum}: {ex.Message}");
                
                // Add a basic issue entry so it still appears in the list
                issues.Add(new IssueListItem
                {
                    Metadata = metadataDict.GetValueOrDefault(issueNum),
                    IssueNumber = issueNum, // Set issue number from folder name (used when metadata is missing)
                    Results = null,
                    Diffs = null,
                    StateValue = IssueState.Synced,
                    DetailedState = "error",
                    NotTestedReason = $"Error loading issue: {ex.Message}",
                    RunType = RunType.DotNet,
                    GitHubUrl = !string.IsNullOrEmpty(baseUrl) ? $"{baseUrl}{issueNum}" : "",
                    Framework = Frameworks.None,
                    ChangeType = ChangeType.None,
                    StatusDisplay = null,
                    ChangeTooltip = null
                });
            }
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

        // BuildToFail: Was not run, now fail (Orange)
        if (baselineStatus == StepResultStatus.NotRun && currentStatus == StepResultStatus.Failed)
        {
            return ChangeType.BuildToFail;
        }

        // None: Any other status change
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


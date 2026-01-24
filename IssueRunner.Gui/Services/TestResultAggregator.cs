using IssueRunner.Models;
using IssueRunner.Services;

namespace IssueRunner.Gui.Services;

/// <summary>
/// Default implementation that aggregates raw <see cref="IssueResult"/> rows into
/// a single status per issue, matching the rules used in the repository summary.
/// </summary>
public sealed class TestResultAggregator : ITestResultAggregator
{
    public IReadOnlyList<AggregatedIssueResult> AggregatePerIssue(
        Dictionary<int, string> folders,
        IReadOnlyList<IssueResult> allResults,
        IMarkerService markerService,
        Action<string>? log = null)
    {
        var resultsByIssue = allResults
            .GroupBy(r => r.Number)
            .ToDictionary(g => g.Key, g => g.ToList());

        var aggregated = new List<AggregatedIssueResult>();

        foreach (var (issueNumber, folderPath) in folders)
        {
            var status = AggregatedIssueStatus.NotTested;
            string? lastRun = null;

            try
            {
                if (markerService.ShouldSkipIssue(folderPath))
                {
                    status = AggregatedIssueStatus.Skipped;
                }
                else if (resultsByIssue.TryGetValue(issueNumber, out var issueResults))
                {
                    // Check RunResult first
                    var mostRecent = issueResults
                        .OrderByDescending(r => r.LastRun)
                        .FirstOrDefault();

                    if (mostRecent?.RunResult == RunResult.Skipped)
                    {
                        status = AggregatedIssueStatus.Skipped;
                        lastRun = mostRecent.LastRun;
                    }
                    else if (mostRecent?.RunResult == RunResult.NotSynced)
                    {
                        status = AggregatedIssueStatus.NotTested;
                        lastRun = mostRecent.LastRun;
                    }
                    else if (mostRecent?.RunResult == RunResult.Run)
                    {
                        // Restore failure has highest priority after skipped/not synced
                        var restoreFailure = issueResults.FirstOrDefault(r =>
                            r.RestoreResult == StepResultStatus.Failed ||
                            !string.IsNullOrWhiteSpace(r.RestoreError));

                        if (restoreFailure != null)
                        {
                            status = AggregatedIssueStatus.NotRestored;
                            lastRun = restoreFailure.LastRun;
                        }
                        else if (issueResults.Any(r => r.BuildResult == StepResultStatus.Failed))
                        {
                            status = AggregatedIssueStatus.NotCompiling;
                            lastRun = issueResults
                                .Where(r => r.LastRun != null)
                                .Select(r => r.LastRun!)
                                .DefaultIfEmpty()
                                .Max();
                        }
                        else
                        {
                            // Determine status from test_result on the most recent row
                            lastRun = mostRecent?.LastRun;

                            if (mostRecent?.TestResult == StepResultStatus.Success)
                            {
                                status = AggregatedIssueStatus.Passed;
                            }
                            else if (mostRecent?.TestResult == StepResultStatus.Failed)
                            {
                                status = AggregatedIssueStatus.Failed;
                            }
                            else
                            {
                                status = AggregatedIssueStatus.NotTested;
                            }
                        }
                    }
                    else
                    {
                        // RunResult is NotRun or null - treat as not tested
                        status = AggregatedIssueStatus.NotTested;
                        lastRun = mostRecent?.LastRun;
                    }
                }
                else
                {
                    // No entry in results.json
                    status = AggregatedIssueStatus.NotTested;
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Warning: Failed to aggregate results for Issue {issueNumber}: {ex.Message}");
                status = AggregatedIssueStatus.NotTested;
            }

            aggregated.Add(new AggregatedIssueResult
            {
                Number = issueNumber,
                Status = status,
                LastRun = lastRun
            });
        }

        return aggregated;
    }
}


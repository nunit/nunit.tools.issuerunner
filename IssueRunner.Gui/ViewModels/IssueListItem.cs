using System.Linq;
using IssueRunner.Models;

namespace IssueRunner.Gui.ViewModels;

/// <summary>
/// Represents an issue in the list.
/// </summary>
public class IssueListItem
{
    // Member properties - actual model classes
    public IssueMetadata? Metadata { get; set; }
    public List<IssueResult>? Results { get; set; }
    public List<TestResultDiff>? Diffs { get; set; }

    // Computed properties from Metadata
    public int Number => Metadata?.Number ?? 0;
    public string Title => Metadata?.Title ?? $"Issue {Number}";
    public GithubIssueState State => Metadata?.State ?? GithubIssueState.Open;
    public string? Milestone => Metadata?.Milestone;
    public string? Type
    {
        get
        {
            if (Metadata?.Labels == null)
                return null;
            
            var typeLabel = Metadata.Labels.FirstOrDefault(l => 
                l.StartsWith("is:", StringComparison.OrdinalIgnoreCase));
            if (typeLabel != null)
            {
                // Extract the part after "is:" (e.g., "is:bug" -> "bug")
                return typeLabel.Substring(3).Trim();
            }
            return null;
        }
    }

    // Computed properties from Results
    public string TestResult
    {
        get
        {
            if (Results == null || Results.Count == 0)
                return "Not tested";
            
            // Check for restore failures first (highest priority)
            var restoreFailure = Results.FirstOrDefault(r =>
                r.RestoreResult == StepResultStatus.Failed ||
                !string.IsNullOrWhiteSpace(r.RestoreError));
            
            if (restoreFailure != null)
            {
                return "not restored";
            }
            
            // Check for build failures
            var buildFailure = Results.FirstOrDefault(r => r.BuildResult == StepResultStatus.Failed);
            if (buildFailure != null)
            {
                return "not compiling";
            }
            
            // Otherwise, determine worst test result
            var (result, _) = DetermineWorstResult(Results);
            return result;
        }
    }

    public string LastRun
    {
        get
        {
            if (Results == null || Results.Count == 0)
                return "";
            
            var (_, lastRun) = DetermineWorstResult(Results);
            if (!string.IsNullOrEmpty(lastRun) &&
                DateTime.TryParse(lastRun, out var dt))
            {
                return dt.ToString("yyyy-MM-dd HH:mm");
            }
            return lastRun ?? "";
        }
    }

    // Settable properties (computed in IssueListLoader from folder/services)
    public IssueState StateValue { get; set; } = IssueState.New; // New state enum value
    public string DetailedState { get; set; } = ""; // Enhanced state (includes not compiling, skipped, etc.) - for display
    public string? NotTestedReason { get; set; } // Why it's not tested (marker reason, not compiling, etc.)
    public RunType RunType { get; set; } = RunType.All; // "Scripts" or "DotNet test"
    public string GitHubUrl { get; set; } = ""; // GitHub issue URL
    public Frameworks Framework { get; set; } = Frameworks.None; // ".Net" or ".Net Framework"
    
    // ChangeType, StatusDisplay, and ChangeTooltip are computed in IssueListLoader from baseline comparison
    // Keeping them as settable properties since baseline comparison requires external context
    public ChangeType ChangeType { get; set; } = ChangeType.None; // Type of change from baseline
    public string? StatusDisplay { get; set; } = null; // Display text for status (e.g., "=> fail" for regressions). Null means use TestResult.
    public string? ChangeTooltip { get; set; } = null; // Tooltip showing exact change (e.g., "Test fail -> Test Succeeds")

    // Helper methods moved from IssueListLoader
    // Made internal so IssueListLoader can use it for baseline comparison
    internal static (string Result, string LastRun) DetermineWorstResult(List<IssueResult> issueResults)
    {
        IssueResult? worstResult = null;
        StepResultStatus? worstStatus = null;
        string? lastRun = null;

        foreach (var result in issueResults)
        {
            var status = result.TestResult ?? StepResultStatus.NotRun;
            var resultLastRun = result.LastRun;

            // Determine priority: Failed > NotRun > Success
            var isWorse = false;
            if (worstStatus == null || (status == StepResultStatus.Failed && worstStatus != StepResultStatus.Failed))
            {
                isWorse = true;
            }
            else if (status == StepResultStatus.NotRun && worstStatus == StepResultStatus.Success)
            {
                isWorse = true;
            }
            // If same priority, use most recent
            else if (status == worstStatus &&
                     !string.IsNullOrEmpty(resultLastRun) &&
                     !string.IsNullOrEmpty(lastRun) &&
                     string.Compare(resultLastRun, lastRun, StringComparison.Ordinal) > 0)
            {
                isWorse = true;
            }

            if (isWorse)
            {
                worstResult = result;
                worstStatus = status;
                lastRun = resultLastRun;
            }
        }

        return (worstStatus?.ToString() ?? "NotRun", lastRun ?? "");
    }
}
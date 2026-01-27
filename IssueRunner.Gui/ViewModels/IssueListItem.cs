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

    // Settable issue number (extracted from folder name when metadata is missing)
    private int? _issueNumber;

    // Computed properties from Metadata
    public int Number => Metadata?.Number ?? _issueNumber ?? 0;
    public string Title
    {
        get
        {
            if (Metadata != null && !string.IsNullOrWhiteSpace(Metadata.Title))
            {
                return Metadata.Title;
            }
            var number = Number;
            return number > 0 ? $"Issue {number} - Metadata missing" : "Issue 0";
        }
    }
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

    /// <summary>
    /// Detailed reason for the current test result (e.g. skip reason, not tested reason).
    /// </summary>
    public string? ResultReason
    {
        get
        {
            // Prefer explicit reasons coming from test results (e.g. skipped with marker reason)
            if (Results != null && Results.Count > 0)
            {
                var withReason = Results.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Reason));
                if (withReason != null)
                {
                    return withReason.Reason;
                }
            }

            // Fall back to NotTestedReason computed in IssueListLoader (missing metadata, not synced, etc.)
            return NotTestedReason;
        }
    }

    // Settable properties (computed in IssueListLoader from folder/services)
    /// <summary>
    /// Sets the issue number extracted from folder name (used when metadata is missing).
    /// </summary>
    public int IssueNumber
    {
        get => _issueNumber ?? 0;
        set => _issueNumber = value;
    }

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

    /// <summary>
    /// Whether this issue has per-project diff details (used in Diff view to show a details indicator).
    /// </summary>
    public bool HasDiffDetails => Diffs != null && Diffs.Count > 0;

    /// <summary>
    /// Short multi-line summary of baseline→current changes per project (for tooltip on details icon).
    /// </summary>
    public string DiffSummaryTooltip
    {
        get
        {
            if (Diffs == null || Diffs.Count == 0)
                return "";
            return string.Join("\n", Diffs.Select(d =>
                $"{d.ProjectPath}: {d.BaselineStatus} → {d.CurrentStatus} ({d.ChangeType})"));
        }
    }

    /// <summary>
    /// Verbose formatted text describing all diffs for this issue (for the details popup in Diff view).
    /// Includes all statuses (Update, Restore, Build, Test, RunResult) and all outputs/errors baseline → current.
    /// </summary>
    public string DiffDetailsText
    {
        get
        {
            if (Diffs == null || Diffs.Count == 0)
                return "";
            var header = $"Issue {Number}: {Title}\n\n";
            var lines = Diffs.Select(FormatDiffDetails);
            return header + string.Join("\n\n", lines);
        }
    }

    private static string FormatDiffDetails(TestResultDiff d)
    {
        var b = d.BaselineResult;
        var c = d.CurrentResult;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"  • {d.ProjectPath}");
        sb.AppendLine($"    Change: {d.ChangeType}  (Test: {d.BaselineStatus} → {d.CurrentStatus})");

        if (b != null || c != null)
        {
            sb.AppendLine("    Statuses (Baseline → Current):");
            sb.AppendLine($"      Update:   {StatusOrNotRun(b?.UpdateResult)} → {StatusOrNotRun(c?.UpdateResult)}");
            sb.AppendLine($"      Restore:  {StatusOrNotRun(b?.RestoreResult)} → {StatusOrNotRun(c?.RestoreResult)}");
            sb.AppendLine($"      Build:    {StatusOrNotRun(b?.BuildResult)} → {StatusOrNotRun(c?.BuildResult)}");
            sb.AppendLine($"      Test:     {StatusOrNotRun(b?.TestResult)} → {StatusOrNotRun(c?.TestResult)}");
            if (b?.RunResult != null || c?.RunResult != null)
                sb.AppendLine($"      RunResult: {b?.RunResult?.ToString() ?? "(none)"} → {c?.RunResult?.ToString() ?? "(none)"}");
            if (!string.IsNullOrWhiteSpace(b?.Reason) || !string.IsNullOrWhiteSpace(c?.Reason))
                sb.AppendLine($"      Reason:   {(string.IsNullOrWhiteSpace(b?.Reason) ? "(none)" : b!.Reason)} → {(string.IsNullOrWhiteSpace(c?.Reason) ? "(none)" : c!.Reason)}");

            sb.AppendLine("    Outputs / errors:");
            AppendOutputs(sb, "Baseline", b);
            AppendOutputs(sb, "Current", c);
        }

        return sb.ToString();
    }

    private static string StatusOrNotRun(StepResultStatus? s) => s?.ToString() ?? "NotRun";

    private static void AppendOutputs(System.Text.StringBuilder sb, string label, IssueResult? r)
    {
        if (r == null)
            return;
        sb.AppendLine($"      [{label}]");
        AppendIfNonEmpty(sb, "  UpdateOutput", r.UpdateOutput);
        AppendIfNonEmpty(sb, "  UpdateError", r.UpdateError);
        AppendIfNonEmpty(sb, "  RestoreOutput", r.RestoreOutput);
        AppendIfNonEmpty(sb, "  RestoreError", r.RestoreError);
        AppendIfNonEmpty(sb, "  BuildOutput", r.BuildOutput);
        AppendIfNonEmpty(sb, "  BuildError", r.BuildError);
        AppendIfNonEmpty(sb, "  TestOutput", r.TestOutput);
        AppendIfNonEmpty(sb, "  TestError", r.TestError);
        AppendIfNonEmpty(sb, "  TestConclusion", r.TestConclusion);
    }

    private static void AppendIfNonEmpty(System.Text.StringBuilder sb, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        sb.AppendLine($"        {name}:");
        foreach (var line in value.Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
            sb.AppendLine($"          {line}");
    }

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
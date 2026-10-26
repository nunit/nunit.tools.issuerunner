using IssueRunner.Models;

namespace IssueRunner.Services;

/// <summary>
/// Service for creating and verifying issue_initialstate.json per issue,
/// using read-only git (git show HEAD:path), markdown migration, or current csproj content.
/// </summary>
public interface IInitialStateService
{
    /// <summary>
    /// Ensures issue_initialstate.json exists for the issue and covers all csproj files.
    /// Uses git show HEAD for committed content when no state/markdown exists; otherwise migrates or creates from current.
    /// </summary>
    /// <param name="repositoryRoot">Repository root (for git and relative paths).</param>
    /// <param name="issueFolderPath">Full path to the issue folder (e.g. .../Issue228).</param>
    /// <param name="issueNumber">Issue number.</param>
    /// <param name="forceUpdate">If true, (re)build state from current projects or git even when JSON exists.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with status, messages, and projects updated.</returns>
    Task<InitialStateIssueResult> ProcessIssueAsync(
        string repositoryRoot,
        string issueFolderPath,
        int issueNumber,
        bool forceUpdate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Processes all (or filtered) issue folders and returns aggregated results.
    /// </summary>
    /// <param name="repositoryRoot">Repository root.</param>
    /// <param name="issueNumbers">Optional list of issue numbers to process; null means all.</param>
    /// <param name="forceUpdate">If true, rebuild state for each issue even when JSON exists.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of results per issue.</returns>
    Task<List<InitialStateIssueResult>> ProcessAllAsync(
        string repositoryRoot,
        List<int>? issueNumbers,
        bool forceUpdate,
        CancellationToken cancellationToken);
}

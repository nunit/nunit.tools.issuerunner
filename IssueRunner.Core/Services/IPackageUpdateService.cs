using IssueRunner.Models;

namespace IssueRunner.Services;

/// <summary>
/// Service for executing package updates.
/// </summary>
public interface IPackageUpdateService
{
    /// <summary>
    /// Updates NuGet packages in a project.
    /// </summary>
    /// <param name="projectPath">Path to the project file.</param>
    /// <param name="nunitOnly">Whether to update only NUnit packages.</param>
    /// <param name="feed">Package feed option.</param>
    /// <param name="timeoutSeconds">Command timeout in seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of (success, output, error).</returns>
    Task<(bool Success, string Output, string Error)> UpdatePackagesAsync(
        string projectPath,
        bool nunitOnly,
        PackageFeed feed,
        int timeoutSeconds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies nuget.config so it only contains sources required for the given feed.
    /// Removes IssueRunner-added sources (nunit-myget, nunit-local) that are not needed;
    /// adds sources required for the feed. Call once at start of run with repository root.
    /// </summary>
    /// <param name="workingDirectory">Repository root (where nuget.config is applied).</param>
    /// <param name="feed">Current package feed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ApplyNuGetConfigForFeedAsync(
        string workingDirectory,
        PackageFeed feed,
        CancellationToken cancellationToken);

    /// <summary>
    /// Restores package versions in a project to the initial state from issue_initialstate.json.
    /// Used when feed is Initial (nuget.org only; packages kept at initial state).
    /// </summary>
    /// <param name="issueFolderPath">Path to the issue folder containing issue_initialstate.json.</param>
    /// <param name="projectPath">Path to the project file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of (success, output, error).</returns>
    Task<(bool Success, string Output, string Error)> RestoreToInitialStateAsync(
        string issueFolderPath,
        string projectPath,
        CancellationToken cancellationToken);
}


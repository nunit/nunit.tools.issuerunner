using IssueRunner.Models;
using IssueRunner.Services;
using Microsoft.Extensions.Logging;

namespace IssueRunner.Commands;

/// <summary>
/// Command to ensure issue_initialstate.json exists and covers all csproj files,
/// using git show HEAD, markdown migration, or current project content.
/// </summary>
public sealed class UpdateNewIssuesCommand
{
    private readonly IInitialStateService _initialStateService;
    private readonly IIssueDiscoveryService _issueDiscovery;
    private readonly ILogger<UpdateNewIssuesCommand> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateNewIssuesCommand"/> class.
    /// </summary>
    public UpdateNewIssuesCommand(
        IInitialStateService initialStateService,
        IIssueDiscoveryService issueDiscovery,
        ILogger<UpdateNewIssuesCommand> logger)
    {
        _initialStateService = initialStateService;
        _issueDiscovery = issueDiscovery;
        _logger = logger;
    }

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="repositoryRoot">Repository root.</param>
    /// <param name="issueNumbers">Optional issue numbers to process; null means all.</param>
    /// <param name="forceUpdate">If true, rebuild initial state even when JSON exists.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 for success).</returns>
    public async Task<int> ExecuteAsync(
        string repositoryRoot,
        List<int>? issueNumbers,
        bool forceUpdate,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("Updating initial state for issues...");
        Console.WriteLine();

        var folders = _issueDiscovery.DiscoverIssueFolders();
        var toProcess = issueNumbers == null
            ? folders
            : folders.Where(kvp => issueNumbers.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (toProcess.Count == 0)
        {
            Console.WriteLine("No issues to process.");
            return 0;
        }

        var results = await _initialStateService.ProcessAllAsync(
            repositoryRoot,
            issueNumbers,
            forceUpdate,
            cancellationToken);

        var newCount = results.Count(r => r.Status == InitialStateStatus.Created);
        var migratedCount = results.Count(r => r.Status == InitialStateStatus.Migrated);
        var reconstructedCount = results.Count(r => r.Status == InitialStateStatus.Reconstructed);
        var okCount = results.Count(r => r.Status == InitialStateStatus.Ok);
        var errorCount = results.Count(r => r.Status == InitialStateStatus.Error);

        Console.WriteLine($"Processed {results.Count} issue(s).");
        Console.WriteLine($"  New initial states: {newCount}, Migrated from markdown: {migratedCount}, Reconstructed: {reconstructedCount}, Already up-to-date: {okCount}, Errors: {errorCount}");
        Console.WriteLine();

        foreach (var r in results.Where(r => r.Status != InitialStateStatus.Ok || r.Messages.Count > 0))
        {
            Console.WriteLine($"[{r.Number}] {r.Status}: {string.Join("; ", r.Messages)}");
            if (r.ProjectsUpdated.Count > 0)
            {
                Console.WriteLine($"  Updated: {string.Join(", ", r.ProjectsUpdated)}");
            }
        }

        return errorCount > 0 ? 1 : 0;
    }
}

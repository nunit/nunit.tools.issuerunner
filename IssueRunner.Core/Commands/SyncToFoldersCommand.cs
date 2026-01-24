using IssueRunner.Models;
using IssueRunner.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IssueRunner.Commands;

/// <summary>
/// Command to sync metadata from central file to issue folders.
/// </summary>
public sealed class SyncToFoldersCommand
{
    private readonly IIssueDiscoveryService _issueDiscovery;
    private readonly IProjectAnalyzerService _projectAnalyzer;
    private readonly ILogger<SyncToFoldersCommand> _logger;
    private readonly IEnvironmentService _environmentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncToFoldersCommand"/> class.
    /// </summary>
    public SyncToFoldersCommand(
        IIssueDiscoveryService issueDiscovery,
        IProjectAnalyzerService projectAnalyzer,
        ILogger<SyncToFoldersCommand> logger,
        IEnvironmentService environmentService)
    {
        _issueDiscovery = issueDiscovery;
        _projectAnalyzer = projectAnalyzer;
        _logger = logger;
        _environmentService = environmentService;
    }

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="repositoryRoot">Root path of the repository.</param>
    /// <param name="centralMetadataPath">Path to the central metadata file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 for success).</returns>
    public async Task<int> ExecuteAsync(
        string repositoryRoot,
        string? centralMetadataPath,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("Syncing metadata to issue folders...");
        Console.WriteLine();

        centralMetadataPath ??= Path.Combine(
            _environmentService.GetDataDirectory(repositoryRoot),
            "issues_metadata.json");

        if (!File.Exists(centralMetadataPath))
        {
            Console.WriteLine($"ERROR: Central metadata file not found: {centralMetadataPath}");
            return 1;
        }

        var centralMetadata = await LoadCentralMetadataAsync(
            centralMetadataPath,
            cancellationToken);

        var issueFolders = _issueDiscovery.DiscoverIssueFolders();

        var successCount = 0;
        var skippedCount = 0;

        foreach (var (issueNumber, folderPath) in issueFolders.OrderBy(kvp => kvp.Key))
        {
            if (!centralMetadata.TryGetValue(issueNumber, out var metadata))
            {
                Console.WriteLine($"[{issueNumber}]: Skipped");
                Console.WriteLine($"  No metadata found in central file");
                skippedCount++;
                continue;
            }

            var projectCount = await ProcessIssueFolderAsync(
                folderPath,
                metadata,
                cancellationToken);

            Console.WriteLine($"[{issueNumber}]: Updated - {metadata.Title}");
            if (projectCount > 1)
            {
                Console.WriteLine($"  {projectCount} projects processed");
            }
            successCount++;
        }

        Console.WriteLine();
        Console.WriteLine($"Sync complete: {successCount} updated, {skippedCount} skipped");

        return 0;
    }

    private async Task<Dictionary<int, IssueMetadata>> LoadCentralMetadataAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"ERROR: Central metadata file not found: {path}");
            return new Dictionary<int, IssueMetadata>();
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<int, IssueMetadata>();
        var items = JsonSerializer.Deserialize<List<IssueMetadata>>(json)
                        ?? new List<IssueMetadata>();

        var map = new Dictionary<int, IssueMetadata>(items.Count);

        foreach (var item in items)
            map.TryAdd(item.Number, item);
        return map;
    }

    private async Task<int> ProcessIssueFolderAsync(
        string folderPath,
        IssueMetadata metadata,
        CancellationToken cancellationToken)
    {
        var projectFiles = _projectAnalyzer.FindProjectFiles(folderPath);
        var projectMetadataList = new List<IssueProjectMetadata>();

        foreach (var projectFile in projectFiles)
        {
            var (frameworks, packages) =
                _projectAnalyzer.ParseProjectFile(projectFile);

            var relativePath = Path.GetRelativePath(folderPath, projectFile);

            projectMetadataList.Add(new IssueProjectMetadata
            {
                Number = metadata.Number,
                Title = metadata.Title,
                State = metadata.State,
                Milestone = metadata.Milestone,
                Labels = metadata.Labels,
                Url = metadata.Url,
                ProjectPath = relativePath,
                TargetFrameworks = frameworks,
                Packages = packages
            });
        }

        var outputPath = Path.Combine(folderPath, "issue_metadata.json");
        await WriteIssueMetadataAsync(
            outputPath,
            projectMetadataList,
            cancellationToken);

        // Handle initial state creation and migration
        await ProcessInitialStateAsync(
            folderPath,
            metadata.Number,
            projectMetadataList,
            cancellationToken);

        return projectMetadataList.Count;
    }

    private async Task ProcessInitialStateAsync(
        string folderPath,
        int issueNumber,
        List<IssueProjectMetadata> projectMetadataList,
        CancellationToken cancellationToken)
    {
        var initialStatePath = Path.Combine(folderPath, "issue_initialstate.json");
        var markdownPath = Path.Combine(folderPath, "readme.initialstate.md");

        // Check if initial state already exists
        if (File.Exists(initialStatePath))
        {
            return; // Already has initial state
        }

        // Try to migrate from readme.initialstate.md
        if (File.Exists(markdownPath))
        {
            var migrated = await MigrateFromMarkdownAsync(
                markdownPath,
                initialStatePath,
                issueNumber,
                projectMetadataList,
                cancellationToken);
            
            if (migrated)
            {
                // Delete markdown file after successful migration
                try
                {
                    File.Delete(markdownPath);
                    Console.WriteLine($"  Migrated readme.initialstate.md to issue_initialstate.json");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[{Issue}] Failed to delete readme.initialstate.md after migration", issueNumber);
                }
                return;
            }
        }

        // Always create initial state when it doesn't exist (represents state when first synced)
        await CreateInitialStateAsync(
            initialStatePath,
            issueNumber,
            projectMetadataList,
            cancellationToken);
    }

    private async Task<bool> MigrateFromMarkdownAsync(
        string markdownPath,
        string jsonPath,
        int issueNumber,
        List<IssueProjectMetadata> projectMetadataList,
        CancellationToken cancellationToken)
    {
        try
        {
            var markdownContent = await File.ReadAllTextAsync(markdownPath, cancellationToken);
            
            // Try to parse markdown and extract frameworks/packages
            // For now, fall back to creating from current project metadata if parsing fails
            // This ensures we always have an initial state even if markdown format is unknown
            var initialState = new IssueInitialState
            {
                Number = issueNumber,
                CreatedAt = File.GetCreationTime(markdownPath).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Projects = projectMetadataList.Select(p => new ProjectInitialState
                {
                    ProjectPath = p.ProjectPath,
                    TargetFrameworks = p.TargetFrameworks,
                    Packages = p.Packages
                }).ToList()
            };

            await WriteInitialStateAsync(jsonPath, initialState, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{Issue}] Failed to migrate readme.initialstate.md, will create from current state", issueNumber);
            return false;
        }
    }

    private async Task CreateInitialStateAsync(
        string jsonPath,
        int issueNumber,
        List<IssueProjectMetadata> projectMetadataList,
        CancellationToken cancellationToken)
    {
        var initialState = new IssueInitialState
        {
            Number = issueNumber,
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Projects = projectMetadataList.Select(p => new ProjectInitialState
            {
                ProjectPath = p.ProjectPath,
                TargetFrameworks = p.TargetFrameworks,
                Packages = p.Packages
            }).ToList()
        };

        await WriteInitialStateAsync(jsonPath, initialState, cancellationToken);
    }

    private static async Task WriteInitialStateAsync(
        string outputPath,
        IssueInitialState initialState,
        CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(initialState, options);
        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
    }

    private static async Task WriteIssueMetadataAsync(
        string outputPath,
        List<IssueProjectMetadata> metadata,
        CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(metadata, options);
        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
    }
}


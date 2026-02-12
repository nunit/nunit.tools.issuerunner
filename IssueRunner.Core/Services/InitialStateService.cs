using IssueRunner.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IssueRunner.Services;

/// <summary>
/// Creates and verifies issue_initialstate.json using read-only git, markdown migration, or current csproj content.
/// </summary>
public sealed class InitialStateService : IInitialStateService
{
    private readonly IIssueDiscoveryService _issueDiscovery;
    private readonly IProjectAnalyzerService _projectAnalyzer;
    private readonly IProcessExecutor _processExecutor;
    private readonly ILogger<InitialStateService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InitialStateService"/> class.
    /// </summary>
    public InitialStateService(
        IIssueDiscoveryService issueDiscovery,
        IProjectAnalyzerService projectAnalyzer,
        IProcessExecutor processExecutor,
        ILogger<InitialStateService> logger)
    {
        _issueDiscovery = issueDiscovery;
        _projectAnalyzer = projectAnalyzer;
        _processExecutor = processExecutor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<InitialStateIssueResult> ProcessIssueAsync(
        string repositoryRoot,
        string issueFolderPath,
        int issueNumber,
        bool forceUpdate,
        CancellationToken cancellationToken)
    {
        var projectFiles = _projectAnalyzer.FindProjectFiles(issueFolderPath);
        if (projectFiles.Count == 0)
        {
            return new InitialStateIssueResult
            {
                Number = issueNumber,
                Status = InitialStateStatus.Ok,
                Messages = ["No project files found"]
            };
        }

        var initialStatePath = Path.Combine(issueFolderPath, "issue_initialstate.json");
        var markdownPath = Path.Combine(issueFolderPath, "readme.initialstate.md");

        var state = await LoadOrCreateStateAsync(initialStatePath, issueNumber, cancellationToken);
        var covered = state.Projects
            .Select(p => NormalizePath(p.ProjectPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var uncovered = projectFiles
            .Select(f => (FullPath: f, Relative: NormalizePath(Path.GetRelativePath(issueFolderPath, f))))
            .Where(t => !covered.Contains(t.Relative))
            .ToList();

        if (!forceUpdate && uncovered.Count == 0)
        {
            return new InitialStateIssueResult
            {
                Number = issueNumber,
                Status = InitialStateStatus.Ok,
                ProjectsUpdated = []
            };
        }

        var projectsUpdated = new List<string>();
        InitialStateStatus status = InitialStateStatus.Ok;

        if (File.Exists(markdownPath) && (forceUpdate || uncovered.Count > 0))
        {
            var snapshots = projectFiles.Select(f =>
            {
                var rel = NormalizePath(Path.GetRelativePath(issueFolderPath, f));
                var (frameworks, packages) = _projectAnalyzer.ParseProjectFile(f);
                return new ProjectInitialState
                {
                    ProjectPath = rel,
                    TargetFrameworks = frameworks,
                    Packages = packages
                };
            }).ToList();

            state = new IssueInitialState
            {
                Number = issueNumber,
                CreatedAt = File.GetCreationTime(markdownPath).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Projects = snapshots
            };
            try
            {
                File.Delete(markdownPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Issue}] Failed to delete readme.initialstate.md after migration", issueNumber);
            }
            projectsUpdated.AddRange(snapshots.Select(p => p.ProjectPath));
            status = InitialStateStatus.Migrated;
        }
        else
        {
            var existingByPath = state.Projects
                .ToDictionary(p => NormalizePath(p.ProjectPath), p => p, StringComparer.OrdinalIgnoreCase);

            foreach (var (fullPath, relativePath) in uncovered)
            {
                var repoRelative = Path.GetRelativePath(repositoryRoot, fullPath)
                    .Replace('\\', '/');
                var (frameworks, packages) = await GetProjectStateFromGitOrCurrentAsync(
                    repositoryRoot,
                    fullPath,
                    repoRelative,
                    issueNumber,
                    cancellationToken);

                existingByPath[relativePath] = new ProjectInitialState
                {
                    ProjectPath = relativePath,
                    TargetFrameworks = frameworks,
                    Packages = packages
                };
                projectsUpdated.Add(relativePath);
            }

            state = new IssueInitialState
            {
                Number = issueNumber,
                CreatedAt = state.CreatedAt,
                Projects = existingByPath.Values.ToList()
            };
            status = status == InitialStateStatus.Ok ? InitialStateStatus.Reconstructed : status;
        }

        await WriteStateAsync(initialStatePath, state, cancellationToken);

        var missing = projectFiles
            .Select(f => NormalizePath(Path.GetRelativePath(issueFolderPath, f)))
            .Where(rel => !state.Projects.Any(p => NormalizePath(p.ProjectPath).Equals(rel, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var messages = new List<string>();
        if (missing.Count > 0)
        {
            messages.Add($"Missing initial state for: {string.Join(", ", missing)}");
            status = InitialStateStatus.Error;
        }

        return new InitialStateIssueResult
        {
            Number = issueNumber,
            Status = status,
            Messages = messages,
            ProjectsUpdated = projectsUpdated
        };
    }

    /// <inheritdoc />
    public async Task<List<InitialStateIssueResult>> ProcessAllAsync(
        string repositoryRoot,
        List<int>? issueNumbers,
        bool forceUpdate,
        CancellationToken cancellationToken)
    {
        var folders = _issueDiscovery.DiscoverIssueFolders();
        var toProcess = issueNumbers == null
            ? folders
            : folders.Where(kvp => issueNumbers.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        var results = new List<InitialStateIssueResult>();
        foreach (var (number, folderPath) in toProcess.OrderBy(kvp => kvp.Key))
        {
            var result = await ProcessIssueAsync(repositoryRoot, folderPath, number, forceUpdate, cancellationToken);
            results.Add(result);
        }
        return results;
    }

    private async Task<IssueInitialState> LoadOrCreateStateAsync(
        string initialStatePath,
        int issueNumber,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(initialStatePath))
        {
            return new IssueInitialState
            {
                Number = issueNumber,
                Projects = [],
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
        }

        var json = await File.ReadAllTextAsync(initialStatePath, cancellationToken);
        var state = JsonSerializer.Deserialize<IssueInitialState>(json);
        return state ?? new IssueInitialState
        {
            Number = issueNumber,
            Projects = [],
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
    }

    private async Task<(List<string> Frameworks, List<PackageInfo> Packages)> GetProjectStateFromGitOrCurrentAsync(
        string repositoryRoot,
        string projectFilePath,
        string repoRelativePath,
        int issueNumber,
        CancellationToken cancellationToken)
    {
        var (exitCode, output, _) = await _processExecutor.ExecuteAsync(
            "git",
            $"show HEAD:{repoRelativePath}",
            repositoryRoot,
            30,
            cancellationToken);

        if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
        {
            var (frameworks, packages) = _projectAnalyzer.ParseProjectContent(output);
            if (frameworks.Count > 0 || packages.Count > 0)
            {
                return (frameworks, packages);
            }
        }

        _logger.LogDebug("[{Issue}] Git show failed or empty for {Path}, using current file", issueNumber, repoRelativePath);
        return _projectAnalyzer.ParseProjectFile(projectFilePath);
    }

    private static async Task WriteStateAsync(
        string path,
        IssueInitialState state,
        CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(state, options);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}

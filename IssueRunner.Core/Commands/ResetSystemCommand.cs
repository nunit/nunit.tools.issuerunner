using IssueRunner.Models;
using IssueRunner.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IssueRunner.Commands;

/// <summary>
/// Command to reset the system by resetting packages, deleting local metadata/result files, and cleaning the data directory.
/// </summary>
public sealed class ResetSystemCommand
{
    private readonly IEnvironmentService _environmentService;
    private readonly IIssueDiscoveryService _issueDiscovery;
    private readonly IProjectAnalyzerService _projectAnalyzer;
    private readonly IMarkerService _markerService;
    private readonly ILogger<ResetSystemCommand> _logger;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetSystemCommand"/> class.
    /// </summary>
    public ResetSystemCommand(
        IEnvironmentService environmentService,
        IIssueDiscoveryService issueDiscovery,
        IProjectAnalyzerService projectAnalyzer,
        IMarkerService markerService,
        ILogger<ResetSystemCommand> logger,
        ILoggerFactory loggerFactory)
    {
        _environmentService = environmentService;
        _issueDiscovery = issueDiscovery;
        _projectAnalyzer = projectAnalyzer;
        _markerService = markerService;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Executes the command.
    /// </summary>
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var repositoryRoot = _environmentService.Root;
            if (string.IsNullOrWhiteSpace(repositoryRoot))
            {
                Console.WriteLine("ERROR: No repository root configured.");
                return 1;
            }

            Console.WriteLine("Starting system reset...");
            Console.WriteLine();

            // Step 1: Reset all packages
            Console.WriteLine("Step 1: Resetting all packages...");
            try
            {
                var resetLogger = _loggerFactory.CreateLogger<ResetPackagesCommand>();
                var resetCommand = new ResetPackagesCommand(
                    _issueDiscovery,
                    _projectAnalyzer,
                    resetLogger,
                    _markerService);

                var resetExitCode = await resetCommand.ExecuteAsync(
                    repositoryRoot,
                    null, // Reset all issues
                    LogVerbosity.Normal,
                    cancellationToken);

                if (resetExitCode != 0)
                {
                    Console.WriteLine($"Warning: Package reset completed with exit code {resetExitCode}");
                }
                else
                {
                    Console.WriteLine("Package reset completed successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during package reset");
                Console.WriteLine($"Error during package reset: {ex.Message}");
                return 1;
            }

            Console.WriteLine();

            // Step 2: Delete issue_metadata.json files
            Console.WriteLine("Step 2: Deleting issue_metadata.json files from local folders...");
            try
            {
                var issueFolders = _issueDiscovery.DiscoverIssueFolders();
                var deletedCount = 0;

                foreach (var (issueNumber, folderPath) in issueFolders)
                {
                    var metadataPath = Path.Combine(folderPath, "issue_metadata.json");
                    if (File.Exists(metadataPath))
                    {
                        try
                        {
                            File.Delete(metadataPath);
                            deletedCount++;
                            if (deletedCount % 10 == 0)
                            {
                                Console.WriteLine($"  Deleted {deletedCount} metadata files...");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete issue_metadata.json for Issue{IssueNumber}", issueNumber);
                            Console.WriteLine($"  Warning: Failed to delete metadata for Issue{issueNumber}: {ex.Message}");
                        }
                    }
                }

                Console.WriteLine($"Deleted {deletedCount} issue_metadata.json files.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting issue_metadata.json files");
                Console.WriteLine($"Error deleting issue_metadata.json files: {ex.Message}");
                return 1;
            }

            Console.WriteLine();

            // Step 3: Delete local result JSON files
            Console.WriteLine("Step 3: Deleting local result JSON files from issue folders...");
            try
            {
                var issueFolders = _issueDiscovery.DiscoverIssueFolders();
                var deletedCount = 0;

                foreach (var (issueNumber, folderPath) in issueFolders)
                {
                    try
                    {
                        var jsonFiles = Directory.GetFiles(folderPath, "*result*.json", SearchOption.TopDirectoryOnly);
                        foreach (var jsonFile in jsonFiles)
                        {
                            try
                            {
                                File.Delete(jsonFile);
                                deletedCount++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to delete result file {FilePath}", jsonFile);
                                Console.WriteLine($"  Warning: Failed to delete {Path.GetFileName(jsonFile)}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing Issue{IssueNumber} folder", issueNumber);
                        Console.WriteLine($"  Warning: Error processing Issue{issueNumber}: {ex.Message}");
                    }
                }

                Console.WriteLine($"Deleted {deletedCount} result JSON files.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting result JSON files");
                Console.WriteLine($"Error deleting result JSON files: {ex.Message}");
                return 1;
            }

            Console.WriteLine();

            // Step 4: Delete files in .nunit\IssueRunner (except repository.json)
            Console.WriteLine("Step 4: Cleaning data directory...");
            try
            {
                var dataDir = _environmentService.GetDataDirectory(repositoryRoot);
                if (!Directory.Exists(dataDir))
                {
                    Console.WriteLine("Data directory does not exist. Nothing to clean.");
                    return 0;
                }

                var deletedFiles = 0;
                var deletedDirs = 0;

                // Delete all files except repository.json
                var files = Directory.GetFiles(dataDir, "*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    if (fileName.Equals("repository.json", StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Preserve repository.json
                    }

                    try
                    {
                        File.Delete(file);
                        deletedFiles++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete file {FileName}", fileName);
                        Console.WriteLine($"  Warning: Failed to delete {fileName}: {ex.Message}");
                    }
                }

                // Delete subdirectories recursively
                var subdirs = Directory.GetDirectories(dataDir, "*", SearchOption.TopDirectoryOnly);
                foreach (var subdir in subdirs)
                {
                    try
                    {
                        Directory.Delete(subdir, recursive: true);
                        deletedDirs++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete directory {DirectoryName}", subdir);
                        Console.WriteLine($"  Warning: Failed to delete directory {Path.GetFileName(subdir)}: {ex.Message}");
                    }
                }

                Console.WriteLine($"Deleted {deletedFiles} files and {deletedDirs} directories from data directory.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning data directory");
                Console.WriteLine($"Error cleaning data directory: {ex.Message}");
                return 1;
            }

            Console.WriteLine();
            Console.WriteLine("System reset completed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during system reset");
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}

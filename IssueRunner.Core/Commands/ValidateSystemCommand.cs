using IssueRunner.Models;
using IssueRunner.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IssueRunner.Commands;

/// <summary>
/// Command to validate system data files can be loaded correctly.
/// </summary>
public sealed class ValidateSystemCommand
{
    private readonly IEnvironmentService _environmentService;
    private readonly ILogger<ValidateSystemCommand> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateSystemCommand"/> class.
    /// </summary>
    public ValidateSystemCommand(
        IEnvironmentService environmentService,
        ILogger<ValidateSystemCommand> logger)
    {
        _environmentService = environmentService;
        _logger = logger;
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

            Console.WriteLine("Validating system data files...");
            Console.WriteLine();

            var dataDir = _environmentService.GetDataDirectory(repositoryRoot);
            var hasErrors = false;

            // Step 1: Validate metadata file
            Console.WriteLine("Step 1: Validating issues_metadata.json...");
            var metadataIssues = new HashSet<int>();
            try
            {
                var metadataPath = Path.Combine(dataDir, "issues_metadata.json");
                if (!File.Exists(metadataPath))
                {
                    Console.WriteLine("  Warning: issues_metadata.json not found.");
                }
                else
                {
                    var metadataJson = await File.ReadAllTextAsync(metadataPath, cancellationToken);
                    var options = new JsonSerializerOptions
                    {
                        Converters = { new JsonStringEnumConverter() }
                    };

                    var metadata = JsonSerializer.Deserialize<List<IssueMetadata>>(metadataJson, options);
                    if (metadata == null)
                    {
                        Console.WriteLine("  ERROR: Failed to deserialize issues_metadata.json (returned null).");
                        hasErrors = true;
                    }
                    else
                    {
                        var invalidNumbers = new List<int>();
                        foreach (var item in metadata)
                        {
                            if (item.Number < 1)
                            {
                                invalidNumbers.Add(item.Number);
                            }
                            else
                            {
                                metadataIssues.Add(item.Number);
                            }
                        }

                        if (invalidNumbers.Count > 0)
                        {
                            Console.WriteLine($"  ERROR: Found {invalidNumbers.Count} issue(s) with invalid numbers (< 1): {string.Join(", ", invalidNumbers)}");
                            hasErrors = true;
                        }
                        else
                        {
                            Console.WriteLine($"  OK: Loaded {metadata.Count} metadata entries. All issue numbers are valid (>= 1).");
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"  ERROR: Invalid JSON in issues_metadata.json: {ex.Message}");
                hasErrors = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR: Failed to load issues_metadata.json: {ex.Message}");
                hasErrors = true;
            }

            Console.WriteLine();

            // Step 2: Validate results.json
            Console.WriteLine("Step 2: Validating results.json...");
            var resultsIssues = new HashSet<int>();
            try
            {
                var resultsPath = Path.Combine(dataDir, "results.json");
                if (!File.Exists(resultsPath))
                {
                    Console.WriteLine("  Info: results.json not found (this is OK if no tests have been run).");
                }
                else
                {
                    var resultsJson = await File.ReadAllTextAsync(resultsPath, cancellationToken);
                    var options = new JsonSerializerOptions
                    {
                        Converters = { new JsonStringEnumConverter() }
                    };

                    var results = JsonSerializer.Deserialize<List<IssueResult>>(resultsJson, options);
                    if (results == null)
                    {
                        Console.WriteLine("  ERROR: Failed to deserialize results.json (returned null).");
                        hasErrors = true;
                    }
                    else
                    {
                        var invalidNumbers = new List<int>();
                        foreach (var result in results)
                        {
                            if (result.Number < 1)
                            {
                                invalidNumbers.Add(result.Number);
                            }
                            else
                            {
                                resultsIssues.Add(result.Number);
                            }
                        }

                        if (invalidNumbers.Count > 0)
                        {
                            Console.WriteLine($"  ERROR: Found {invalidNumbers.Count} result(s) with invalid numbers (< 1): {string.Join(", ", invalidNumbers)}");
                            hasErrors = true;
                        }
                        else
                        {
                            Console.WriteLine($"  OK: Loaded {results.Count} result entries. All issue numbers are valid (>= 1).");
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"  ERROR: Invalid JSON in results.json: {ex.Message}");
                hasErrors = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR: Failed to load results.json: {ex.Message}");
                hasErrors = true;
            }

            Console.WriteLine();

            // Step 3: Validate results-baseline.json
            Console.WriteLine("Step 3: Validating results-baseline.json...");
            var baselineIssues = new HashSet<int>();
            try
            {
                var baselinePath = Path.Combine(dataDir, "results-baseline.json");
                if (!File.Exists(baselinePath))
                {
                    Console.WriteLine("  Info: results-baseline.json not found (this is OK if no baseline has been set).");
                }
                else
                {
                    var baselineJson = await File.ReadAllTextAsync(baselinePath, cancellationToken);
                    var options = new JsonSerializerOptions
                    {
                        Converters = { new JsonStringEnumConverter() }
                    };

                    var baselineResults = JsonSerializer.Deserialize<List<IssueResult>>(baselineJson, options);
                    if (baselineResults == null)
                    {
                        Console.WriteLine("  ERROR: Failed to deserialize results-baseline.json (returned null).");
                        hasErrors = true;
                    }
                    else
                    {
                        var invalidNumbers = new List<int>();
                        foreach (var result in baselineResults)
                        {
                            if (result.Number < 1)
                            {
                                invalidNumbers.Add(result.Number);
                            }
                            else
                            {
                                baselineIssues.Add(result.Number);
                            }
                        }

                        if (invalidNumbers.Count > 0)
                        {
                            Console.WriteLine($"  ERROR: Found {invalidNumbers.Count} baseline result(s) with invalid numbers (< 1): {string.Join(", ", invalidNumbers)}");
                            hasErrors = true;
                        }
                        else
                        {
                            Console.WriteLine($"  OK: Loaded {baselineResults.Count} baseline result entries. All issue numbers are valid (>= 1).");
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"  ERROR: Invalid JSON in results-baseline.json: {ex.Message}");
                hasErrors = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR: Failed to load results-baseline.json: {ex.Message}");
                hasErrors = true;
            }

            Console.WriteLine();

            // Step 4: Cross-validation
            Console.WriteLine("Step 4: Cross-validation...");
            try
            {
                var issuesInResultsButNotMetadata = resultsIssues.Except(metadataIssues).ToList();
                var issuesInBaselineButNotMetadata = baselineIssues.Except(metadataIssues).ToList();
                var issuesInMetadataButNoResults = metadataIssues.Except(resultsIssues).ToList();

                if (issuesInResultsButNotMetadata.Count > 0)
                {
                    Console.WriteLine($"  Warning: {issuesInResultsButNotMetadata.Count} issue(s) in results.json but not in metadata: {string.Join(", ", issuesInResultsButNotMetadata.OrderBy(n => n))}");
                }

                if (issuesInBaselineButNotMetadata.Count > 0)
                {
                    Console.WriteLine($"  Warning: {issuesInBaselineButNotMetadata.Count} issue(s) in results-baseline.json but not in metadata: {string.Join(", ", issuesInBaselineButNotMetadata.OrderBy(n => n))}");
                }

                if (issuesInMetadataButNoResults.Count > 0 && resultsIssues.Count > 0)
                {
                    Console.WriteLine($"  Info: {issuesInMetadataButNoResults.Count} issue(s) in metadata but no results (this is OK for untested issues).");
                }

                if (issuesInResultsButNotMetadata.Count == 0 && 
                    issuesInBaselineButNotMetadata.Count == 0)
                {
                    Console.WriteLine("  OK: All issue numbers in results files are present in metadata.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR: Failed during cross-validation: {ex.Message}");
                hasErrors = true;
            }

            Console.WriteLine();

            if (hasErrors)
            {
                Console.WriteLine("Validation completed with ERRORS. Please review the errors above.");
                return 1;
            }
            else
            {
                Console.WriteLine("Validation completed successfully. All files are valid.");
                return 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during validation");
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}

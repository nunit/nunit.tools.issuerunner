using IssueRunner.Models;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace IssueRunner.Services;

/// <summary>
/// Implementation of package update service.
/// </summary>
public sealed class PackageUpdateService : IPackageUpdateService
{
    private static readonly string[] NUnitPackages =
    [
        "NUnit",
        "NUnit3TestAdapter",
        "Microsoft.NET.Test.Sdk"
    ];

    private readonly IProcessExecutor _processExecutor;
    private readonly ILogger<PackageUpdateService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly INuGetPackageVersionService _nugetVersions;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageUpdateService"/> class.
    /// </summary>
    public PackageUpdateService(
        IProcessExecutor processExecutor,
        IHttpClientFactory httpClientFactory,
        INuGetPackageVersionService nugetVersions,
        ILogger<PackageUpdateService> logger)
    {
        _processExecutor = processExecutor;
        _httpClientFactory = httpClientFactory;
        _nugetVersions = nugetVersions;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ApplyNuGetConfigForFeedAsync(
        string workingDirectory,
        PackageFeed feed,
        CancellationToken cancellationToken)
    {
        switch (feed)
        {
            case PackageFeed.Stable:
            case PackageFeed.Beta:
            case PackageFeed.Initial:
                await RemoveMyGetSourceAsync(workingDirectory, cancellationToken);
                await RemoveLocalSourceAsync(workingDirectory, cancellationToken);
                break;
            case PackageFeed.Alpha:
                await RemoveLocalSourceAsync(workingDirectory, cancellationToken);
                await EnsureMyGetSourceAsync(workingDirectory, cancellationToken);
                break;
            case PackageFeed.Local:
                await RemoveMyGetSourceAsync(workingDirectory, cancellationToken);
                await EnsureLocalSourceAsync(workingDirectory, cancellationToken);
                break;
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Output, string Error)> RestoreToInitialStateAsync(
        string issueFolderPath,
        string projectPath,
        CancellationToken cancellationToken)
    {
        var initialStatePath = Path.Combine(issueFolderPath, "issue_initialstate.json");
        if (!File.Exists(initialStatePath))
        {
            return (false, "", "issue_initialstate.json not found");
        }

        IssueInitialState? state;
        try
        {
            var json = await File.ReadAllTextAsync(initialStatePath, cancellationToken);
            state = JsonSerializer.Deserialize<IssueInitialState>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load issue_initialstate.json");
            return (false, "", ex.Message);
        }

        if (state?.Projects == null || state.Projects.Count == 0)
        {
            return (false, "", "No projects in issue_initialstate.json");
        }

        var projectRel = Path.GetRelativePath(issueFolderPath, projectPath).Replace('\\', '/');
        var projectState = state.Projects
            .FirstOrDefault(p => p.ProjectPath.Replace('\\', '/').Equals(projectRel, StringComparison.OrdinalIgnoreCase));
        if (projectState == null)
        {
            return (false, "", $"No initial state for project {projectRel}");
        }

        try
        {
            var doc = XDocument.Load(projectPath);
            var root = doc.Root;
            if (root == null)
            {
                return (false, "", "Invalid project file");
            }

            var updated = false;
            var metadataPackages = projectState.Packages?
                .ToDictionary(p => p.Name, p => p.Version, StringComparer.OrdinalIgnoreCase);
            if (metadataPackages != null)
            {
                foreach (var packageRef in root.Descendants("PackageReference"))
                {
                    var name = packageRef.Attribute("Include")?.Value;
                    if (name != null && metadataPackages.TryGetValue(name, out var version))
                    {
                        var versionAttr = packageRef.Attribute("Version");
                        if (versionAttr != null && versionAttr.Value != version)
                        {
                            versionAttr.Value = version;
                            updated = true;
                            _logger.LogDebug("Restored {Package} to {Version}", name, version);
                        }
                    }
                }
            }

            if (updated)
            {
                doc.Save(projectPath);
            }

            return (true, "Restored to initial state", "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring to initial state");
            return (false, "", ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Output, string Error)>
        UpdatePackagesAsync(
            string projectPath,
            bool nunitOnly,
            PackageFeed feed,
            int timeoutSeconds,
            CancellationToken cancellationToken)
    {
        // NuGet config is applied once at run start via ApplyNuGetConfigForFeedAsync(repositoryRoot, feed).

        if (nunitOnly)
        {
            return await UpdateNUnitPackagesDirectlyAsync(
                projectPath,
                feed,
                cancellationToken);
        }

        // For Stable feed, make sure we can move off prereleases even when the
        // current version is a higher prerelease (dotnet-outdated won't "downgrade").
        if (feed == PackageFeed.Stable)
        {
            try
            {
                await UpdateNUnitPackagesDirectlyAsync(projectPath, feed, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Stable pre-pass package update failed; continuing with dotnet-outdated.");
            }
        }

        return await UpdateUsingDotnetOutdatedAsync(
            projectPath,
            feed,
            timeoutSeconds,
            cancellationToken);
    }

    private async Task RemoveMyGetSourceAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        const string sourceName = "nunit-myget";
        try
        {
            var (_, output, _) = await _processExecutor.ExecuteAsync(
                "dotnet",
                "nuget list source",
                workingDirectory,
                30,
                cancellationToken);

            if (!output.Contains(sourceName, StringComparison.OrdinalIgnoreCase))
                return;

            _logger.LogDebug("Removing MyGet source");
            await _processExecutor.ExecuteAsync(
                "dotnet",
                "nuget remove source " + sourceName,
                workingDirectory,
                30,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove MyGet source");
        }
    }

    private async Task RemoveLocalSourceAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        const string sourceName = "nunit-local";
        try
        {
            var (_, output, _) = await _processExecutor.ExecuteAsync(
                "dotnet",
                "nuget list source",
                workingDirectory,
                30,
                cancellationToken);

            if (output.IndexOf(sourceName, StringComparison.OrdinalIgnoreCase) < 0)
                return;

            _logger.LogDebug("Removing local source");
            await _processExecutor.ExecuteAsync(
                "dotnet",
                "nuget remove source " + sourceName,
                workingDirectory,
                30,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove local source");
        }
    }

    private async Task EnsureMyGetSourceAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        const string mygetSourceName = "nunit-myget";
        const string mygetUrl = "https://www.myget.org/F/nunit/api/v3/index.json";

        try
        {
            var (exitCode, output, _) = await _processExecutor.ExecuteAsync(
                "dotnet",
                "nuget list source",
                workingDirectory,
                30,
                cancellationToken);

            if (exitCode == 0 && output.Contains(mygetSourceName))
            {
                _logger.LogDebug("MyGet source already configured");
                return;
            }

            var (addExitCode, addOutput, addError) = await _processExecutor.ExecuteAsync(
                "dotnet",
                $"nuget add source {mygetUrl} --name {mygetSourceName}",
                workingDirectory,
                30,
                cancellationToken);

            if (addExitCode == 0)
            {
                _logger.LogDebug("Added MyGet source for Alpha feed");
            }
            else
            {
                _logger.LogWarning("Failed to add MyGet source: {Error}", addError);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error configuring MyGet source");
        }
    }

    private async Task<(bool Success, string Output, string Error)>
        UpdateNUnitPackagesDirectlyAsync(
            string projectPath,
            PackageFeed feed,
            CancellationToken cancellationToken)
    {
        try
        {
            var doc = XDocument.Load(projectPath);
            var root = doc.Root;

            if (root == null)
            {
                return (false, "", "Invalid project file");
            }

            var latestVersions = await GetLatestNUnitVersionsAsync(
                feed,
                cancellationToken);
            var updated = false;

            foreach (var packageRef in root.Descendants("PackageReference"))
            {
                var name = packageRef.Attribute("Include")?.Value;
                
                if (name != null && latestVersions.TryGetValue(name, out var version))
                {
                    var versionAttr = packageRef.Attribute("Version");
                    if (versionAttr != null)
                    {
                        versionAttr.Value = version;
                        updated = true;
                        _logger.LogDebug(
                            "Updated {Package} to {Version}",
                            name,
                            version);
                    }
                }
            }

            if (updated)
            {
                doc.Save(projectPath);
                return (true, "Updated NUnit packages via text substitution", "");
            }

            return (true, "No NUnit packages to update", "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating packages directly");
            return (false, "", ex.Message);
        }
    }

    private async Task<Dictionary<string, string>> GetLatestNUnitVersionsAsync(
        PackageFeed feed,
        CancellationToken cancellationToken)
    {
        try
        {
            var latest = await _nugetVersions.GetLatestVersionsAsync(NUnitPackages, feed, cancellationToken);
            if (latest.Count > 0)
            {
                return latest.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToNormalizedString(),
                    StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query NuGet for latest versions; falling back to defaults.");
        }

        // Fallback (best-effort) defaults.
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["NUnit"] = "4.4.0",
            ["NUnit3TestAdapter"] = "6.0.0",
            ["Microsoft.NET.Test.Sdk"] = "18.0.1"
        };
    }

    private async Task<(bool Success, string Output, string Error)>
        UpdateUsingDotnetOutdatedAsync(
            string projectPath,
            PackageFeed feed,
            int timeoutSeconds,
            CancellationToken cancellationToken)
    {
        var workingDir = Path.GetDirectoryName(projectPath)!;

        // Add MyGet source for Alpha feed
        if (feed == PackageFeed.Alpha)
        {
            var addSourceResult = await AddMyGetSourceAsync(workingDir, timeoutSeconds, cancellationToken);
            if (!addSourceResult.Success)
            {
                return addSourceResult;
            }
        }

        if (feed == PackageFeed.Local)
        {
            var addLocalResult = await AddLocalSourceAsync(workingDir, timeoutSeconds, cancellationToken);
            if (!addLocalResult.Success)
            {
                return addLocalResult;
            }
        }

        // Build command based on feed
        var args = "outdated --upgrade";

        // dotnet-outdated defaults to "--pre-release Auto", which will keep using prereleases
        // if the project is already on a prerelease version. For Stable runs we want to force
        // stable-only resolution.
        args += feed switch
        {
            PackageFeed.Stable or PackageFeed.Initial => " --pre-release Never",
            PackageFeed.Beta or PackageFeed.Alpha or PackageFeed.Local => " --pre-release Always",
            _ => ""
        };
        
        var (exitCode, output, error) = await _processExecutor.ExecuteAsync(
            "dotnet",
            args,
            workingDir,
            timeoutSeconds,
            cancellationToken);

        var success = exitCode == 0;
        
        return (success, output, error);
    }

    private async Task<(bool Success, string Output, string Error)>
        AddMyGetSourceAsync(
            string workingDir,
            int timeoutSeconds,
            CancellationToken cancellationToken)
    {
        const string myGetSource = "https://www.myget.org/F/nunit/api/v3/index.json";
        const string sourceName = "nunit-myget";

        // Check if source already exists
        var (checkCode, checkOutput, checkError) = await _processExecutor.ExecuteAsync(
            "dotnet",
            "nuget list source",
            workingDir,
            timeoutSeconds,
            cancellationToken);

        if (checkCode == 0 && checkOutput.Contains(sourceName))
        {
            _logger.LogDebug("MyGet source already configured");
            return (true, "MyGet source already exists", "");
        }

        // Add the source
        var (exitCode, output, error) = await _processExecutor.ExecuteAsync(
            "dotnet",
            $"nuget add source {myGetSource} --name {sourceName}",
            workingDir,
            timeoutSeconds,
            cancellationToken);

        if (exitCode == 0)
        {
            _logger.LogDebug("Added MyGet source for NUnit packages");
            return (true, "Added MyGet source", "");
        }

        _logger.LogWarning("Failed to add MyGet source: {Error}", error);
        return (false, output, error);
    }

    private async Task EnsureLocalSourceAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        const string sourceName = "nunit-local";
        const string sourcePath = "C:\\nuget";

        try
        {
            var (exitCode, output, _) = await _processExecutor.ExecuteAsync(
                "dotnet",
                "nuget list source",
                workingDirectory,
                30,
                cancellationToken);

            if (exitCode == 0 && output.IndexOf(sourceName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _logger.LogDebug("Local source already configured");
                return;
            }

            var (addExitCode, addOutput, addError) = await _processExecutor.ExecuteAsync(
                "dotnet",
                $"nuget add source \"{sourcePath}\" --name {sourceName}",
                workingDirectory,
                30,
                cancellationToken);

            if (addExitCode == 0)
            {
                _logger.LogDebug("Added local source for Local feed at {Path}", sourcePath);
                return;
            }

            if (addError.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
                addError.Contains("already present", StringComparison.OrdinalIgnoreCase) ||
                addOutput.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
                addOutput.Contains("already present", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Local source already exists, skipping add");
                return;
            }

            _logger.LogWarning("Failed to add local source: {Error}", addError);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error configuring local source");
        }
    }

    private async Task<(bool Success, string Output, string Error)>
        AddLocalSourceAsync(
            string workingDir,
            int timeoutSeconds,
            CancellationToken cancellationToken)
    {
        const string sourcePath = "C:\\nuget";
        const string sourceName = "nunit-local";

        var (checkCode, checkOutput, checkError) = await _processExecutor.ExecuteAsync(
            "dotnet",
            "nuget list source",
            workingDir,
            timeoutSeconds,
            cancellationToken);

        if (checkCode == 0 && checkOutput.IndexOf(sourceName, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            _logger.LogDebug("Local source already configured");
            return (true, "Local source already exists", "");
        }

        var (exitCode, output, error) = await _processExecutor.ExecuteAsync(
            "dotnet",
            $"nuget add source \"{sourcePath}\" --name {sourceName}",
            workingDir,
            timeoutSeconds,
            cancellationToken);

        if (exitCode == 0)
        {
            _logger.LogDebug("Added local source at {Path}", sourcePath);
            return (true, output, "");
        }

        // Check if the source already exists (this is not an error)
        if (error.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("already present", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("already present", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Local source already exists, skipping add");
            return (true, "Local source already exists", "");
        }

        _logger.LogWarning("Failed to add local source: {Error}", error);
        return (false, output, error);
    }
}


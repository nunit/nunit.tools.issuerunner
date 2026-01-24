using System.Text.Json.Serialization;

namespace IssueRunner.Models;

/// <summary>
/// Represents the initial state of an issue (frameworks and packages when first synced).
/// </summary>
public sealed class IssueInitialState
{
    /// <summary>
    /// Gets or sets the issue number.
    /// </summary>
    [JsonPropertyName("number")]
    public required int Number { get; init; }

    /// <summary>
    /// Gets or sets the list of projects with their initial state.
    /// </summary>
    [JsonPropertyName("projects")]
    public required List<ProjectInitialState> Projects { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when the initial state was created (ISO 8601 format).
    /// </summary>
    [JsonPropertyName("created_at")]
    public required string CreatedAt { get; init; }
}

/// <summary>
/// Represents the initial state of a project within an issue.
/// </summary>
public sealed class ProjectInitialState
{
    /// <summary>
    /// Gets or sets the relative path to the project file.
    /// </summary>
    [JsonPropertyName("project_path")]
    public required string ProjectPath { get; init; }

    /// <summary>
    /// Gets or sets the target frameworks.
    /// </summary>
    [JsonPropertyName("target_frameworks")]
    public required List<string> TargetFrameworks { get; init; }

    /// <summary>
    /// Gets or sets the package references.
    /// </summary>
    [JsonPropertyName("packages")]
    public required List<PackageInfo> Packages { get; init; }
}

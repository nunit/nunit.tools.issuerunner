using System.Text.Json.Serialization;

namespace IssueRunner.Models;

/// <summary>
/// Status of initial state processing for a single issue.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InitialStateStatus
{
    /// <summary>Initial state already present and all projects covered.</summary>
    Ok,

    /// <summary>Migrated from readme.initialstate.md.</summary>
    Migrated,

    /// <summary>Reconstructed from git (git show HEAD:path).</summary>
    Reconstructed,

    /// <summary>Created from current working tree (no git/markdown).</summary>
    Created,

    /// <summary>Error during processing.</summary>
    Error
}

/// <summary>
/// Result of initial state processing for one issue.
/// </summary>
public sealed class InitialStateIssueResult
{
    /// <summary>Issue number.</summary>
    [JsonPropertyName("number")]
    public int Number { get; init; }

    /// <summary>Status of processing.</summary>
    [JsonPropertyName("status")]
    public InitialStateStatus Status { get; init; }

    /// <summary>Human-readable messages (errors, warnings).</summary>
    [JsonPropertyName("messages")]
    public List<string> Messages { get; init; } = [];

    /// <summary>Project paths that were added or updated in issue_initialstate.json.</summary>
    [JsonPropertyName("projects_updated")]
    public List<string> ProjectsUpdated { get; init; } = [];
}

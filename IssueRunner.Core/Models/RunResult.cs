using System.Text.Json.Serialization;

namespace IssueRunner.Models;

/// <summary>
/// Represents whether an issue is runnable and why it might not be run.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RunResult
{
    /// <summary>Issue is runnable, proceed with tests.</summary>
    Run,
    
    /// <summary>Skipped due to marker file.</summary>
    Skipped,
    
    /// <summary>Missing initial state file (not synced).</summary>
    NotSynced,
    
    /// <summary>Not run for other reasons (or default/unknown state).</summary>
    NotRun
}

using System.Text.Json.Serialization;

namespace IssueRunner.Models;

/// <summary>
/// Represents the result status of a test execution step (update, restore, build, or test).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StepResultStatus
{
    /// <summary>Step completed successfully.</summary>
    Success,
    
    /// <summary>Step failed.</summary>
    Failed,
    
    /// <summary>Step has not been run yet.</summary>
    NotRun
}

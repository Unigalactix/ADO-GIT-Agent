namespace ErrorDetectionAgent.Core.Models;

/// <summary>
/// Groups multiple identical or similar errors together for reporting and triage.
/// </summary>
public sealed class AggregatedError
{
    /// <summary>A normalised fingerprint that groups related errors together.</summary>
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>Representative error message for the group.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Severity of the errors in this group.</summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>Source application or service.</summary>
    public string? Source { get; set; }

    /// <summary>Total number of occurrences in the current time window.</summary>
    public int OccurrenceCount { get; set; }

    /// <summary>True when the error has been seen more than once.</summary>
    public bool IsRecurring => OccurrenceCount > 1;

    /// <summary>Timestamp of the first occurrence in the window.</summary>
    public DateTime FirstSeen { get; set; }

    /// <summary>Timestamp of the most recent occurrence.</summary>
    public DateTime LastSeen { get; set; }

    /// <summary>Individual log entries that belong to this group.</summary>
    public List<ErrorLogEntry> Entries { get; set; } = new();

    /// <summary>Representative stack trace (from the most recent entry).</summary>
    public string? StackTrace { get; set; }
}

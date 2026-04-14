namespace ErrorDetectionAgent.Core.Models;

/// <summary>
/// Represents a single error or exception entry retrieved from the Azure SQL DB log database.
/// </summary>
public sealed class ErrorLogEntry
{
    public long Id { get; set; }

    /// <summary>Timestamp when the error was logged.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Severity level (e.g., Error, Critical, Warning).</summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>The error/exception message text.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Full stack trace if available.</summary>
    public string? StackTrace { get; set; }

    /// <summary>Source application or service that produced the error.</summary>
    public string? Source { get; set; }

    /// <summary>A normalized error code or hash used for deduplication.</summary>
    public string? ErrorCode { get; set; }
}

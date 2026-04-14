using ErrorDetectionAgent.Core.Interfaces;
using ErrorDetectionAgent.Core.Models;

namespace ErrorDetectionAgent.Tests.Fakes;

/// <summary>
/// In-memory fake that returns pre-configured error log entries.
/// Simulates the Azure SQL DB connection without any real database.
/// </summary>
public sealed class FakeErrorLogReader : IErrorLogReader
{
    private readonly List<ErrorLogEntry> _entries;

    public FakeErrorLogReader(IEnumerable<ErrorLogEntry>? entries = null)
    {
        _entries = entries?.ToList() ?? [];
    }

    /// <summary>Number of times GetErrorsAsync was called.</summary>
    public int CallCount { get; private set; }

    /// <summary>Last 'since' parameter passed to GetErrorsAsync.</summary>
    public DateTime? LastSinceArg { get; private set; }

    public Task<IReadOnlyList<ErrorLogEntry>> GetErrorsAsync(
        DateTime since,
        DateTime? until = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CallCount++;
        LastSinceArg = since;

        IReadOnlyList<ErrorLogEntry> result = _entries
            .Where(e => e.Timestamp >= since && (until == null || e.Timestamp <= until))
            .ToList();

        return Task.FromResult(result);
    }
}

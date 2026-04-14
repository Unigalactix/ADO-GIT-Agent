using ErrorDetectionAgent.Core.Models;

namespace ErrorDetectionAgent.Core.Interfaces;

/// <summary>
/// Aggregates raw error log entries and detects duplicate/recurring errors.
/// </summary>
public interface IErrorAggregator
{
    /// <summary>
    /// Groups the provided error entries by a normalised fingerprint and returns
    /// aggregated results, highlighting recurring errors.
    /// </summary>
    IReadOnlyList<AggregatedError> Aggregate(IEnumerable<ErrorLogEntry> entries);
}

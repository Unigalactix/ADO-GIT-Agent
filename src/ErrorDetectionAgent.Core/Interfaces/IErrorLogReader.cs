using ErrorDetectionAgent.Core.Models;

namespace ErrorDetectionAgent.Core.Interfaces;

/// <summary>
/// Reads error and exception records from the Azure SQL DB log database.
/// </summary>
public interface IErrorLogReader
{
    /// <summary>
    /// Retrieves error log entries within the specified time window.
    /// </summary>
    /// <param name="since">Start of the time window (UTC).</param>
    /// <param name="until">End of the time window (UTC). Defaults to now.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of error log entries.</returns>
    Task<IReadOnlyList<ErrorLogEntry>> GetErrorsAsync(
        DateTime since,
        DateTime? until = null,
        CancellationToken cancellationToken = default);
}

using Dapper;
using ErrorDetectionAgent.Core.Configuration;
using ErrorDetectionAgent.Core.Interfaces;
using ErrorDetectionAgent.Core.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErrorDetectionAgent.Core.Services;

/// <summary>
/// Reads error and exception records from an Azure SQL DB log table.
/// 
/// Expected table schema (customise the query in <see cref="GetErrorsAsync"/> to match
/// your actual log table):
/// 
///   CREATE TABLE dbo.ApplicationLog (
///       Id          BIGINT IDENTITY PRIMARY KEY,
///       Timestamp   DATETIME2       NOT NULL,
///       Severity    NVARCHAR(50)    NOT NULL,
///       Message     NVARCHAR(MAX)   NOT NULL,
///       StackTrace  NVARCHAR(MAX)   NULL,
///       Source      NVARCHAR(200)   NULL,
///       ErrorCode   NVARCHAR(100)   NULL
///   );
/// </summary>
public sealed class SqlErrorLogReader : IErrorLogReader
{
    private readonly AgentSettings _settings;
    private readonly ILogger<SqlErrorLogReader> _logger;

    public SqlErrorLogReader(
        IOptions<AgentSettings> settings,
        ILogger<SqlErrorLogReader> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ErrorLogEntry>> GetErrorsAsync(
        DateTime since,
        DateTime? until = null,
        CancellationToken cancellationToken = default)
    {
        until ??= DateTime.UtcNow;

        _logger.LogInformation(
            "Querying error logs from {Since} to {Until}",
            since, until);

        const string sql = """
            SELECT
                Id,
                [Timestamp],
                Severity,
                [Message],
                StackTrace,
                [Source],
                ErrorCode
            FROM dbo.ApplicationLog
            WHERE Severity IN ('Error', 'Critical', 'Exception')
              AND [Timestamp] BETWEEN @Since AND @Until
            ORDER BY [Timestamp] DESC;
            """;

        await using var connection = new SqlConnection(_settings.SqlConnectionString);
        await connection.OpenAsync(cancellationToken);

        var entries = (await connection.QueryAsync<ErrorLogEntry>(
            new CommandDefinition(sql, new { Since = since, Until = until },
                cancellationToken: cancellationToken)))
            .ToList();

        _logger.LogInformation("Retrieved {Count} error log entries", entries.Count);
        return entries;
    }
}

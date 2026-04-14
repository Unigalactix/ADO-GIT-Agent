using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ErrorDetectionAgent.Core.Interfaces;
using ErrorDetectionAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace ErrorDetectionAgent.Core.Services;

/// <summary>
/// Groups raw error entries by a normalised fingerprint and flags recurring errors.
/// 
/// Fingerprinting strategy:
///   1. Strip numeric literals, GUIDs, and timestamps from the message.
///   2. Concatenate the normalised message with the error source.
///   3. Produce a SHA-256 hash as the fingerprint.
/// 
/// This ensures that errors that differ only in runtime values (IDs, timestamps, etc.)
/// are grouped together.
/// </summary>
public sealed partial class ErrorAggregator : IErrorAggregator
{
    private readonly ILogger<ErrorAggregator> _logger;

    public ErrorAggregator(ILogger<ErrorAggregator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyList<AggregatedError> Aggregate(IEnumerable<ErrorLogEntry> entries)
    {
        var groups = entries
            .GroupBy(e => ComputeFingerprint(e))
            .Select(g =>
            {
                var sorted = g.OrderBy(e => e.Timestamp).ToList();
                var latest = sorted.Last();
                return new AggregatedError
                {
                    Fingerprint = g.Key,
                    Message = latest.Message,
                    Severity = latest.Severity,
                    Source = latest.Source,
                    OccurrenceCount = sorted.Count,
                    FirstSeen = sorted.First().Timestamp,
                    LastSeen = latest.Timestamp,
                    StackTrace = latest.StackTrace,
                    Entries = sorted
                };
            })
            .OrderByDescending(a => a.OccurrenceCount)
            .ThenByDescending(a => a.LastSeen)
            .ToList();

        var recurringCount = groups.Count(g => g.IsRecurring);
        _logger.LogInformation(
            "Aggregated {Total} error groups — {Recurring} are recurring (>1 occurrence)",
            groups.Count, recurringCount);

        return groups;
    }

    // ── Fingerprint helpers ──────────────────────────────────────────────

    private static string ComputeFingerprint(ErrorLogEntry entry)
    {
        var normalised = NormaliseMessage(entry.Message);
        var raw = $"{entry.Source ?? "unknown"}|{normalised}";

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hashBytes);
    }

    private static string NormaliseMessage(string message)
    {
        // Remove GUIDs
        var result = GuidPattern().Replace(message, "<GUID>");

        // Remove numeric literals (integers and decimals)
        result = NumberPattern().Replace(result, "<NUM>");

        // Collapse whitespace
        result = WhitespacePattern().Replace(result, " ").Trim();

        return result;
    }

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidPattern();

    [GeneratedRegex(@"\b\d+(\.\d+)?\b")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}

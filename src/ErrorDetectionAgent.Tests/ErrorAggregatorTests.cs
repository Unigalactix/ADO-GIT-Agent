using ErrorDetectionAgent.Core.Models;
using ErrorDetectionAgent.Core.Services;
using ErrorDetectionAgent.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace ErrorDetectionAgent.Tests;

/// <summary>
/// Tests for <see cref="ErrorAggregator"/> — the pure in-process logic
/// that groups raw error entries by normalised fingerprint and flags recurring errors.
/// </summary>
public sealed class ErrorAggregatorTests
{
    private readonly ErrorAggregator _aggregator = new(NullLogger<ErrorAggregator>.Instance);

    [Fact]
    public void Aggregate_EmptyInput_ReturnsEmptyList()
    {
        var result = _aggregator.Aggregate([]);

        Assert.Empty(result);
    }

    [Fact]
    public void Aggregate_SingleEntry_ReturnsOneGroupNotRecurring()
    {
        var entries = new[]
        {
            new ErrorLogEntry
            {
                Id = 1,
                Timestamp = DateTime.UtcNow,
                Severity = "Error",
                Message = "SomeException: something broke",
                Source = "TestService"
            }
        };

        var result = _aggregator.Aggregate(entries);

        Assert.Single(result);
        Assert.Equal(1, result[0].OccurrenceCount);
        Assert.False(result[0].IsRecurring);
    }

    [Fact]
    public void Aggregate_DuplicateErrors_GroupedTogether_MarkedRecurring()
    {
        var entries = TestData.GetSampleErrors()
            .Where(e => e.ErrorCode == "NRE-001")
            .ToList();

        var result = _aggregator.Aggregate(entries);

        Assert.Single(result);
        Assert.Equal(2, result[0].OccurrenceCount);
        Assert.True(result[0].IsRecurring);
    }

    [Fact]
    public void Aggregate_SampleData_ProducesExpectedGroups()
    {
        // The sample data has 4 entries: 2x NRE-001, 1x SQL-TIMEOUT, 1x LINQ-001
        // NRE-001 entries should be grouped together → 3 groups total
        var entries = TestData.GetSampleErrors();

        var result = _aggregator.Aggregate(entries);

        Assert.Equal(3, result.Count);

        // Sorted by occurrence count descending, so NRE group should be first
        Assert.Equal(2, result[0].OccurrenceCount);
        Assert.True(result[0].IsRecurring);

        // Remaining two should have count = 1
        Assert.All(result.Skip(1), g => Assert.Equal(1, g.OccurrenceCount));
        Assert.All(result.Skip(1), g => Assert.False(g.IsRecurring));
    }

    [Fact]
    public void Aggregate_FingerprintIgnoresNumericDifferences()
    {
        // Two messages differing only in a numeric ID should produce the same fingerprint
        var entries = new[]
        {
            new ErrorLogEntry
            {
                Id = 10,
                Timestamp = DateTime.UtcNow.AddMinutes(-5),
                Severity = "Error",
                Message = "Timeout after 30 seconds for request 12345",
                Source = "ApiGateway"
            },
            new ErrorLogEntry
            {
                Id = 11,
                Timestamp = DateTime.UtcNow.AddMinutes(-1),
                Severity = "Error",
                Message = "Timeout after 60 seconds for request 99999",
                Source = "ApiGateway"
            }
        };

        var result = _aggregator.Aggregate(entries);

        Assert.Single(result);
        Assert.Equal(2, result[0].OccurrenceCount);
    }

    [Fact]
    public void Aggregate_FingerprintIgnoresGuidDifferences()
    {
        var entries = new[]
        {
            new ErrorLogEntry
            {
                Id = 20,
                Timestamp = DateTime.UtcNow.AddMinutes(-5),
                Severity = "Error",
                Message = "Failed to process entity 12345678-1234-1234-1234-123456789012",
                Source = "EntityService"
            },
            new ErrorLogEntry
            {
                Id = 21,
                Timestamp = DateTime.UtcNow.AddMinutes(-1),
                Severity = "Error",
                Message = "Failed to process entity abcdefab-cdef-abcd-efab-cdefabcdefab",
                Source = "EntityService"
            }
        };

        var result = _aggregator.Aggregate(entries);

        Assert.Single(result);
        Assert.Equal(2, result[0].OccurrenceCount);
    }

    [Fact]
    public void Aggregate_DifferentSources_NotGroupedTogether()
    {
        var entries = new[]
        {
            new ErrorLogEntry
            {
                Id = 30,
                Timestamp = DateTime.UtcNow,
                Severity = "Error",
                Message = "Connection timeout",
                Source = "ServiceA"
            },
            new ErrorLogEntry
            {
                Id = 31,
                Timestamp = DateTime.UtcNow,
                Severity = "Error",
                Message = "Connection timeout",
                Source = "ServiceB"
            }
        };

        var result = _aggregator.Aggregate(entries);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Aggregate_OrderedByOccurrenceCountDescending()
    {
        var entries = TestData.GetSampleErrors();

        var result = _aggregator.Aggregate(entries);

        // First group should have the highest occurrence count
        for (var i = 1; i < result.Count; i++)
        {
            Assert.True(result[i - 1].OccurrenceCount >= result[i].OccurrenceCount);
        }
    }
}

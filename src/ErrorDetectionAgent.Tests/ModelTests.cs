using ErrorDetectionAgent.Core.Models;

namespace ErrorDetectionAgent.Tests;

/// <summary>
/// Tests for the data models to verify default values, computed properties,
/// and overall model correctness.
/// </summary>
public sealed class ModelTests
{
    [Fact]
    public void ErrorLogEntry_DefaultValues()
    {
        var entry = new ErrorLogEntry();

        Assert.Equal(0, entry.Id);
        Assert.Equal(string.Empty, entry.Severity);
        Assert.Equal(string.Empty, entry.Message);
        Assert.Null(entry.StackTrace);
        Assert.Null(entry.Source);
        Assert.Null(entry.ErrorCode);
    }

    [Fact]
    public void AggregatedError_IsRecurring_WhenCountGreaterThanOne()
    {
        var error = new AggregatedError { OccurrenceCount = 2 };

        Assert.True(error.IsRecurring);
    }

    [Fact]
    public void AggregatedError_IsNotRecurring_WhenCountIsOne()
    {
        var error = new AggregatedError { OccurrenceCount = 1 };

        Assert.False(error.IsRecurring);
    }

    [Fact]
    public void AggregatedError_IsNotRecurring_WhenCountIsZero()
    {
        var error = new AggregatedError { OccurrenceCount = 0 };

        Assert.False(error.IsRecurring);
    }

    [Fact]
    public void FixProposal_DefaultValues()
    {
        var fix = new FixProposal();

        Assert.Equal(string.Empty, fix.Summary);
        Assert.Equal(string.Empty, fix.SuggestedDiff);
        Assert.Empty(fix.AffectedFiles);
        Assert.Equal(0.0, fix.Confidence);
        Assert.Equal(string.Empty, fix.BranchName);
    }

    [Fact]
    public void WorkItemResult_DefaultValues()
    {
        var result = new WorkItemResult();

        Assert.Equal(0, result.Id);
        Assert.Equal(string.Empty, result.Url);
        Assert.Equal(string.Empty, result.Title);
        Assert.Equal(string.Empty, result.State);
    }

    [Fact]
    public void FixProposal_CanSetAllProperties()
    {
        var fix = new FixProposal
        {
            Summary = "Test summary",
            SuggestedDiff = "// test diff",
            AffectedFiles = ["file1.cs", "file2.cs"],
            Confidence = 0.95,
            BranchName = "fix/test"
        };

        Assert.Equal("Test summary", fix.Summary);
        Assert.Equal("// test diff", fix.SuggestedDiff);
        Assert.Equal(2, fix.AffectedFiles.Count);
        Assert.Equal(0.95, fix.Confidence);
        Assert.Equal("fix/test", fix.BranchName);
    }
}

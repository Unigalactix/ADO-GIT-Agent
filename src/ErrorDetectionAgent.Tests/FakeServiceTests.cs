using ErrorDetectionAgent.Tests.Fakes;
using ErrorDetectionAgent.Tests.Helpers;

namespace ErrorDetectionAgent.Tests;

/// <summary>
/// Tests for the fake service implementations to ensure they behave
/// correctly and can be relied upon in orchestrator integration tests.
/// </summary>
public sealed class FakeServiceTests
{
    [Fact]
    public async Task FakeErrorLogReader_ReturnsConfiguredEntries()
    {
        var entries = TestData.GetSampleErrors();
        var reader = new FakeErrorLogReader(entries);

        var result = await reader.GetErrorsAsync(DateTime.UtcNow.AddDays(-1));

        Assert.Equal(entries.Count, result.Count);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task FakeErrorLogReader_FiltersBy_Since()
    {
        var entries = TestData.GetSampleErrors();
        var reader = new FakeErrorLogReader(entries);

        // Only entries from the last 20 minutes
        var result = await reader.GetErrorsAsync(DateTime.UtcNow.AddMinutes(-20));

        Assert.Single(result); // Only the LINQ-001 entry at -15 minutes
    }

    [Fact]
    public async Task FakeErrorLogReader_EmptyByDefault()
    {
        var reader = new FakeErrorLogReader();

        var result = await reader.GetErrorsAsync(DateTime.UtcNow.AddDays(-1));

        Assert.Empty(result);
    }

    [Fact]
    public async Task FakeDevOpsWorkItemService_CreatesWithIncrementingIds()
    {
        var service = new FakeDevOpsWorkItemService();
        var error = TestData.GetSampleAggregatedError();

        var result1 = await service.CreateBugAsync(error);
        var result2 = await service.CreateBugAsync(error);

        Assert.NotEqual(result1.Id, result2.Id);
        Assert.Equal(2, service.CreatedBugs.Count);
        Assert.Equal("New", result1.State);
    }

    [Fact]
    public async Task FakeGitService_TracksBranchesCommitsAndPRs()
    {
        var service = new FakeGitService();
        var fix = TestData.GetSampleFixProposal();

        await service.CreateBranchAsync("fix/test-branch");
        await service.CommitFixAsync("fix/test-branch", fix);
        var prUrl = await service.CreatePullRequestAsync("fix/test-branch", "Test PR", "Description");

        Assert.Single(service.CreatedBranches);
        Assert.Single(service.Commits);
        Assert.Single(service.PullRequests);
        Assert.Contains("pullrequest", prUrl);
    }

    [Fact]
    public async Task FakeLlmFixProposer_ReturnsConfiguredConfidence()
    {
        var proposer = new FakeLlmFixProposer(confidence: 0.95);
        var error = TestData.GetSampleAggregatedError();

        var fix = await proposer.ProposeFixAsync(error);

        Assert.Equal(0.95, fix.Confidence);
        Assert.Single(proposer.RequestedErrors);
        Assert.NotEmpty(fix.Summary);
        Assert.NotEmpty(fix.AffectedFiles);
    }

    [Fact]
    public async Task FakeNotificationService_RecordsSentNotifications()
    {
        var service = new FakeNotificationService();

        await service.NotifyForReviewAsync("https://pr-url", "Fix summary", "https://work-item-url");

        Assert.Single(service.SentNotifications);
        var (prUrl, summary, wiUrl) = service.SentNotifications[0];
        Assert.Equal("https://pr-url", prUrl);
        Assert.Equal("Fix summary", summary);
        Assert.Equal("https://work-item-url", wiUrl);
    }

    [Fact]
    public async Task FakeMergeAssistant_AutoApprove_ReturnsTrue()
    {
        var assistant = new FakeMergeAssistant(autoApprove: true);

        var merged = await assistant.WaitForApprovalAndMergeAsync("https://pr-url");

        Assert.True(merged);
        Assert.Single(assistant.MonitoredPrs);
    }

    [Fact]
    public async Task FakeMergeAssistant_TimeoutMode_ReturnsFalse()
    {
        var assistant = new FakeMergeAssistant(autoApprove: false);

        var merged = await assistant.WaitForApprovalAndMergeAsync("https://pr-url");

        Assert.False(merged);
    }
}

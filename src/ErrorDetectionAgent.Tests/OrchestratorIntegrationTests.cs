using ErrorDetectionAgent.Core.Configuration;
using ErrorDetectionAgent.Core.Services;
using ErrorDetectionAgent.Tests.Fakes;
using ErrorDetectionAgent.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ErrorDetectionAgent.Tests;

/// <summary>
/// End-to-end tests for <see cref="ErrorDetectionOrchestrator"/> using
/// in-memory fakes for every external dependency. Validates the complete
/// pipeline without any real Azure SQL, Azure DevOps, Git, or OpenAI connections.
/// </summary>
public sealed class OrchestratorIntegrationTests
{
    private static AgentSettings CreateSettings() => new()
    {
        SqlConnectionString = "Server=tcp:dummy.database.windows.net;Database=DummyDb;",
        LookbackHours = 24,
        DevOpsOrgUrl = "https://dev.azure.com/testorg",
        DevOpsProject = "testproject",
        DevOpsPat = "dummy-pat-token",
        RepoLocalPath = "/tmp/test-repo",
        RepoRemoteUrl = "https://dev.azure.com/testorg/testproject/_git/testrepo",
        GitUserName = "TestAgent",
        GitUserEmail = "test@agent.local",
        GitPat = "dummy-git-pat",
        OpenAiEndpoint = "https://dummy.openai.azure.com/",
        OpenAiApiKey = "dummy-openai-key",
        OpenAiDeployment = "gpt-4",
        NotificationWebhookUrl = "https://dummy.webhook.example.com",
        MergePollIntervalSeconds = 1,
        MergeTimeoutMinutes = 1
    };

    [Fact]
    public async Task RunAsync_NoErrors_CompletesWithoutProcessing()
    {
        // Arrange — empty error log
        var logReader = new FakeErrorLogReader([]);
        var aggregator = new ErrorAggregator(NullLogger<ErrorAggregator>.Instance);
        var devOps = new FakeDevOpsWorkItemService();
        var git = new FakeGitService();
        var llm = new FakeLlmFixProposer();
        var notifications = new FakeNotificationService();
        var merge = new FakeMergeAssistant();

        var orchestrator = new ErrorDetectionOrchestrator(
            logReader, aggregator, devOps, git, llm, notifications, merge,
            Options.Create(CreateSettings()),
            NullLogger<ErrorDetectionOrchestrator>.Instance);

        // Act
        await orchestrator.RunAsync();

        // Assert — pipeline exits early, nothing processed
        Assert.Equal(1, logReader.CallCount);
        Assert.Empty(devOps.CreatedBugs);
        Assert.Empty(git.CreatedBranches);
        Assert.Empty(notifications.SentNotifications);
    }

    [Fact]
    public async Task RunAsync_WithSampleErrors_ProcessesAllGroups()
    {
        // Arrange
        var logReader = new FakeErrorLogReader(TestData.GetSampleErrors());
        var aggregator = new ErrorAggregator(NullLogger<ErrorAggregator>.Instance);
        var devOps = new FakeDevOpsWorkItemService();
        var git = new FakeGitService();
        var llm = new FakeLlmFixProposer(confidence: 0.85);
        var notifications = new FakeNotificationService();
        var merge = new FakeMergeAssistant(autoApprove: true);

        var orchestrator = new ErrorDetectionOrchestrator(
            logReader, aggregator, devOps, git, llm, notifications, merge,
            Options.Create(CreateSettings()),
            NullLogger<ErrorDetectionOrchestrator>.Instance);

        // Act
        await orchestrator.RunAsync();

        // Assert — 3 unique error groups processed
        Assert.Equal(3, devOps.CreatedBugs.Count);
        Assert.Equal(3, git.CreatedBranches.Count);
        Assert.Equal(3, git.Commits.Count);
        Assert.Equal(3, git.PullRequests.Count);
        Assert.Equal(3, notifications.SentNotifications.Count);
        Assert.Equal(3, merge.MonitoredPrs.Count);
    }

    [Fact]
    public async Task RunAsync_LowConfidenceFix_SkipsCommit()
    {
        // Arrange — LLM returns very low confidence
        var logReader = new FakeErrorLogReader(TestData.GetSampleErrors().Take(1).ToList());
        var aggregator = new ErrorAggregator(NullLogger<ErrorAggregator>.Instance);
        var devOps = new FakeDevOpsWorkItemService();
        var git = new FakeGitService();
        var llm = new FakeLlmFixProposer(confidence: 0.05); // Below 0.1 threshold
        var notifications = new FakeNotificationService();
        var merge = new FakeMergeAssistant();

        var orchestrator = new ErrorDetectionOrchestrator(
            logReader, aggregator, devOps, git, llm, notifications, merge,
            Options.Create(CreateSettings()),
            NullLogger<ErrorDetectionOrchestrator>.Instance);

        // Act
        await orchestrator.RunAsync();

        // Assert — bug was created and branch was created, but commit was skipped
        Assert.Single(devOps.CreatedBugs);
        Assert.Single(git.CreatedBranches);
        Assert.Empty(git.Commits);        // Skipped due to low confidence
        Assert.Empty(git.PullRequests);    // No PR without commit
        Assert.Empty(notifications.SentNotifications);
    }

    [Fact]
    public async Task RunAsync_MergeTimesOut_StillCompletes()
    {
        // Arrange — merge assistant simulates timeout (returns false)
        var logReader = new FakeErrorLogReader(TestData.GetSampleErrors().Take(1).ToList());
        var aggregator = new ErrorAggregator(NullLogger<ErrorAggregator>.Instance);
        var devOps = new FakeDevOpsWorkItemService();
        var git = new FakeGitService();
        var llm = new FakeLlmFixProposer(confidence: 0.90);
        var notifications = new FakeNotificationService();
        var merge = new FakeMergeAssistant(autoApprove: false); // Simulates timeout

        var orchestrator = new ErrorDetectionOrchestrator(
            logReader, aggregator, devOps, git, llm, notifications, merge,
            Options.Create(CreateSettings()),
            NullLogger<ErrorDetectionOrchestrator>.Instance);

        // Act — should not throw
        await orchestrator.RunAsync();

        // Assert — pipeline still ran, just the merge wasn't completed
        Assert.Single(devOps.CreatedBugs);
        Assert.Single(git.PullRequests);
        Assert.Single(merge.MonitoredPrs);
    }

    [Fact]
    public async Task RunAsync_RecurringErrors_FlaggedCorrectly()
    {
        // Arrange — two identical errors = recurring
        var logReader = new FakeErrorLogReader(TestData.GetSampleErrors());
        var aggregator = new ErrorAggregator(NullLogger<ErrorAggregator>.Instance);
        var devOps = new FakeDevOpsWorkItemService();
        var git = new FakeGitService();
        var llm = new FakeLlmFixProposer();
        var notifications = new FakeNotificationService();
        var merge = new FakeMergeAssistant();

        var orchestrator = new ErrorDetectionOrchestrator(
            logReader, aggregator, devOps, git, llm, notifications, merge,
            Options.Create(CreateSettings()),
            NullLogger<ErrorDetectionOrchestrator>.Instance);

        // Act
        await orchestrator.RunAsync();

        // Assert — the NRE-001 group should be flagged as recurring
        var recurringBugs = devOps.CreatedBugs.Where(b => b.Error.IsRecurring).ToList();
        Assert.Single(recurringBugs);
        Assert.Equal(2, recurringBugs[0].Error.OccurrenceCount);
    }

    [Fact]
    public async Task RunAsync_BranchNamesAreValid()
    {
        // Arrange
        var logReader = new FakeErrorLogReader(TestData.GetSampleErrors());
        var aggregator = new ErrorAggregator(NullLogger<ErrorAggregator>.Instance);
        var devOps = new FakeDevOpsWorkItemService();
        var git = new FakeGitService();
        var llm = new FakeLlmFixProposer();
        var notifications = new FakeNotificationService();
        var merge = new FakeMergeAssistant();

        var orchestrator = new ErrorDetectionOrchestrator(
            logReader, aggregator, devOps, git, llm, notifications, merge,
            Options.Create(CreateSettings()),
            NullLogger<ErrorDetectionOrchestrator>.Instance);

        // Act
        await orchestrator.RunAsync();

        // Assert — all branch names start with "fix/" and contain only safe characters
        // The fingerprint prefix uses uppercase hex (from Convert.ToHexString), followed
        // by the lowercase slug.
        Assert.All(git.CreatedBranches, branch =>
        {
            Assert.StartsWith("fix/", branch);
            Assert.Matches(@"^fix/[a-zA-Z0-9\-]+$", branch);
        });
    }

    [Fact]
    public async Task RunAsync_PullRequestTitlesContainBugIds()
    {
        // Arrange
        var logReader = new FakeErrorLogReader(TestData.GetSampleErrors().Take(1).ToList());
        var aggregator = new ErrorAggregator(NullLogger<ErrorAggregator>.Instance);
        var devOps = new FakeDevOpsWorkItemService();
        var git = new FakeGitService();
        var llm = new FakeLlmFixProposer();
        var notifications = new FakeNotificationService();
        var merge = new FakeMergeAssistant();

        var orchestrator = new ErrorDetectionOrchestrator(
            logReader, aggregator, devOps, git, llm, notifications, merge,
            Options.Create(CreateSettings()),
            NullLogger<ErrorDetectionOrchestrator>.Instance);

        // Act
        await orchestrator.RunAsync();

        // Assert — PR title includes [AutoFix] prefix and Bug # reference
        Assert.Single(git.PullRequests);
        var (_, title, _) = git.PullRequests[0];
        Assert.StartsWith("[AutoFix]", title);
        Assert.Contains("Bug #", title);
    }

    [Fact]
    public async Task RunAsync_CancellationRespected()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        var logReader = new FakeErrorLogReader(TestData.GetSampleErrors());
        var aggregator = new ErrorAggregator(NullLogger<ErrorAggregator>.Instance);
        var devOps = new FakeDevOpsWorkItemService();
        var git = new FakeGitService();
        var llm = new FakeLlmFixProposer();
        var notifications = new FakeNotificationService();
        var merge = new FakeMergeAssistant();

        var orchestrator = new ErrorDetectionOrchestrator(
            logReader, aggregator, devOps, git, llm, notifications, merge,
            Options.Create(CreateSettings()),
            NullLogger<ErrorDetectionOrchestrator>.Instance);

        // Act & Assert — should throw OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => orchestrator.RunAsync(cts.Token));
    }
}

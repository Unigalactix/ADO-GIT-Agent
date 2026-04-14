using ErrorDetectionAgent.Core.Interfaces;

namespace ErrorDetectionAgent.Tests.Fakes;

/// <summary>
/// In-memory fake that records webhook notifications without sending HTTP requests.
/// </summary>
public sealed class FakeNotificationService : INotificationService
{
    private readonly List<(string PrUrl, string Summary, string WorkItemUrl)> _notifications = [];

    /// <summary>All notifications that were "sent".</summary>
    public IReadOnlyList<(string PrUrl, string Summary, string WorkItemUrl)> SentNotifications
        => _notifications;

    public Task NotifyForReviewAsync(
        string pullRequestUrl,
        string summary,
        string workItemUrl,
        CancellationToken cancellationToken = default)
    {
        _notifications.Add((pullRequestUrl, summary, workItemUrl));
        return Task.CompletedTask;
    }
}

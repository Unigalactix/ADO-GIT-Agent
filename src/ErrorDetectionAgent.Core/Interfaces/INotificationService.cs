namespace ErrorDetectionAgent.Core.Interfaces;

/// <summary>
/// Sends notifications to humans so they can review proposed changes.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends an alert about a proposed fix that requires human review.
    /// </summary>
    /// <param name="pullRequestUrl">URL of the pull request to review.</param>
    /// <param name="summary">Human-readable summary of the change.</param>
    /// <param name="workItemUrl">Link to the related Azure DevOps work item.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyForReviewAsync(
        string pullRequestUrl,
        string summary,
        string workItemUrl,
        CancellationToken cancellationToken = default);
}

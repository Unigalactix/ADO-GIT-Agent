namespace ErrorDetectionAgent.Core.Interfaces;

/// <summary>
/// Monitors pull requests and assists the human-in-the-loop to merge approved changes.
/// </summary>
public interface IMergeAssistant
{
    /// <summary>
    /// Monitors the pull request and, once the human approves it,
    /// completes the merge back to the main branch.
    /// </summary>
    /// <param name="pullRequestUrl">The URL of the pull request to monitor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the merge was completed successfully.</returns>
    Task<bool> WaitForApprovalAndMergeAsync(
        string pullRequestUrl,
        CancellationToken cancellationToken = default);
}

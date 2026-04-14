using ErrorDetectionAgent.Core.Models;

namespace ErrorDetectionAgent.Core.Interfaces;

/// <summary>
/// Manages Git repository operations — branching, committing proposed fixes,
/// and creating pull requests.
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Creates a new feature branch from the main branch for a proposed fix.
    /// </summary>
    /// <param name="branchName">Name of the new branch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full branch reference name.</returns>
    Task<string> CreateBranchAsync(
        string branchName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the supplied fix proposal to the given branch.
    /// </summary>
    Task CommitFixAsync(
        string branchName,
        FixProposal fix,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a pull request from the feature branch back to main.
    /// </summary>
    /// <returns>The URL of the created pull request.</returns>
    Task<string> CreatePullRequestAsync(
        string branchName,
        string title,
        string description,
        CancellationToken cancellationToken = default);
}

using ErrorDetectionAgent.Core.Interfaces;
using ErrorDetectionAgent.Core.Models;

namespace ErrorDetectionAgent.Tests.Fakes;

/// <summary>
/// In-memory fake that simulates Git branch creation, committing, and PR creation.
/// Records all calls for assertion.
/// </summary>
public sealed class FakeGitService : IGitService
{
    private readonly List<string> _createdBranches = [];
    private readonly List<(string Branch, FixProposal Fix)> _commits = [];
    private readonly List<(string Branch, string Title, string Description)> _pullRequests = [];

    public IReadOnlyList<string> CreatedBranches => _createdBranches;
    public IReadOnlyList<(string Branch, FixProposal Fix)> Commits => _commits;
    public IReadOnlyList<(string Branch, string Title, string Description)> PullRequests => _pullRequests;

    public Task<string> CreateBranchAsync(
        string branchName,
        CancellationToken cancellationToken = default)
    {
        _createdBranches.Add(branchName);
        return Task.FromResult($"refs/heads/{branchName}");
    }

    public Task CommitFixAsync(
        string branchName,
        FixProposal fix,
        CancellationToken cancellationToken = default)
    {
        _commits.Add((branchName, fix));
        return Task.CompletedTask;
    }

    public Task<string> CreatePullRequestAsync(
        string branchName,
        string title,
        string description,
        CancellationToken cancellationToken = default)
    {
        _pullRequests.Add((branchName, title, description));
        return Task.FromResult(
            $"https://dev.azure.com/testorg/testproject/_git/testrepo/pullrequest/{_pullRequests.Count}");
    }
}

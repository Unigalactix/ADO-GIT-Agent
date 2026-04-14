using ErrorDetectionAgent.Core.Interfaces;

namespace ErrorDetectionAgent.Tests.Fakes;

/// <summary>
/// In-memory fake that simulates the merge assistant without polling any API.
/// Configurable to either approve or time-out.
/// </summary>
public sealed class FakeMergeAssistant : IMergeAssistant
{
    private readonly bool _autoApprove;
    private readonly List<string> _monitoredPrs = [];

    /// <summary>
    /// Creates a fake merge assistant.
    /// </summary>
    /// <param name="autoApprove">
    /// When true, every PR is immediately approved and merged. When false, simulates a timeout.
    /// </param>
    public FakeMergeAssistant(bool autoApprove = true)
    {
        _autoApprove = autoApprove;
    }

    /// <summary>PR URLs that were monitored.</summary>
    public IReadOnlyList<string> MonitoredPrs => _monitoredPrs;

    public Task<bool> WaitForApprovalAndMergeAsync(
        string pullRequestUrl,
        CancellationToken cancellationToken = default)
    {
        _monitoredPrs.Add(pullRequestUrl);
        return Task.FromResult(_autoApprove);
    }
}

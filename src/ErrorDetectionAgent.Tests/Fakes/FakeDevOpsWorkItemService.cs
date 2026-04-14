using ErrorDetectionAgent.Core.Interfaces;
using ErrorDetectionAgent.Core.Models;

namespace ErrorDetectionAgent.Tests.Fakes;

/// <summary>
/// In-memory fake that simulates Azure DevOps work item creation.
/// Records every call so tests can assert the correct bugs were created.
/// </summary>
public sealed class FakeDevOpsWorkItemService : IDevOpsWorkItemService
{
    private int _nextId = 100;
    private readonly List<(AggregatedError Error, WorkItemResult Result)> _created = [];

    /// <summary>All bugs that were "created".</summary>
    public IReadOnlyList<(AggregatedError Error, WorkItemResult Result)> CreatedBugs => _created;

    public Task<WorkItemResult> CreateBugAsync(
        AggregatedError error,
        CancellationToken cancellationToken = default)
    {
        var result = new WorkItemResult
        {
            Id = _nextId++,
            Url = $"https://dev.azure.com/testorg/testproject/_workitems/edit/{_nextId - 1}",
            Title = $"{error.Severity}: {(error.Message.Length > 80 ? error.Message[..80] + "…" : error.Message)}",
            State = "New"
        };

        _created.Add((error, result));
        return Task.FromResult(result);
    }
}

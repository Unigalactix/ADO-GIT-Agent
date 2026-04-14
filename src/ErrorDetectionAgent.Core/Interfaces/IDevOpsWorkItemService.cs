using ErrorDetectionAgent.Core.Models;

namespace ErrorDetectionAgent.Core.Interfaces;

/// <summary>
/// Creates and manages work items (bugs) in Azure DevOps.
/// </summary>
public interface IDevOpsWorkItemService
{
    /// <summary>
    /// Creates a bug work item in Azure DevOps for the given aggregated error.
    /// </summary>
    Task<WorkItemResult> CreateBugAsync(
        AggregatedError error,
        CancellationToken cancellationToken = default);
}

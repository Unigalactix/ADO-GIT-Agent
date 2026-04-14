namespace ErrorDetectionAgent.Core.Models;

/// <summary>
/// Contains the result of creating a work item in Azure DevOps.
/// </summary>
public sealed class WorkItemResult
{
    /// <summary>Azure DevOps work item ID.</summary>
    public int Id { get; set; }

    /// <summary>Direct URL to the work item in Azure DevOps.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Title of the created work item.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Work item state (e.g., New, Active).</summary>
    public string State { get; set; } = string.Empty;
}

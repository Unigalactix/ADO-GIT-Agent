using System.Text;
using ErrorDetectionAgent.Core.Configuration;
using ErrorDetectionAgent.Core.Interfaces;
using ErrorDetectionAgent.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace ErrorDetectionAgent.Core.Services;

/// <summary>
/// Creates Bug work items in Azure DevOps for detected errors.
/// 
/// Each aggregated error becomes a single Bug with:
///   • Title — a concise description of the error.
///   • Repro Steps — full error details including occurrence count, stack trace,
///     and whether the error is recurring.
///   • Tags — "AutoDetected" so triagers can filter agent-created items.
/// </summary>
public sealed class DevOpsWorkItemService : IDevOpsWorkItemService
{
    private readonly AgentSettings _settings;
    private readonly ILogger<DevOpsWorkItemService> _logger;

    public DevOpsWorkItemService(
        IOptions<AgentSettings> settings,
        ILogger<DevOpsWorkItemService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<WorkItemResult> CreateBugAsync(
        AggregatedError error,
        CancellationToken cancellationToken = default)
    {
        var credentials = new VssBasicCredential(string.Empty, _settings.DevOpsPat);
        using var connection = new VssConnection(new Uri(_settings.DevOpsOrgUrl), credentials);
        var witClient = connection.GetClient<WorkItemTrackingHttpClient>();

        var title = BuildTitle(error);
        var description = BuildDescription(error);

        var patchDocument = new JsonPatchDocument
        {
            new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/System.Title",
                Value = title
            },
            new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/Microsoft.VSTS.TCM.ReproSteps",
                Value = description
            },
            new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/System.Tags",
                Value = error.IsRecurring ? "AutoDetected;Recurring" : "AutoDetected"
            }
        };

        _logger.LogInformation("Creating Bug work item: {Title}", title);

        var workItem = await witClient.CreateWorkItemAsync(
            patchDocument,
            _settings.DevOpsProject,
            "Bug",
            cancellationToken: cancellationToken);

        var result = new WorkItemResult
        {
            Id = workItem.Id ?? 0,
            Url = workItem.Url ?? string.Empty,
            Title = title,
            State = workItem.Fields.TryGetValue("System.State", out var state)
                ? state?.ToString() ?? "New"
                : "New"
        };

        _logger.LogInformation(
            "Created Bug #{Id} — {Title}", result.Id, result.Title);

        return result;
    }

    // ── Private helpers ─────────────────────────────────────────────

    private static string BuildTitle(AggregatedError error)
    {
        var prefix = error.IsRecurring
            ? $"[RECURRING x{error.OccurrenceCount}] "
            : string.Empty;

        var truncatedMessage = error.Message.Length > 120
            ? error.Message[..120] + "…"
            : error.Message;

        return $"{prefix}{error.Severity}: {truncatedMessage}";
    }

    private static string BuildDescription(AggregatedError error)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<h2>Error Detection Agent — Auto-Generated Bug</h2>");
        sb.AppendLine($"<p><strong>Severity:</strong> {error.Severity}</p>");
        sb.AppendLine($"<p><strong>Source:</strong> {error.Source ?? "N/A"}</p>");
        sb.AppendLine($"<p><strong>Occurrences:</strong> {error.OccurrenceCount}</p>");

        if (error.IsRecurring)
        {
            sb.AppendLine("<p>⚠️ <strong>This error is recurring.</strong> " +
                          "It has been detected more than once in the current time window.</p>");
        }

        sb.AppendLine($"<p><strong>First seen:</strong> {error.FirstSeen:u}</p>");
        sb.AppendLine($"<p><strong>Last seen:</strong> {error.LastSeen:u}</p>");
        sb.AppendLine($"<h3>Message</h3><pre>{error.Message}</pre>");

        if (!string.IsNullOrWhiteSpace(error.StackTrace))
        {
            sb.AppendLine($"<h3>Stack Trace</h3><pre>{error.StackTrace}</pre>");
        }

        return sb.ToString();
    }
}

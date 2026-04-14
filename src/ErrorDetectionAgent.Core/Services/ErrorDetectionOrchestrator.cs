using System.Text.RegularExpressions;
using ErrorDetectionAgent.Core.Interfaces;
using ErrorDetectionAgent.Core.Configuration;
using ErrorDetectionAgent.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErrorDetectionAgent.Core.Services;

/// <summary>
/// Orchestrates the complete Error Detection Agent workflow end-to-end:
/// 
///   Step 1 — Read errors from Azure SQL DB.
///   Step 2 — Aggregate errors and detect recurring patterns.
///   Step 3 — For each aggregated error:
///       a. Create an Azure DevOps Bug work item.
///       b. Create a feature branch from main.
///       c. Ask the LLM to propose a fix.
///       d. Commit the fix to the branch and create a pull request.
///       e. Notify a human to review the PR.
///       f. Wait for approval, then complete the merge.
/// </summary>
public sealed partial class ErrorDetectionOrchestrator
{
    private readonly IErrorLogReader _logReader;
    private readonly IErrorAggregator _aggregator;
    private readonly IDevOpsWorkItemService _workItemService;
    private readonly IGitService _gitService;
    private readonly ILlmFixProposer _llmProposer;
    private readonly INotificationService _notificationService;
    private readonly IMergeAssistant _mergeAssistant;
    private readonly AgentSettings _settings;
    private readonly ILogger<ErrorDetectionOrchestrator> _logger;

    public ErrorDetectionOrchestrator(
        IErrorLogReader logReader,
        IErrorAggregator aggregator,
        IDevOpsWorkItemService workItemService,
        IGitService gitService,
        ILlmFixProposer llmProposer,
        INotificationService notificationService,
        IMergeAssistant mergeAssistant,
        IOptions<AgentSettings> settings,
        ILogger<ErrorDetectionOrchestrator> logger)
    {
        _logReader = logReader;
        _aggregator = aggregator;
        _workItemService = workItemService;
        _gitService = gitService;
        _llmProposer = llmProposer;
        _notificationService = notificationService;
        _mergeAssistant = mergeAssistant;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Runs the full agent pipeline once.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("═══ Error Detection Agent — Starting pipeline ═══");

        // ── Step 1: Read errors ──────────────────────────────────
        var since = DateTime.UtcNow.AddHours(-_settings.LookbackHours);
        var errors = await _logReader.GetErrorsAsync(since, cancellationToken: cancellationToken);

        if (errors.Count == 0)
        {
            _logger.LogInformation("No errors found in the last {Hours} hours. Nothing to do.",
                _settings.LookbackHours);
            return;
        }

        // ── Step 2: Aggregate & highlight recurring ──────────────
        var aggregated = _aggregator.Aggregate(errors);

        _logger.LogInformation(
            "Found {Total} unique error groups ({Recurring} recurring) from {Raw} raw entries",
            aggregated.Count,
            aggregated.Count(a => a.IsRecurring),
            errors.Count);

        foreach (var group in aggregated.Where(a => a.IsRecurring))
        {
            _logger.LogWarning(
                "⚠ RECURRING ERROR (x{Count}): {Message}",
                group.OccurrenceCount,
                group.Message.Length > 100 ? group.Message[..100] + "…" : group.Message);
        }

        // ── Step 3: Process each error group ─────────────────────
        foreach (var errorGroup in aggregated)
        {
            try
            {
                await ProcessErrorGroupAsync(errorGroup, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process error group [{Fingerprint}]: {Message}",
                    errorGroup.Fingerprint[..8], errorGroup.Message);
            }
        }

        _logger.LogInformation("═══ Error Detection Agent — Pipeline complete ═══");
    }

    // ── Private workflow per error group ──────────────────────────────

    private async Task ProcessErrorGroupAsync(
        AggregatedError errorGroup,
        CancellationToken cancellationToken)
    {
        var shortFp = errorGroup.Fingerprint[..8];
        _logger.LogInformation(
            "── Processing error [{Fp}] ({Count} occurrences) ──",
            shortFp, errorGroup.OccurrenceCount);

        // Step 3a: Create Azure DevOps work item
        var workItem = await _workItemService.CreateBugAsync(errorGroup, cancellationToken);
        _logger.LogInformation(
            "Created Bug #{Id}: {Title}", workItem.Id, workItem.Title);

        // Step 3b: Create a feature branch
        var branchName = GenerateBranchName(errorGroup);
        await _gitService.CreateBranchAsync(branchName, cancellationToken);

        // Step 3c: Propose a fix via LLM
        var fix = await _llmProposer.ProposeFixAsync(errorGroup, cancellationToken);
        fix.BranchName = branchName;

        if (fix.Confidence < 0.1)
        {
            _logger.LogWarning(
                "LLM confidence too low ({Confidence:P0}) — skipping commit for [{Fp}]",
                fix.Confidence, shortFp);
            return;
        }

        _logger.LogInformation(
            "LLM proposed fix (confidence: {Confidence:P0}): {Summary}",
            fix.Confidence, fix.Summary);

        // Step 3d: Commit and push, then create PR
        await _gitService.CommitFixAsync(branchName, fix, cancellationToken);

        var prTitle = $"[AutoFix] {fix.Summary} (Bug #{workItem.Id})";
        var prDescription = BuildPrDescription(errorGroup, workItem, fix);

        var prUrl = await _gitService.CreatePullRequestAsync(
            branchName, prTitle, prDescription, cancellationToken);

        // Step 3e: Notify human for review
        await _notificationService.NotifyForReviewAsync(
            prUrl, fix.Summary, workItem.Url, cancellationToken);

        _logger.LogInformation("Human notified — awaiting approval for PR: {PrUrl}", prUrl);

        // Step 3f: Wait for approval and merge
        var merged = await _mergeAssistant.WaitForApprovalAndMergeAsync(
            prUrl, cancellationToken);

        if (merged)
        {
            _logger.LogInformation("✅ Fix merged for error [{Fp}]", shortFp);
        }
        else
        {
            _logger.LogWarning("⏳ Merge not completed for error [{Fp}] — human action pending",
                shortFp);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static string GenerateBranchName(AggregatedError error)
    {
        // Create a safe, readable branch name
        var slug = error.Message.Length > 40
            ? error.Message[..40]
            : error.Message;

        slug = InvalidBranchCharsRegex().Replace(slug, "-").Trim('-').ToLowerInvariant();

        return $"fix/{error.Fingerprint[..8]}-{slug}";
    }

    private static string BuildPrDescription(
        AggregatedError error, WorkItemResult workItem, FixProposal fix)
    {
        return $"""
            ## 🤖 Auto-Generated Fix — Error Detection Agent
            
            **Related Work Item:** [Bug #{workItem.Id}]({workItem.Url})
            **Error Severity:** {error.Severity}
            **Occurrences:** {error.OccurrenceCount} {(error.IsRecurring ? "⚠️ RECURRING" : "")}
            **LLM Confidence:** {fix.Confidence:P0}
            
            ### Error Message
            ```
            {error.Message}
            ```
            
            ### Proposed Fix
            {fix.Summary}
            
            ### Affected Files
            {string.Join("\n", fix.AffectedFiles.Select(f => $"- `{f}`"))}
            
            ---
            *This pull request was created automatically by the Error Detection Agent.
            Please review the changes carefully before approving.*
            """;
    }

    [GeneratedRegex(@"[^a-zA-Z0-9\-]")]
    private static partial Regex InvalidBranchCharsRegex();
}

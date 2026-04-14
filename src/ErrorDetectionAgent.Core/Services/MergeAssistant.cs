using System.Text.Json;
using ErrorDetectionAgent.Core.Configuration;
using ErrorDetectionAgent.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErrorDetectionAgent.Core.Services;

/// <summary>
/// Polls the Azure DevOps pull request API for approval status and, once approved,
/// completes the merge automatically.
/// 
/// Workflow:
///   1. Periodically poll the PR status (configurable interval).
///   2. When the PR is approved by at least one reviewer, call the "complete" API.
///   3. If the configured timeout is reached without approval, log a warning and return false.
/// </summary>
public sealed class MergeAssistant : IMergeAssistant
{
    private readonly AgentSettings _settings;
    private readonly ILogger<MergeAssistant> _logger;
    private readonly HttpClient _httpClient;

    public MergeAssistant(
        IOptions<AgentSettings> settings,
        ILogger<MergeAssistant> logger,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settings.Value;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("AzureDevOps");
    }

    /// <inheritdoc/>
    public async Task<bool> WaitForApprovalAndMergeAsync(
        string pullRequestUrl,
        CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromMinutes(_settings.MergeTimeoutMinutes);
        var pollInterval = TimeSpan.FromSeconds(_settings.MergePollIntervalSeconds);
        var deadline = DateTime.UtcNow + timeout;

        // Set up auth
        var token = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($":{_settings.DevOpsPat}"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);

        _logger.LogInformation(
            "Monitoring PR for approval (timeout: {Timeout} min, poll: {Poll} sec)",
            _settings.MergeTimeoutMinutes, _settings.MergePollIntervalSeconds);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var (isApproved, prId) = await CheckApprovalStatusAsync(
                    pullRequestUrl, cancellationToken);

                if (isApproved && prId.HasValue)
                {
                    _logger.LogInformation("PR #{PrId} is approved — completing merge", prId);
                    return await CompleteMergeAsync(pullRequestUrl, prId.Value, cancellationToken);
                }

                _logger.LogDebug("PR not yet approved — next check in {Seconds}s",
                    pollInterval.TotalSeconds);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Error checking PR status — will retry");
            }

            await Task.Delay(pollInterval, cancellationToken);
        }

        _logger.LogWarning("Timed out waiting for PR approval after {Minutes} minutes",
            _settings.MergeTimeoutMinutes);
        return false;
    }

    // ── Private helpers ─────────────────────────────────────────────

    private async Task<(bool IsApproved, int? PrId)> CheckApprovalStatusAsync(
        string pullRequestUrl,
        CancellationToken cancellationToken)
    {
        // Append API version if not present
        var apiUrl = pullRequestUrl.Contains("api-version")
            ? pullRequestUrl
            : pullRequestUrl + (pullRequestUrl.Contains('?') ? "&" : "?") + "api-version=7.0";

        var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var prId = root.TryGetProperty("pullRequestId", out var idProp)
            ? idProp.GetInt32() : (int?)null;

        // Check reviewers — a vote of 10 means "Approved"
        if (root.TryGetProperty("reviewers", out var reviewers))
        {
            foreach (var reviewer in reviewers.EnumerateArray())
            {
                if (reviewer.TryGetProperty("vote", out var vote) && vote.GetInt32() == 10)
                {
                    return (true, prId);
                }
            }
        }

        return (false, prId);
    }

    private async Task<bool> CompleteMergeAsync(
        string pullRequestUrl,
        int prId,
        CancellationToken cancellationToken)
    {
        var apiUrl = pullRequestUrl.Contains("api-version")
            ? pullRequestUrl
            : pullRequestUrl + (pullRequestUrl.Contains('?') ? "&" : "?") + "api-version=7.0";

        var payload = new
        {
            status = "completed",
            completionOptions = new
            {
                mergeStrategy = "squash",
                deleteSourceBranch = true,
                mergeCommitMessage = $"Merged fix from Error Detection Agent (PR #{prId})"
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Patch, apiUrl)
        {
            Content = content
        };

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("PR #{PrId} merged successfully", prId);
            return true;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError(
            "Failed to complete merge for PR #{PrId}: {Status} — {Body}",
            prId, response.StatusCode, errorBody);
        return false;
    }
}

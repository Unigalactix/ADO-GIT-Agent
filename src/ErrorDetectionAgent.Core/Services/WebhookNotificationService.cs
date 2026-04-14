using System.Text;
using System.Text.Json;
using ErrorDetectionAgent.Core.Configuration;
using ErrorDetectionAgent.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErrorDetectionAgent.Core.Services;

/// <summary>
/// Sends notifications via an incoming webhook (Microsoft Teams or Slack).
/// 
/// The message includes:
///   • A link to the pull request for review.
///   • A summary of the proposed change.
///   • A link to the related Azure DevOps work item.
/// </summary>
public sealed class WebhookNotificationService : INotificationService
{
    private readonly AgentSettings _settings;
    private readonly ILogger<WebhookNotificationService> _logger;
    private readonly HttpClient _httpClient;

    public WebhookNotificationService(
        IOptions<AgentSettings> settings,
        ILogger<WebhookNotificationService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settings.Value;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("Notifications");
    }

    /// <inheritdoc/>
    public async Task NotifyForReviewAsync(
        string pullRequestUrl,
        string summary,
        string workItemUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.NotificationWebhookUrl))
        {
            _logger.LogWarning("Notification webhook URL is not configured — skipping alert");
            return;
        }

        _logger.LogInformation("Sending review notification for PR: {PrUrl}", pullRequestUrl);

        // Adaptive Card payload (works with Microsoft Teams incoming webhooks)
        var payload = new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = new object[]
                        {
                            new { type = "TextBlock", text = "🤖 Error Detection Agent — Review Needed", weight = "Bolder", size = "Medium" },
                            new { type = "TextBlock", text = summary, wrap = true },
                            new { type = "TextBlock", text = $"**Pull Request:** [{pullRequestUrl}]({pullRequestUrl})", wrap = true },
                            new { type = "TextBlock", text = $"**Work Item:** [{workItemUrl}]({workItemUrl})", wrap = true }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            _settings.NotificationWebhookUrl, content, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Notification sent successfully");
        }
        else
        {
            _logger.LogWarning(
                "Notification webhook returned {StatusCode}: {Body}",
                response.StatusCode,
                await response.Content.ReadAsStringAsync(cancellationToken));
        }
    }
}

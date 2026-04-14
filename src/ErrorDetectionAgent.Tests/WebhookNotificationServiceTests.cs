using ErrorDetectionAgent.Core.Configuration;
using ErrorDetectionAgent.Core.Services;
using ErrorDetectionAgent.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Net.Http.Headers;

namespace ErrorDetectionAgent.Tests;

/// <summary>
/// Tests for <see cref="WebhookNotificationService"/> using a mocked HttpClient
/// to verify the notification payload and behaviour without sending real HTTP requests.
/// </summary>
public sealed class WebhookNotificationServiceTests
{
    [Fact]
    public async Task NotifyForReviewAsync_SkipsWhenWebhookNotConfigured()
    {
        // Arrange — empty webhook URL
        var settings = Options.Create(new AgentSettings
        {
            NotificationWebhookUrl = ""
        });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("Notifications")).Returns(httpClient);

        var service = new WebhookNotificationService(
            settings,
            NullLogger<WebhookNotificationService>.Instance,
            factory.Object);

        // Act
        await service.NotifyForReviewAsync("https://pr", "summary", "https://wi");

        // Assert — no HTTP call was made
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task NotifyForReviewAsync_SendsPostToConfiguredWebhook()
    {
        // Arrange
        var settings = Options.Create(new AgentSettings
        {
            NotificationWebhookUrl = "https://webhook.example.com/test"
        });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "1");
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("Notifications")).Returns(httpClient);

        var service = new WebhookNotificationService(
            settings,
            NullLogger<WebhookNotificationService>.Instance,
            factory.Object);

        // Act
        await service.NotifyForReviewAsync(
            "https://dev.azure.com/pr/1",
            "Add null check",
            "https://dev.azure.com/wi/42");

        // Assert
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://webhook.example.com/test", handler.LastRequest.RequestUri!.ToString());

        var body = await handler.LastRequest.Content!.ReadAsStringAsync();
        Assert.Contains("AdaptiveCard", body);
        Assert.Contains("Add null check", body);
        Assert.Contains("https://dev.azure.com/pr/1", body);
    }

    /// <summary>
    /// Simple HttpMessageHandler that returns a fixed response and records requests.
    /// </summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public int CallCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody)
            });
        }
    }
}

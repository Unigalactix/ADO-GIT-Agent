using System.ClientModel;
using System.Text;
using Azure.AI.OpenAI;
using ErrorDetectionAgent.Core.Configuration;
using ErrorDetectionAgent.Core.Interfaces;
using ErrorDetectionAgent.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace ErrorDetectionAgent.Core.Services;

/// <summary>
/// Uses Azure OpenAI to analyse an error and propose a code fix.
/// 
/// The prompt instructs the model to:
///   1. Diagnose the probable root cause.
///   2. Suggest specific file-level changes.
///   3. Return a structured JSON response with the fix details.
/// </summary>
public sealed class LlmFixProposer : ILlmFixProposer
{
    private readonly AgentSettings _settings;
    private readonly ILogger<LlmFixProposer> _logger;

    public LlmFixProposer(
        IOptions<AgentSettings> settings,
        ILogger<LlmFixProposer> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<FixProposal> ProposeFixAsync(
        AggregatedError error,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Requesting LLM fix proposal for error: {Message}",
            error.Message.Length > 80 ? error.Message[..80] + "…" : error.Message);

        var client = new AzureOpenAIClient(
            new Uri(_settings.OpenAiEndpoint),
            new ApiKeyCredential(_settings.OpenAiApiKey));

        var chatClient = client.GetChatClient(_settings.OpenAiDeployment);

        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(error);

        var messages = new ChatMessage[]
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.2f,
            MaxOutputTokenCount = 4096
        };

        ChatCompletion completion = await chatClient.CompleteChatAsync(
            messages, options, cancellationToken);

        var responseText = completion.Content[0].Text;

        _logger.LogDebug("LLM response: {Response}", responseText);

        // Parse the structured response
        return ParseResponse(responseText, error);
    }

    // ── Prompt construction ──────────────────────────────────────────

    private static string BuildSystemPrompt() => """
        You are an expert software engineer specialising in diagnosing and fixing
        production errors in .NET / C# applications. When given an error message
        and stack trace, you:
        
        1. Identify the probable root cause.
        2. Propose a minimal, targeted code fix.
        3. Return your answer in the following JSON format (no markdown fences):
        
        {
          "summary": "One-line description of the fix",
          "confidence": 0.85,
          "affectedFiles": ["path/to/File.cs"],
          "suggestedDiff": "// Full corrected code for the affected region"
        }
        
        Guidelines:
        - Only propose changes you are confident about.
        - Keep fixes minimal — avoid large refactors.
        - If you cannot determine a fix, set confidence to 0.0 and explain in summary.
        """;

    private static string BuildUserPrompt(AggregatedError error)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Error Details");
        sb.AppendLine($"**Severity:** {error.Severity}");
        sb.AppendLine($"**Source:** {error.Source ?? "Unknown"}");
        sb.AppendLine($"**Occurrences:** {error.OccurrenceCount}");
        sb.AppendLine($"**Recurring:** {error.IsRecurring}");
        sb.AppendLine();
        sb.AppendLine("### Message");
        sb.AppendLine($"```\n{error.Message}\n```");

        if (!string.IsNullOrWhiteSpace(error.StackTrace))
        {
            sb.AppendLine("### Stack Trace");
            sb.AppendLine($"```\n{error.StackTrace}\n```");
        }

        sb.AppendLine();
        sb.AppendLine("Please diagnose the root cause and propose a fix in the JSON format described.");
        return sb.ToString();
    }

    // ── Response parsing ────────────────────────────────────────────

    private FixProposal ParseResponse(string response, AggregatedError error)
    {
        try
        {
            // Try to extract JSON from the response (the model may wrap it in markdown)
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonText = response[jsonStart..(jsonEnd + 1)];
                using var doc = System.Text.Json.JsonDocument.Parse(jsonText);
                var root = doc.RootElement;

                return new FixProposal
                {
                    Summary = root.TryGetProperty("summary", out var s)
                        ? s.GetString() ?? "No summary" : "No summary",
                    Confidence = root.TryGetProperty("confidence", out var c)
                        ? c.GetDouble() : 0.0,
                    AffectedFiles = root.TryGetProperty("affectedFiles", out var af)
                        ? af.EnumerateArray()
                              .Select(e => e.GetString() ?? "")
                              .Where(f => !string.IsNullOrEmpty(f))
                              .ToList()
                        : new List<string>(),
                    SuggestedDiff = root.TryGetProperty("suggestedDiff", out var sd)
                        ? sd.GetString() ?? "" : ""
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM JSON response — using raw text");
        }

        // Fallback: treat the whole response as the summary
        return new FixProposal
        {
            Summary = response.Length > 200 ? response[..200] + "…" : response,
            SuggestedDiff = response,
            Confidence = 0.0,
            AffectedFiles = new List<string>()
        };
    }
}

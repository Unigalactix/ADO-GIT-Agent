using ErrorDetectionAgent.Core.Interfaces;
using ErrorDetectionAgent.Core.Models;

namespace ErrorDetectionAgent.Tests.Fakes;

/// <summary>
/// In-memory fake that returns pre-configured fix proposals without
/// calling Azure OpenAI. Simulates the LLM response.
/// </summary>
public sealed class FakeLlmFixProposer : ILlmFixProposer
{
    private readonly double _confidence;
    private readonly List<AggregatedError> _requestedErrors = [];

    /// <summary>
    /// Creates a fake proposer that always returns fixes at the given confidence level.
    /// </summary>
    public FakeLlmFixProposer(double confidence = 0.85)
    {
        _confidence = confidence;
    }

    /// <summary>Errors that were passed to ProposeFixAsync.</summary>
    public IReadOnlyList<AggregatedError> RequestedErrors => _requestedErrors;

    public Task<FixProposal> ProposeFixAsync(
        AggregatedError error,
        CancellationToken cancellationToken = default)
    {
        _requestedErrors.Add(error);

        var fix = new FixProposal
        {
            Summary = $"Auto-fix for {error.Severity} in {error.Source ?? "unknown"}",
            SuggestedDiff = $"// Proposed fix for: {error.Message}",
            AffectedFiles = [$"src/Services/{error.Source ?? "Unknown"}.cs"],
            Confidence = _confidence
        };

        return Task.FromResult(fix);
    }
}

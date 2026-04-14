using ErrorDetectionAgent.Core.Models;

namespace ErrorDetectionAgent.Core.Interfaces;

/// <summary>
/// Uses a Large Language Model to propose code fixes for detected errors.
/// </summary>
public interface ILlmFixProposer
{
    /// <summary>
    /// Analyses the aggregated error and proposes a code fix.
    /// </summary>
    Task<FixProposal> ProposeFixAsync(
        AggregatedError error,
        CancellationToken cancellationToken = default);
}

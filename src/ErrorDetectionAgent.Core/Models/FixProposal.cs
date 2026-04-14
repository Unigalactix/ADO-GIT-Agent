namespace ErrorDetectionAgent.Core.Models;

/// <summary>
/// Encapsulates the LLM-generated fix proposal for a given error.
/// </summary>
public sealed class FixProposal
{
    /// <summary>Human-readable summary of what the fix changes.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>The suggested code change as a unified diff or code block.</summary>
    public string SuggestedDiff { get; set; } = string.Empty;

    /// <summary>File path(s) that should be modified.</summary>
    public List<string> AffectedFiles { get; set; } = new();

    /// <summary>Confidence score from the LLM (0.0 – 1.0).</summary>
    public double Confidence { get; set; }

    /// <summary>Name of the feature branch where the fix was committed.</summary>
    public string BranchName { get; set; } = string.Empty;
}

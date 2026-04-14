namespace ErrorDetectionAgent.Core.Configuration;

/// <summary>
/// Strongly-typed settings loaded from appsettings.json.
/// </summary>
public sealed class AgentSettings
{
    public const string SectionName = "ErrorDetectionAgent";

    // ── Azure SQL DB ──────────────────────────────────────────────
    /// <summary>Connection string for the Azure SQL log database.</summary>
    public string SqlConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// How far back (in hours) to look for errors each time the agent runs.
    /// </summary>
    public int LookbackHours { get; set; } = 24;

    // ── Azure DevOps ──────────────────────────────────────────────
    /// <summary>Azure DevOps organisation URL (e.g., https://dev.azure.com/myorg).</summary>
    public string DevOpsOrgUrl { get; set; } = string.Empty;

    /// <summary>Azure DevOps project name.</summary>
    public string DevOpsProject { get; set; } = string.Empty;

    /// <summary>Personal access token for Azure DevOps API calls.</summary>
    public string DevOpsPat { get; set; } = string.Empty;

    // ── Git / Repository ──────────────────────────────────────────
    /// <summary>Local path to the cloned Git repository.</summary>
    public string RepoLocalPath { get; set; } = string.Empty;

    /// <summary>Remote URL of the repository (HTTPS).</summary>
    public string RepoRemoteUrl { get; set; } = string.Empty;

    /// <summary>Git username for commit authoring.</summary>
    public string GitUserName { get; set; } = "ErrorDetectionAgent";

    /// <summary>Git email for commit authoring.</summary>
    public string GitUserEmail { get; set; } = "agent@errordetection.local";

    /// <summary>PAT or token used when pushing to the remote.</summary>
    public string GitPat { get; set; } = string.Empty;

    // ── LLM / Azure OpenAI ────────────────────────────────────────
    /// <summary>Azure OpenAI endpoint URL.</summary>
    public string OpenAiEndpoint { get; set; } = string.Empty;

    /// <summary>Azure OpenAI API key.</summary>
    public string OpenAiApiKey { get; set; } = string.Empty;

    /// <summary>Deployment name of the model (e.g., gpt-4).</summary>
    public string OpenAiDeployment { get; set; } = "gpt-4";

    // ── Notifications ─────────────────────────────────────────────
    /// <summary>Webhook URL for sending alerts (e.g., Teams or Slack).</summary>
    public string NotificationWebhookUrl { get; set; } = string.Empty;

    // ── Merge Assistant ───────────────────────────────────────────
    /// <summary>How often (in seconds) to poll for PR approval status.</summary>
    public int MergePollIntervalSeconds { get; set; } = 60;

    /// <summary>Maximum time (in minutes) to wait for human approval before timing out.</summary>
    public int MergeTimeoutMinutes { get; set; } = 1440; // 24 hours
}

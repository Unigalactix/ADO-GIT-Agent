using ErrorDetectionAgent.Core.Configuration;

namespace ErrorDetectionAgent.Tests;

/// <summary>
/// Tests for <see cref="AgentSettings"/> to verify default configuration values.
/// </summary>
public sealed class AgentSettingsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var settings = new AgentSettings();

        Assert.Equal(string.Empty, settings.SqlConnectionString);
        Assert.Equal(24, settings.LookbackHours);
        Assert.Equal(string.Empty, settings.DevOpsOrgUrl);
        Assert.Equal(string.Empty, settings.DevOpsProject);
        Assert.Equal(string.Empty, settings.DevOpsPat);
        Assert.Equal(string.Empty, settings.RepoLocalPath);
        Assert.Equal(string.Empty, settings.RepoRemoteUrl);
        Assert.Equal("ErrorDetectionAgent", settings.GitUserName);
        Assert.Equal("agent@errordetection.local", settings.GitUserEmail);
        Assert.Equal(string.Empty, settings.GitPat);
        Assert.Equal(string.Empty, settings.OpenAiEndpoint);
        Assert.Equal(string.Empty, settings.OpenAiApiKey);
        Assert.Equal("gpt-4", settings.OpenAiDeployment);
        Assert.Equal(string.Empty, settings.NotificationWebhookUrl);
        Assert.Equal(60, settings.MergePollIntervalSeconds);
        Assert.Equal(1440, settings.MergeTimeoutMinutes);
    }

    [Fact]
    public void SectionName_IsCorrect()
    {
        Assert.Equal("ErrorDetectionAgent", AgentSettings.SectionName);
    }

    [Fact]
    public void CanSetAllProperties()
    {
        var settings = new AgentSettings
        {
            SqlConnectionString = "Server=test;",
            LookbackHours = 48,
            DevOpsOrgUrl = "https://dev.azure.com/myorg",
            DevOpsProject = "MyProject",
            DevOpsPat = "my-pat",
            RepoLocalPath = "/tmp/repo",
            RepoRemoteUrl = "https://dev.azure.com/myorg/MyProject/_git/MyRepo",
            GitUserName = "TestBot",
            GitUserEmail = "bot@test.com",
            GitPat = "git-pat",
            OpenAiEndpoint = "https://myoai.openai.azure.com/",
            OpenAiApiKey = "oai-key",
            OpenAiDeployment = "gpt-4o",
            NotificationWebhookUrl = "https://webhook.example.com",
            MergePollIntervalSeconds = 30,
            MergeTimeoutMinutes = 60
        };

        Assert.Equal("Server=test;", settings.SqlConnectionString);
        Assert.Equal(48, settings.LookbackHours);
        Assert.Equal("https://dev.azure.com/myorg", settings.DevOpsOrgUrl);
        Assert.Equal("MyProject", settings.DevOpsProject);
        Assert.Equal("my-pat", settings.DevOpsPat);
        Assert.Equal("/tmp/repo", settings.RepoLocalPath);
        Assert.Equal("TestBot", settings.GitUserName);
        Assert.Equal("bot@test.com", settings.GitUserEmail);
        Assert.Equal("gpt-4o", settings.OpenAiDeployment);
        Assert.Equal(30, settings.MergePollIntervalSeconds);
        Assert.Equal(60, settings.MergeTimeoutMinutes);
    }
}

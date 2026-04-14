using ErrorDetectionAgent.Core.Configuration;
using ErrorDetectionAgent.Core.Interfaces;
using ErrorDetectionAgent.Core.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErrorDetectionAgent.Core.Services;

/// <summary>
/// Manages Git repository operations using LibGit2Sharp:
///   • Creates feature branches from main.
///   • Commits proposed code fixes.
///   • Creates pull requests via the Azure DevOps REST API.
/// 
/// NOTE: Pull request creation uses the Azure DevOps Git REST API because
/// LibGit2Sharp does not support server-side PR operations.
/// </summary>
public sealed class GitService : IGitService
{
    private readonly AgentSettings _settings;
    private readonly ILogger<GitService> _logger;
    private readonly HttpClient _httpClient;

    public GitService(
        IOptions<AgentSettings> settings,
        ILogger<GitService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settings.Value;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("AzureDevOps");
    }

    /// <inheritdoc/>
    public Task<string> CreateBranchAsync(
        string branchName,
        CancellationToken cancellationToken = default)
    {
        using var repo = new Repository(_settings.RepoLocalPath);

        // Ensure we are on the latest main
        var mainBranch = repo.Branches["main"]
            ?? repo.Branches["master"]
            ?? throw new InvalidOperationException(
                "Neither 'main' nor 'master' branch found in the repository.");

        Commands.Checkout(repo, mainBranch);

        // Pull latest from remote
        PullLatest(repo);

        // Create and checkout the new branch
        var newBranch = repo.CreateBranch(branchName, mainBranch.Tip);
        Commands.Checkout(repo, newBranch);

        _logger.LogInformation("Created and checked out branch '{Branch}'", branchName);
        return Task.FromResult(newBranch.CanonicalName);
    }

    /// <inheritdoc/>
    public Task CommitFixAsync(
        string branchName,
        FixProposal fix,
        CancellationToken cancellationToken = default)
    {
        using var repo = new Repository(_settings.RepoLocalPath);

        // Checkout the target branch
        var branch = repo.Branches[branchName]
            ?? throw new InvalidOperationException(
                $"Branch '{branchName}' does not exist.");
        Commands.Checkout(repo, branch);

        // Write proposed changes to the affected files
        foreach (var file in fix.AffectedFiles)
        {
            var fullPath = Path.Combine(_settings.RepoLocalPath, file);
            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            // The SuggestedDiff field contains the new file content produced by the LLM.
            File.WriteAllText(fullPath, fix.SuggestedDiff);
            Commands.Stage(repo, file);
        }

        // Commit
        var author = new Signature(
            _settings.GitUserName,
            _settings.GitUserEmail,
            DateTimeOffset.UtcNow);

        repo.Commit(
            $"fix: {fix.Summary}",
            author,
            author,
            new CommitOptions { AllowEmptyCommit = false });

        // Push to remote
        PushBranch(repo, branchName);

        _logger.LogInformation(
            "Committed and pushed fix to branch '{Branch}'", branchName);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<string> CreatePullRequestAsync(
        string branchName,
        string title,
        string description,
        CancellationToken cancellationToken = default)
    {
        // Use Azure DevOps REST API to create a pull request
        // POST https://dev.azure.com/{org}/{project}/_apis/git/repositories/{repo}/pullrequests?api-version=7.0
        var repoName = Path.GetFileName(_settings.RepoLocalPath.TrimEnd('/', '\\'));

        var apiUrl = $"{_settings.DevOpsOrgUrl.TrimEnd('/')}/{_settings.DevOpsProject}" +
                     $"/_apis/git/repositories/{repoName}/pullrequests?api-version=7.0";

        var payload = new
        {
            sourceRefName = $"refs/heads/{branchName}",
            targetRefName = "refs/heads/main",
            title,
            description
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Add Basic auth header
        var token = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($":{_settings.DevOpsPat}"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);

        var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
        var prUrl = doc.RootElement.TryGetProperty("url", out var urlProp)
            ? urlProp.GetString() ?? apiUrl
            : apiUrl;

        _logger.LogInformation("Created pull request: {Url}", prUrl);
        return prUrl;
    }

    // ── Private helpers ─────────────────────────────────────────────

    private void PullLatest(Repository repo)
    {
        var options = new PullOptions
        {
            FetchOptions = BuildFetchOptions()
        };

        var signature = new Signature(
            _settings.GitUserName,
            _settings.GitUserEmail,
            DateTimeOffset.UtcNow);

        Commands.Pull(repo, signature, options);
    }

    private void PushBranch(Repository repo, string branchName)
    {
        var remote = repo.Network.Remotes["origin"];
        var pushOptions = new PushOptions
        {
            CredentialsProvider = (_, _, _) =>
                new UsernamePasswordCredentials
                {
                    Username = _settings.GitUserName,
                    Password = _settings.GitPat
                }
        };

        repo.Network.Push(
            remote,
            $"refs/heads/{branchName}",
            pushOptions);
    }

    private FetchOptions BuildFetchOptions()
    {
        return new FetchOptions
        {
            CredentialsProvider = (_, _, _) =>
                new UsernamePasswordCredentials
                {
                    Username = _settings.GitUserName,
                    Password = _settings.GitPat
                }
        };
    }
}

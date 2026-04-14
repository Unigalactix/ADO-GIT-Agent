# Error Detection Agent

An end-to-end **AI-powered error detection and remediation workflow** built in C# (.NET 8). The agent automatically monitors your Azure SQL DB log database for production errors, triages them into Azure DevOps work items, proposes code fixes using an LLM, and guides a human-in-the-loop through the review-and-merge cycle.

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Pipeline Workflow](#pipeline-workflow)
4. [Project Structure](#project-structure)
5. [Prerequisites](#prerequisites)
6. [Configuration](#configuration)
7. [Database Setup](#database-setup)
8. [Building & Running](#building--running)
9. [Service Descriptions](#service-descriptions)
10. [Key Design Decisions](#key-design-decisions)
11. [Extending the Agent](#extending-the-agent)
12. [Troubleshooting](#troubleshooting)

---

## Overview

**Use Case:** UPG Error Detection Agent (Use Case 1)

The agent performs the following tasks automatically:

| Step | Action | Component |
|------|--------|-----------|
| 1 | **Read errors** from Azure SQL DB log database | `SqlErrorLogReader` |
| 2 | **Aggregate & deduplicate** — group identical errors and highlight recurring ones | `ErrorAggregator` |
| 3 | **Create Azure DevOps Bug** for each unique error group | `DevOpsWorkItemService` |
| 4 | **Create a feature branch** from main | `GitService` |
| 5 | **Propose a code fix** using Azure OpenAI (GPT-4) | `LlmFixProposer` |
| 6 | **Commit, push, and open a pull request** | `GitService` |
| 7 | **Alert a human** via webhook (Teams / Slack) | `WebhookNotificationService` |
| 8 | **Wait for approval** and **complete the merge** | `MergeAssistant` |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Error Detection Agent                        │
│                                                                 │
│  ┌──────────┐   ┌──────────────┐   ┌──────────────────────┐    │
│  │ Azure SQL│──▶│ Error        │──▶│ Error                │    │
│  │ DB Logs  │   │ Log Reader   │   │ Aggregator           │    │
│  └──────────┘   └──────────────┘   │ (dedup & recurring)  │    │
│                                    └──────────┬───────────┘    │
│                                               │                 │
│                                    ┌──────────▼───────────┐    │
│                                    │ For each error group  │    │
│                                    └──────────┬───────────┘    │
│                                               │                 │
│   ┌────────────────────┐    ┌─────────────────▼──────────┐     │
│   │ Azure DevOps       │◀──│ Create Bug Work Item        │     │
│   │ (Bug work item)    │    └─────────────────┬──────────┘     │
│   └────────────────────┘                      │                 │
│                                  ┌────────────▼─────────┐      │
│   ┌────────────────────┐        │ Create Feature Branch │      │
│   │ Git Repository     │◀───────│ from main             │      │
│   └────────────────────┘        └────────────┬──────────┘      │
│                                              │                  │
│   ┌────────────────────┐        ┌────────────▼──────────┐      │
│   │ Azure OpenAI       │◀───────│ LLM Fix Proposer      │      │
│   │ (GPT-4)            │        │ (analyse & fix)       │      │
│   └────────────────────┘        └────────────┬──────────┘      │
│                                              │                  │
│                                 ┌────────────▼──────────┐      │
│                                 │ Commit + Push + PR    │      │
│                                 └────────────┬──────────┘      │
│                                              │                  │
│   ┌────────────────────┐        ┌────────────▼──────────┐      │
│   │ Teams / Slack      │◀───────│ Notify Human          │      │
│   │ Webhook            │        └────────────┬──────────┘      │
│   └────────────────────┘                     │                  │
│                                 ┌────────────▼──────────┐      │
│                                 │ Wait for Approval     │      │
│                                 │ & Complete Merge      │      │
│                                 └───────────────────────┘      │
└─────────────────────────────────────────────────────────────────┘
```

---

## Pipeline Workflow

### Step 1 — Read Errors from Azure SQL DB

The `SqlErrorLogReader` connects to the configured Azure SQL Database and queries the `dbo.ApplicationLog` table for entries with severity `Error`, `Critical`, or `Exception` within a configurable lookback window (default: 24 hours).

**Key details:**
- Uses **Dapper** for lightweight, performant data access.
- Connection string supports Azure AD authentication or SQL auth.
- The query filters by severity and time range, ordered by timestamp descending.

### Step 2 — Aggregate Errors & Highlight Recurring Ones

The `ErrorAggregator` groups raw error entries using a **fingerprinting strategy**:

1. **Normalise** the error message by stripping GUIDs, numeric literals, and excess whitespace.
2. **Hash** the normalised message + source using SHA-256 to produce a stable fingerprint.
3. **Group** entries by fingerprint and count occurrences.

Errors with `OccurrenceCount > 1` are flagged as **recurring** and receive special attention:
- They are logged with a warning (`⚠ RECURRING ERROR`).
- Their Azure DevOps work items receive an extra `Recurring` tag.
- They appear at the top of the aggregated list (sorted by count descending).

### Step 3a — Create Azure DevOps Work Item

The `DevOpsWorkItemService` creates a **Bug** work item for each aggregated error group:

- **Title** includes severity, a truncated message, and a `[RECURRING x{N}]` prefix when applicable.
- **Repro Steps** contain the full error details (message, stack trace, occurrence count, timestamps).
- **Tags** include `AutoDetected` (always) and `Recurring` (when applicable).
- Uses the `Microsoft.TeamFoundationServer.Client` SDK with PAT-based authentication.

### Step 3b — Fork / Branch from Main

The `GitService` uses **LibGit2Sharp** to:

1. Check out the `main` (or `master`) branch.
2. Pull the latest changes from the remote.
3. Create a new feature branch named `fix/{fingerprint}-{slug}`.
4. Check out the new branch.

### Step 3c — Propose a Fix via LLM

The `LlmFixProposer` sends the error details to **Azure OpenAI** (GPT-4) with a structured prompt:

- A **system prompt** instructs the model to diagnose the root cause and return a fix in JSON format.
- A **user prompt** includes the error severity, source, message, stack trace, and occurrence count.
- The response is parsed to extract `summary`, `confidence`, `affectedFiles`, and `suggestedDiff`.
- If confidence is below 10%, the fix is skipped (to avoid committing unreliable changes).

### Step 3d — Commit, Push, and Create Pull Request

The `GitService`:

1. Writes the LLM-suggested code to the affected file(s) on the feature branch.
2. Stages and commits the changes with a descriptive commit message.
3. Pushes the branch to the remote using PAT-based credentials.
4. Creates a **pull request** via the Azure DevOps REST API (since LibGit2Sharp doesn't support server-side PR operations).

The PR description includes:
- A link to the related Bug work item.
- Error details and occurrence count.
- The LLM confidence score.
- A list of affected files.

### Step 3e — Alert Human for Review

The `WebhookNotificationService` posts an **Adaptive Card** message to a configured webhook (Microsoft Teams or Slack):

- Includes the PR URL, a summary of the proposed fix, and a link to the work item.
- If no webhook is configured, the notification step is skipped gracefully.

### Step 3f — Assist Merge (Human-in-the-Loop)

The `MergeAssistant`:

1. **Polls** the PR status at a configurable interval (default: 60 seconds).
2. Checks if any reviewer has approved the PR (vote = 10 in Azure DevOps).
3. Once approved, **completes the merge** using a squash strategy and deletes the source branch.
4. Times out after the configured maximum (default: 24 hours) if no approval is received.

---

## Project Structure

```
ADO-GIT-Agent/
├── .gitignore
├── README.md
├── sql/
│   └── setup-log-table.sql          # SQL script to create the log table + sample data
└── src/
    ├── ErrorDetectionAgent.slnx      # Solution file
    │
    ├── ErrorDetectionAgent.App/      # Console application (entry point)
    │   ├── Program.cs                # Host builder, DI setup, hosted service
    │   ├── appsettings.json          # Configuration (connection strings, tokens, etc.)
    │   └── ErrorDetectionAgent.App.csproj
    │
    └── ErrorDetectionAgent.Core/     # Class library (business logic)
        ├── Configuration/
        │   └── AgentSettings.cs      # Strongly-typed settings POCO
        ├── Interfaces/
        │   ├── IErrorLogReader.cs
        │   ├── IErrorAggregator.cs
        │   ├── IDevOpsWorkItemService.cs
        │   ├── IGitService.cs
        │   ├── ILlmFixProposer.cs
        │   ├── INotificationService.cs
        │   └── IMergeAssistant.cs
        ├── Models/
        │   ├── ErrorLogEntry.cs      # Single error log record
        │   ├── AggregatedError.cs    # Grouped/deduplicated error
        │   ├── WorkItemResult.cs     # ADO work item creation result
        │   └── FixProposal.cs        # LLM fix proposal
        └── Services/
            ├── SqlErrorLogReader.cs          # Step 1: Read from Azure SQL
            ├── ErrorAggregator.cs            # Step 2: Aggregate & dedup
            ├── DevOpsWorkItemService.cs      # Step 3a: Create ADO Bug
            ├── GitService.cs                 # Steps 3b/3d: Branch + commit + PR
            ├── LlmFixProposer.cs             # Step 3c: LLM fix proposal
            ├── WebhookNotificationService.cs # Step 3e: Alert human
            ├── MergeAssistant.cs             # Step 3f: Poll approval + merge
            └── ErrorDetectionOrchestrator.cs # End-to-end pipeline orchestrator
```

---

## Prerequisites

| Requirement | Details |
|------------|---------|
| **.NET 8 SDK** | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Azure SQL Database** | With the `dbo.ApplicationLog` table (see [Database Setup](#database-setup)) |
| **Azure DevOps** | Organisation with a project. A [Personal Access Token (PAT)](https://learn.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate) with **Work Items (Read & Write)** and **Code (Read & Write)** scopes |
| **Azure OpenAI** | An [Azure OpenAI resource](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/create-resource) with a GPT-4 deployment |
| **Git repository** | A cloned repository that the agent can branch and push to |
| **Webhook (optional)** | A Microsoft Teams or Slack incoming webhook URL for notifications |

---

## Configuration

All settings are stored in `src/ErrorDetectionAgent.App/appsettings.json` under the `ErrorDetectionAgent` section. They can also be overridden via environment variables prefixed with `EDA_` (double underscores for nesting).

### Settings Reference

| Setting | Description | Default |
|---------|-------------|---------|
| `SqlConnectionString` | Azure SQL DB connection string | *(required)* |
| `LookbackHours` | How many hours back to scan for errors | `24` |
| `DevOpsOrgUrl` | Azure DevOps organisation URL | *(required)* |
| `DevOpsProject` | Azure DevOps project name | *(required)* |
| `DevOpsPat` | Personal Access Token for Azure DevOps | *(required)* |
| `RepoLocalPath` | Local file path to the cloned repository | *(required)* |
| `RepoRemoteUrl` | HTTPS URL of the remote repository | *(required)* |
| `GitUserName` | Git commit author name | `ErrorDetectionAgent` |
| `GitUserEmail` | Git commit author email | `agent@errordetection.local` |
| `GitPat` | PAT for Git push authentication | *(required)* |
| `OpenAiEndpoint` | Azure OpenAI endpoint URL | *(required)* |
| `OpenAiApiKey` | Azure OpenAI API key | *(required)* |
| `OpenAiDeployment` | Model deployment name | `gpt-4` |
| `NotificationWebhookUrl` | Teams/Slack webhook URL | *(optional)* |
| `MergePollIntervalSeconds` | Seconds between PR approval checks | `60` |
| `MergeTimeoutMinutes` | Max minutes to wait for approval | `1440` (24h) |

### Environment Variable Overrides

```bash
# Example: override the SQL connection string via environment variable
export EDA_ErrorDetectionAgent__SqlConnectionString="Server=tcp:myserver.database.windows.net..."
```

> **⚠ Security Note:** Never commit real credentials to source control. Use environment variables, Azure Key Vault, or a secrets manager in production.

---

## Database Setup

1. Connect to your Azure SQL Database using SSMS, Azure Data Studio, or the Azure Portal query editor.

2. Run the SQL script:

   ```bash
   # The script is located at:
   sql/setup-log-table.sql
   ```

   This creates the `dbo.ApplicationLog` table with an optimised index and optionally inserts sample error data for testing.

3. Configure your application's logging framework (e.g., Serilog SQL Server sink, NLog database target) to write error-level entries to this table.

---

## Building & Running

### Build

```bash
cd src
dotnet build ErrorDetectionAgent.slnx
```

### Run

```bash
cd src/ErrorDetectionAgent.App
dotnet run
```

The agent will:
1. Read the configuration from `appsettings.json`.
2. Execute the full pipeline once.
3. Shut down after completion.

### Run as a Scheduled Task

For continuous monitoring, schedule the agent to run periodically:

- **Linux (cron):**
  ```cron
  0 * * * *  cd /opt/error-agent/src/ErrorDetectionAgent.App && dotnet run >> /var/log/error-agent.log 2>&1
  ```

- **Windows (Task Scheduler):** Create a task that runs `dotnet run` from the App directory.

- **Azure Container Instance:** Package as a Docker container and run on a schedule with Azure Logic Apps or a Timer Trigger Azure Function.

### Docker (Optional)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/ErrorDetectionAgent.App/ErrorDetectionAgent.App.csproj \
    -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "ErrorDetectionAgent.App.dll"]
```

---

## Service Descriptions

### `SqlErrorLogReader`
- **Purpose:** Reads error/exception entries from Azure SQL DB.
- **Technology:** `Microsoft.Data.SqlClient` + `Dapper`.
- **Query:** Filters by severity (`Error`, `Critical`, `Exception`) and a time window.

### `ErrorAggregator`
- **Purpose:** Groups raw errors by a normalised fingerprint; flags recurring errors.
- **Strategy:** Strips GUIDs and numeric literals → SHA-256 hash → group by hash.
- **Output:** `AggregatedError` objects sorted by occurrence count (descending).

### `DevOpsWorkItemService`
- **Purpose:** Creates Bug work items in Azure DevOps.
- **SDK:** `Microsoft.TeamFoundationServer.Client`.
- **Tags:** `AutoDetected` (always), `Recurring` (when count > 1).

### `GitService`
- **Purpose:** Manages branching, committing, pushing, and PR creation.
- **Technology:** `LibGit2Sharp` for local Git operations; Azure DevOps REST API for pull requests.
- **Branch naming:** `fix/{fingerprint-prefix}-{message-slug}`.

### `LlmFixProposer`
- **Purpose:** Uses Azure OpenAI to diagnose errors and propose code fixes.
- **SDK:** `Azure.AI.OpenAI`.
- **Prompt:** Structured system + user prompts; expects JSON response with `summary`, `confidence`, `affectedFiles`, `suggestedDiff`.
- **Safety:** Skips commit if confidence < 10%.

### `WebhookNotificationService`
- **Purpose:** Alerts humans via Teams/Slack incoming webhooks.
- **Format:** Microsoft Adaptive Card with PR link, summary, and work item link.

### `MergeAssistant`
- **Purpose:** Polls PR status and completes the merge once a human approves.
- **Strategy:** Squash merge with source branch deletion.
- **Timeout:** Configurable (default 24 hours).

### `ErrorDetectionOrchestrator`
- **Purpose:** End-to-end pipeline orchestrator that chains all services together.
- **Error handling:** Each error group is processed independently; failures are logged but don't halt the pipeline.

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Dependency Injection** | All services are registered via `Microsoft.Extensions.DependencyInjection`, making them testable and swappable. |
| **Interface-driven design** | Every service has an interface, enabling mock-based unit testing and alternative implementations. |
| **.NET Generic Host** | Provides structured configuration, logging, and graceful shutdown out of the box. |
| **Dapper over EF Core** | The agent only reads log data — Dapper's lightweight, low-overhead approach is a better fit than a full ORM. |
| **LibGit2Sharp** | Provides native Git operations without requiring a Git CLI installation. |
| **Azure DevOps REST API for PRs** | LibGit2Sharp doesn't support server-side operations like pull request creation. |
| **SHA-256 fingerprinting** | Deterministic, collision-resistant grouping of similar errors that differ only in runtime values. |
| **Confidence threshold** | Prevents the agent from committing low-confidence LLM suggestions that could introduce new bugs. |
| **Squash merge** | Keeps the main branch history clean with a single commit per fix. |
| **Configurable polling** | The merge assistant uses configurable intervals and timeouts to balance responsiveness against API rate limits. |

---

## Extending the Agent

### Custom Notification Channels

Implement `INotificationService` for email, PagerDuty, or other alerting platforms:

```csharp
public class EmailNotificationService : INotificationService
{
    public Task NotifyForReviewAsync(string prUrl, string summary, string wiUrl, CancellationToken ct)
    {
        // Send email via SMTP / SendGrid / etc.
    }
}
```

Register your implementation in `Program.cs`:
```csharp
services.AddSingleton<INotificationService, EmailNotificationService>();
```

### Different LLM Providers

Implement `ILlmFixProposer` for OpenAI (non-Azure), Anthropic Claude, or a local model:

```csharp
public class AnthropicFixProposer : ILlmFixProposer { ... }
```

### Continuous / Looping Mode

Wrap the orchestrator call in a loop inside `AgentHostedService.ExecuteAsync`:

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    await _orchestrator.RunAsync(stoppingToken);
    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
}
```

### GitHub Instead of Azure DevOps

Implement `IGitService` and `IDevOpsWorkItemService` using the GitHub REST API or Octokit SDK.

---

## Troubleshooting

| Symptom | Possible Cause | Solution |
|---------|---------------|----------|
| `SqlException: Login failed` | Incorrect connection string or credentials | Verify `SqlConnectionString` in appsettings; ensure the SQL server firewall allows your IP |
| `VssUnauthorizedException` | Invalid or expired Azure DevOps PAT | Generate a new PAT with **Work Items** and **Code** scopes |
| `LibGit2SharpException: too many redirects` | Incorrect remote URL or credentials | Verify `RepoRemoteUrl` and `GitPat` |
| `Azure.RequestFailedException: 401` | Invalid Azure OpenAI key or endpoint | Check `OpenAiEndpoint` and `OpenAiApiKey`; ensure the deployment exists |
| No notification received | Missing or invalid webhook URL | Verify `NotificationWebhookUrl` in appsettings |
| Merge times out | No reviewer approved the PR within the timeout | Increase `MergeTimeoutMinutes` or manually approve the PR |
| `LLM confidence too low` | The model couldn't confidently diagnose the error | Review the error manually; consider providing more context in the prompt |

---

## License

This project is provided as-is for the UPG Error Detection Agent use case. Modify and extend as needed for your environment.

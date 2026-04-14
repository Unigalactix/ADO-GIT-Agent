# Error Detection Agent — Setup Guide

This guide walks you through configuring and running the Error Detection Agent from scratch.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Repository Structure](#repository-structure)
3. [Database Setup](#database-setup)
4. [Azure DevOps Configuration](#azure-devops-configuration)
5. [Azure OpenAI Configuration](#azure-openai-configuration)
6. [Application Configuration](#application-configuration)
7. [Building the Application](#building-the-application)
8. [Running the Agent](#running-the-agent)
9. [Running the Tests](#running-the-tests)
10. [Notifications Setup](#notifications-setup)
11. [Troubleshooting](#troubleshooting)

---

## Prerequisites

| Requirement | Minimum Version | Purpose |
|---|---|---|
| **.NET SDK** | 8.0+ | Build and run the application |
| **Azure SQL Database** | Any tier | Stores application error logs |
| **Azure DevOps Organization** | Any plan | Bug work-item creation and Git PR management |
| **Azure OpenAI Service** | GPT-4 deployment | LLM-based code fix proposals |
| **Microsoft Teams / Slack** (optional) | N/A | Webhook notifications for human review |

---

## Repository Structure

```
ADO-GIT-Agent/
├── README.md                       # Project overview and architecture
├── SETUP_GUIDE.md                  # ← This file
├── TEST_REPORT.md                  # Test results and coverage summary
├── sql/
│   └── setup-log-table.sql         # SQL script to create the log table
├── src/
│   ├── ErrorDetectionAgent.slnx    # Solution file
│   ├── ErrorDetectionAgent.App/    # Console application entry point
│   │   ├── Program.cs              # Host builder + DI setup
│   │   └── appsettings.json        # Configuration (edit this)
│   ├── ErrorDetectionAgent.Core/   # Core library
│   │   ├── Configuration/          # AgentSettings POCO
│   │   ├── Interfaces/             # Service contracts
│   │   ├── Models/                 # Data models
│   │   └── Services/               # Service implementations
│   └── ErrorDetectionAgent.Tests/  # Unit and integration tests
│       ├── Fakes/                  # In-memory fake implementations
│       ├── Helpers/                # Shared test data
│       └── *.cs                    # Test classes
```

---

## Database Setup

### 1. Create the Log Table

Run the SQL script against your Azure SQL Database:

```bash
# Using sqlcmd
sqlcmd -S tcp:<your-server>.database.windows.net,1433 \
       -d <your-database> \
       -U <username> \
       -P <password> \
       -i sql/setup-log-table.sql
```

Or open `sql/setup-log-table.sql` in Azure Data Studio / SSMS and execute it.

### 2. Table Schema

The script creates:

```sql
CREATE TABLE dbo.ApplicationLog (
    Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
    [Timestamp] DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
    Severity    NVARCHAR(50)        NOT NULL,  -- 'Error', 'Critical', 'Exception'
    [Message]   NVARCHAR(MAX)       NOT NULL,
    StackTrace  NVARCHAR(MAX)       NULL,
    [Source]    NVARCHAR(200)       NULL,
    ErrorCode   NVARCHAR(100)       NULL
);
```

### 3. Configure Your Application Logger

Point your application's logging framework (Serilog, NLog, etc.) to write error-level entries to this table. The agent reads rows where `Severity IN ('Error', 'Critical', 'Exception')`.

### 4. (Optional) Insert Sample Data

The SQL script includes sample data for testing. You can remove those `INSERT` statements in production.

---

## Azure DevOps Configuration

### 1. Create a Personal Access Token (PAT)

1. Go to **Azure DevOps** → **User Settings** → **Personal Access Tokens**
2. Create a new token with these scopes:
   - **Work Items**: Read & Write (for bug creation)
   - **Code**: Read & Write (for branch creation, push, and PR)
3. Copy the token — you'll need it for `DevOpsPat` and `GitPat` in `appsettings.json`.

### 2. Ensure a Git Repository Exists

The agent commits fixes and creates PRs in an Azure DevOps Git repository. Make sure:
- The repo has a `main` (or `master`) branch.
- The PAT user has contributor-level access.

---

## Azure OpenAI Configuration

### 1. Deploy a GPT-4 Model

1. In the [Azure Portal](https://portal.azure.com), create or use an existing **Azure OpenAI** resource.
2. Deploy a model (e.g., `gpt-4`, `gpt-4o`).
3. Note the:
   - **Endpoint** (e.g., `https://myresource.openai.azure.com/`)
   - **API Key** (from Keys and Endpoint blade)
   - **Deployment Name** (the name you gave the deployment)

---

## Application Configuration

Edit `src/ErrorDetectionAgent.App/appsettings.json`:

```json
{
  "ErrorDetectionAgent": {
    "SqlConnectionString": "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<db>;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
    "LookbackHours": 24,

    "DevOpsOrgUrl": "https://dev.azure.com/<your-organisation>",
    "DevOpsProject": "<your-project>",
    "DevOpsPat": "<your-personal-access-token>",

    "RepoLocalPath": "/path/to/local/repo",
    "RepoRemoteUrl": "https://dev.azure.com/<org>/<project>/_git/<repo>",
    "GitUserName": "ErrorDetectionAgent",
    "GitUserEmail": "agent@errordetection.local",
    "GitPat": "<your-git-pat>",

    "OpenAiEndpoint": "https://<your-resource>.openai.azure.com/",
    "OpenAiApiKey": "<your-openai-api-key>",
    "OpenAiDeployment": "gpt-4",

    "NotificationWebhookUrl": "https://outlook.office.com/webhook/<your-webhook>",

    "MergePollIntervalSeconds": 60,
    "MergeTimeoutMinutes": 1440
  }
}
```

### Using Environment Variables

All settings can also be overridden via environment variables prefixed with `EDA_`. The configuration system reads them after `appsettings.json`:

```bash
export EDA_ErrorDetectionAgent__SqlConnectionString="Server=tcp:..."
export EDA_ErrorDetectionAgent__DevOpsPat="my-pat-token"
export EDA_ErrorDetectionAgent__OpenAiApiKey="my-openai-key"
```

> **Security note:** Never commit real secrets to source control. Use environment variables, Azure Key Vault, or a secrets manager in production.

### Configuration Reference

| Setting | Type | Default | Description |
|---|---|---|---|
| `SqlConnectionString` | string | `""` | ADO.NET connection string for the log database |
| `LookbackHours` | int | `24` | How far back to scan for errors |
| `DevOpsOrgUrl` | string | `""` | Azure DevOps org URL |
| `DevOpsProject` | string | `""` | Azure DevOps project name |
| `DevOpsPat` | string | `""` | PAT for Azure DevOps API |
| `RepoLocalPath` | string | `""` | Path to local Git clone |
| `RepoRemoteUrl` | string | `""` | Remote URL of the repo |
| `GitUserName` | string | `"ErrorDetectionAgent"` | Git commit author name |
| `GitUserEmail` | string | `"agent@errordetection.local"` | Git commit author email |
| `GitPat` | string | `""` | PAT for Git push operations |
| `OpenAiEndpoint` | string | `""` | Azure OpenAI endpoint |
| `OpenAiApiKey` | string | `""` | Azure OpenAI API key |
| `OpenAiDeployment` | string | `"gpt-4"` | Model deployment name |
| `NotificationWebhookUrl` | string | `""` | Teams/Slack webhook URL |
| `MergePollIntervalSeconds` | int | `60` | How often to poll for PR approval |
| `MergeTimeoutMinutes` | int | `1440` | Max wait time for approval (24h) |

---

## Building the Application

```bash
cd src

# Restore NuGet packages
dotnet restore ErrorDetectionAgent.slnx

# Build the solution
dotnet build ErrorDetectionAgent.slnx --configuration Release
```

---

## Running the Agent

```bash
cd src/ErrorDetectionAgent.App

# Run in one-shot mode (processes current errors, then exits)
dotnet run --configuration Release
```

The agent will:
1. Query the SQL database for errors within the lookback window.
2. Aggregate and deduplicate errors.
3. For each unique error group:
   - Create an Azure DevOps Bug work item.
   - Create a feature branch from `main`.
   - Ask the LLM to propose a fix.
   - Commit the fix and open a pull request.
   - Send a webhook notification.
   - Poll for PR approval and auto-merge when approved.

### Running on a Schedule

Use a cron job, Azure Container Instances, or a scheduled pipeline to run the agent periodically:

```bash
# Example crontab entry — run every 6 hours
0 */6 * * * cd /opt/agent && dotnet ErrorDetectionAgent.App.dll
```

---

## Running the Tests

The test project (`ErrorDetectionAgent.Tests`) includes **37 tests** covering:
- Error aggregation logic
- Full orchestrator pipeline with in-memory fakes
- Webhook notification behaviour
- Data model validation
- Configuration defaults

```bash
cd src

# Run all tests
dotnet test ErrorDetectionAgent.Tests/ErrorDetectionAgent.Tests.csproj --verbosity normal

# Run with detailed output
dotnet test ErrorDetectionAgent.Tests/ErrorDetectionAgent.Tests.csproj --logger "console;verbosity=detailed"
```

No real Azure connections are needed — all external dependencies are replaced with in-memory fakes.

---

## Notifications Setup

### Microsoft Teams

1. In your Teams channel, add an **Incoming Webhook** connector.
2. Copy the webhook URL.
3. Set `NotificationWebhookUrl` in `appsettings.json`.

The agent sends [Adaptive Card](https://adaptivecards.io/) messages with:
- The proposed fix summary
- A link to the pull request
- A link to the related work item

### Slack

For Slack, you can use a Slack Incoming Webhook. The payload format is compatible. Alternatively, modify `WebhookNotificationService` to use Slack's block format.

---

## Troubleshooting

| Issue | Solution |
|---|---|
| `SqlException: Login failed` | Verify your `SqlConnectionString`. Ensure the server firewall allows your IP. |
| `VssUnauthorizedException` | Check your `DevOpsPat` — it may have expired or lack required scopes. |
| `Repository not found` | Verify `RepoLocalPath` points to a valid Git clone and `RepoRemoteUrl` is correct. |
| `OpenAI 401 Unauthorized` | Verify `OpenAiEndpoint`, `OpenAiApiKey`, and `OpenAiDeployment` are correct. |
| `Notification webhook returned 4xx` | Verify the webhook URL is active and hasn't been rotated. |
| `Neither 'main' nor 'master' branch found` | Ensure the local repo has a `main` or `master` branch checked out. |
| Agent exits without processing | Check the `LookbackHours` setting — the agent only scans errors within this window. |

---

*For architecture details and service descriptions, see the main [README.md](README.md).*

using ErrorDetectionAgent.Core.Configuration;
using ErrorDetectionAgent.Core.Interfaces;
using ErrorDetectionAgent.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ══════════════════════════════════════════════════════════════════════
//  Error Detection Agent — Console Application Entry Point
//
//  This application orchestrates the full error-detection-and-fix
//  pipeline as a hosted service. It can be run:
//    • Manually from the command line (one-shot mode)
//    • As a scheduled task / cron job
//    • Inside a container (Azure Container Instances, AKS, etc.)
// ══════════════════════════════════════════════════════════════════════

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                optional: true, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "EDA_");
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .ConfigureServices((context, services) =>
    {
        // Bind configuration
        services.Configure<AgentSettings>(
            context.Configuration.GetSection(AgentSettings.SectionName));

        // Register HTTP clients
        services.AddHttpClient("AzureDevOps");
        services.AddHttpClient("Notifications");

        // Register services
        services.AddSingleton<IErrorLogReader, SqlErrorLogReader>();
        services.AddSingleton<IErrorAggregator, ErrorAggregator>();
        services.AddSingleton<IDevOpsWorkItemService, DevOpsWorkItemService>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<ILlmFixProposer, LlmFixProposer>();
        services.AddSingleton<INotificationService, WebhookNotificationService>();
        services.AddSingleton<IMergeAssistant, MergeAssistant>();
        services.AddSingleton<ErrorDetectionOrchestrator>();

        // Register the hosted service that drives the pipeline
        services.AddHostedService<AgentHostedService>();
    })
    .Build();

await host.RunAsync();

// ══════════════════════════════════════════════════════════════════════
//  Hosted Service — runs the orchestrator once, then shuts down.
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// A background service that executes the Error Detection Agent pipeline
/// once and then signals the application to stop.
/// </summary>
internal sealed class AgentHostedService : BackgroundService
{
    private readonly ErrorDetectionOrchestrator _orchestrator;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<AgentHostedService> _logger;

    public AgentHostedService(
        ErrorDetectionOrchestrator orchestrator,
        IHostApplicationLifetime lifetime,
        ILogger<AgentHostedService> logger)
    {
        _orchestrator = orchestrator;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Agent starting...");
            await _orchestrator.RunAsync(stoppingToken);
            _logger.LogInformation("Agent completed successfully.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Agent was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Agent encountered a fatal error.");
        }
        finally
        {
            // Stop the host after the pipeline completes
            _lifetime.StopApplication();
        }
    }
}

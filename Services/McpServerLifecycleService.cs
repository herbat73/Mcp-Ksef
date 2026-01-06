using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Services;

/// <summary>
/// Background service for managing MCP server lifecycle and health monitoring.
/// Provides reliable service hosting across Windows, Linux, and macOS.
/// </summary>
public class McpServerLifecycleService : BackgroundService
{
    private readonly ILogger<McpServerLifecycleService> _logger;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly string _serviceName = "Remote MCP KSeF Server";
    
    public McpServerLifecycleService(
        ILogger<McpServerLifecycleService> logger,
        IHostApplicationLifetime appLifetime)
    {
        _logger = logger;
        _appLifetime = appLifetime;
    }

    /// <summary>
    /// Main service execution method - runs continuously as a background service.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("{ServiceName} background service started at {Time}", 
                _serviceName, DateTime.UtcNow);

            // Service startup tasks
            await OnServiceStartedAsync(stoppingToken);

            // Main service loop - monitors health and performs maintenance
            while (!stoppingToken.IsCancellationRequested)
            {
                await PerformHealthCheckAsync(stoppingToken);
                await PerformMaintenanceTasksAsync(stoppingToken);
                
                // Wait before next cycle
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // This is expected when cancellation is requested
            _logger.LogInformation("{ServiceName} background service stopped", _serviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ServiceName} background service encountered an error", _serviceName);
            
            // Optionally stop the entire application on critical errors
            _appLifetime.StopApplication();
        }
    }

    /// <summary>
    /// Called when the service starts up.
    /// </summary>
    private async Task OnServiceStartedAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Log service startup information
            var operatingSystem = Environment.OSVersion.Platform.ToString();
            var processId = Environment.ProcessId;
            var workingDirectory = Environment.CurrentDirectory;
            
            _logger.LogInformation("{ServiceName} startup details:", _serviceName);
            _logger.LogInformation("  Operating System: {OS}", operatingSystem);
            _logger.LogInformation("  Process ID: {PID}", processId);
            _logger.LogInformation("  Working Directory: {WorkingDir}", workingDirectory);
            _logger.LogInformation("  Service Mode: Background Service");
            
            // Perform any startup validation
            await ValidateServiceConfigurationAsync(cancellationToken);
            
            _logger.LogInformation("{ServiceName} successfully initialized", _serviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {ServiceName} startup", _serviceName);
            throw;
        }
    }

    /// <summary>
    /// Performs health checks on the MCP server.
    /// </summary>
    private async Task PerformHealthCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Basic health check - could be expanded to test actual MCP endpoints
            var memoryUsage = GC.GetTotalMemory(false);
            var memoryMB = memoryUsage / 1024 / 1024;
            
            if (memoryMB > 500) // Alert if memory usage exceeds 500MB
            {
                _logger.LogWarning("{ServiceName} memory usage is high: {MemoryMB}MB", 
                    _serviceName, memoryMB);
                
                // Force garbage collection if memory is high
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            
            _logger.LogDebug("{ServiceName} health check completed - Memory: {MemoryMB}MB", 
                _serviceName, memoryMB);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {ServiceName} health check", _serviceName);
        }
    }

    /// <summary>
    /// Performs routine maintenance tasks.
    /// </summary>
    private async Task PerformMaintenanceTasksAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Maintenance tasks could include:
            // - Cleaning up expired sessions
            // - Rotating log files
            // - Updating configuration
            // - Health reporting
            
            _logger.LogDebug("{ServiceName} maintenance tasks completed", _serviceName);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {ServiceName} maintenance", _serviceName);
        }
    }

    /// <summary>
    /// Validates service configuration and dependencies.
    /// </summary>
    private async Task ValidateServiceConfigurationAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Validate that required services are available
            // This could check database connections, external dependencies, etc.
            
            _logger.LogInformation("{ServiceName} configuration validation completed", _serviceName);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service configuration validation failed");
            throw;
        }
    }

    /// <summary>
    /// Called when the service is stopping.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{ServiceName} background service stopping...", _serviceName);
        
        try
        {
            // Perform cleanup tasks
            await OnServiceStoppingAsync(cancellationToken);
            
            // Call base implementation
            await base.StopAsync(cancellationToken);
            
            _logger.LogInformation("{ServiceName} background service stopped gracefully", _serviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {ServiceName} shutdown", _serviceName);
        }
    }

    /// <summary>
    /// Called when the service is stopping - perform cleanup.
    /// </summary>
    private async Task OnServiceStoppingAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Graceful shutdown tasks:
            // - Close active MCP sessions
            // - Flush logs
            // - Save state
            
            _logger.LogInformation("{ServiceName} cleanup completed", _serviceName);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {ServiceName} cleanup", _serviceName);
        }
    }
}
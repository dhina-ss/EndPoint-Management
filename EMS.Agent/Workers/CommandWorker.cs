using EMS.Agent.Configuration;
using EMS.Agent.Helpers;
using EMS.Agent.Models;
using EMS.Agent.Services;
using Microsoft.Extensions.Options;

namespace EMS.Agent.Workers;

/// <summary>
/// Drains the software-management command queue: polls the server for pending
/// commands, runs each one silently to completion, and reports the result.
/// Separate from <see cref="HeartbeatWorker"/> so a multi-minute install never
/// delays liveness heartbeats. Runs only in the SYSTEM service (default mode),
/// which has the rights to install/uninstall machine-wide software.
/// </summary>
public class CommandWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ApiSettings _apiSettings;
    private readonly ILogger<CommandWorker> _logger;

    public CommandWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ApiSettings> apiSettings,
        ILogger<CommandWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _apiSettings = apiSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Command worker started. Poll interval: {Interval} seconds.", _apiSettings.CommandPollIntervalSeconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessPendingAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(_apiSettings.CommandPollIntervalSeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            _logger.LogInformation("Command worker stopped.");
        }
    }

    private async Task ProcessPendingAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();

            // Nothing runs until the device is activated (same gate as heartbeats).
            var activation = scope.ServiceProvider.GetRequiredService<IActivationStore>();
            if (!activation.IsActivated())
            {
                return;
            }

            var apiClient = scope.ServiceProvider.GetRequiredService<IApiClientService>();
            var executor = scope.ServiceProvider.GetRequiredService<ICommandExecutionService>();

            var pending = await apiClient.GetPendingCommandsAsync(stoppingToken);
            if (pending.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Processing {Count} pending command(s).", pending.Count);

            foreach (var command in pending)
            {
                stoppingToken.ThrowIfCancellationRequested();
                await RunOneAsync(command, executor, apiClient, stoppingToken);
            }

            // A command may have installed or removed software, so refresh the
            // inventory now instead of waiting for the next hourly scan.
            await RefreshInstalledAppsAsync(apiClient, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Command poll cycle failed; will retry next interval.");
        }
    }

    private async Task RunOneAsync(
        PendingCommandModel command, ICommandExecutionService executor,
        IApiClientService apiClient, CancellationToken stoppingToken)
    {
        CommandExecutionResult result;
        try
        {
            result = await executor.ExecuteAsync(command, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command {CommandId} ({Type}) threw.", command.Id, command.Type);
            result = CommandExecutionResult.Fail($"Unhandled error: {ex.Message}");
        }

        _logger.LogInformation(
            "Command {CommandId} ({Type}) -> {Status}: {Message}",
            command.Id, command.Type, result.Success ? "Succeeded" : "Failed", result.Message);

        await apiClient.ReportCommandResultAsync(
            command.Id,
            new CommandResultModel
            {
                Success = result.Success,
                ResultCode = result.ExitCode,
                Message = result.Message
            },
            stoppingToken);
    }

    /// <summary>
    /// Re-scans installed software after a batch of commands so an install or
    /// uninstall is reflected in the dashboard promptly. Best-effort.
    /// </summary>
    private async Task RefreshInstalledAppsAsync(IApiClientService apiClient, CancellationToken stoppingToken)
    {
        try
        {
            var apps = InstalledAppsHelper.Collect(_logger);
            if (apps.Count > 0)
            {
                await apiClient.SendInstalledAppsAsync(apps, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Post-command installed-apps refresh failed.");
        }
    }
}


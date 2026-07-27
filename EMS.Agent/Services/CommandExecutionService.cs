using System.Runtime.Versioning;
using EMS.Agent.Configuration;
using EMS.Agent.Helpers;
using EMS.Agent.Models;
using Microsoft.Extensions.Options;

namespace EMS.Agent.Services;

/// <summary>
/// Dispatches a pending command to the right silent executor: uninstall via the
/// registry/Store, install/update via a downloaded package. Runs as SYSTEM in
/// the service, so it has the rights to change machine-wide software.
/// </summary>
[SupportedOSPlatform("windows")]
public class CommandExecutionService : ICommandExecutionService
{
    private readonly IApiClientService _apiClient;
    private readonly TimeSpan _timeout;
    private readonly ILogger<CommandExecutionService> _logger;

    public CommandExecutionService(
        IApiClientService apiClient,
        IOptions<ApiSettings> settings,
        ILogger<CommandExecutionService> logger)
    {
        _apiClient = apiClient;
        _timeout = TimeSpan.FromMinutes(Math.Max(1, settings.Value.CommandTimeoutMinutes));
        _logger = logger;
    }

    public async Task<CommandExecutionResult> ExecuteAsync(
        PendingCommandModel command, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<AgentCommandType>(command.Type, ignoreCase: true, out var type))
        {
            return CommandExecutionResult.Fail($"Unknown command type '{command.Type}'.");
        }

        return type switch
        {
            AgentCommandType.Uninstall => await ExecuteUninstallAsync(command, cancellationToken),
            AgentCommandType.Install or AgentCommandType.Update => await ExecuteInstallAsync(command, cancellationToken),
            _ => CommandExecutionResult.Fail($"Unsupported command type '{command.Type}'.")
        };
    }

    private async Task<CommandExecutionResult> ExecuteUninstallAsync(
        PendingCommandModel command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.TargetAppName))
        {
            return CommandExecutionResult.Fail("Uninstall command had no target application.");
        }

        return await AppUninstallHelper.UninstallAsync(
            command.TargetAppName, command.TargetAppVersion, command.TargetIsStoreApp,
            _timeout, _logger, cancellationToken);
    }

    private async Task<CommandExecutionResult> ExecuteInstallAsync(
        PendingCommandModel command, CancellationToken cancellationToken)
    {
        if (command.PackageId is not { } packageId)
        {
            return CommandExecutionResult.Fail("Install command had no package.");
        }

        var tempPath = await _apiClient.DownloadPackageAsync(packageId, command.Sha256, cancellationToken);
        if (tempPath is null)
        {
            return CommandExecutionResult.Fail("Could not download the installer package (or it failed verification).");
        }

        try
        {
            return await InstallerRunHelper.RunAsync(
                command.PackageKind ?? string.Empty, tempPath, command.SilentArgs,
                _timeout, _logger, cancellationToken);
        }
        finally
        {
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not delete temp installer {Path}.", tempPath);
            }
        }
    }
}

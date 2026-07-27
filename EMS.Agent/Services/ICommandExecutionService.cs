using EMS.Agent.Models;

namespace EMS.Agent.Services;

/// <summary>Runs a single pending command locally and returns its outcome.</summary>
public interface ICommandExecutionService
{
    Task<CommandExecutionResult> ExecuteAsync(
        PendingCommandModel command, CancellationToken cancellationToken = default);
}

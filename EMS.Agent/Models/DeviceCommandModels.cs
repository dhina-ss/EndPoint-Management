namespace EMS.Agent.Models;

/// <summary>The kind of software action a command asks for.</summary>
public enum AgentCommandType
{
    Uninstall,
    Install,
    Update
}

/// <summary>
/// A command the server handed to this agent to run. Mirrors the API's
/// PendingCommandDto (camelCase over the wire).
/// </summary>
public class PendingCommandModel
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    // Uninstall / Update target.
    public string? TargetAppName { get; set; }
    public string? TargetAppVersion { get; set; }
    public bool TargetIsStoreApp { get; set; }

    // Install / Update payload.
    public Guid? PackageId { get; set; }
    public string? PackageKind { get; set; }
    public string? SilentArgs { get; set; }
    public string? Sha256 { get; set; }
}

/// <summary>The outcome of running a command, sent back to the server.</summary>
public class CommandResultModel
{
    public bool Success { get; set; }
    public int? ResultCode { get; set; }
    public string? Message { get; set; }
}

/// <summary>Result of executing a command locally, before reporting it.</summary>
public sealed record CommandExecutionResult(bool Success, int? ExitCode, string Message)
{
    public static CommandExecutionResult Ok(string message, int? exitCode = 0) => new(true, exitCode, message);
    public static CommandExecutionResult Fail(string message, int? exitCode = null) => new(false, exitCode, message);
}

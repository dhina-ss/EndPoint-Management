using System.ComponentModel.DataAnnotations;

namespace EMS.API.DTOs;

// ---- Installer packages ----

/// <summary>Installer package as listed in the dashboard (no bytes).</summary>
public class InstallerPackageResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? SilentArgs { get; set; }
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

// ---- Enqueue (dashboard -> server) ----

/// <summary>Queues an Install/Update command that runs an uploaded package.</summary>
public class EnqueueInstallRequest
{
    [Required]
    public Guid PackageId { get; set; }
}

// ---- Command status (server -> dashboard) ----

/// <summary>A software-management command and its current status.</summary>
public class DeviceCommandResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TargetAppName { get; set; }
    public string? TargetAppVersion { get; set; }
    public string? PackageName { get; set; }
    public string? ResultMessage { get; set; }
    public int? ResultCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// ---- Command poll + result (agent <-> server) ----

/// <summary>A command handed to the agent, with everything it needs to run it.</summary>
public class PendingCommandDto
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

/// <summary>The agent's report of how a command turned out.</summary>
public class CommandResultRequest
{
    [Required]
    public bool Success { get; set; }

    public int? ResultCode { get; set; }

    [MaxLength(2000)]
    public string? Message { get; set; }
}

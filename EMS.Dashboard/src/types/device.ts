/** Server-computed device status. */
export type DeviceStatus = 'Online' | 'Sleep' | 'Offline';

/** MUI Chip color for a device status. */
export function statusColor(status: DeviceStatus): 'success' | 'warning' | 'default' {
  if (status === 'Online') {
    return 'success';
  }
  if (status === 'Sleep') {
    return 'warning';
  }
  return 'default';
}

/** Mirrors EMS.API DeviceResponse (camelCase over the wire). */
export interface Device {
  id: string;
  deviceId: string;
  deviceName: string;
  serialNumber: string;
  manufacturer: string | null;
  model: string | null;
  processor: string | null;
  ramSize: string | null;
  storageSize: string | null;
  osVersion: string | null;
  osBuildNumber: string | null;
  ipAddress: string | null;
  macAddress: string | null;
  username: string | null;
  lastBootTime: string | null;
  createdDate: string;
  updatedDate: string;
  lastSeen: string;
  lastHeartbeatTime: string | null;
  usbBlockingEnabled: boolean;
  storeGatingEnabled: boolean;
  status: DeviceStatus;
  activatedByUserId: string | null;
  activatedByEmployeeCode: string | null;
  activatedByName: string | null;
  activatedByEmail: string | null;
  activatedAt: string | null;
}

/**
 * A device is online when its last heartbeat is recent. Heartbeats arrive
 * every 60 s; 3 minutes = 3 missed beats, matching the pilot test guide.
 */
const ONLINE_THRESHOLD_MS = 3 * 60 * 1000;

export function isOnline(device: Device, now: number = Date.now()): boolean {
  if (!device.lastHeartbeatTime) {
    return false;
  }
  return now - Date.parse(device.lastHeartbeatTime) < ONLINE_THRESHOLD_MS;
}

/** Mirrors EMS.API AppUsageSummaryResponse (camelCase over the wire). */
export interface AppUsageEntry {
  applicationName: string;
  durationSeconds: number;
  usageDate: string;
}

/** Mirrors EMS.API BlockedWebsiteResponse. */
export interface BlockedWebsite {
  id: string;
  domain: string;
  createdDate: string;
}

/** Mirrors EMS.API InstalledAppResponse (read-only inventory). */
export interface InstalledApp {
  id: string;
  name: string;
  version: string | null;
  publisher: string | null;
  executableName: string | null;
  isStoreApp: boolean;
}

/** Mirrors EMS.API InstallerPackageResponse. */
export interface InstallerPackage {
  id: string;
  fileName: string;
  displayName: string;
  kind: 'Msi' | 'Exe';
  silentArgs: string | null;
  sizeBytes: number;
  sha256: string;
  uploadedAt: string;
}

export type CommandType = 'Uninstall' | 'Install' | 'Update';
export type CommandStatus = 'Pending' | 'Dispatched' | 'Succeeded' | 'Failed';

/** Mirrors EMS.API DeviceCommandResponse. */
export interface DeviceCommand {
  id: string;
  type: CommandType;
  status: CommandStatus;
  targetAppName: string | null;
  targetAppVersion: string | null;
  packageName: string | null;
  resultMessage: string | null;
  resultCode: number | null;
  createdAt: string;
  dispatchedAt: string | null;
  completedAt: string | null;
}

/** True while a command is still queued or running on the device. */
export function isCommandActive(status: CommandStatus): boolean {
  return status === 'Pending' || status === 'Dispatched';
}

/** "12.3 MB" style size from a byte count. */
export function formatBytes(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** Mirrors EMS.API NetworkUsageResponse — one day's data usage. */
export interface NetworkUsageEntry {
  usageDate: string;
  bytesSent: number;
  bytesReceived: number;
}

/** Mirrors EMS.API WorkTimeResponse — one day's working seconds. */
export interface WorkTimeEntry {
  workDate: string;
  workedSeconds: number;
}

/** "7h 42m" from seconds; "0m" when empty. */
export function formatHoursMinutes(totalSeconds: number): string {
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  if (hours === 0) {
    return `${minutes}m`;
  }
  return `${hours}h ${minutes}m`;
}

/** Mirrors EMS.API DeviceMetricsResponse. Every metric may be null. */
export interface DeviceMetrics {
  collectedAt: string | null;
  isOnline: boolean;
  status: DeviceStatus;
  cpuUsagePercent: number | null;
  memoryUsagePercent: number | null;
  memoryUsedMb: number | null;
  memoryTotalMb: number | null;
  diskUsagePercent: number | null;
  diskUsedGb: number | null;
  diskTotalGb: number | null;
  networkSentKbps: number | null;
  networkReceivedKbps: number | null;
  uptimeSeconds: number | null;
  batteryPercent: number | null;
  batteryCharging: boolean | null;
  hasBattery: boolean | null;
}

/** "3d 4h 12m" style uptime, from a duration in seconds. */
export function formatUptime(totalSeconds: number): string {
  const days = Math.floor(totalSeconds / 86400);
  const hours = Math.floor((totalSeconds % 86400) / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);

  if (days > 0) {
    return `${days}d ${hours}h ${minutes}m`;
  }
  if (hours > 0) {
    return `${hours}h ${minutes}m`;
  }
  return `${minutes}m`;
}

/** Network rates arrive in KB/s; show MB/s once they get large. */
export function formatRate(kbps: number): string {
  return kbps >= 1024 ? `${(kbps / 1024).toFixed(1)} MB/s` : `${kbps.toFixed(1)} KB/s`;
}

export function formatDuration(totalSeconds: number): string {
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);

  if (hours === 0 && minutes === 0) {
    return '< 1m';
  }
  if (hours === 0) {
    return `${minutes}m`;
  }
  return `${hours}h ${minutes}m`;
}

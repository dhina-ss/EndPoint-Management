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

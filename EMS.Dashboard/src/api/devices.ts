import type {
  AppUsageEntry,
  BlockedWebsite,
  Device,
  DeviceCommand,
  DeviceMetrics,
  InstalledApp,
  NetworkUsageEntry,
  WorkTimeEntry,
} from '../types/device';
import { API_BASE, credentialHeaders, throwForStatus } from './client';

export async function fetchDevices(): Promise<Device[]> {
  const response = await fetch(`${API_BASE}/api/devices`, { headers: credentialHeaders });

  if (!response.ok) {
    throwForStatus(response.status);
  }

  return (await response.json()) as Device[];
}

export async function fetchDevice(id: string): Promise<Device | null> {
  const response = await fetch(`${API_BASE}/api/devices/${encodeURIComponent(id)}`, {
    headers: credentialHeaders,
  });

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throwForStatus(response.status);
  }

  return (await response.json()) as Device;
}

export async function setUsbBlocking(id: string, enabled: boolean): Promise<Device> {
  const response = await fetch(`${API_BASE}/api/devices/${encodeURIComponent(id)}/usb-blocking`, {
    method: 'PUT',
    headers: {
      ...credentialHeaders,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ enabled }),
  });

  if (!response.ok) {
    throwForStatus(response.status);
  }

  return (await response.json()) as Device;
}

export async function setStoreGating(id: string, enabled: boolean): Promise<Device> {
  const response = await fetch(`${API_BASE}/api/devices/${encodeURIComponent(id)}/store-gating`, {
    method: 'PUT',
    headers: {
      ...credentialHeaders,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ enabled }),
  });

  if (!response.ok) {
    throwForStatus(response.status);
  }

  return (await response.json()) as Device;
}

export async function fetchAppUsage(id: string): Promise<AppUsageEntry[]> {
  const response = await fetch(`${API_BASE}/api/devices/${encodeURIComponent(id)}/app-usage`, {
    headers: credentialHeaders,
  });

  if (!response.ok) {
    throwForStatus(response.status);
  }

  return (await response.json()) as AppUsageEntry[];
}

export async function fetchInstalledApps(id: string): Promise<InstalledApp[]> {
  const response = await fetch(`${API_BASE}/api/devices/${encodeURIComponent(id)}/installed-apps`, {
    headers: credentialHeaders,
  });

  if (!response.ok) {
    throwForStatus(response.status);
  }

  return (await response.json()) as InstalledApp[];
}

// ---- Software management ----

/** Queues a silent uninstall of an inventory app. Returns immediately (202). */
export async function uninstallApp(deviceId: string, appId: string): Promise<void> {
  const response = await fetch(
    `${API_BASE}/api/devices/${encodeURIComponent(deviceId)}/installed-apps/${encodeURIComponent(appId)}/uninstall`,
    { method: 'POST', headers: credentialHeaders },
  );

  if (!response.ok) {
    if (response.status === 409 || response.status === 404) {
      const body = (await response.json().catch(() => null)) as { message?: string } | null;
      throw new Error(body?.message ?? `The uninstall could not be queued (${response.status}).`);
    }
    throwForStatus(response.status);
  }
}

/** Queues an Install (or Update) that runs an uploaded package on the device. */
export async function queueInstall(
  deviceId: string,
  packageId: string,
  type: 'install' | 'update' = 'install',
): Promise<void> {
  const response = await fetch(
    `${API_BASE}/api/devices/${encodeURIComponent(deviceId)}/commands?type=${type}`,
    {
      method: 'POST',
      headers: { ...credentialHeaders, 'Content-Type': 'application/json' },
      body: JSON.stringify({ packageId }),
    },
  );

  if (!response.ok) {
    if (response.status === 404) {
      const body = (await response.json().catch(() => null)) as { message?: string } | null;
      throw new Error(body?.message ?? `The command could not be queued (${response.status}).`);
    }
    throwForStatus(response.status);
  }
}

/** Recent software-management commands for a device (newest first). */
export async function fetchDeviceCommands(id: string): Promise<DeviceCommand[]> {
  const response = await fetch(`${API_BASE}/api/devices/${encodeURIComponent(id)}/commands`, {
    headers: credentialHeaders,
  });

  if (!response.ok) {
    throwForStatus(response.status);
  }

  return (await response.json()) as DeviceCommand[];
}

export async function fetchDeviceMetrics(id: string): Promise<DeviceMetrics> {
  const response = await fetch(`${API_BASE}/api/devices/${encodeURIComponent(id)}/metrics`, {
    headers: credentialHeaders,
  });

  if (!response.ok) {
    throwForStatus(response.status);
  }

  return (await response.json()) as DeviceMetrics;
}

export async function fetchNetworkUsage(id: string, days = 7): Promise<NetworkUsageEntry[]> {
  const response = await fetch(
    `${API_BASE}/api/devices/${encodeURIComponent(id)}/network-usage?days=${days}`,
    { headers: credentialHeaders },
  );

  if (!response.ok) {
    throwForStatus(response.status);
  }

  return (await response.json()) as NetworkUsageEntry[];
}

export async function fetchWorkTime(id: string, days = 7): Promise<WorkTimeEntry[]> {
  const response = await fetch(
    `${API_BASE}/api/devices/${encodeURIComponent(id)}/work-time?days=${days}`,
    { headers: credentialHeaders },
  );

  if (!response.ok) {
    throwForStatus(response.status);
  }

  return (await response.json()) as WorkTimeEntry[];
}

export async function fetchBlockedWebsites(id: string): Promise<BlockedWebsite[]> {
  const response = await fetch(`${API_BASE}/api/devices/${encodeURIComponent(id)}/blocked-websites`, {
    headers: credentialHeaders,
  });

  if (!response.ok) {
    throwForStatus(response.status);
  }

  return (await response.json()) as BlockedWebsite[];
}

export async function addBlockedWebsite(id: string, domain: string): Promise<BlockedWebsite> {
  const response = await fetch(`${API_BASE}/api/devices/${encodeURIComponent(id)}/blocked-websites`, {
    method: 'POST',
    headers: {
      ...credentialHeaders,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ domain }),
  });

  if (!response.ok) {
    // 400 (invalid) and 409 (duplicate) carry a { message } body worth surfacing.
    if (response.status === 400 || response.status === 409) {
      const body = (await response.json().catch(() => null)) as { message?: string } | null;
      throw new Error(body?.message ?? `The domain could not be added (${response.status}).`);
    }
    throwForStatus(response.status);
  }

  return (await response.json()) as BlockedWebsite;
}

export async function removeBlockedWebsite(id: string, blockId: string): Promise<void> {
  const response = await fetch(
    `${API_BASE}/api/devices/${encodeURIComponent(id)}/blocked-websites/${encodeURIComponent(blockId)}`,
    {
      method: 'DELETE',
      headers: credentialHeaders,
    },
  );

  if (!response.ok && response.status !== 404) {
    throwForStatus(response.status);
  }
}

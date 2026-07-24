import type {
  AppUsageEntry,
  BlockedWebsite,
  Device,
  DeviceMetrics,
  InstalledApp,
} from '../types/device';

// Empty base URL = same origin; the Vite dev server proxies /api to EMS.API.
const API_BASE = import.meta.env.VITE_API_URL ?? '';

// GET /api/devices requires device credentials until the API grows a proper
// admin authentication scheme. Configure an observer credential in
// .env.local (see README section in the page header).
const DEVICE_ID = import.meta.env.VITE_API_DEVICE_ID ?? '';
const DEVICE_TOKEN = import.meta.env.VITE_API_DEVICE_TOKEN ?? '';

const credentialHeaders = {
  'X-Device-Id': DEVICE_ID,
  'X-Device-Token': DEVICE_TOKEN,
};

function throwForStatus(status: number): never {
  if (status === 401) {
    throw new Error(
      'Unauthorized. Set VITE_API_DEVICE_ID and VITE_API_DEVICE_TOKEN in EMS.Dashboard/.env.local.',
    );
  }
  throw new Error(`The EMS API returned ${status}.`);
}

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

export async function blockApplication(
  id: string,
  executableName: string,
  displayName: string,
): Promise<void> {
  const response = await fetch(`${API_BASE}/api/devices/${encodeURIComponent(id)}/blocked-apps`, {
    method: 'POST',
    headers: {
      ...credentialHeaders,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ executableName, displayName }),
  });

  if (!response.ok) {
    if (response.status === 400) {
      const body = (await response.json().catch(() => null)) as { message?: string } | null;
      throw new Error(body?.message ?? 'This application cannot be blocked.');
    }
    throwForStatus(response.status);
  }
}

export async function unblockApplication(id: string, executableName: string): Promise<void> {
  const response = await fetch(
    `${API_BASE}/api/devices/${encodeURIComponent(id)}/blocked-apps/${encodeURIComponent(executableName)}`,
    {
      method: 'DELETE',
      headers: credentialHeaders,
    },
  );

  if (!response.ok && response.status !== 404) {
    throwForStatus(response.status);
  }
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

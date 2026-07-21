import type { AppUsageEntry, Device } from '../types/device';

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

export async function fetchAppUsage(id: string): Promise<AppUsageEntry[]> {
  const response = await fetch(`${API_BASE}/api/devices/${encodeURIComponent(id)}/app-usage`, {
    headers: credentialHeaders,
  });

  if (!response.ok) {
    throwForStatus(response.status);
  }

  return (await response.json()) as AppUsageEntry[];
}

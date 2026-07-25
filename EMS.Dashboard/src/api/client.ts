// Shared API configuration. Empty base URL = same origin (the Vite dev
// server proxies /api to EMS.API); set VITE_API_URL for a hosted backend.
export const API_BASE = import.meta.env.VITE_API_URL ?? '';

// Every endpoint currently requires device credentials, until the API grows
// a proper admin sign-in. Configure an observer credential in .env.local.
const DEVICE_ID = import.meta.env.VITE_API_DEVICE_ID ?? '';
const DEVICE_TOKEN = import.meta.env.VITE_API_DEVICE_TOKEN ?? '';

export const credentialHeaders = {
  'X-Device-Id': DEVICE_ID,
  'X-Device-Token': DEVICE_TOKEN,
};

export function throwForStatus(status: number): never {
  if (status === 401) {
    throw new Error(
      'Unauthorized. Set VITE_API_DEVICE_ID and VITE_API_DEVICE_TOKEN in EMS.Dashboard/.env.local.',
    );
  }
  throw new Error(`The EMS API returned ${status}.`);
}

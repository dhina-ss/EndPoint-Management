import type { InstallerPackage } from '../types/device';
import { API_BASE, credentialHeaders, throwForStatus } from './client';

/** Uploads an MSI/EXE installer to the shared package library. */
export async function uploadPackage(
  file: File,
  displayName: string,
  silentArgs: string,
): Promise<InstallerPackage> {
  const form = new FormData();
  form.append('file', file);
  if (displayName.trim()) {
    form.append('displayName', displayName.trim());
  }
  if (silentArgs.trim()) {
    form.append('silentArgs', silentArgs.trim());
  }

  // Note: do NOT set Content-Type — the browser adds the multipart boundary.
  const response = await fetch(`${API_BASE}/api/packages`, {
    method: 'POST',
    headers: credentialHeaders,
    body: form,
  });

  if (!response.ok) {
    if (response.status === 400) {
      const body = (await response.json().catch(() => null)) as { message?: string } | null;
      throw new Error(body?.message ?? 'The installer could not be uploaded.');
    }
    throwForStatus(response.status);
  }

  return (await response.json()) as InstallerPackage;
}

export async function fetchPackages(): Promise<InstallerPackage[]> {
  const response = await fetch(`${API_BASE}/api/packages`, { headers: credentialHeaders });

  if (!response.ok) {
    throwForStatus(response.status);
  }

  return (await response.json()) as InstallerPackage[];
}

export async function deletePackage(id: string): Promise<void> {
  const response = await fetch(`${API_BASE}/api/packages/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    headers: credentialHeaders,
  });

  if (!response.ok && response.status !== 404) {
    if (response.status === 409) {
      throw new Error('This package is still referenced by a command and cannot be deleted yet.');
    }
    throwForStatus(response.status);
  }
}

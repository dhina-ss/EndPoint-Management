import { API_BASE, credentialHeaders, throwForStatus } from './client';

export interface CreateUserInput {
  email: string;
  employeeCode: string;
  username: string;
  password: string;
  confirmPassword: string;
}

export interface CreatedUser {
  id: string;
  email: string;
  employeeCode: string;
  username: string;
  createdDate: string;
}

export async function createUser(input: CreateUserInput): Promise<CreatedUser> {
  const response = await fetch(`${API_BASE}/api/users`, {
    method: 'POST',
    headers: {
      ...credentialHeaders,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(input),
  });

  if (!response.ok) {
    // 400 (validation) and 409 (duplicate email/username/emp code) carry a
    // useful message worth surfacing to the operator.
    if (response.status === 400 || response.status === 409) {
      const body = (await response.json().catch(() => null)) as
        | { message?: string; errors?: Record<string, string[]> }
        | null;

      if (body?.errors) {
        const first = Object.values(body.errors).flat()[0];
        if (first) {
          throw new Error(first);
        }
      }
      throw new Error(body?.message ?? 'The user could not be created.');
    }
    throwForStatus(response.status);
  }

  return (await response.json()) as CreatedUser;
}

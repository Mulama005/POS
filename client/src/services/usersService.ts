import { apiClient } from './apiClient'
import type { InviteUserRequest, InviteUserResponse, ManagedUser } from '../types/user'
import type { UserRole } from '../types/auth'

export async function listUsers(): Promise<ManagedUser[]> {
  const { data } = await apiClient.get<ManagedUser[]>('/api/users')
  return data
}

export async function inviteUser(request: InviteUserRequest): Promise<InviteUserResponse> {
  const { data } = await apiClient.post<InviteUserResponse>('/api/users/invite', request)
  return data
}

export async function changeUserRole(userId: string, role: UserRole): Promise<void> {
  await apiClient.put(`/api/users/${userId}/role`, { role })
}

export async function deactivateUser(userId: string): Promise<void> {
  await apiClient.post(`/api/users/${userId}/deactivate`)
}

export async function reactivateUser(userId: string): Promise<void> {
  await apiClient.post(`/api/users/${userId}/reactivate`)
}

/** Public — the invited person doesn't have an account to authenticate with yet. */
export async function acceptInvite(userId: string, token: string, password: string): Promise<void> {
  await apiClient.post('/api/users/accept-invite', { userId, token, password })
}
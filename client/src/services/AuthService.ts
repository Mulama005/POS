import { apiClient } from './apiClient'
import type { LoginResponse, LoginSuccessResponse } from '../types/auth'

export async function login(email: string, password: string): Promise<LoginResponse> {
  const { data } = await apiClient.post<LoginResponse>('/api/auth/login', { email, password })
  return data
}

export async function verifyMfa(challengeToken: string, code: string): Promise<LoginSuccessResponse> {
  const { data } = await apiClient.post<LoginSuccessResponse>('/api/auth/mfa/verify', {
    challengeToken,
    code,
  })
  return data
}

/** Relies on the httpOnly refresh cookie being sent automatically — no token passed explicitly. */
export async function refresh(): Promise<LoginSuccessResponse> {
  const { data } = await apiClient.post<LoginSuccessResponse>('/api/auth/refresh')
  return data
}

export async function logout(): Promise<void> {
  await apiClient.post('/api/auth/logout')
}

export interface MfaSetupResponse {
  otpAuthUri: string
  rawSecret: string
}

/** Manager/Admin only, per the backend's [Authorize(Roles = "Manager,Admin")]. */
export async function mfaSetup(): Promise<MfaSetupResponse> {
  const { data } = await apiClient.post<MfaSetupResponse>('/api/auth/mfa/setup')
  return data
}

/** Confirms the secret actually works before MfaEnabled flips to true server-side. */
export async function mfaEnable(code: string): Promise<void> {
  await apiClient.post('/api/auth/mfa/enable', { code })
}
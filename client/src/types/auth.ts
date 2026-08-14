export type UserRole = 'Cashier' | 'Manager' | 'Admin' | 'Technician'

export interface AuthUser {
  id: string
  fullName: string
  email: string
  role: UserRole
  assignedRegisterId: string | null
  mfaEnabled: boolean
}

export interface LoginSuccessResponse {
  accessToken: string
  user: AuthUser
}

export interface LoginMfaRequiredResponse {
  mfaRequired: true
  challengeToken: string
  user: AuthUser
}

export type LoginResponse = LoginSuccessResponse | LoginMfaRequiredResponse

export function isMfaRequired(response: LoginResponse): response is LoginMfaRequiredResponse {
  return 'mfaRequired' in response && response.mfaRequired === true
}

export interface ApiErrorBody {
  message?: string
}
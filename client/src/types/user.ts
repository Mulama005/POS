import type { UserRole } from './auth'

export interface ManagedUser {
  id: string
  fullName: string
  email: string
  role: UserRole
  isActive: boolean
  mfaEnabled: boolean
}

export interface InviteUserRequest {
  email: string
  fullName: string
  role: UserRole
}

export interface InviteUserResponse {
  message: string
  /**
   * Only present because no real email provider is wired up yet (see
   * ConsoleEmailSender on the backend) — once one is, this field goes away
   * and the link only ever reaches the invited person's inbox. Don't build
   * UI that assumes this will always be here.
   */
  inviteLink?: string
}
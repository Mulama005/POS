import type { UserRole } from '../types/auth'

const ROLE_LANDING_ROUTES: Record<UserRole, string> = {
  Cashier: '/checkout',
  Manager: '/dashboard/manager',
  Admin: '/dashboard/admin',
  Technician: '/repairs',
}

export function getLandingRouteForRole(role: UserRole): string {
  return ROLE_LANDING_ROUTES[role]
}
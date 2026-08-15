import { useEffect, useState, type FormEvent } from 'react'
import { isAxiosError } from 'axios'
import { changeUserRole, deactivateUser, inviteUser, listUsers, reactivateUser } from '../services/usersService'
import type { ManagedUser } from '../types/user'
import type { UserRole, ApiErrorBody } from '../types/auth'
import './UserManagementPage.css'

const ROLES: UserRole[] = ['Cashier', 'Manager', 'Admin', 'Technician']

function getErrorMessage(err: unknown, fallback: string): string {
  if (isAxiosError<ApiErrorBody>(err) && err.response?.data?.message) {
    return err.response.data.message
  }
  if (isAxiosError(err) && typeof err.response?.data === 'string') {
    return err.response.data
  }
  return fallback
}

export function UserManagementPage() {
  const [users, setUsers] = useState<ManagedUser[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [rowError, setRowError] = useState<Record<string, string>>({})
  const [rowBusy, setRowBusy] = useState<Record<string, boolean>>({})

  const [inviteOpen, setInviteOpen] = useState(false)
  const [inviteFullName, setInviteFullName] = useState('')
  const [inviteEmail, setInviteEmail] = useState('')
  const [inviteRole, setInviteRole] = useState<UserRole>('Cashier')
  const [inviteSubmitting, setInviteSubmitting] = useState(false)
  const [inviteError, setInviteError] = useState<string | null>(null)
  const [inviteLink, setInviteLink] = useState<string | null>(null)

  const loadUsers = async () => {
    setLoading(true)
    setLoadError(null)
    try {
      setUsers(await listUsers())
    } catch (err) {
      setLoadError(getErrorMessage(err, 'Could not load users.'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void loadUsers()
  }, [])

  const withRowState = async (userId: string, action: () => Promise<void>) => {
    setRowError((prev) => ({ ...prev, [userId]: '' }))
    setRowBusy((prev) => ({ ...prev, [userId]: true }))
    try {
      await action()
      await loadUsers()
    } catch (err) {
      setRowError((prev) => ({ ...prev, [userId]: getErrorMessage(err, 'Action failed.') }))
    } finally {
      setRowBusy((prev) => ({ ...prev, [userId]: false }))
    }
  }

  const handleRoleChange = (user: ManagedUser, newRole: UserRole) => {
    if (newRole === user.role) return
    void withRowState(user.id, () => changeUserRole(user.id, newRole))
  }

  const handleDeactivate = (user: ManagedUser) => {
    if (!confirm(`Deactivate ${user.fullName}? This immediately ends any active session they have.`)) return
    void withRowState(user.id, () => deactivateUser(user.id))
  }

  const handleReactivate = (user: ManagedUser) => {
    void withRowState(user.id, () => reactivateUser(user.id))
  }

  const handleInviteSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setInviteError(null)
    setInviteSubmitting(true)
    try {
      const res = await inviteUser({ fullName: inviteFullName, email: inviteEmail, role: inviteRole })
      setInviteLink(res.inviteLink ?? null)
      setInviteFullName('')
      setInviteEmail('')
      setInviteRole('Cashier')
      await loadUsers()
    } catch (err) {
      setInviteError(getErrorMessage(err, 'Could not send invite.'))
    } finally {
      setInviteSubmitting(false)
    }
  }

  return (
    <div className="user-mgmt-screen">
      <div className="user-mgmt-header">
        <h1 className="user-mgmt-title">User management</h1>
        <button type="button" className="user-mgmt-invite-btn" onClick={() => setInviteOpen((v) => !v)}>
          {inviteOpen ? 'Cancel' : 'Invite user'}
        </button>
      </div>

      {inviteOpen && (
        <form className="user-mgmt-invite-form" onSubmit={(e) => void handleInviteSubmit(e)}>
          <div className="user-mgmt-invite-fields">
            <input
              type="text"
              placeholder="Full name"
              value={inviteFullName}
              onChange={(e) => setInviteFullName(e.target.value)}
              required
            />
            <input
              type="email"
              placeholder="Email"
              value={inviteEmail}
              onChange={(e) => setInviteEmail(e.target.value)}
              required
            />
            <select value={inviteRole} onChange={(e) => setInviteRole(e.target.value as UserRole)}>
              {ROLES.map((r) => (
                <option key={r} value={r}>{r}</option>
              ))}
            </select>
            <button type="submit" disabled={inviteSubmitting}>
              {inviteSubmitting ? 'Sending…' : 'Send invite'}
            </button>
          </div>
          {inviteError && <p className="user-mgmt-error" role="alert">{inviteError}</p>}
          {inviteLink && (
            <p className="user-mgmt-invite-link-note">
              No email provider is connected yet — share this link with them directly:
              <br />
              <code>{inviteLink}</code>
            </p>
          )}
        </form>
      )}

      {loading && <p className="user-mgmt-hint">Loading users…</p>}
      {loadError && <p className="user-mgmt-error" role="alert">{loadError}</p>}

      {!loading && !loadError && (
        <table className="user-mgmt-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Email</th>
              <th>Role</th>
              <th>Status</th>
              <th>2FA</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.id} className={u.isActive ? '' : 'user-mgmt-row--inactive'}>
                <td>{u.fullName}</td>
                <td>{u.email}</td>
                <td>
                  <select
                    value={u.role}
                    disabled={rowBusy[u.id]}
                    onChange={(e) => handleRoleChange(u, e.target.value as UserRole)}
                  >
                    {ROLES.map((r) => (
                      <option key={r} value={r}>{r}</option>
                    ))}
                  </select>
                </td>
                <td>{u.isActive ? 'Active' : 'Deactivated'}</td>
                <td>{u.mfaEnabled ? 'On' : 'Off'}</td>
                <td>
                  {u.isActive ? (
                    <button
                      type="button"
                      className="user-mgmt-danger-btn"
                      disabled={rowBusy[u.id]}
                      onClick={() => handleDeactivate(u)}
                    >
                      Deactivate
                    </button>
                  ) : (
                    <button
                      type="button"
                      disabled={rowBusy[u.id]}
                      onClick={() => handleReactivate(u)}
                    >
                      Reactivate
                    </button>
                  )}
                  {rowError[u.id] && <p className="user-mgmt-error user-mgmt-error--row">{rowError[u.id]}</p>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
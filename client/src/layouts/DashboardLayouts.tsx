import { type JSX, useEffect, useState } from 'react'
import { Outlet, NavLink } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { listRegisters, type RegisterSummary } from '../services/registersService'
import type { UserRole } from '../types/auth'
import './DashboardLayout.css'

// ── Icons — Feather-style, single-weight, no fills. Kept deliberately plain
// so the sidebar's one visual flourish stays the barcode motif below, not
// the icon set. ──────────────────────────────────────────────────────────
function Icon({ name, size = 16 }: { name: string; size?: number }) {
  const s = size
  const paths: Record<string, JSX.Element> = {
    checkout: (
      <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <circle cx="9" cy="21" r="1" /><circle cx="20" cy="21" r="1" />
        <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6" />
      </svg>
    ),
    inventory: (
      <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
        <polyline points="3.27 6.96 12 12.01 20.73 6.96" /><line x1="12" y1="22.08" x2="12" y2="12" />
      </svg>
    ),
    receive: (
      <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M21 8v13H3V8" /><path d="M1 3h22v5H1z" /><line x1="10" y1="12" x2="14" y2="12" />
      </svg>
    ),
    warranty: (
      <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
        <path d="M9 12l2 2 4-4" />
      </svg>
    ),
    repairs: (
      <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z" />
      </svg>
    ),
    customers: (
      <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" /><circle cx="9" cy="7" r="4" />
        <path d="M23 21v-2a4 4 0 0 0-3-3.87" /><path d="M16 3.13a4 4 0 0 1 0 7.75" />
      </svg>
    ),
    users: (
      <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <circle cx="12" cy="8" r="4" /><path d="M4 21c0-4.4 3.6-8 8-8s8 3.6 8 8" />
      </svg>
    ),
    monitor: (
      <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <rect x="2" y="3" width="20" height="14" rx="2" ry="2" />
        <line x1="8" y1="21" x2="16" y2="21" /><line x1="12" y1="17" x2="12" y2="21" />
      </svg>
    ),
    logout: (
      <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
        <polyline points="16 17 21 12 16 7" /><line x1="21" y1="12" x2="9" y2="12" />
      </svg>
    ),
    chevronLeft: (
      <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <polyline points="15 18 9 12 15 6" />
      </svg>
    ),
    chevronRight: (
      <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <polyline points="9 18 15 12 9 6" />
      </svg>
    ),
    chevronDown: (
      <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <polyline points="6 9 12 15 18 9" />
      </svg>
    ),
  }
  return paths[name] ?? null
}

// ── Navigation config — every path here must correspond to a real route in
// App.tsx. Nothing links anywhere unbuilt: an unreachable nav item reads
// worse than no nav item at all. ────────────────────────────────────────
interface NavItem {
  id: string
  label: string
  icon: string
  path: string
}

interface NavGroup {
  label: string
  items: NavItem[]
}

const NAV_BY_ROLE: Record<UserRole, NavGroup[]> = {
  // Warranty lookup has no role restriction on the backend, and answering "is this still
  // covered" is a genuine front-line task — Cashier gets it too, not just Manager/Admin.
  Cashier: [
    {
      label: 'Operations',
      items: [{ id: 'checkout', label: 'Checkout', icon: 'checkout', path: '/checkout' }],
    },
    {
      label: 'Lookups',
      items: [{ id: 'warranty', label: 'Warranty lookup', icon: 'warranty', path: '/warranty-lookup' }],
    },
  ],
  Manager: [
    {
      label: 'Overview',
      items: [{ id: 'dashboard', label: 'Dashboard', icon: 'monitor', path: '/dashboard/manager' }],
    },
    {
      label: 'Operations',
      items: [
        { id: 'checkout', label: 'Checkout', icon: 'checkout', path: '/checkout' },
        { id: 'inventory', label: 'Inventory', icon: 'inventory', path: '/inventory' },
        { id: 'receive-stock', label: 'Receive stock', icon: 'receive', path: '/stock/receive' },
        { id: 'repairs', label: 'Repairs', icon: 'repairs', path: '/repairs' },
        { id: 'customers', label: 'Customers', icon: 'customers', path: '/customers' },
      ],
    },
    {
      label: 'Lookups',
      items: [{ id: 'warranty', label: 'Warranty lookup', icon: 'warranty', path: '/warranty-lookup' }],
    },
  ],
  Admin: [
    {
      label: 'Overview',
      items: [{ id: 'dashboard', label: 'Dashboard', icon: 'monitor', path: '/dashboard/admin' }],
    },
    {
      label: 'Operations',
      items: [
        { id: 'checkout', label: 'Checkout', icon: 'checkout', path: '/checkout' },
        { id: 'inventory', label: 'Inventory', icon: 'inventory', path: '/inventory' },
        { id: 'receive-stock', label: 'Receive stock', icon: 'receive', path: '/stock/receive' },
        { id: 'repairs', label: 'Repairs', icon: 'repairs', path: '/repairs' },
        { id: 'customers', label: 'Customers', icon: 'customers', path: '/customers' },
      ],
    },
    {
      label: 'Lookups',
      items: [{ id: 'warranty', label: 'Warranty lookup', icon: 'warranty', path: '/warranty-lookup' }],
    },
    {
      label: 'Administration',
      items: [{ id: 'users', label: 'Users', icon: 'users', path: '/users' }],
    },
  ],
  // Technician is blocked from register/till access entirely (Step 9 RBAC) — their world is
  // repairs, plus warranty lookup to answer a customer's "is this still covered" on the spot.
  Technician: [
    {
      label: 'Operations',
      items: [{ id: 'repairs', label: 'Repairs', icon: 'repairs', path: '/repairs' }],
    },
    {
      label: 'Lookups',
      items: [{ id: 'warranty', label: 'Warranty lookup', icon: 'warranty', path: '/warranty-lookup' }],
    },
  ],
}

const HOME_PATH: Record<UserRole, string> = {
  Cashier: '/checkout',
  Manager: '/dashboard/manager',
  Admin: '/dashboard/admin',
  Technician: '/repairs',
}

function Sidebar({ expanded, onToggle }: { expanded: boolean; onToggle: () => void }) {
  const { user } = useAuth()
  const role: UserRole = user?.role ?? 'Cashier'
  const groups = NAV_BY_ROLE[role]
  const [collapsedGroups, setCollapsedGroups] = useState<Record<string, boolean>>({})

  const toggleGroup = (label: string) =>
    setCollapsedGroups((prev) => ({ ...prev, [label]: !prev[label] }))

  return (
    <aside className={`sidebar ${expanded ? '' : 'collapsed'}`}>
      <NavLink to={HOME_PATH[role]} className="logo-link">
        <div className="logo-area">
          {/* Signature mark — a barcode rhythm in brass, not a generic
              gradient square. The one deliberate flourish in an otherwise
              quiet shell, tying the mark directly to what this app is for. */}
          <div className="logo-icon" aria-hidden>
            <svg width="22" height="22" viewBox="0 0 20 20">
              <rect x="1" y="2" width="1.5" height="16" fill="var(--pos-shell-brass)" />
              <rect x="4" y="2" width="1" height="16" fill="var(--pos-shell-brass)" />
              <rect x="6.5" y="2" width="2" height="16" fill="var(--pos-shell-brass)" />
              <rect x="10" y="2" width="1" height="16" fill="var(--pos-shell-brass)" />
              <rect x="12.5" y="2" width="1.5" height="16" fill="var(--pos-shell-brass)" />
              <rect x="16" y="2" width="1" height="16" fill="var(--pos-shell-brass)" />
            </svg>
          </div>
          {expanded && (
            <div className="logo-text">
              <div className="brand">AyiyaPOS</div>
            </div>
          )}
        </div>
      </NavLink>

      <nav className="nav">
        {groups.map((group) => {
          const isCollapsed = collapsedGroups[group.label]
          return (
            <div key={group.label} className="group">
              {expanded && (
                <button type="button" className="group-label" onClick={() => toggleGroup(group.label)}>
                  {group.label}
                  <span className={`chevron ${isCollapsed ? 'closed' : 'open'}`}>
                    <Icon name="chevronDown" size={12} />
                  </span>
                </button>
              )}
              {(!isCollapsed || !expanded) &&
                group.items.map((item) => (
                  <NavLink
                    key={item.id}
                    to={item.path}
                    title={!expanded ? item.label : undefined}
                    className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}
                  >
                    <span className="icon"><Icon name={item.icon} size={16} /></span>
                    {expanded && <span className="label">{item.label}</span>}
                  </NavLink>
                ))}
            </div>
          )
        })}
      </nav>

      <div className="toggle-area">
        <button type="button" className="toggle-btn" onClick={onToggle} title={expanded ? 'Collapse sidebar' : 'Expand sidebar'}>
          <Icon name={expanded ? 'chevronLeft' : 'chevronRight'} size={14} />
        </button>
      </div>
    </aside>
  )
}

function TopBar() {
  const { user, logout } = useAuth()
  const [registers, setRegisters] = useState<RegisterSummary[]>([])

  useEffect(() => {
    // Registers list is small (a handful per store) and rarely changes —
    // one fetch per session is enough; no need for polling here.
    void listRegisters().then(setRegisters).catch(() => setRegisters([]))
  }, [])

  const displayName = user?.fullName || 'User'
  const initials = displayName
    .split(' ')
    .map((n) => n[0])
    .join('')
    .toUpperCase()
    .slice(0, 2)
  const role = user?.role ?? 'Cashier'

  const registerLabel = user?.assignedRegisterId
    ? registers.find((r) => r.id === user.assignedRegisterId)?.name ?? 'Register'
    : role === 'Cashier'
      ? 'Unassigned'
      : 'All registers'

  const handleLogout = () => {
    void logout()
  }

  return (
    <header className="topbar">
      <div className="left">
        <div className="register-badge"><Icon name="monitor" size={13} /> {registerLabel}</div>
        <div className="online-badge"><span className="dot" /> Online</div>
      </div>
      <div className="right">
        <div className="user-block">
          <div className="avatar">{initials}</div>
          <div className="user-info">
            <div className="name">{displayName}</div>
            <div className="role">{role}</div>
          </div>
        </div>
        <div className="divider" />
        <button type="button" className="logout-btn" onClick={handleLogout}>
          <Icon name="logout" size={14} /> Log out
        </button>
      </div>
    </header>
  )
}

export function DashboardLayout() {
  const [sidebarExpanded, setSidebarExpanded] = useState(true)
  return (
    <div className="dashboard-layout">
      <Sidebar expanded={sidebarExpanded} onToggle={() => setSidebarExpanded((v) => !v)} />
      <div className="main">
        <TopBar />
        <main className="page-content"><Outlet /></main>
      </div>
    </div>
  )
}
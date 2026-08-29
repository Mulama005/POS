import {type JSX, useState} from "react";
import { Outlet, NavLink } from "react-router-dom";
import { useAuth } from "../hooks/useAuth"; // adjust path to your actual AuthContext location
import "./DashboardLayout.css";

// ── Feather-style inline SVG icons ──────────────────────────────────────────
function Icon({ name, size = 16 }: { name: string; size?: number }) {
    const s = size;
    const paths: Record<string, JSX.Element> = {
        checkout: (
            <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="9" cy="21" r="1" /><circle cx="20" cy="21" r="1" />
                <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6" />
            </svg>
        ),
        shift: (
            <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                <line x1="16" y1="2" x2="16" y2="6" /><line x1="8" y1="2" x2="8" y2="6" />
                <line x1="3" y1="10" x2="21" y2="10" />
            </svg>
        ),
        inventory: (
            <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
                <polyline points="3.27 6.96 12 12.01 20.73 6.96" /><line x1="12" y1="22.08" x2="12" y2="12" />
            </svg>
        ),
        repairs: (
            <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z" />
            </svg>
        ),
        reports: (
            <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="18" y1="20" x2="18" y2="10" /><line x1="12" y1="20" x2="12" y2="4" />
                <line x1="6" y1="20" x2="6" y2="14" /><line x1="2" y1="20" x2="22" y2="20" />
            </svg>
        ),
        users: (
            <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" /><circle cx="9" cy="7" r="4" />
                <path d="M23 21v-2a4 4 0 0 0-3-3.87" /><path d="M16 3.13a4 4 0 0 1 0 7.75" />
            </svg>
        ),
        health: (
            <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
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
        bell: (
            <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
                <path d="M13.73 21a2 2 0 0 1-3.46 0" />
            </svg>
        ),
        monitor: (
            <svg width={s} height={s} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect x="2" y="3" width="20" height="14" rx="2" ry="2" />
                <line x1="8" y1="21" x2="16" y2="21" /><line x1="12" y1="17" x2="12" y2="21" />
            </svg>
        ),
    };
    return paths[name] ?? null;
}

// ── Navigation config ──────────────────────────────────────────────────────────
type Role = "cashier" | "manager" | "admin";

interface NavItem {
    id: string;
    label: string;
    icon: string;
    path: string;
}

interface NavGroup {
    label: string;
    items: NavItem[];
}

// Note: roles are case‑sensitive; if your auth returns 'Manager' etc., you need to map.
const NAV_BY_ROLE: Record<Role, NavGroup[]> = {
    cashier: [
        {
            label: "Operations",
            items: [
                { id: "checkout", label: "Checkout", icon: "checkout", path: "/checkout" },
                { id: "shift", label: "My Shift", icon: "shift", path: "/shift" },
            ],
        },
    ],
    manager: [
        {
            label: "Overview",
            items: [
                { id: "dashboard", label: "Dashboard", icon: "monitor", path: "/dashboard/manager" },
            ],
        },
        {
            label: "Operations",
            items: [
                { id: "checkout", label: "Checkout", icon: "checkout", path: "/checkout" },
                { id: "inventory", label: "Inventory", icon: "inventory", path: "/inventory" },
                { id: "repairs", label: "Repairs", icon: "repairs", path: "/repairs" },
            ],
        },
        {
            label: "Insights",
            items: [
                { id: "reports", label: "Reports", icon: "reports", path: "/reports" },
            ],
        },
    ],
    admin: [
        {
            label: "Overview",
            items: [
                { id: "dashboard", label: "Dashboard", icon: "monitor", path: "/dashboard/admin" },
            ],
        },
        {
            label: "Operations",
            items: [
                { id: "checkout", label: "Checkout", icon: "checkout", path: "/checkout" },
                { id: "inventory", label: "Inventory", icon: "inventory", path: "/inventory" },
                { id: "repairs", label: "Repairs", icon: "repairs", path: "/repairs" },
            ],
        },
        {
            label: "Insights",
            items: [
                { id: "reports", label: "Reports", icon: "reports", path: "/reports" },
            ],
        },
        {
            label: "Administration",
            items: [
                { id: "users", label: "Users", icon: "users", path: "/users" },
                { id: "health", label: "System Health", icon: "health", path: "/health" },
            ],
        },
    ],
};

// ── Sidebar component ──────────────────────────────────────────────────────────
function Sidebar({ expanded, onToggle }: { expanded: boolean; onToggle: () => void }) {
    const { user } = useAuth();
    const rawRole = user?.role?.toLowerCase() ?? "cashier";
    const role: "cashier" | "manager" | "admin" = ["cashier", "manager", "admin"].includes(rawRole) ? (rawRole as any) : "cashier";
    const groups = NAV_BY_ROLE[role];
    const [collapsedGroups, setCollapsedGroups] = useState<Record<string, boolean>>({});

    const toggleGroup = (label: string) =>
        setCollapsedGroups((prev) => ({ ...prev, [label]: !prev[label] }));

    return (
        <aside className={`sidebar ${expanded ? "" : "collapsed"}`}>
            <NavLink to={role === 'admin' ? '/dashboard/admin' : role === 'manager' ? '/dashboard/manager' : '/checkout'} className="logo-link">
                <div className="logo-area">
                    <div className="logo-icon"><Icon name="monitor" size={16} /></div>
                    {expanded && (
                        <div className="logo-text">
                            <div className="brand">AyiyaPOS</div>
                        </div>
                    )}
                </div>
            </NavLink>

            <nav className="nav">
                {groups.map((group) => {
                    const isCollapsed = collapsedGroups[group.label];
                    return (
                        <div key={group.label} className="group">
                            {expanded && (
                                <button className="group-label" onClick={() => toggleGroup(group.label)}>
                                    {group.label}
                                    <span className={`chevron ${isCollapsed ? "closed" : "open"}`}>
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
                                        className={({ isActive }) =>
                                            `nav-link ${isActive ? "active" : ""}`
                                        }
                                    >
                                        <span className="icon"><Icon name={item.icon} size={16} /></span>
                                        {expanded && <span className="label">{item.label}</span>}
                                    </NavLink>
                                ))}
                        </div>
                    );
                })}
            </nav>

            <div className="toggle-area">
                <button className="toggle-btn" onClick={onToggle} title={expanded ? "Collapse sidebar" : "Expand sidebar"}>
                    <Icon name={expanded ? "chevronLeft" : "chevronRight"} size={14} />
                </button>
            </div>
        </aside>
    );
}

function TopBar() {
    const { user, logout } = useAuth();
    const displayName = user?.fullName || "User";
    const initials = displayName.split(" ").map((n: string) => n[0]).join("").toUpperCase().slice(0, 2);
    const role = user?.role || "cashier";
    const register = user?.assignedRegisterId || "No Register";

    const handleLogout = async () => { await logout(); };

    return (
        <header className="topbar">
            <div className="left">
                <div className="register-badge"><Icon name="monitor" size={13} /> {register}</div>
                <div className="online-badge"><span className="dot" /> Online</div>
            </div>
            <div className="right">
                <button className="notif-btn"><Icon name="bell" size={16} /><span className="notif-dot" /></button>
                <div className="user-block">
                    <div className="avatar">{initials}</div>
                    <div className="user-info">
                        <div className="name">{displayName}</div>
                        <div className="role">{role}</div>
                    </div>
                </div>
                <div className="divider" />
                <button className="logout-btn" onClick={handleLogout}>
                    <Icon name="logout" size={14} /> Log out
                </button>
            </div>
        </header>
    );
}

export default function DashboardLayout() {
    const [sidebarExpanded, setSidebarExpanded] = useState(true);
    return (
        <div className="dashboard-layout">
            <Sidebar expanded={sidebarExpanded} onToggle={() => setSidebarExpanded((v) => !v)} />
            <div className="main">
                <TopBar />
                <main className="page-content"><Outlet /></main>
            </div>
        </div>
    );
}
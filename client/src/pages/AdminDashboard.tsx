import { useState, useEffect } from "react";
import { useAuth } from "../hooks/useAuth";
import LoadingScreen from "../components/LoadingScreen";
import "./AdminDashboard.css";

type StatusLevel = "ok" | "warn" | "error";

interface ServiceStatus {
    name: string;
    label: string;
    status: StatusLevel;
    detail: string;
    meta: string;
    latency?: string;
}

interface AuditEntry {
    id: number;
    ts: string;
    user: string;
    action: string;
    details: string;
    level: "info" | "warn" | "error";
}

// ── Static config (icons, colours) ──────────────────────────────────────────
const STATUS_CFG = {
    ok: {
        icon: (
            <svg viewBox="0 0 16 16" fill="none" className="status-icon" aria-hidden>
                <circle cx="8" cy="8" r="7.5" stroke="#22c55e" strokeWidth="1" />
                <path d="M5 8.5l2 2 4-4" stroke="#22c55e" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
        ),
        dotClass: "dot-ok",
        textClass: "status-text-ok",
        pulseClass: "pulse-ok",
    },
    warn: {
        icon: (
            <svg viewBox="0 0 16 16" fill="none" className="status-icon" aria-hidden>
                <path d="M8 2L14.5 13.5H1.5L8 2Z" stroke="#f59e0b" strokeWidth="1" strokeLinejoin="round" />
                <path d="M8 6.5v3M8 11h.01" stroke="#f59e0b" strokeWidth="1.5" strokeLinecap="round" />
            </svg>
        ),
        dotClass: "dot-warn",
        textClass: "status-text-warn",
        pulseClass: "pulse-warn",
    },
    error: {
        icon: (
            <svg viewBox="0 0 16 16" fill="none" className="status-icon" aria-hidden>
                <circle cx="8" cy="8" r="7.5" stroke="#ef4444" strokeWidth="1" />
                <path d="M5.5 5.5l5 5M10.5 5.5l-5 5" stroke="#ef4444" strokeWidth="1.5" strokeLinecap="round" />
            </svg>
        ),
        dotClass: "dot-err",
        textClass: "status-text-err",
        pulseClass: "pulse-err",
    },
};

const LEVEL_CFG = {
    info: { color: "level-info", label: "INFO" },
    warn: { color: "level-warn", label: "WARN" },
    error: { color: "level-error", label: "ERR " },
};

// ── Sub‑components ──────────────────────────────────────────────────────────
function PulsingDot({ status }: { status: StatusLevel }) {
    const cfg = STATUS_CFG[status];
    return (
        <span className="pulsing-dot">
      {status !== "error" && <span className={`pulse ${cfg.pulseClass}`} />}
            <span className={`dot ${cfg.dotClass}`} />
    </span>
    );
}

function HealthScore({ services }: { services: ServiceStatus[] }) {
    if (services.length === 0) return <div className="no-data">No data</div>;
    const score = Math.round((services.filter((s) => s.status === "ok").length / services.length) * 100);
    const arc = 2 * Math.PI * 28;
    const offset = arc - (arc * score) / 100;
    const color = score >= 90 ? "#22c55e" : score >= 70 ? "#f59e0b" : "#ef4444";

    return (
        <div className="health-score">
            <svg width="64" height="64" viewBox="0 0 64 64" className="score-ring">
                <circle cx="32" cy="32" r="28" fill="none" stroke="#1e2d45" strokeWidth="6" />
                <circle
                    cx="32" cy="32" r="28"
                    fill="none"
                    stroke={color}
                    strokeWidth="6"
                    strokeDasharray={arc}
                    strokeDashoffset={offset}
                    strokeLinecap="round"
                    style={{ transition: "stroke-dashoffset 1s ease" }}
                />
            </svg>
            <div>
                <div className="score-number">{score}%</div>
                <div className="score-label">uptime score</div>
            </div>
        </div>
    );
}

function ServiceCard({ svc }: { svc: ServiceStatus }) {
    const cfg = STATUS_CFG[svc.status];
    return (
        <div className="service-card">
            <div className="card-header">
                <span className="service-label">{svc.label}</span>
                {cfg.icon}
            </div>
            <div className="status-row">
                <PulsingDot status={svc.status} />
                <span className={`status-text ${cfg.textClass}`}>{svc.detail}</span>
            </div>
            <div className="card-footer">
                <span className="meta">{svc.meta}</span>
                {svc.latency && <span className="latency">{svc.latency}</span>}
            </div>
        </div>
    );
}

function AuditTable({ entries }: { entries: AuditEntry[] }) {
    return (
        <div className="audit-table-wrapper">
            <table className="audit-table">
                <thead>
                <tr>
                    <th>Timestamp</th><th>User</th><th>Action</th><th>Details</th><th>Level</th>
                </tr>
                </thead>
                <tbody>
                {entries.map((row, i) => {
                    const lv = LEVEL_CFG[row.level] || LEVEL_CFG.info;
                    return (
                        <tr key={row.id} className={i % 2 === 0 ? "row-even" : "row-odd"}>
                            <td>{row.ts}</td>
                            <td title={row.user}>{row.user}</td>
                            <td>{row.action}</td>
                            <td>{row.details}</td>
                            <td><span className={`level-badge ${lv.color}`}>{lv.label}</span></td>
                        </tr>
                    );
                })}
                </tbody>
            </table>
        </div>
    );
}

// ── Main Component ──────────────────────────────────────────────────────────
export default function AdminDashboard() {
    const { accessToken } = useAuth();
    const [services, setServices] = useState<ServiceStatus[]>([]);
    const [auditLog, setAuditLog] = useState<AuditEntry[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const fetchData = async () => {
        try {
            setError(null);
            const headers = {
                'Authorization': `Bearer ${accessToken}`,
                'Content-Type': 'application/json',
            };

            const healthRes = await fetch('/api/admin/health', {
                headers,
                credentials: 'include',
            });
            if (!healthRes.ok) throw new Error('Failed to fetch health status');
            const healthData = await healthRes.json();
            setServices(healthData);

            const auditRes = await fetch('/api/admin/audit?limit=20', {
                headers,
                credentials: 'include',
            });
            if (!auditRes.ok) throw new Error('Failed to fetch audit log');
            const auditData = await auditRes.json();
            setAuditLog(auditData);
        } catch (err: any) {
            setError(err.message || 'Failed to load dashboard data');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchData();
        const interval = setInterval(fetchData, 30000);
        return () => clearInterval(interval);
    }, []);

    const now = new Date().toLocaleTimeString("en-KE", { hour12: false });
    const okCount = services.filter((s) => s.status === "ok").length;
    const warnCount = services.filter((s) => s.status === "warn").length;
    const errCount = services.filter((s) => s.status === "error").length;

    if (loading) {
        return <LoadingScreen message="Loading dashboard..." />;
    }

    if (error) {
        return (
            <div className="dashboard-error">
                <div>Error: {error}</div>
                <button onClick={fetchData} className="retry-btn">Retry</button>
            </div>
        );
    }

    return (
        <div className="admin-dashboard">
            {/* Header */}
            <div className="header">
                <div className="header-left">
                    <div className="system-label">System Operations</div>
                    <h1>Admin Dashboard</h1>
                </div>
                <div className="header-right">
                    <div className="status-badges">
                        <span className="badge ok"><span className="dot" /> {okCount} OK</span>
                        <span className="badge warn"><span className="dot" /> {warnCount} WARN</span>
                        <span className="badge err"><span className="dot" /> {errCount} ERR</span>
                    </div>
                    <div className="timestamp">{now}</div>
                </div>
            </div>

            {/* Health + Services */}
            <div className="health-grid">
                <div className="health-score-panel">
                    <span className="label">Health Score</span>
                    <div className="score-wrapper"><HealthScore services={services} /></div>
                    <div className="score-footer">{services.length} integrations monitored</div>
                </div>
                <div className="service-grid">
                    {services.map((svc) => <ServiceCard key={svc.name} svc={svc} />)}
                </div>
            </div>

            {/* Audit log */}
            <div className="audit-panel">
                <div className="audit-header">
                    <div className="left">
                        <span className="title">Live Audit Feed</span>
                        <span className="live-indicator"><span className="pulse-dot" /> Live</span>
                    </div>
                    <span className="count">{auditLog.length} recent entries</span>
                </div>
                <AuditTable entries={auditLog} />
            </div>
        </div>
    );
}
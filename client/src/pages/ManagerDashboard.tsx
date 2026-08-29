import { useState, useEffect } from "react";
import { useAuth } from "../hooks/useAuth";
import "./ManagerDashboard.css";

// ── Types ──────────────────────────────────────────────────────────────────
interface Register {
    id: string;
    name: string;
    cashier: string;
    status: "Open" | "Closed";
    expected: number;
    counted: number;
}

interface StockAlert {
    id: string;
    name: string;
    sku: string;
    current: number;
    threshold: number;
}

interface PendingApproval {
    id: string;
    transactionId: string;
    amount: number;
    reason: string;
    time: string;
    type: "Refund" | "Void";
}

interface Summary {
    todaySales: number;
    totalOrders: number;
    avgOrderValue: number;
    activeRegisters: number;
    totalRegisters: number;
}

// ── Helpers ────────────────────────────────────────────────────────────────
const fmt = (n: number) =>
    n.toLocaleString("en-KE", { style: "currency", currency: "KES", minimumFractionDigits: 2 });
// ── KpiCard (using classes) ─────────────────────────────────────────────
function KpiCard({
                     label,
                     value,
                     trend,
                     trendLabel,
                     sub,
                     accent,
                 }: {
    label: string;
    value: string;
    trend?: "up" | "down" | "neutral";
    trendLabel?: string;
    sub?: string;
    accent?: string;
}) {
    return (
        <div className="kpi-card">
            {accent && <div className="accent-bar" style={{ background: accent }} />}
            <span className="label">{label}</span>
            <span className="value">{value}</span>
            <div className="footer">
                {trend && trendLabel && (
                    <span className={`trend ${trend}`}>
            {trend === "up" ? "↑" : trend === "down" ? "↓" : "→"} {trendLabel}
          </span>
                )}
                {sub && <span className="sub">{sub}</span>}
            </div>
        </div>
    );
}

function cashDelta(expected: number, counted: number) {
    const diff = counted - expected;
    const pct = expected === 0 ? 0 : Math.abs(diff / expected) * 100;
    return { diff, pct };
}

// ── ActionButton (using classes) ─────────────────────────────────────────
interface ActionButtonProps {
    label: string;
    onClick: () => void | Promise<void>;
    active?: boolean;
    type: "approve" | "reject";
}

function ActionButton({ label, onClick, active, type }: ActionButtonProps) {
    return (
        <button
            className={`action-btn ${type} ${active ? "active" : ""}`}
            onClick={onClick}
            disabled={active}
        >
            {active ? (type === "approve" ? "✓" : "✕") : label}
        </button>
    );
}

// ── CashBar (using classes) ──────────────────────────────────────────────
function CashBar({ expected, counted }: { expected: number; counted: number }) {
    const { diff, pct } = cashDelta(expected, counted);
    const isOver = diff > 0;
    const isBalanced = Math.abs(diff) < 0.01;
    const fillClass = isBalanced ? "balanced" : pct > 2 ? "under" : "warn";
    const ratio = Math.min(counted / Math.max(expected, 1), 1.2);

    return (
        <div className="cash-bar">
            <div className="bar-track">
                <div className={`bar-fill ${fillClass}`} style={{ width: `${Math.min(ratio * 100, 100)}%` }} />
            </div>
            <div className="bar-labels">
                <span className="expected">Exp {fmt(expected)}</span>
                <span className={`diff ${fillClass}`}>
          {isBalanced ? "Balanced" : `${isOver ? "+" : ""}${fmt(diff)}`}
        </span>
            </div>
        </div>
    );
}

// ── RegisterRow (using classes) ───────────────────────────────────────────
function RegisterRow({ reg }: { reg: Register }) {
    const isOpen = reg.status === "Open";
    return (
        <div className="register-row">
            <div className="info">
                <div className="top">
                    <span className={`status-dot ${isOpen ? "open" : "closed"}`} />
                    <span className="register-name">{reg.name}</span>
                    <span className={`status-badge ${isOpen ? "open" : "closed"}`}>{reg.status}</span>
                </div>
                <span className="cashier">{reg.cashier}</span>
                <div className="cash-bar-wrapper">
                    <CashBar expected={reg.expected} counted={reg.counted} />
                </div>
            </div>
            <button className="action-btn view-details">View Details →</button>
        </div>
    );
}

// ── StockRow (using classes) ──────────────────────────────────────────────
function StockRow({ item }: { item: StockAlert }) {
    const ratio = item.threshold === 0 ? 0 : item.current / item.threshold;
    const isCritical = item.current === 0 || ratio < 0.25;
    const statusClass = isCritical ? "critical" : "warn";

    return (
        <div className={`stock-row ${statusClass}`}>
            <div className="info">
                <span className="name">{item.name}</span>
                <span className="sku">{item.sku}</span>
            </div>
            <div className="qty">
                <span className={`current ${statusClass}`}>{item.current}</span>
                <span className="threshold">/ {item.threshold} threshold</span>
            </div>
        </div>
    );
}

// ── Main Component ────────────────────────────────────────────────────────
export default function ManagerDashboard() {
    const { accessToken } = useAuth();
    const [summary, setSummary] = useState<Summary | null>(null);
    const [registers, setRegisters] = useState<Register[]>([]);
    const [stockAlerts, setStockAlerts] = useState<StockAlert[]>([]);
    const [approvals, setApprovals] = useState<PendingApproval[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const [settled, setSettled] = useState<Record<string, "approved" | "rejected">>({});

    const fetchData = async () => {
        try {
            setError(null);
            const headers = {
                'Authorization': `Bearer ${accessToken}`,
                'Content-Type': 'application/json',
            };

            const [summaryRes, registersRes, stockRes, approvalsRes] = await Promise.all([
                fetch('/api/manager/dashboard/summary', { headers, credentials: 'include' }),
                fetch('/api/manager/dashboard/registers', { headers, credentials: 'include' }),
                fetch('/api/manager/dashboard/stock-alerts', { headers, credentials: 'include' }),
                fetch('/api/manager/dashboard/pending-approvals', { headers, credentials: 'include' }),
            ]);

            if (!summaryRes.ok || !registersRes.ok || !stockRes.ok || !approvalsRes.ok) {
                throw new Error("Failed to fetch dashboard data");
            }

            const summaryData = await summaryRes.json();
            const registersData = await registersRes.json();
            const stockData = await stockRes.json();
            const approvalsData = await approvalsRes.json();

            setSummary(summaryData);
            setRegisters(registersData);
            setStockAlerts(stockData);
            setApprovals(approvalsData);
        } catch (err: any) {
            setError(err.message || "Failed to load dashboard");
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchData();
        const interval = setInterval(fetchData, 30000); // refresh every 30s
        return () => clearInterval(interval);
    }, []);

    const settle = async (id: string, action: "approved" | "rejected") => {
        setSettled((s) => ({ ...s, [id]: action }));
        try {
            const endpoint = action === "approved" ? "approve" : "reject";
            await fetch(`/api/manager/dashboard/approvals/${id}/${endpoint}`, {
                method: "POST",
                credentials: "include",
            });
            // Remove from list after a short delay
            setTimeout(() => {
                setApprovals((a) => a.filter((p) => p.id !== id));
                setSettled((s) => {
                    const next = { ...s };
                    delete next[id];
                    return next;
                });
            }, 900);
        } catch {
            // If API fails, revert UI state
            setSettled((s) => {
                const next = { ...s };
                delete next[id];
                return next;
            });
        }
    };

    if (loading) {
        return (
            <div className="min-h-full bg-[#080b10] flex items-center justify-center">
                <div className="text-[#e8edf5] text-lg font-mono">Loading dashboard...</div>
            </div>
        );
    }

    if (error || !summary) {
        return (
            <div className="min-h-full bg-[#080b10] flex items-center justify-center">
                <div className="text-red-500 text-lg font-mono">
                    Error: {error || "No data"}
                    <button
                        onClick={fetchData}
                        className="ml-4 px-4 py-2 bg-[#1e2d45] text-[#e8edf5] rounded hover:bg-[#243148] transition"
                    >
                        Retry
                    </button>
                </div>
            </div>
        );
    }

    return (
        <div className="manager-dashboard">
            {/* Header */}
            <div className="header">
                <div className="left">
                    <div className="live-indicator">
                        <span className="dot" />
                        <span className="label">Live · {new Date().toLocaleDateString("en-KE", { weekday: "long", day: "numeric", month: "short", year: "numeric" })}</span>
                    </div>
                    <h1>Manager Dashboard</h1>
                </div>
                <div className="right">
                    <div className="updated-label">Last updated</div>
                    <div className="updated-time">{new Date().toLocaleTimeString("en-KE", { hour12: false })}</div>
                </div>
            </div>

            {/* KPI Cards */}
            <div className="kpi-grid">
                <KpiCard label="Today's Sales" value={fmt(summary.todaySales)} trend="up" trendLabel="+12% vs yesterday" accent="var(--status-green)" />
                <KpiCard label="Total Orders" value={summary.totalOrders.toString()} trend="up" trendLabel="+8% vs yesterday" accent="var(--primary)" />
                <KpiCard label="Avg Order Value" value={fmt(summary.avgOrderValue)} trend="down" trendLabel="−3% vs yesterday" accent="var(--status-yellow)" />
                <KpiCard label="Active Registers" value={`${summary.activeRegisters} / ${summary.totalRegisters}`} sub="registers online" accent="var(--status-green)" />
            </div>

            {/* Middle Section */}
            <div className="middle-grid">
                {/* Register Status */}
                <div className="panel">
                    <div className="panel-header">
                        <h2 className="title">Register Status</h2>
                        <span className="badge green">{summary.activeRegisters} Open</span>
                    </div>
                    {registers.map((reg) => <RegisterRow key={reg.id} reg={reg} />)}
                </div>

                {/* Low Stock Alerts */}
                <div className="panel">
                    <div className="panel-header">
                        <h2 className="title">Low Stock Alerts</h2>
                        <span className="badge red">{stockAlerts.filter((s) => s.current === 0).length} out of stock</span>
                    </div>
                    <div className="stock-list">
                        {stockAlerts.map((item) => <StockRow key={item.id} item={item} />)}
                    </div>
                </div>
            </div>

            {/* Pending Approvals */}
            <div className="panel full-width">
                <div className="panel-header">
                    <h2 className="title">Pending Approvals</h2>
                    {approvals.length > 0 && <span className="badge red">{approvals.length} pending</span>}
                </div>

                {approvals.length === 0 ? (
                    <div className="empty-state">No pending approvals</div>
                ) : (
                    <div className="approvals-table-wrapper">
                        <table className="approvals-table">
                            <thead>
                            <tr>
                                <th>Transaction ID</th><th>Type</th><th>Amount</th><th>Reason</th><th>Time</th><th></th>
                            </tr>
                            </thead>
                            <tbody>
                            {approvals.map((p) => {
                                const state = settled[p.id];
                                return (
                                    <tr key={p.id} style={{ opacity: state ? 0 : 1, transition: "opacity 0.6s ease" }}>
                                        <td className="txn-id">{p.transactionId}</td>
                                        <td><span className={`type-badge ${p.type.toLowerCase()}`}>{p.type}</span></td>
                                        <td className="amount">{fmt(p.amount)}</td>
                                        <td className="reason">{p.reason}</td>
                                        <td className="time">{p.time}</td>
                                        <td>
                                            <div className="actions">
                                                <ActionButton
                                                    label="Approve"
                                                    type="approve"
                                                    onClick={() => settle(p.id, "approved")}
                                                    active={state === "approved"}
                                                />
                                                <ActionButton
                                                    label="Reject"
                                                    type="reject"
                                                    onClick={() => settle(p.id, "rejected")}
                                                    active={state === "rejected"}
                                                />
                                            </div>
                                        </td>
                                    </tr>
                                );
                            })}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>
        </div>
    );
}
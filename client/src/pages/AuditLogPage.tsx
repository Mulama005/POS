import { useState, useEffect } from "react";
import { useAuth } from "../hooks/useAuth";
import "./AuditLogPage.css";

interface AuditEntry {
    id: string;
    timestamp: string;
    userName: string;
    actionType: string;
    entityName: string;
    entityId: string;
    details: string;
    ipAddress: string;
}

export default function AuditLogPage() {
    const { accessToken } = useAuth();
    const [entries, setEntries] = useState<AuditEntry[]>([]);
    const [loading, setLoading] = useState(true);
    const [total, setTotal] = useState(0);
    const [page, setPage] = useState(1);
    const [pageSize] = useState(50);
    const [filters, setFilters] = useState({
        userName: "",
        userId: "",
        actionType: "",
        fromDate: "",
        toDate: "",
    });

    const fetchAuditLog = async () => {
        setLoading(true);
        try {
            const params = new URLSearchParams({
                page: page.toString(),
                pageSize: pageSize.toString(),
                ...(filters.userName && { userName: filters.userName }),
                ...(filters.userId && { userId: filters.userId }),
                ...(filters.actionType && { actionType: filters.actionType }),
                ...(filters.fromDate && { fromDate: filters.fromDate }),
                ...(filters.toDate && { toDate: filters.toDate }),
            });

            const res = await fetch(`/api/audit?${params}`, {
                headers: { Authorization: `Bearer ${accessToken}` },
                credentials: "include",
            });

            if (!res.ok) throw new Error("Failed to fetch audit log");
            const data = await res.json();
            setEntries(data.items);
            setTotal(data.total);
        } catch (err) {
            console.error(err);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchAuditLog();
    }, [page, filters]);

    const totalPages = Math.ceil(total / pageSize);
    const visiblePages = Math.max(totalPages, 1);

    return (
        <div className="audit-page">
            <header className="audit-header">
                <div>
                    <p className="audit-eyebrow">System monitoring</p>
                    <h1>Audit log</h1>
                    <p className="audit-subtitle">Review staff activity and important changes across the system.</p>
                </div>
                <div className="audit-count"><strong>{total.toLocaleString()}</strong><span>record{total === 1 ? "" : "s"}</span></div>
            </header>

            {/* Filters */}
            <section className="audit-filter-card" aria-label="Audit log filters">
                <div className="audit-filter-heading"><h2>Filter activity</h2><span>Use one or more filters to narrow the record.</span></div>
            <div className="audit-filters">
                <label>Staff member<input
                    type="text"
                    placeholder="Search by name"
                    value={filters.userName}
                    onChange={(e) => setFilters((f) => ({ ...f, userName: e.target.value }))}
                /></label>
                <label>User ID<input
                    type="text"
                    placeholder="Exact user ID"
                    value={filters.userId}
                    onChange={(e) => setFilters((f) => ({ ...f, userId: e.target.value }))}
                /></label>
                <label>Action<input
                    type="text"
                    placeholder="Action Type"
                    value={filters.actionType}
                    onChange={(e) => setFilters((f) => ({ ...f, actionType: e.target.value }))}
                /></label>
                <label>From<input
                    type="date"
                    value={filters.fromDate}
                    onChange={(e) => setFilters((f) => ({ ...f, fromDate: e.target.value }))}
                /></label>
                <label>To<input
                    type="date"
                    value={filters.toDate}
                    onChange={(e) => setFilters((f) => ({ ...f, toDate: e.target.value }))}
                /></label>
                <button type="button" onClick={() => setPage(1)}>Refresh</button>
            </div>
            </section>

            {/* Table */}
            {loading ? (
                <div className="audit-loading">Loading audit activity…</div>
            ) : (
                <section className="audit-table-card">
                    <div className="audit-table-scroll"><table className="audit-table">
                        <thead>
                        <tr>
                            <th>Timestamp</th>
                            <th>User</th>
                            <th>Action</th>
                            <th>Entity</th>
                            <th>Details</th>
                            <th>IP</th>
                        </tr>
                        </thead>
                        <tbody>
                        {entries.map((e) => (
                            <tr key={e.id}>
                                <td>{new Date(e.timestamp).toLocaleString()}</td>
                                <td>{e.userName}</td>
                                <td><span className="audit-action">{e.actionType}</span></td>
                                <td>{e.entityName}</td>
                                <td>{e.details}</td>
                                <td>{e.ipAddress}</td>
                            </tr>
                        ))}
                        {entries.length === 0 && <tr><td colSpan={6} className="audit-empty">No audit entries match these filters.</td></tr>}
                        </tbody>
                    </table></div>

                    {/* Pagination */}
                    <div className="pagination">
                        <button disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</button>
                        <span>Page {page} of {visiblePages}</span>
                        <button disabled={page >= visiblePages} onClick={() => setPage((p) => p + 1)}>Next</button>
                    </div>
                </section>
            )}
        </div>
    );
}

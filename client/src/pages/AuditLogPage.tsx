import { useState, useEffect } from "react";
import { useAuth } from "../hooks/useAuth";

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

    return (
        <div className="audit-page">
            <h1>Audit Log</h1>

            {/* Filters */}
            <div className="audit-filters">
                <input
                    type="text"
                    placeholder="User ID"
                    value={filters.userId}
                    onChange={(e) => setFilters((f) => ({ ...f, userId: e.target.value }))}
                />
                <input
                    type="text"
                    placeholder="Action Type"
                    value={filters.actionType}
                    onChange={(e) => setFilters((f) => ({ ...f, actionType: e.target.value }))}
                />
                <input
                    type="date"
                    value={filters.fromDate}
                    onChange={(e) => setFilters((f) => ({ ...f, fromDate: e.target.value }))}
                />
                <input
                    type="date"
                    value={filters.toDate}
                    onChange={(e) => setFilters((f) => ({ ...f, toDate: e.target.value }))}
                />
                <button onClick={() => setPage(1)}>Apply Filters</button>
            </div>

            {/* Table */}
            {loading ? (
                <div>Loading...</div>
            ) : (
                <>
                    <table className="audit-table">
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
                                <td><strong>{e.actionType}</strong></td>
                                <td>{e.entityName}</td>
                                <td>{e.details}</td>
                                <td>{e.ipAddress}</td>
                            </tr>
                        ))}
                        </tbody>
                    </table>

                    {/* Pagination */}
                    <div className="pagination">
                        <button disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</button>
                        <span>Page {page} of {totalPages}</span>
                        <button disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>Next</button>
                    </div>
                </>
            )}
        </div>
    );
}
import { useState, useEffect } from "react";
import { apiClient } from "../services/apiClient";
import { downloadBlob } from "../utils/downloadFile";
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

interface AuditLogResponse {
    items: AuditEntry[];
    total: number;
}

const nairobiDateTime = new Intl.DateTimeFormat("en-KE", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "Africa/Nairobi",
});

export default function AuditLogPage() {
    const [entries, setEntries] = useState<AuditEntry[]>([]);
    const [loading, setLoading] = useState(true);
    const [loadError, setLoadError] = useState("");
    const [exporting, setExporting] = useState(false);
    const [exportError, setExportError] = useState("");
    const [exportNotice, setExportNotice] = useState("");
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
        setLoadError("");
        try {
            const { data } = await apiClient.get<AuditLogResponse>("/api/audit", {
                params: {
                    page,
                    pageSize,
                    userName: filters.userName || undefined,
                    userId: filters.userId || undefined,
                    actionType: filters.actionType || undefined,
                    fromDate: filters.fromDate || undefined,
                    toDate: filters.toDate || undefined,
                },
            });
            setEntries(data.items);
            setTotal(data.total);
        } catch {
            setLoadError("Could not load audit activity. Please try again.");
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchAuditLog();
    }, [page, filters]);

    const totalPages = Math.ceil(total / pageSize);
    const visiblePages = Math.max(totalPages, 1);

    const downloadPdf = async () => {
        setExporting(true);
        setExportError("");
        setExportNotice("Gathering your audit trail and preparing the PDF…");
        try {
            const { data } = await apiClient.get<Blob>("/api/audit/export/pdf", {
                params: {
                    userName: filters.userName || undefined,
                    userId: filters.userId || undefined,
                    actionType: filters.actionType || undefined,
                    fromDate: filters.fromDate || undefined,
                    toDate: filters.toDate || undefined,
                },
                responseType: "blob",
            });
            const pdfSignature = await data.slice(0, 5).text();
            if (pdfSignature !== "%PDF-") {
                throw new Error("The audit export endpoint did not return a PDF.");
            }
            downloadBlob(data, `EddTechPOS-audit-log-${new Date().toISOString().slice(0, 10)}.pdf`);
            setExportNotice("Your audit PDF is downloading now.");
            window.setTimeout(() => setExportNotice(""), 4500);
        } catch (err) {
            console.error(err);
            setExportError("We could not create the audit PDF. Please try again.");
            setExportNotice("That PDF didn’t come through. Please try again.");
            window.setTimeout(() => setExportNotice(""), 6000);
        } finally {
            setExporting(false);
        }
    };

    return (
        <div className="audit-page">
            {exportNotice && <div className="export-toast" role="status" aria-live="polite"><span className="export-toast__mark">↓</span>{exportNotice}</div>}
            <header className="audit-header">
                <div className="audit-heading-copy">
                    <p className="audit-eyebrow">System monitoring</p>
                    <h1>Audit log</h1>
                    <p className="audit-subtitle">Review staff activity and important changes across the system.</p>
                </div>
                <div className="audit-header-actions">
                    <div className="audit-count"><strong>{total.toLocaleString()}</strong><span>record{total === 1 ? "" : "s"}</span></div>
                    <button type="button" className="audit-export" onClick={() => void downloadPdf()} disabled={exporting}>
                        {exporting ? "Preparing PDF…" : "Download PDF"}
                    </button>
                </div>
            </header>
            {loadError && <p className="audit-export-error" role="alert">{loadError}</p>}
            {exportError && <p className="audit-export-error" role="alert">{exportError}</p>}

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
                <button type="button" onClick={() => { setPage(1); void fetchAuditLog(); }}>Refresh</button>
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
                            <th>Timestamp · EAT</th>
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
                                <td>{nairobiDateTime.format(new Date(e.timestamp))}</td>
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

import { useState, useEffect } from "react";
import { useAuth } from "../hooks/useAuth";
import LoadingScreen from "../components/LoadingScreen";

// ── Types ────────────────────────────────────────────────────────────────
interface SalesReport {
    totalSales: number;
    totalOrders: number;
    avgOrderValue: number;
    topProducts: Array<{ productName: string; totalSold: number; totalRevenue: number }>;
    trend: Array<{ date: string; total: number; orders: number }>;
}

interface InventoryReport {
    totalItems: number;
    totalValue: number;
    lowStock: Array<{ name: string; sku: string; stock: number; threshold: number }>;
    warrantyStatus: Array<{ status: string; count: number }>;
}

interface FinancialReport {
    totalRevenue: number;
    totalTax: number;
    totalDiscounts: number;
    outstandingCredit: number;
    netRevenue: number;
}

interface StaffPerformance {
    cashierName: string;
    totalSales: number;
    totalOrders: number;
    avgOrder: number;
    totalItems: number;
}

// ── Main Component ────────────────────────────────────────────────────────
export default function ReportsPage() {
    const { accessToken } = useAuth();
    const [salesData, setSalesData] = useState<SalesReport | null>(null);
    const [inventoryData, setInventoryData] = useState<InventoryReport | null>(null);
    const [financialData, setFinancialData] = useState<FinancialReport | null>(null);
    const [staffData, setStaffData] = useState<StaffPerformance[]>([]);
    const [loading, setLoading] = useState(true);
    const [exporting, setExporting] = useState<{ [key: string]: boolean }>({});
    const [dateRange, setDateRange] = useState({ from: "", to: "" });

    // ── Data fetching ──────────────────────────────────────────────────────
    const fetchReports = async () => {
        setLoading(true);
        try {
            const params = new URLSearchParams();
            if (dateRange.from) params.append("fromDate", dateRange.from);
            if (dateRange.to) params.append("toDate", dateRange.to);

            const [salesRes, inventoryRes, financialRes, staffRes] = await Promise.all([
                fetch(`/api/reports/sales?${params}`, {
                    headers: { Authorization: `Bearer ${accessToken}` },
                    credentials: "include",
                }),
                fetch("/api/reports/inventory", {
                    headers: { Authorization: `Bearer ${accessToken}` },
                    credentials: "include",
                }),
                fetch("/api/reports/financial", {
                    headers: { Authorization: `Bearer ${accessToken}` },
                    credentials: "include",
                }),
                fetch(`/api/reports/staff?${params}`, {
                    headers: { Authorization: `Bearer ${accessToken}` },
                    credentials: "include",
                }),
            ]);

            if (salesRes.ok) setSalesData(await salesRes.json());
            if (inventoryRes.ok) setInventoryData(await inventoryRes.json());
            if (financialRes.ok) setFinancialData(await financialRes.json());
            if (staffRes.ok) setStaffData(await staffRes.json());
        } catch (err) {
            console.error(err);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchReports();
    }, [dateRange]);

    // ── Export handler ──────────────────────────────────────────────────────
    const handleExport = async (endpoint: string, filename: string, format: "csv" | "pdf") => {
        const key = `${endpoint}-${format}`;
        setExporting((prev) => ({ ...prev, [key]: true }));

        try {
            const params = new URLSearchParams({ format });
            if (dateRange.from) params.append("fromDate", dateRange.from);
            if (dateRange.to) params.append("toDate", dateRange.to);

            const response = await fetch(`/api/reports/export/${endpoint}?${params}`, {
                headers: { Authorization: `Bearer ${accessToken}` },
                credentials: "include",
            });

            if (!response.ok) throw new Error("Export failed");

            const blob = await response.blob();
            const url = URL.createObjectURL(blob);
            const link = document.createElement("a");
            link.href = url;
            link.download = filename;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        } catch (err) {
            console.error(err);
            alert("Failed to download report. Please try again.");
        } finally {
            setExporting((prev) => ({ ...prev, [key]: false }));
        }
    };

    // ── Loading / error states ──────────────────────────────────────────────
    if (loading || !salesData || !inventoryData || !financialData) {
        return <LoadingScreen message="Loading reports..." />;
    }

    // ── Render ──────────────────────────────────────────────────────────────
    return (
        <div className="reports-page">
            <div className="page-header">
                <h1>Analytics & Reports</h1>
                <div className="date-filter">
                    <input
                        type="date"
                        value={dateRange.from}
                        onChange={(e) => setDateRange((d) => ({ ...d, from: e.target.value }))}
                    />
                    <input
                        type="date"
                        value={dateRange.to}
                        onChange={(e) => setDateRange((d) => ({ ...d, to: e.target.value }))}
                    />
                    <button onClick={fetchReports}>Apply</button>
                </div>
            </div>

            {/* ── Sales Performance ── */}
            <section className="report-section">
                <div className="section-header">
                    <h2>Sales Performance</h2>
                    <div className="export-buttons">
                        <button
                            className="btn-export"
                            onClick={() => handleExport("sales", `sales_report_${dateRange.from || "all"}.csv`, "csv")}
                            disabled={exporting["sales-csv"]}
                        >
                            {exporting["sales-csv"] ? "Generating..." : "CSV"}
                        </button>
                        <button
                            className="btn-export"
                            onClick={() => handleExport("sales", `sales_report_${dateRange.from || "all"}.pdf`, "pdf")}
                            disabled={exporting["sales-pdf"]}
                        >
                            {exporting["sales-pdf"] ? "Generating..." : "PDF"}
                        </button>
                    </div>
                </div>

                <div className="kpi-grid">
                    <div className="kpi-card">
                        <span className="label">Total Sales</span>
                        <span className="value">KES {salesData.totalSales.toFixed(2)}</span>
                    </div>
                    <div className="kpi-card">
                        <span className="label">Total Orders</span>
                        <span className="value">{salesData.totalOrders}</span>
                    </div>
                    <div className="kpi-card">
                        <span className="label">Average Order</span>
                        <span className="value">KES {salesData.avgOrderValue.toFixed(2)}</span>
                    </div>
                </div>

                <h3>Top Selling Products</h3>
                <table>
                    <thead><tr><th>Product</th><th>Sold</th><th>Revenue</th></tr></thead>
                    <tbody>
                    {salesData.topProducts.map((p) => (
                        <tr key={p.productName}>
                            <td>{p.productName}</td>
                            <td>{p.totalSold}</td>
                            <td>KES {p.totalRevenue.toFixed(2)}</td>
                        </tr>
                    ))}
                    </tbody>
                </table>
            </section>

            {/* ── Inventory Report ── */}
            <section className="report-section">
                <div className="section-header">
                    <h2>Inventory</h2>
                    <div className="export-buttons">
                        <div className="export-buttons">
                            <button onClick={() => handleExport("inventory", `inventory_report_${dateRange.from || "all"}.csv`, "csv")}>
                                CSV
                            </button>
                            <button onClick={() => handleExport("inventory", `inventory_report_${dateRange.from || "all"}.pdf`, "pdf")}>
                                PDF
                            </button>
                        </div>
                    </div>
                </div>

                <div className="kpi-grid">
                    <div className="kpi-card">
                        <span className="label">Total Items</span>
                        <span className="value">{inventoryData.totalItems}</span>
                    </div>
                    <div className="kpi-card">
                        <span className="label">Total Value</span>
                        <span className="value">KES {inventoryData.totalValue.toFixed(2)}</span>
                    </div>
                </div>

                <h3>Low Stock Items</h3>
                {inventoryData.lowStock.length === 0 ? (
                    <p>No low stock items.</p>
                ) : (
                    <table>
                        <thead><tr><th>Name</th><th>SKU</th><th>Stock</th><th>Threshold</th></tr></thead>
                        <tbody>
                        {inventoryData.lowStock.map((item) => (
                            <tr key={item.sku}>
                                <td>{item.name}</td>
                                <td>{item.sku}</td>
                                <td style={{ color: "red" }}>{item.stock}</td>
                                <td>{item.threshold}</td>
                            </tr>
                        ))}
                        </tbody>
                    </table>
                )}
            </section>

            {/* ── Financial Report ── */}
            <section className="report-section">
                <div className="section-header">
                    <h2>Financial</h2>
                    <div className="export-buttons">
                        <button
                            className="btn-export"
                            onClick={() => handleExport("financial", `financial_report_${dateRange.from || "all"}.csv`, "csv")}
                            disabled={exporting["financial-csv"]}
                        >
                            {exporting["financial-csv"] ? "Generating..." : "CSV"}
                        </button>
                        <button
                            className="btn-export"
                            onClick={() => handleExport("financial", `financial_report_${dateRange.from || "all"}.pdf`, "pdf")}
                            disabled={exporting["financial-pdf"]}
                        >
                            {exporting["financial-pdf"] ? "Generating..." : "PDF"}
                        </button>
                    </div>
                </div>
                <div className="kpi-grid">
                    <div className="kpi-card">
                        <span className="label">Total Revenue</span>
                        <span className="value">KES {financialData.totalRevenue.toFixed(2)}</span>
                    </div>
                    <div className="kpi-card">
                        <span className="label">Net Revenue</span>
                        <span className="value">KES {financialData.netRevenue.toFixed(2)}</span>
                    </div>
                    <div className="kpi-card">
                        <span className="label">Outstanding Credit</span>
                        <span className="value">KES {financialData.outstandingCredit.toFixed(2)}</span>
                    </div>
                </div>
            </section>

            {/* ── Staff Performance ── */}
            <section className="report-section">
                <div className="section-header">
                    <h2>Staff Performance</h2>
                    <div className="export-buttons">
                        <button onClick={() => handleExport("staff", `staff_performance_${dateRange.from || "all"}.csv`, "csv")}>
                            CSV
                        </button>
                        <button onClick={() => handleExport("staff", `staff_performance_${dateRange.from || "all"}.pdf`, "pdf")}>
                            PDF
                        </button>
                    </div>
                </div>

                <table>
                    <thead>
                    <tr><th>Cashier</th><th>Sales</th><th>Orders</th><th>Avg Order</th><th>Items Sold</th></tr>
                    </thead>
                    <tbody>
                    {staffData.map((staff) => (
                        <tr key={staff.cashierName}>
                            <td>{staff.cashierName}</td>
                            <td>KES {staff.totalSales.toFixed(2)}</td>
                            <td>{staff.totalOrders}</td>
                            <td>KES {staff.avgOrder.toFixed(2)}</td>
                            <td>{staff.totalItems}</td>
                        </tr>
                    ))}
                    </tbody>
                </table>
            </section>
        </div>
    );
}
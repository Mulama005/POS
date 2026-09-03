import { useState, useEffect } from "react";
import { useAuth } from "../hooks/useAuth";

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

export default function ReportsPage() {
    const { accessToken } = useAuth();
    const [salesData, setSalesData] = useState<SalesReport | null>(null);
    const [inventoryData, setInventoryData] = useState<InventoryReport | null>(null);
    const [financialData, setFinancialData] = useState<FinancialReport | null>(null);
    const [staffData, setStaffData] = useState<StaffPerformance[]>([]);
    const [loading, setLoading] = useState(true);
    const [dateRange, setDateRange] = useState({ from: "", to: "" });

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

    if (loading || !salesData || !inventoryData || !financialData) {
        return <div>Loading reports...</div>;
    }

    return (
        <div className="reports-page">
            <h1>Analytics & Reports</h1>

            {/* Date range filter */}
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

            {/* Sales Performance */}
            <section className="report-section">
                <h2>Sales Performance</h2>
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

            {/* Inventory Report */}
            <section className="report-section">
                <h2>Inventory</h2>
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

                <h3>Warranty Status</h3>
                <ul>
                    {inventoryData.warrantyStatus.map((w) => (
                        <li key={w.status}>{w.status}: {w.count}</li>
                    ))}
                </ul>
            </section>

            {/* Financial Report */}
            <section className="report-section">
                <h2>Financial</h2>
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

            {/* Staff Performance */}
            <section className="report-section">
                <h2>Staff Performance</h2>
                <table>
                    <thead><tr><th>Cashier</th><th>Sales</th><th>Orders</th><th>Avg Order</th><th>Items Sold</th></tr></thead>
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
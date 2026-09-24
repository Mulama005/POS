import { useEffect, useState } from "react";
import { useAuth } from "../hooks/useAuth";
import LoadingScreen from "../components/LoadingScreen";
import { apiClient } from "../services/apiClient";
import "./ReportsPage.css";

interface Period { label: string; startDate: string; endDate: string; total: number; subtotal: number; discountTotal: number; taxTotal: number; }
interface Delta { absolute: number; percent: number | null; }
interface CreditSales { currentTotal: number; previousTotal: number; absoluteDelta: number; percent: number | null; }
interface Breakdown { name: string; currentTotal: number; previousTotal: number; absoluteDelta: number; percent: number | null; }
interface PaymentBreakdown extends Breakdown { method: string; }
interface TrendMonth { month: string; label: string; total: number; }
interface PerformanceMetric { current: number | null; previous: number | null; absoluteDelta: number | null; percent: number | null; }
interface SalesPerformance {
    salesGrowthRate: number | null;
    averageOrderValue: PerformanceMetric;
    unitsPerTransaction: PerformanceMetric;
    grossProfit: { currentGrossProfit: number; currentCogs: number; currentMarginPercent: number | null; previousMarginPercent: number | null; };
    discountPercentage: PerformanceMetric;
    current: { transactions: number; unitsSold: number; };
}
interface InventoryPerformance { endingInventoryValue: number; cogs: number; turnoverRate: number | null; turnoverNote: string; }
interface OperatingCosts { monthlyTotal: number; items: Array<{ name: string; category: string; monthlyAmount: number }>; }
interface SkuPerformance { productId: string; sku: string; name: string; unitsSold: number; revenue: number; grossProfit: number; }
interface PeakPeriod { label: string; total: number; transactions: number; }
interface MonthlySummary {
    currentPeriod: Period;
    previousPeriod: Period;
    delta: Delta;
    creditSales: CreditSales;
    paymentMethodBreakdown: PaymentBreakdown[];
    categoryBreakdown: Breakdown[];
    productBreakdown: Breakdown[];
    trailingMonths: TrendMonth[];
    salesPerformance: SalesPerformance;
    inventoryPerformance: InventoryPerformance;
    operatingCosts: OperatingCosts;
    topSkuPerformance: SkuPerformance[];
    slowSkuPerformance: SkuPerformance[];
    peakHours: PeakPeriod[];
    peakDays: PeakPeriod[];
}

const money = (value: number) => `KES ${value.toLocaleString("en-KE", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
const signedMoney = (value: number) => `${value >= 0 ? "+" : "−"}${money(Math.abs(value))}`;
const signedPercent = (value: number | null) => value === null ? "New" : `${value >= 0 ? "+" : ""}${value.toFixed(1)}%`;
const percent = (value: number | null) => value === null ? "—" : `${value.toFixed(1)}%`;

const csvCell = (value: string | number | null) => `"${String(value ?? "").replaceAll("\"", "\"\"")}"`;
const downloadCsv = (filename: string, rows: Array<Array<string | number | null>>) => {
    const blob = new Blob([rows.map(row => row.map(csvCell).join(",")).join("\n")], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url; link.download = filename; link.click(); URL.revokeObjectURL(url);
};
const currentNairobiMonth = () => {
    const parts = new Intl.DateTimeFormat("en", { timeZone: "Africa/Nairobi", year: "numeric", month: "2-digit" }).formatToParts(new Date());
    return `${parts.find(part => part.type === "year")?.value}-${parts.find(part => part.type === "month")?.value}`;
};

function ChangeBadge({ delta, higherIsBetter = true }: { delta: Delta; higherIsBetter?: boolean }) {
    const isGood = higherIsBetter ? delta.absolute >= 0 : delta.absolute < 0;
    return <span className={`report-change report-change--${isGood ? "good" : "bad"}`}>{signedMoney(delta.absolute)} · {signedPercent(delta.percent)}</span>;
}

function BreakdownTable({ title, rows, firstColumn }: { title: string; rows: Breakdown[]; firstColumn: string }) {
    return <section className="report-breakdown">
        <h3>{title}</h3>
        {rows.length === 0 ? <p className="report-empty">No sales in either period.</p> : (
            <table>
                <thead><tr><th>{firstColumn}</th><th>Current</th><th>Previous</th><th>Change</th></tr></thead>
                <tbody>{rows.map((row) => <tr key={row.name}>
                    <td>{row.name}</td><td>{money(row.currentTotal)}</td><td>{money(row.previousTotal)}</td>
                    <td><ChangeBadge delta={{ absolute: row.absoluteDelta, percent: row.percent }} /></td>
                </tr>)}</tbody>
            </table>
        )}
    </section>;
}

function MetricCard({ label, value, detail }: { label: string; value: string; detail: string }) {
    return <article className="performance-metric"><span>{label}</span><strong>{value}</strong><small>{detail}</small></article>;
}

function PeakChart({ title, rows }: { title: string; rows: PeakPeriod[] }) {
    const maximum = Math.max(...rows.map(row => row.total), 1);
    return <section className="peak-chart"><h3>{title}</h3><div className="peak-bars">
        {rows.map(row => <div className="peak-bar-row" key={row.label} title={`${row.label}: ${money(row.total)} across ${row.transactions} transactions`}>
            <span>{row.label}</span><div><i style={{ width: `${(row.total / maximum) * 100}%` }} /></div><b>{money(row.total)}</b>
        </div>)}
    </div></section>;
}

function SkuTable({ title, rows }: { title: string; rows: SkuPerformance[] }) {
    return <section className="report-breakdown"><h3>{title}</h3>{rows.length === 0 ? <p className="report-empty">No active products are available for this period.</p> : <table><thead><tr><th>SKU / product</th><th>Units</th><th>Revenue</th><th>Gross profit</th></tr></thead>
        <tbody>{rows.map(row => <tr key={row.productId}><td><span className="sku-code">{row.sku}</span>{row.name}</td><td>{row.unitsSold}</td><td>{money(row.revenue)}</td><td>{money(row.grossProfit)}</td></tr>)}</tbody>
    </table>}</section>;
}

function ComparisonChart({ label, current, previous, format = (value) => value === null ? "—" : money(value) }: { label: string; current: number | null; previous: number | null; format?: (value: number | null) => string }) {
    const max = Math.max(Math.abs(current ?? 0), Math.abs(previous ?? 0), 1);
    return <article className="metric-chart"><h3>{label}</h3><div className="metric-chart__bars">
        <div><span>Current</span><i style={{ width: `${(Math.abs(current ?? 0) / max) * 100}%` }} /><b>{format(current)}</b></div>
        <div><span>Previous</span><i style={{ width: `${(Math.abs(previous ?? 0) / max) * 100}%` }} /><b>{format(previous)}</b></div>
    </div></article>;
}

function CalculationNote({ title, formula, evidence }: { title: string; formula: string; evidence: string }) {
    return <article className="calculation-note">
        <strong>{title}</strong><span>{formula}</span><small>{evidence}</small>
    </article>;
}

function RankedSkuChart({ title, rows, value = "revenue" }: { title: string; rows: SkuPerformance[]; value?: "revenue" | "units" }) {
    const maximum = Math.max(...rows.map(row => value === "revenue" ? row.revenue : row.unitsSold), 1);
    return <section className="ranked-chart"><h3>{title}</h3>
        {rows.length === 0 ? <p className="report-empty">No stock movement was recorded for this period.</p> : <div className="ranked-chart__rows">
            {rows.slice(0, 6).map(row => {
                const amount = value === "revenue" ? row.revenue : row.unitsSold;
                return <div className="ranked-chart__row" key={row.productId} title={`${row.name}: ${value === "revenue" ? money(amount) : `${amount} units`}`}>
                    <span>{row.name}</span><div><i style={{ width: `${(amount / maximum) * 100}%` }} /></div><b>{value === "revenue" ? money(amount) : `${amount} units`}</b>
                </div>;
            })}
        </div>}
    </section>;
}

export default function ReportsPage() {
    const { accessToken } = useAuth();
    const [selectedMonth, setSelectedMonth] = useState("");
    const [summary, setSummary] = useState<MonthlySummary | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");

    useEffect(() => {
        const load = async () => {
            setLoading(true);
            setError("");
            try {
                const params: { year?: string; month?: string } = {};
                if (selectedMonth) {
                    const [year, month] = selectedMonth.split("-");
                    params.year = year;
                    params.month = month;
                }
                const { data } = await apiClient.get<MonthlySummary>("/api/reports/monthly-summary", { params });
                setSummary(data);
            } catch (requestError) {
                console.error(requestError);
                setError("We could not load this monthly report. Please try again.");
            } finally {
                setLoading(false);
            }
        };
        void load();
    }, [accessToken, selectedMonth]);

    if (loading && !summary) return <LoadingScreen message="Loading monthly insights..." />;
    if (!summary) return <main className="reports-page"><p className="report-empty">{error}</p></main>;

    const maxTrend = Math.max(...summary.trailingMonths.map((item) => item.total), 1);
    const paymentRows: Breakdown[] = summary.paymentMethodBreakdown.map((item) => ({ ...item, name: item.method }));
    const reportMonth = selectedMonth || currentNairobiMonth();
    const downloadSalesExport = async (format: "csv" | "pdf") => {
        const [year, month] = reportMonth.split("-");
        const lastDay = new Date(Date.UTC(Number(year), Number(month), 0)).getUTCDate().toString().padStart(2, "0");
        try {
            const { data } = await apiClient.get<Blob>("/api/reports/export/sales", { params: { format, fromDate: `${year}-${month}-01`, toDate: `${year}-${month}-${lastDay}` }, responseType: "blob" });
            const url = URL.createObjectURL(data);
            const link = document.createElement("a"); link.href = url; link.download = `sales-performance-${reportMonth}.${format}`; link.click(); URL.revokeObjectURL(url);
        } catch {
            setError("We could not create the sales export. Please try again.");
        }
    };
    const downloadInsightsCsv = () => downloadCsv(`monthly-insights-${reportMonth}.csv`, [
        ["Monthly Sales Insights", summary.currentPeriod.label], ["Metric", "Current", "Previous", "Calculation"],
        ["Total sales", summary.currentPeriod.total, summary.previousPeriod.total, "Sum of completed Sale.Total"],
        ["Sales growth rate", percent(summary.salesPerformance.salesGrowthRate), "", "(Current sales - Previous sales) / Previous sales x 100"],
        ["Average order value", summary.salesPerformance.averageOrderValue.current, summary.salesPerformance.averageOrderValue.previous, "Total sales / completed transactions"],
        ["Units per transaction", summary.salesPerformance.unitsPerTransaction.current, summary.salesPerformance.unitsPerTransaction.previous, "Units sold / completed transactions"],
        ["Gross margin", percent(summary.salesPerformance.grossProfit.currentMarginPercent), percent(summary.salesPerformance.grossProfit.previousMarginPercent), "(Net revenue - COGS) / Net revenue x 100"],
        ["Markdown percentage", percent(summary.salesPerformance.discountPercentage.current), percent(summary.salesPerformance.discountPercentage.previous), "Discounts / total sales x 100"]
    ]);
    const downloadInventoryCsv = () => downloadCsv(`inventory-performance-${reportMonth}.csv`, [
        ["Inventory Performance", summary.currentPeriod.label], ["Metric", "Value", "Calculation / note"],
        ["On-hand inventory value", summary.inventoryPerformance.endingInventoryValue, "Current bulk and serialized on-hand units x current cost price"],
        ["Period COGS", summary.inventoryPerformance.cogs, "Units sold x current product cost price"],
        ["Inventory turnover", summary.inventoryPerformance.turnoverRate, summary.inventoryPerformance.turnoverNote]
    ]);
    const downloadSkuCsv = () => downloadCsv(`sku-performance-${reportMonth}.csv`, [["Stock movement", "SKU", "Product", "Units", "Revenue", "Gross profit"],
        ...summary.topSkuPerformance.map(row => ["Top", row.sku, row.name, row.unitsSold, row.revenue, row.grossProfit]),
        ...summary.slowSkuPerformance.map(row => ["Slow", row.sku, row.name, row.unitsSold, row.revenue, row.grossProfit])]);
    const downloadSingleSkuCsv = (kind: "top" | "slow", rows: SkuPerformance[]) => downloadCsv(`${kind}-moving-stock-${reportMonth}.csv`, [
        [kind === "top" ? "Top-moving stock" : "Slow-moving stock", "SKU", "Product", "Units sold", "Revenue", "Gross profit"],
        ...rows.map(row => [kind, row.sku, row.name, row.unitsSold, row.revenue, row.grossProfit])
    ]);
    const downloadFinancialExport = async (format: "csv" | "pdf") => {
        const [year, month] = reportMonth.split("-");
        const lastDay = new Date(Date.UTC(Number(year), Number(month), 0)).getUTCDate().toString().padStart(2, "0");
        try {
            const { data } = await apiClient.get<Blob>("/api/reports/export/financial", { params: { format, fromDate: `${year}-${month}-01`, toDate: `${year}-${month}-${lastDay}` }, responseType: "blob" });
            const url = URL.createObjectURL(data);
            const link = document.createElement("a"); link.href = url; link.download = `financial-summary-${reportMonth}.${format}`; link.click(); URL.revokeObjectURL(url);
        } catch {
            setError("We could not create the financial export. Please try again.");
        }
    };
    const periodDays = Math.max(1, Math.ceil((new Date(summary.currentPeriod.endDate).getTime() - new Date(summary.currentPeriod.startDate).getTime()) / 86_400_000));
    const grossProfit = summary.salesPerformance.grossProfit.currentGrossProfit;
    const grossMargin = summary.salesPerformance.grossProfit.currentMarginPercent;
    const inventoryValue = summary.inventoryPerformance.endingInventoryValue;
    const cogs = summary.salesPerformance.grossProfit.currentCogs;
    const gmroi = inventoryValue === 0 ? null : grossProfit / inventoryValue;
    const dsi = cogs === 0 ? null : inventoryValue / cogs * periodDays;
    const projectedRevenue = summary.salesPerformance.salesGrowthRate === null ? null : summary.currentPeriod.total * (1 + summary.salesPerformance.salesGrowthRate / 100);
    const monthlyOperatingCosts = summary.operatingCosts.monthlyTotal;
    const operatingProfit = grossProfit - monthlyOperatingCosts;
    const breakEvenRevenue = grossMargin === null || grossMargin <= 0 ? null : monthlyOperatingCosts / (grossMargin / 100);

    return <main className="reports-page">
        <header className="page-header report-masthead">
            <div><p className="report-eyebrow">Nairobi time · EAT (UTC+3)</p><h1>Monthly sales insights</h1></div>
            <label className="month-picker">Reporting month<input type="month" value={selectedMonth} onChange={(event) => setSelectedMonth(event.target.value)} /></label>
        </header>

        {error && <p className="report-error">{error}</p>}
        <section className="report-section report-overview">
            <div className="report-periods"><span>{summary.currentPeriod.label}</span><span>Compared with {summary.previousPeriod.label}</span></div>
            <div className="report-kpis">
                <article className="report-kpi"><span>Total sales</span><strong>{money(summary.currentPeriod.total)}</strong><ChangeBadge delta={summary.delta} /></article>
                <article className="report-kpi"><span>Subtotal</span><strong>{money(summary.currentPeriod.subtotal)}</strong><small>Before tax and discounts</small></article>
                <article className="report-kpi"><span>Discounts</span><strong>{money(summary.currentPeriod.discountTotal)}</strong><small>For the selected period</small></article>
                <article className="report-kpi report-kpi--credit"><span>Credit sales</span><strong>{money(summary.creditSales.currentTotal)}</strong><ChangeBadge delta={{ absolute: summary.creditSales.absoluteDelta, percent: summary.creditSales.percent }} /><small>Separate Deni ledger entries — not included in Total Sales.</small></article>
            </div>
        </section>

        <section className="report-section">
            <div className="section-header"><div><h2>Sales performance</h2><p>Completed retail sales for {summary.currentPeriod.label}.</p></div><div className="export-buttons"><button onClick={() => void downloadSalesExport("pdf")}>PDF</button><button onClick={() => void downloadSalesExport("csv")}>CSV</button></div></div>
            <div className="performance-grid">
                <MetricCard label="Sales growth rate" value={summary.salesPerformance.salesGrowthRate === null ? "No prior sales" : percent(summary.salesPerformance.salesGrowthRate)} detail={summary.salesPerformance.salesGrowthRate === null ? `No completed sales in ${summary.previousPeriod.label}` : "Month-over-month total-sales change"} />
                <MetricCard label="Average order value" value={money(summary.salesPerformance.averageOrderValue.current ?? 0)} detail={`${summary.salesPerformance.current.transactions} completed transactions`} />
                <MetricCard label="Units per transaction" value={(summary.salesPerformance.unitsPerTransaction.current ?? 0).toFixed(2)} detail={`${summary.salesPerformance.current.unitsSold} units sold`} />
                <MetricCard label="Gross profit margin" value={percent(summary.salesPerformance.grossProfit.currentMarginPercent)} detail={`Gross profit ${money(summary.salesPerformance.grossProfit.currentGrossProfit)} · COGS ${money(summary.salesPerformance.grossProfit.currentCogs)}`} />
                <MetricCard label="Markdown percentage" value={percent(summary.salesPerformance.discountPercentage.current)} detail="Discounts as a percentage of total sales" />
            </div>
            <div className="calculation-grid">
                <CalculationNote title="Sales growth" formula="(current sales − prior sales) ÷ prior sales" evidence={`${money(summary.currentPeriod.total)} compared with ${money(summary.previousPeriod.total)}`} />
                <CalculationNote title="Average order value" formula="total sales ÷ completed transactions" evidence={`${money(summary.currentPeriod.total)} ÷ ${summary.salesPerformance.current.transactions} transactions`} />
                <CalculationNote title="Gross margin" formula="(net revenue − cost of goods sold) ÷ net revenue" evidence={`${money(summary.salesPerformance.grossProfit.currentGrossProfit)} gross profit after ${money(summary.salesPerformance.grossProfit.currentCogs)} COGS`} />
            </div>
            <div className="metric-chart-grid metric-chart-grid--sales">
                <ComparisonChart label="Total sales" current={summary.currentPeriod.total} previous={summary.previousPeriod.total} />
                <ComparisonChart label="Average order value" current={summary.salesPerformance.averageOrderValue.current} previous={summary.salesPerformance.averageOrderValue.previous} />
                <ComparisonChart label="Units per transaction" current={summary.salesPerformance.unitsPerTransaction.current} previous={summary.salesPerformance.unitsPerTransaction.previous} format={(value) => value === null ? "—" : value.toFixed(2)} />
                <ComparisonChart label="Gross margin" current={summary.salesPerformance.grossProfit.currentMarginPercent} previous={summary.salesPerformance.grossProfit.previousMarginPercent} format={percent} />
                <ComparisonChart label="Markdown rate" current={summary.salesPerformance.discountPercentage.current} previous={summary.salesPerformance.discountPercentage.previous} format={percent} />
            </div>
        </section>

        <section className="report-section financial-summary">
            <div className="section-header"><div><h2>Financial summary</h2><p>A decision-ready monthly view of profit, stock capital and forward targets.</p></div><div className="export-buttons"><a href="/financial-setup">Operating costs</a><button onClick={() => void downloadFinancialExport("pdf")}>PDF</button><button onClick={() => void downloadFinancialExport("csv")}>CSV</button></div></div>
            <div className="financial-groups">
                <section className="financial-group"><div><p className="financial-group__number">01</p><h3>Income statement</h3><p>What the business made from completed sales before overheads.</p></div><div className="financial-metrics">
                    <MetricCard label="Gross revenue" value={money(summary.currentPeriod.total)} detail="Completed sales, including tax" />
                    <MetricCard label="Cost of goods sold" value={money(cogs)} detail="Sold units × current product cost" />
                    <MetricCard label="Gross profit" value={money(grossProfit)} detail={`Gross margin ${percent(grossMargin)}`} />
                    <MetricCard label="Operating costs" value={money(monthlyOperatingCosts)} detail={`${summary.operatingCosts.items.length} saved monthly cost${summary.operatingCosts.items.length === 1 ? "" : "s"}`} />
                    <MetricCard label="Operating profit" value={money(operatingProfit)} detail="Gross profit less saved operating costs; before tax and finance" />
                </div><div className="operating-cost-list"><strong>Operating cost breakdown</strong>{summary.operatingCosts.items.length === 0 ? <span>No active monthly operating costs saved.</span> : summary.operatingCosts.items.map(item => <div key={`${item.category}-${item.name}`}><span>{item.category} · {item.name}</span><b>{money(item.monthlyAmount)}</b></div>)}</div></section>
                <section className="financial-group"><div><p className="financial-group__number">02</p><h3>Cash & working capital</h3><p>Cash-cover metrics require balances and supplier invoices, which are kept separate from sales.</p></div><div className="financial-metrics financial-metrics--three">
                    <MetricCard label="Fixed monthly burn" value={money(monthlyOperatingCosts)} detail="Saved recurring operating costs" />
                    <MetricCard label="Current / quick ratio" value="Awaiting balances" detail="Needs current assets and liabilities" />
                    <MetricCard label="Cash conversion cycle" value="Awaiting invoices" detail="Needs receivable, payable and inventory timing" />
                </div></section>
                <section className="financial-group"><div><p className="financial-group__number">03</p><h3>Inventory financial health</h3><p>Shows the return being generated by the cash currently held in stock.</p></div><div className="financial-metrics financial-metrics--three">
                    <MetricCard label="Inventory at cost" value={money(inventoryValue)} detail="Current bulk and serialised stock at cost" />
                    <MetricCard label="GMROI" value={gmroi === null ? "—" : `${gmroi.toFixed(2)}x`} detail="Gross profit per KES currently invested in stock" />
                    <MetricCard label="Estimated days in stock" value={dsi === null ? "—" : `${dsi.toFixed(0)} days`} detail="Uses current inventory value; average snapshots improve this estimate" />
                </div></section>
                <section className="financial-group"><div><p className="financial-group__number">04</p><h3>Benchmarks & projection</h3><p>Forward-looking indicators, clearly separated from recorded actuals.</p></div><div className="financial-metrics financial-metrics--three">
                    <MetricCard label="Actual revenue" value={money(summary.currentPeriod.total)} detail={`${periodDays} reporting days`} />
                    <MetricCard label="Next-month projection" value={projectedRevenue === null ? "Needs prior sales" : money(projectedRevenue)} detail="Actual revenue × current month-over-month growth" />
                    <MetricCard label="Breakeven target" value={breakEvenRevenue === null ? "Needs margin" : money(breakEvenRevenue)} detail="Operating costs ÷ gross margin percentage" />
                </div></section>
            </div>
            <div className="calculation-grid">
                <CalculationNote title="Gross margin" formula="(revenue excluding tax − COGS) ÷ revenue excluding tax" evidence={`${money(grossProfit)} gross profit is generated after ${money(cogs)} cost of goods sold.`} />
                <CalculationNote title="GMROI" formula="gross profit ÷ current inventory cost" evidence={gmroi === null ? "No on-hand inventory value is recorded." : `${money(grossProfit)} ÷ ${money(inventoryValue)} = ${gmroi.toFixed(2)}x.`} />
                <CalculationNote title="Sales projection" formula="actual revenue × (1 + sales growth rate)" evidence={projectedRevenue === null ? "A completed prior-month sales period is required." : `${money(summary.currentPeriod.total)} extended by ${percent(summary.salesPerformance.salesGrowthRate)} growth.`} />
            </div>
            <div className="metric-chart-grid metric-chart-grid--financial">
                <ComparisonChart label="Revenue vs cost of goods sold" current={summary.currentPeriod.total} previous={cogs} format={(value) => value === null ? "—" : money(value)} />
                <ComparisonChart label="Gross profit vs stock value" current={grossProfit} previous={inventoryValue} format={(value) => value === null ? "—" : money(value)} />
                <ComparisonChart label="Actual vs projected revenue" current={summary.currentPeriod.total} previous={projectedRevenue} format={(value) => value === null ? "Needs prior sales" : money(value)} />
            </div>
        </section>

        <section className="report-section inventory-insight">
            <div className="section-header"><div><h2>Inventory performance</h2><p>Inventory value at current product cost.</p></div><div className="export-buttons"><button onClick={downloadInventoryCsv}>CSV</button></div></div>
            <div className="performance-grid performance-grid--two">
                <MetricCard label="On-hand inventory value" value={money(summary.inventoryPerformance.endingInventoryValue)} detail="Current on-hand stock at current cost" />
                <MetricCard label="Inventory turnover" value={summary.inventoryPerformance.turnoverRate === null ? "Not available" : summary.inventoryPerformance.turnoverRate.toFixed(2)} detail={summary.inventoryPerformance.turnoverNote} />
            </div>
            <div className="calculation-grid calculation-grid--two">
                <CalculationNote title="On-hand value" formula="on-hand bulk and serialised units × current cost price" evidence={`The valuation currently totals ${money(summary.inventoryPerformance.endingInventoryValue)}.`} />
                <CalculationNote title="Inventory turnover" formula="period COGS ÷ average inventory value" evidence={summary.inventoryPerformance.turnoverNote} />
            </div>
            <div className="metric-chart-grid metric-chart-grid--two">
                <ComparisonChart label="Inventory value vs period COGS" current={summary.inventoryPerformance.endingInventoryValue} previous={summary.inventoryPerformance.cogs} format={(value) => value === null ? "—" : money(value)} />
                <ComparisonChart label="Inventory turnover" current={summary.inventoryPerformance.turnoverRate} previous={null} format={(value) => value === null ? "Needs opening value" : `${value.toFixed(2)}x`} />
            </div>
        </section>

        <section className="report-section">
            <div className="section-header"><div><h2>Monthly sales insights</h2><p>Completed retail sales only.</p></div><div className="export-buttons"><button onClick={downloadInsightsCsv}>CSV</button></div></div>
            <div className="trend-chart" aria-label="Trailing twelve-month sales trend">
                {summary.trailingMonths.map((item) => <div className="trend-column" key={item.month} title={`${item.label}: ${money(item.total)}`}>
                    <span className="trend-value">{money(item.total)}</span><div className="trend-bar-wrap"><div className="trend-bar" style={{ height: `${Math.max((item.total / maxTrend) * 100, item.total ? 4 : 0)}%` }} /></div><span className="trend-label">{item.label.split(" ")[0]}</span>
                </div>)}
            </div>
            <div className="calculation-grid calculation-grid--two">
                <CalculationNote title="Monthly totals" formula="sum of completed sale totals in each Nairobi calendar month" evidence={`The selected period contains ${summary.salesPerformance.current.transactions} completed transactions.`} />
                <CalculationNote title="Period comparison" formula="current period is matched with the same elapsed days in the prior month" evidence={`This keeps month-to-date comparisons fair when the month is still in progress.`} />
            </div>
        </section>

        <section className="report-section report-tables">
            <BreakdownTable title="Payment methods" rows={paymentRows} firstColumn="Method" />
            <BreakdownTable title="Category movers" rows={summary.categoryBreakdown} firstColumn="Category" />
        </section>
        <section className="report-section report-tables">
            <div><SkuTable title="Top-performing SKUs" rows={summary.topSkuPerformance} /><div className="table-export"><button className="btn-export" onClick={() => downloadSingleSkuCsv("top", summary.topSkuPerformance)}>Download CSV</button></div></div>
            <div><SkuTable title="Slow-moving SKUs" rows={summary.slowSkuPerformance} /><div className="table-export"><button className="btn-export" onClick={() => downloadSingleSkuCsv("slow", summary.slowSkuPerformance)}>Download CSV</button></div></div>
        </section>
        <div className="report-section__export"><button className="btn-export" onClick={downloadSkuCsv}>Download combined SKU CSV</button></div>

        <section className="report-section">
            <div className="section-header"><div><h2>Stock movement evidence</h2><p>Ranked by net sales revenue; zero-sales products remain visible in slow-moving stock.</p></div></div>
            <div className="ranked-charts"><RankedSkuChart title="Leading products by revenue" rows={summary.topSkuPerformance} /><RankedSkuChart title="Slow-moving products by units" rows={summary.slowSkuPerformance} value="units" /></div>
            <div className="calculation-grid calculation-grid--two">
                <CalculationNote title="Top moving" formula="products ranked from highest to lowest net sales revenue" evidence="Revenue excludes tax; gross profit deducts current product cost." />
                <CalculationNote title="Slow moving" formula="active products ranked from lowest to highest net sales revenue" evidence="This includes items with no recorded sale in the selected month." />
            </div>
        </section>

        <section className="report-section">
            <div className="section-header"><div><h2>Peak trading times</h2><p>Sales and transaction timing in Nairobi time.</p></div></div>
            <div className="peak-charts"><PeakChart title="By hour" rows={summary.peakHours} /><PeakChart title="By day of week" rows={summary.peakDays} /></div>
        </section>

    </main>;
}

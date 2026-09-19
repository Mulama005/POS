import { useEffect, useState } from "react";
import { apiClient } from "../services/apiClient";
import { formatKes } from "../utils/currency";
import "./OperatingCostsPage.css";

type Expense = { id: string; name: string; category: string; monthlyAmount: number; isActive: boolean };
const starterCosts = [
  ["Rent", "Occupancy", 60000], ["Electricity", "Utilities", 5000], ["Wi‑Fi", "Utilities", 2500], ["Soap", "Supplies", 300], ["Water dispenser", "Utilities", 2400],
] as const;

export default function OperatingCostsPage() {
  const [items, setItems] = useState<Expense[]>([]);
  const [name, setName] = useState(""); const [category, setCategory] = useState("Occupancy"); const [amount, setAmount] = useState("");
  const [saving, setSaving] = useState(false); const [message, setMessage] = useState("");
  const load = async () => { const { data } = await apiClient.get<Expense[]>("/api/operating-expenses"); setItems(data); };
  useEffect(() => { void load().catch(() => setMessage("Could not load operating costs.")); }, []);
  const add = async (entry = { name, category, amount: Number(amount) }) => {
    if (!entry.name.trim() || !Number.isFinite(entry.amount) || entry.amount < 0) { setMessage("Enter a cost name and a valid monthly amount."); return; }
    setSaving(true); setMessage("");
    try { await apiClient.post("/api/operating-expenses", { name: entry.name, category: entry.category, monthlyAmount: entry.amount }); setName(""); setAmount(""); await load(); }
    catch { setMessage("Could not save this operating cost."); } finally { setSaving(false); }
  };
  const addStarterCosts = async () => { for (const [starterName, starterCategory, starterAmount] of starterCosts) { if (!items.some(item => item.name.toLowerCase() === starterName.toLowerCase())) await add({ name: starterName, category: starterCategory, amount: starterAmount }); } };
  const remove = async (id: string) => { await apiClient.delete(`/api/operating-expenses/${id}`); await load(); };
  const total = items.filter(item => item.isActive).reduce((sum, item) => sum + item.monthlyAmount, 0);

  return <main className="operating-costs-page">
    <header className="costs-header"><div><p>Financial setup</p><h1>Operating costs</h1><span>Enter your recurring monthly costs once. Financial reports then calculate operating profit and breakeven targets from them.</span></div><strong>{formatKes(total)}<small>active monthly costs</small></strong></header>
    {message && <p className="costs-message">{message}</p>}
    <section className="costs-section"><div className="costs-section__heading"><div><h2>Add a monthly cost</h2><p>Use the monthly amount—even when a bill is paid at a different time.</p></div><button type="button" className="costs-starter" onClick={() => void addStarterCosts()} disabled={saving}>Use my five costs</button></div>
      <div className="costs-form"><label>Cost name<input value={name} onChange={e => setName(e.target.value)} placeholder="e.g. Shop rent" /></label><label>Category<input value={category} onChange={e => setCategory(e.target.value)} placeholder="e.g. Occupancy" /></label><label>Monthly amount (KES)<input type="number" min="0" value={amount} onChange={e => setAmount(e.target.value)} placeholder="0.00" /></label><button type="button" onClick={() => void add()} disabled={saving}>Add cost</button></div>
    </section>
    <section className="costs-section"><div className="costs-section__heading"><div><h2>Saved costs</h2><p>Remove an item to stop using it in future reports.</p></div></div>
      {items.length === 0 ? <p className="costs-empty">No operating costs saved yet. You can add each cost above or use your supplied list.</p> : <table><thead><tr><th>Cost</th><th>Category</th><th>Monthly amount</th><th></th></tr></thead><tbody>{items.map(item => <tr key={item.id}><td>{item.name}</td><td>{item.category}</td><td>{formatKes(item.monthlyAmount)}</td><td><button type="button" onClick={() => void remove(item.id)}>Remove</button></td></tr>)}</tbody></table>}
    </section>
  </main>;
}

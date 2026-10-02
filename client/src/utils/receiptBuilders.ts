import type { CompleteSaleResult } from '../types/sale'
import type { ReceiptData, ReceiptRow } from '../components/receipt/ReceiptSheet'

const kes = (n: number) =>
  n.toLocaleString('en-KE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

const dateTime = (iso: string) =>
  new Date(iso).toLocaleString('en-KE', { dateStyle: 'medium', timeStyle: 'short' })

const METHOD_LABEL: Record<string, string> = { Cash: 'Cash', Mpesa: 'M-Pesa', Card: 'Card', Credit: 'On credit' }

export function buildSaleReceipt(
  sale: CompleteSaleResult & { pending?: boolean },
  ctx: { cashierName?: string; registerName?: string },
): ReceiptData {
  const meta: ReceiptRow[] = [
    { label: 'Receipt', value: sale.etimsInvoiceNumber ?? sale.saleId.slice(0, 8).toUpperCase() },
    { label: 'Date', value: dateTime(sale.saleDate) },
  ]
  if (ctx.registerName) meta.push({ label: 'Register', value: ctx.registerName })
  if (ctx.cashierName) meta.push({ label: 'Served by', value: ctx.cashierName })

  const totals: ReceiptRow[] = [{ label: 'Subtotal', value: kes(sale.subtotal) }]
  if (sale.discountTotal > 0) totals.push({ label: 'Discount', value: `-${kes(sale.discountTotal)}` })
  totals.push({ label: 'TOTAL (KES)', value: kes(sale.total), strong: true })
  totals.push({ label: 'Includes VAT', value: kes(sale.taxTotal), muted: true })

  const sections: ReceiptData['sections'] = [
    {
      heading: 'Payment',
      rows: sale.payments.map((p) => {
        const label = METHOD_LABEL[p.method] ?? p.method
        const state = p.status === 'Success' ? '' : ` (${p.status === 'TimedOut' ? 'timed out' : p.status.toLowerCase()})`
        return { label: `${label}${state}`, value: kes(p.amount) }
      }),
    },
  ]
  const mpesaRefs = sale.payments
    .filter((p) => p.method === 'Mpesa' && p.status === 'Success' && p.externalReference)
    .map((p) => ({ label: 'M-Pesa ref', value: p.externalReference as string, muted: true }))
  sections[0].rows.push(...mpesaRefs)

  if (!sale.pending && sale.etimsSubmitted) {
    const rows: ReceiptRow[] = []
    if (sale.etimsReceiptNumber !== null) rows.push({ label: 'Receipt no.', value: String(sale.etimsReceiptNumber) })
    if (sale.etimsSdcId) rows.push({ label: 'SDC ID', value: sale.etimsSdcId })
    if (sale.etimsMrcNo) rows.push({ label: 'MRC no.', value: sale.etimsMrcNo })
    if (sale.etimsReceiptPublishedDate) rows.push({ label: 'Published', value: dateTime(sale.etimsReceiptPublishedDate) })
    if (rows.length) sections.push({ heading: 'KRA eTIMS', rows })
  }

  return {
    title: 'Sales receipt',
    notice: sale.pending
      ? 'Saved offline - will sync when back online. eTIMS invoice not yet issued.'
      : undefined,
    meta,
    items: sale.items.map((i) => ({
      name: i.productName,
      detail: `${i.quantity} x ${kes(i.unitPrice)}${i.discountAmount > 0 ? `  (disc -${kes(i.discountAmount)})` : ''}`,
      amount: kes(i.lineTotal),
    })),
    totals,
    sections,
    qr: !sale.pending && sale.etimsQrCodeData
      ? { value: sale.etimsQrCodeData, caption: 'Scan to verify with KRA eTIMS' }
      : undefined,
  }
}

// ---------- Repairs ----------

export interface RepairReceiptDto {
  ticketNumber: string
  createdAt: string
  collectedAt: string | null
  status: string
  customerName: string
  customerPhone: string | null
  deviceDescription: string
  reportedFault: string
  quotedCost: number | null
  finalCost: number | null
  amountPaid: number
  balance: number | null
  payments: { timestamp: string; method: string | null; amount: number; reference: string | null }[]
}

export function buildRepairReceipt(r: RepairReceiptDto): ReceiptData {
  const finalized = r.finalCost !== null
  const meta: ReceiptRow[] = [
    { label: 'Ticket', value: r.ticketNumber, strong: true },
    { label: 'Received', value: dateTime(r.createdAt) },
    { label: 'Customer', value: r.customerName },
  ]
  if (r.customerPhone) meta.push({ label: 'Phone', value: r.customerPhone })
  meta.push({ label: 'Status', value: r.status })
  if (r.collectedAt) meta.push({ label: 'Collected', value: dateTime(r.collectedAt) })

  const totals: ReceiptRow[] = []
  if (finalized) {
    totals.push({ label: 'Repair cost', value: kes(r.finalCost as number) })
    totals.push({ label: 'Paid', value: kes(r.amountPaid) })
    const bal = r.balance ?? (r.finalCost as number) - r.amountPaid
    totals.push({ label: bal > 0 ? 'BALANCE DUE (KES)' : 'BALANCE', value: bal > 0 ? kes(bal) : 'PAID IN FULL', strong: true })
  } else if (r.quotedCost !== null) {
    totals.push({ label: 'Quoted (estimate)', value: kes(r.quotedCost), strong: true })
    totals.push({ label: 'Final price confirmed after diagnosis', value: '', muted: true })
  } else {
    totals.push({ label: 'Cost to be quoted after diagnosis', value: '', muted: true })
  }

  const sections: ReceiptData['sections'] = []
  if (r.payments.length > 0) {
    sections.push({
      heading: 'Payments received',
      rows: r.payments.flatMap((p) => {
        const rows: ReceiptRow[] = [
          { label: `${dateTime(p.timestamp)} ${p.method ?? ''}`.trim(), value: kes(p.amount) },
        ]
        if (p.reference) rows.push({ label: `  Ref: ${p.reference}`, value: '', muted: true })
        return rows
      }),
    })
  }

  return {
    title: finalized ? 'Repair receipt' : 'Repair intake slip',
    meta,
    items: [{ name: r.deviceDescription, detail: `Fault: ${r.reportedFault}`, amount: finalized ? kes(r.finalCost as number) : '' }],
    totals,
    sections,
    footerLines: [
      `Track your repair: ${window.location.origin}/track-repair`,
      'Quote ticket number + last 4 digits of your phone.',
      'Please bring this slip when collecting your device.',
    ],
  }
}
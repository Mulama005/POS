import { createPortal } from 'react-dom'
import { QRCodeSVG } from 'qrcode.react'
import { BUSINESS } from '../../config/business'
import './receipt.css'

export interface ReceiptRow {
  label: string
  value: string
  strong?: boolean
  muted?: boolean
}

export interface ReceiptItem {
  name: string
  /** e.g. "2 × 1,500.00" or a repair fault description */
  detail?: string
  amount: string
}

export interface ReceiptSection {
  heading: string
  rows: ReceiptRow[]
}

export interface ReceiptData {
  title: string
  /** Amber banner, e.g. "Saved offline - not yet synced". */
  notice?: string
  meta: ReceiptRow[]
  items: ReceiptItem[]
  totals: ReceiptRow[]
  sections?: ReceiptSection[]
  qr?: { value: string; caption: string }
  /** Extra lines under the thank-you line (e.g. tracking instructions). */
  footerLines?: string[]
}

function Rows({ rows }: { rows: ReceiptRow[] }) {
  return (
    <>
      {rows.map((row, i) => (
        <div
          key={`${row.label}-${i}`}
          className={`rcpt-row${row.strong ? ' rcpt-row--strong' : ''}${row.muted ? ' rcpt-row--muted' : ''}`}
        >
          <span>{row.label}</span>
          <span>{row.value}</span>
        </div>
      ))}
    </>
  )
}

/** The receipt itself - used for the on-screen preview and (via the portal) for printing. */
export function ReceiptSheet({ data }: { data: ReceiptData }) {
  return (
    <div className="rcpt">
      <header className="rcpt-head">
        <div className="rcpt-brand">{BUSINESS.name}</div>
        {BUSINESS.tagline && <div className="rcpt-sub">{BUSINESS.tagline}</div>}
        {BUSINESS.address && <div className="rcpt-sub">{BUSINESS.address}</div>}
        {BUSINESS.phone && <div className="rcpt-sub">Tel: {BUSINESS.phone}</div>}
        {BUSINESS.kraPin && <div className="rcpt-sub">PIN: {BUSINESS.kraPin}</div>}
      </header>

      <div className="rcpt-title">{data.title}</div>
      {data.notice && <div className="rcpt-notice">{data.notice}</div>}

      <div className="rcpt-block">
        <Rows rows={data.meta} />
      </div>

      {data.items.length > 0 && (
        <div className="rcpt-block">
          {data.items.map((item, i) => (
            <div className="rcpt-item" key={`${item.name}-${i}`}>
              <div className="rcpt-row">
                <span className="rcpt-item__name">{item.name}</span>
                <span>{item.amount}</span>
              </div>
              {item.detail && <div className="rcpt-item__detail">{item.detail}</div>}
            </div>
          ))}
        </div>
      )}

      <div className="rcpt-block">
        <Rows rows={data.totals} />
      </div>

      {data.sections?.map((section) => (
        <div className="rcpt-block" key={section.heading}>
          <div className="rcpt-heading">{section.heading}</div>
          <Rows rows={section.rows} />
        </div>
      ))}

      {data.qr && (
        <div className="rcpt-qr">
          <QRCodeSVG value={data.qr.value} size={132} level="M" marginSize={2} />
          <div className="rcpt-sub">{data.qr.caption}</div>
        </div>
      )}

      <footer className="rcpt-foot">
        <div>{BUSINESS.footer}</div>
        {data.footerLines?.map((line) => <div key={line} className="rcpt-sub">{line}</div>)}
      </footer>
    </div>
  )
}

/**
 * Renders a second copy of the receipt directly under <body>, hidden on screen. The
 * print stylesheet hides the whole app (#root) and shows only this copy, so
 * window.print() outputs just the receipt - no modal chrome, no page behind it.
 */
export function ReceiptPrintPortal({ data }: { data: ReceiptData }) {
  return createPortal(
    <div className="rcpt-print-root"><ReceiptSheet data={data} /></div>,
    document.body,
  )
}

/** Preview + Print/Close buttons, for use inside any modal. */
export function ReceiptPreview({
  data,
  onPrint = () => window.print(),
}: {
  data: ReceiptData
  onPrint?: () => void
}) {
  return (
    <>
      <div className="rcpt-preview"><ReceiptSheet data={data} /></div>
      <ReceiptPrintPortal data={data} />
      <button type="button" className="rcpt-print-btn" onClick={onPrint}>Print receipt</button>
    </>
  )
}
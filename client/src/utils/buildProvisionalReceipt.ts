import type { Cart, CompleteSaleResult, PaymentInput } from '../types/sale'
import { calculateLineBreakdowns, type CartTotals } from './CartMath'

/**
 * A CompleteSaleResult the receipt UI can render immediately, built from local
 * (client-computed) totals rather than the server's authoritative response — because
 * there IS no server response yet when this is used; the sale is sitting in the offline
 * queue. `pending: true` is what the UI checks to show "will sync" instead of treating
 * this as final. Per-line amounts and tax come from calculateLineBreakdowns — the same
 * discount-distribution + VAT math calculateCartTotals uses — so these lines sum exactly
 * to `totals.total` instead of drifting from it.
 */
export function buildProvisionalReceipt(
  cart: Cart,
  totals: CartTotals,
  payments: PaymentInput[],
  clientTransactionId: string
): CompleteSaleResult & { pending: true } {
  const breakdowns = calculateLineBreakdowns(cart)

  return {
    saleId: clientTransactionId, // no real server-issued id yet — this is what ties it back to the queue entry
    saleDate: new Date().toISOString(),
    subtotal: totals.rawSubtotal,
    discountTotal: totals.discountTotal,
    taxTotal: totals.taxTotal,
    total: totals.total,
    status: 'PendingSync',
    items: breakdowns.map(({ line, finalLineAmount, lineTax }) => ({
      productId: line.product.id,
      productName: line.product.name,
      unitId: line.unitId,
      quantity: line.quantity,
      unitPrice: line.product.salePrice,
      discountAmount: line.discountAmount,
      taxAmount: lineTax,
      lineTotal: finalLineAmount,
    })),
    payments: payments.map((p) => ({
      method: p.method,
      amount: p.amount,
      status: p.method === 'Cash' ? 'Success' : 'Pending',
      externalReference: null,
    })),
    pending: true,
  }
}
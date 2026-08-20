import type { ProductSummary } from './product'

export type PaymentMethod = 'Cash' | 'Mpesa' | 'Card'

/** One line in an in-progress cart, kept client-side (Dexie) until checkout completes.
 * Price/tax shown here are for display only — the server recomputes everything
 * authoritatively from the current Product row at completion time. */
export interface CartLine {
  /** Stable id for this line within the cart (not a server id) — lets the same
   * product appear as two lines if the cashier deliberately wants that. */
  lineId: string
  product: ProductSummary
  unitId: string | null
  quantity: number
  /** Manager-applied markdown on this line, in KES. */
  discountAmount: number
}

export type CartStatus = 'active' | 'held'

export interface Cart {
  id: string
  registerId: string
  cashierId: string
  customerId: string | null
  /** Free-text label shown in the "resume a held sale" list, e.g. a customer name —
   * purely a local convenience, never sent to the server. */
  heldLabel: string | null
  status: CartStatus
  lines: CartLine[]
  cartDiscountAmount: number
  createdAt: string
  updatedAt: string
}

export interface PaymentInput {
  method: PaymentMethod
  amount: number
  mpesaPhoneNumber?: string | null
}

export interface CompleteSaleRequest {
  registerId: string
  customerId: string | null
  items: {
    productId: string
    unitId: string | null
    quantity: number
    discountAmount: number
  }[]
  cartDiscountAmount: number
  discountApprovalToken: string | null
  payments: PaymentInput[]
}

export interface SaleItemResult {
  productId: string
  productName: string
  unitId: string | null
  quantity: number
  unitPrice: number
  discountAmount: number
  taxAmount: number
  lineTotal: number
}

export interface PaymentResult {
  method: PaymentMethod
  amount: number
  status: string
  externalReference: string | null
}

export interface CompleteSaleResult {
  saleId: string
  saleDate: string
  subtotal: number
  discountTotal: number
  taxTotal: number
  total: number
  status: string
  items: SaleItemResult[]
  payments: PaymentResult[]
}

/** Thrown by salesService when the server responds 428 Precondition Required —
 * i.e. the discount needs Manager/Admin approval before the sale can complete. */
export class DiscountApprovalRequiredError extends Error {
  public readonly totalDiscount: number
  public readonly thresholdMessage: string

  constructor(totalDiscount: number, thresholdMessage: string) {
    super(thresholdMessage)
    this.name = 'DiscountApprovalRequiredError'
    this.totalDiscount = totalDiscount
    this.thresholdMessage = thresholdMessage
  }
}
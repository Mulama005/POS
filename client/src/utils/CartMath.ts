import type { Cart } from '../types/sale'

const STANDARD_VAT_RATE = 0.16

/**
 * Mirrors the server's override rule. At or above list the agreed price is the unit price;
 * below list the unit price stays at list and the difference is a per-unit markdown.
 */
export function linePricing(line: Cart['lines'][number]) {
  const list = line.product.salePrice
  const agreed = line.overrideUnitPrice != null && line.overrideUnitPrice > 0 ? round2(line.overrideUnitPrice) : list
  return {
    list,
    agreed,
    unitBase: Math.max(agreed, list),
    markdownPerUnit: Math.max(0, list - agreed),
    overridden: agreed !== list,
  }
}

export interface CartTotals {
  rawSubtotal: number
  lineDiscountTotal: number
  cartDiscount: number
  discountTotal: number
  taxTotal: number
  total: number
}

export interface CartLineBreakdown {
  line: Cart['lines'][number]
  rawAmount: number // unit base price * quantity, before any discount
  lineDiscount: number // line discount + price-override markdown, clamped to rawAmount
  finalLineAmount: number // after line discount AND this line's proportional share of the cart discount, rounded
  lineTax: number // VAT portion of finalLineAmount, rounded
}

interface FullBreakdown {
  breakdowns: CartLineBreakdown[]
  rawSubtotal: number
  afterLineDiscountsTotal: number
  cartDiscount: number
}

function computeFullBreakdown(cart: Cart): FullBreakdown {
  let rawSubtotal = 0
  let afterLineDiscountsTotal = 0

  const intermediate = cart.lines.map((line) => {
    const pricing = linePricing(line)
    const rawAmount = pricing.unitBase * line.quantity
    const lineDiscount = Math.min(line.discountAmount + pricing.markdownPerUnit * line.quantity, rawAmount)
    const afterLineDiscount = rawAmount - lineDiscount
    rawSubtotal += rawAmount
    afterLineDiscountsTotal += afterLineDiscount
    return { line, rawAmount, lineDiscount, afterLineDiscount }
  })

  const cartDiscount = Math.min(cart.cartDiscountAmount, afterLineDiscountsTotal)

  const breakdowns: CartLineBreakdown[] = intermediate.map(({ line, rawAmount, lineDiscount, afterLineDiscount }) => {
    const shareOfCartDiscount =
      afterLineDiscountsTotal > 0 ? cartDiscount * (afterLineDiscount / afterLineDiscountsTotal) : 0
    const finalLineAmount = round2(afterLineDiscount - shareOfCartDiscount)
    const lineTax =
      line.product.taxClass === 'Standard' ? round2(finalLineAmount - finalLineAmount / (1 + STANDARD_VAT_RATE)) : 0
    return { line, rawAmount, lineDiscount, finalLineAmount, lineTax }
  })

  return { breakdowns, rawSubtotal, afterLineDiscountsTotal, cartDiscount }
}

/**
 * Per-line discount distribution + VAT extraction, exposed separately so anything that
 * needs the same server-mirroring math per line (e.g. buildProvisionalReceipt, for the
 * offline receipt) can reuse it instead of re-approximating it — that mismatch is what
 * caused line totals not to sum to the header total on offline sales.
 */
export function calculateLineBreakdowns(cart: Cart): CartLineBreakdown[] {
  return computeFullBreakdown(cart).breakdowns
}

/** Mirrors SalesController.Complete's pricing logic for live display purposes only. The
 * server is the sole source of truth — this exists so the cashier sees an accurate running
 * total before hitting "Complete sale," not to make any decision that matters for money. */
export function calculateCartTotals(cart: Cart): CartTotals {
  const { breakdowns, rawSubtotal, afterLineDiscountsTotal, cartDiscount } = computeFullBreakdown(cart)

  const lineDiscountTotal = rawSubtotal - afterLineDiscountsTotal
  const taxTotal = breakdowns.reduce((sum, b) => sum + b.lineTax, 0)
  const total = breakdowns.reduce((sum, b) => sum + b.finalLineAmount, 0)

  return {
    rawSubtotal: round2(rawSubtotal),
    lineDiscountTotal: round2(lineDiscountTotal),
    cartDiscount: round2(cartDiscount),
    discountTotal: round2(lineDiscountTotal + cartDiscount),
    taxTotal: round2(taxTotal),
    total: round2(total),
  }
}

function round2(value: number): number {
  return Math.round(value * 100) / 100
}
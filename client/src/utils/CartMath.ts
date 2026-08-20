import type { Cart } from '../types/sale'

const STANDARD_VAT_RATE = 0.16

export interface CartTotals {
  rawSubtotal: number
  lineDiscountTotal: number
  cartDiscount: number
  discountTotal: number
  taxTotal: number
  total: number
}

/** Mirrors SalesController.Complete's pricing logic for live display purposes only. The
 * server is the sole source of truth — this exists so the cashier sees an accurate running
 * total before hitting "Complete sale," not to make any decision that matters for money. */
export function calculateCartTotals(cart: Cart): CartTotals {
  let rawSubtotal = 0
  let afterLineDiscountsTotal = 0

  const afterLineDiscounts = cart.lines.map((line) => {
    const rawAmount = line.product.salePrice * line.quantity
    const lineDiscount = Math.min(line.discountAmount, rawAmount)
    const afterLineDiscount = rawAmount - lineDiscount
    rawSubtotal += rawAmount
    afterLineDiscountsTotal += afterLineDiscount
    return { line, rawAmount, afterLineDiscount }
  })

  const cartDiscount = Math.min(cart.cartDiscountAmount, afterLineDiscountsTotal)

  let taxTotal = 0
  let total = 0

  for (const { line, afterLineDiscount } of afterLineDiscounts) {
    const shareOfCartDiscount =
      afterLineDiscountsTotal > 0 ? cartDiscount * (afterLineDiscount / afterLineDiscountsTotal) : 0
    const finalLineAmount = round2(afterLineDiscount - shareOfCartDiscount)
    const lineTax = line.product.taxClass === 'Standard' ? round2(finalLineAmount - finalLineAmount / (1 + STANDARD_VAT_RATE)) : 0
    taxTotal += lineTax
    total += finalLineAmount
  }

  const lineDiscountTotal = rawSubtotal - afterLineDiscountsTotal

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
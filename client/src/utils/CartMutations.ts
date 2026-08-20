import type { Cart, CartLine } from '../types/sale'
import type { ProductSummary } from '../types/product'

function newLineId(): string {
  return crypto.randomUUID()
}

/** Adds a product to the cart — increments quantity if the same product (with no specific
 * unit selected) is already a line, rather than creating a duplicate row. */
export function addProductToCart(cart: Cart, product: ProductSummary): Cart {
  const existing = cart.lines.find((l) => l.product.id === product.id && l.unitId === null)
  if (existing) {
    return updateLineQuantity(cart, existing.lineId, existing.quantity + 1)
  }
  const line: CartLine = {
    lineId: newLineId(),
    product,
    unitId: null,
    quantity: 1,
    discountAmount: 0,
  }
  return { ...cart, lines: [...cart.lines, line] }
}

/** Quantity <= 0 removes the line entirely. */
export function updateLineQuantity(cart: Cart, lineId: string, quantity: number): Cart {
  if (quantity <= 0) {
    return removeLine(cart, lineId)
  }
  return {
    ...cart,
    lines: cart.lines.map((l) => (l.lineId === lineId ? { ...l, quantity } : l)),
  }
}

export function updateLineDiscount(cart: Cart, lineId: string, discountAmount: number): Cart {
  const clamped = Math.max(0, discountAmount)
  return {
    ...cart,
    lines: cart.lines.map((l) => (l.lineId === lineId ? { ...l, discountAmount: clamped } : l)),
  }
}

export function removeLine(cart: Cart, lineId: string): Cart {
  return { ...cart, lines: cart.lines.filter((l) => l.lineId !== lineId) }
}

export function setCartDiscount(cart: Cart, amount: number): Cart {
  return { ...cart, cartDiscountAmount: Math.max(0, amount) }
}
import type { Cart } from '../types/sale'
import { formatKes } from '../utils/currency'

interface HeldSalesListProps {
  heldCarts: Cart[]
  onResume: (cartId: string) => void
  onDiscard: (cartId: string) => void
}

function cartTotalPreview(cart: Cart): number {
  return cart.lines.reduce((sum, l) => sum + l.product.salePrice * l.quantity - l.discountAmount, 0)
}

export function HeldSalesList({ heldCarts, onResume, onDiscard }: HeldSalesListProps) {
  if (heldCarts.length === 0) {
    return null
  }

  return (
    <div className="held-sales">
      <h3 className="held-sales__title">Held sales ({heldCarts.length})</h3>
      <ul className="held-sales__list">
        {heldCarts.map((cart) => (
          <li key={cart.id} className="held-sales__item">
            <div className="held-sales__item-info">
              <span className="held-sales__item-label">{cart.heldLabel || `${cart.lines.length} item(s)`}</span>
              <span className="held-sales__item-meta">
                {cart.lines.length} item(s) · {formatKes(cartTotalPreview(cart))}
              </span>
            </div>
            <div className="held-sales__item-actions">
              <button type="button" onClick={() => onResume(cart.id)}>
                Resume
              </button>
              <button
                type="button"
                className="held-sales__discard"
                onClick={() => {
                  if (window.confirm('Discard this held sale? This cannot be undone.')) {
                    onDiscard(cart.id)
                  }
                }}
              >
                Discard
              </button>
            </div>
          </li>
        ))}
      </ul>
    </div>
  )
}
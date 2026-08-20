import type { Cart, CartLine } from '../types/sale'
import type { CartTotals } from '../utils/CartMath'
import { formatKes } from '../utils/currency'

interface CartPanelProps {
  cart: Cart
  totals: CartTotals
  onQuantityChange: (lineId: string, quantity: number) => void
  onLineDiscountChange: (lineId: string, discount: number) => void
  onRemoveLine: (lineId: string) => void
  onCartDiscountChange: (discount: number) => void
  disabled?: boolean
}

export function CartPanel({
  cart,
  totals,
  onQuantityChange,
  onLineDiscountChange,
  onRemoveLine,
  onCartDiscountChange,
  disabled,
}: CartPanelProps) {
  return (
    <div className="cart-panel">
      {cart.lines.length === 0 ? (
        <div className="cart-panel__empty">Scan or search to add items.</div>
      ) : (
        <ul className="cart-panel__lines">
          {cart.lines.map((line) => (
            <CartLineRow
              key={line.lineId}
              line={line}
              disabled={disabled}
              onQuantityChange={(qty) => onQuantityChange(line.lineId, qty)}
              onDiscountChange={(discount) => onLineDiscountChange(line.lineId, discount)}
              onRemove={() => onRemoveLine(line.lineId)}
            />
          ))}
        </ul>
      )}

      <div className="cart-panel__cart-discount">
        <label htmlFor="cart-discount">Cart discount (KES)</label>
        <input
          id="cart-discount"
          type="number"
          min={0}
          step="0.01"
          value={cart.cartDiscountAmount || ''}
          placeholder="0.00"
          disabled={disabled || cart.lines.length === 0}
          onChange={(e) => onCartDiscountChange(Math.max(0, Number(e.target.value) || 0))}
        />
      </div>

      <div className="cart-panel__totals">
        <div className="cart-panel__totals-row">
          <span>Subtotal</span>
          <span>{formatKes(totals.rawSubtotal)}</span>
        </div>
        {totals.discountTotal > 0 && (
          <div className="cart-panel__totals-row cart-panel__totals-row--discount">
            <span>Discount</span>
            <span>-{formatKes(totals.discountTotal)}</span>
          </div>
        )}
        <div className="cart-panel__totals-row">
          <span>Tax (VAT, included)</span>
          <span>{formatKes(totals.taxTotal)}</span>
        </div>
        <div className="cart-panel__totals-row cart-panel__totals-row--total">
          <span>Total</span>
          <span>{formatKes(totals.total)}</span>
        </div>
      </div>
    </div>
  )
}

interface CartLineRowProps {
  line: CartLine
  disabled?: boolean
  onQuantityChange: (quantity: number) => void
  onDiscountChange: (discount: number) => void
  onRemove: () => void
}

function CartLineRow({ line, disabled, onQuantityChange, onDiscountChange, onRemove }: CartLineRowProps) {
  const lineRaw = line.product.salePrice * line.quantity

  return (
    <li className="cart-line">
      <div className="cart-line__main">
        <span className="cart-line__name">{line.product.name}</span>
        <span className="cart-line__unit-price">{formatKes(line.product.salePrice)} each</span>
      </div>

      <div className="cart-line__controls">
        <div className="cart-line__qty">
          <button type="button" disabled={disabled || line.quantity <= 1} onClick={() => onQuantityChange(line.quantity - 1)}>
            −
          </button>
          <span>{line.quantity}</span>
          <button
            type="button"
            disabled={disabled || line.quantity >= line.product.stockQuantity + line.quantity}
            onClick={() => onQuantityChange(line.quantity + 1)}
          >
            +
          </button>
        </div>

        <label className="cart-line__discount">
          Discount
          <input
            type="number"
            min={0}
            max={lineRaw}
            step="0.01"
            value={line.discountAmount || ''}
            placeholder="0.00"
            disabled={disabled}
            onChange={(e) => onDiscountChange(Math.max(0, Number(e.target.value) || 0))}
          />
        </label>

        <button type="button" className="cart-line__remove" disabled={disabled} onClick={onRemove} aria-label={`Remove ${line.product.name}`}>
          ×
        </button>
      </div>
    </li>
  )
}
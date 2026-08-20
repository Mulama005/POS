import { useCallback, useEffect, useState } from 'react'
import { isAxiosError } from 'axios'
import { useAuth } from '../hooks/useAuth'
import { listRegisters, type RegisterSummary } from '../services/registersService'
import { approveDiscount, completeSale } from '../services/SalesService'
import { closeTill, getCurrentTillSession, openTill } from '../services/tillService'
import { createEmptyCart, deleteCart, getActiveCart, listHeldCarts, saveCart } from '../store/CartDb'
import {
  addProductToCart,
  removeLine,
  setCartDiscount,
  updateLineDiscount,
  updateLineQuantity,
} from '../utils/CartMutations'
import { calculateCartTotals } from '../utils/CartMath'
import { ProductSearchBar } from '../components/ProductSearchBar'
import { CartPanel } from '../components/CartPanel'
import { HeldSalesList } from '../components/HeldSalesList'
import { PaymentModal } from '../components/PaymentModal'
import { DiscountApprovalModal } from '../components/DiscountApprovalModal'
import { TillOpenModal } from '../components/TillOpenModal'
import { TillCloseModal } from '../components/TillCloseModal'
import { DiscountApprovalRequiredError } from '../types/sale'
import type { Cart, CompleteSaleResult, PaymentInput } from '../types/sale'
import type { ProductSummary } from '../types/product'
import type { ApiErrorBody } from '../types/auth'
import type { TillReconciliation, TillSession } from '../types/till'
import { formatKes } from '../utils/currency'
import './CheckoutPage.css'

function getErrorMessage(err: unknown, fallback: string): string {
  if (isAxiosError<ApiErrorBody>(err) && err.response?.data?.message) {
    return err.response.data.message
  }
  return fallback
}

export function CheckoutPage() {
  const { user } = useAuth()
  const isManagerOrAdmin = user?.role === 'Manager' || user?.role === 'Admin'

  const [registers, setRegisters] = useState<RegisterSummary[]>([])
  const [registersError, setRegistersError] = useState<string | null>(null)
  // Only set once the user explicitly picks from the dropdown (Manager/Admin). Everyone
  // else, and the initial state before any pick, falls through to the derived default
  // below — no effect needed to "initialize" this, which avoids the extra render pass
  // an effect + setState would cause every time registers finish loading.
  const [registerOverride, setRegisterOverride] = useState<string | null>(null)

  const [cart, setCart] = useState<Cart | null>(null)
  const [heldCarts, setHeldCarts] = useState<Cart[]>([])

  const [paymentModalOpen, setPaymentModalOpen] = useState(false)
  const [completing, setCompleting] = useState(false)
  const [completeError, setCompleteError] = useState<string | null>(null)
  const [receipt, setReceipt] = useState<CompleteSaleResult | null>(null)

  const [pendingApproval, setPendingApproval] = useState<{ payments: PaymentInput[]; message: string } | null>(null)
  const [approving, setApproving] = useState(false)
  const [approvalError, setApprovalError] = useState<string | null>(null)

  useEffect(() => {
    listRegisters()
      .then(setRegisters)
      .catch((err) => setRegistersError(getErrorMessage(err, 'Could not load registers.')))
  }, [])

  // Cashier: locked to their assigned register (RegisterAccessHandler enforces this
  // server-side too). Manager/Admin: default to the first active register, but can
  // switch — till reconciliation is their job across every register, not just one.
  const defaultRegisterId =
    user?.role === 'Cashier' && user.assignedRegisterId ? user.assignedRegisterId : (registers[0]?.id ?? null)
  const selectedRegisterId = registerOverride ?? defaultRegisterId

  const selectedRegister = registers.find((r) => r.id === selectedRegisterId) ?? null

  const [currentTillSession, setCurrentTillSession] = useState<TillSession | null>(null)
  const [tillOpenModalOpen, setTillOpenModalOpen] = useState(false)
  const [tillCloseModalOpen, setTillCloseModalOpen] = useState(false)
  const [tillActionSubmitting, setTillActionSubmitting] = useState(false)
  const [tillActionError, setTillActionError] = useState<string | null>(null)
  const [tillReconciliation, setTillReconciliation] = useState<TillReconciliation | null>(null)

  useEffect(() => {
    if (!selectedRegisterId) {
      setCurrentTillSession(null)
      return
    }
    let cancelled = false
    getCurrentTillSession(selectedRegisterId)
      .then((session) => {
        if (!cancelled) setCurrentTillSession(session)
      })
      .catch(() => {
        if (!cancelled) setCurrentTillSession(null)
      })
    return () => {
      cancelled = true
    }
  }, [selectedRegisterId])

  const refreshRegistersAndTill = async () => {
    if (!selectedRegisterId) return
    const [freshRegisters, freshSession] = await Promise.all([
      listRegisters(),
      getCurrentTillSession(selectedRegisterId),
    ])
    setRegisters(freshRegisters)
    setCurrentTillSession(freshSession)
  }

  const handleOpenTill = async (openingFloat: number) => {
    if (!selectedRegisterId) return
    setTillActionSubmitting(true)
    setTillActionError(null)
    try {
      await openTill(selectedRegisterId, openingFloat)
      setTillOpenModalOpen(false)
      await refreshRegistersAndTill()
    } catch (err) {
      setTillActionError(getErrorMessage(err, 'Could not open the till. Try again.'))
    } finally {
      setTillActionSubmitting(false)
    }
  }

  const handleCloseTill = async (countedCashAtClose: number) => {
    if (!selectedRegisterId) return
    setTillActionSubmitting(true)
    setTillActionError(null)
    try {
      const reconciliation = await closeTill(selectedRegisterId, countedCashAtClose)
      setTillCloseModalOpen(false)
      setTillReconciliation(reconciliation)
      await refreshRegistersAndTill()
    } catch (err) {
      setTillActionError(getErrorMessage(err, 'Could not close the till. Try again.'))
    } finally {
      setTillActionSubmitting(false)
    }
  }

  const refreshHeldCarts = useCallback(async (registerId: string) => {
    setHeldCarts(await listHeldCarts(registerId))
  }, [])

  useEffect(() => {
    if (!selectedRegisterId || !user) return

    let cancelled = false
    ;(async () => {
      const existing = await getActiveCart(selectedRegisterId)
      const active = existing ?? createEmptyCart(selectedRegisterId, user.id)
      if (!existing) await saveCart(active)
      if (!cancelled) setCart(active)
      await refreshHeldCarts(selectedRegisterId)
    })()

    return () => {
      cancelled = true
    }
  }, [selectedRegisterId, user, refreshHeldCarts])

  const persist = useCallback((updated: Cart) => {
    setCart(updated)
    void saveCart(updated)
  }, [])

  const handleAddProduct = (product: ProductSummary) => {
    if (!cart) return
    persist(addProductToCart(cart, product))
  }
  const handleQuantityChange = (lineId: string, quantity: number) => {
    if (!cart) return
    persist(updateLineQuantity(cart, lineId, quantity))
  }
  const handleLineDiscountChange = (lineId: string, discount: number) => {
    if (!cart) return
    persist(updateLineDiscount(cart, lineId, discount))
  }
  const handleRemoveLine = (lineId: string) => {
    if (!cart) return
    persist(removeLine(cart, lineId))
  }
  const handleCartDiscountChange = (discount: number) => {
    if (!cart) return
    persist(setCartDiscount(cart, discount))
  }

  const handleHold = async () => {
    if (!cart || cart.lines.length === 0 || !selectedRegisterId || !user) return
    const label = window.prompt('Label this held sale (e.g. customer name) — optional:')?.trim() || null
    await saveCart({ ...cart, status: 'held', heldLabel: label })
    const fresh = createEmptyCart(selectedRegisterId, user.id)
    await saveCart(fresh)
    setCart(fresh)
    await refreshHeldCarts(selectedRegisterId)
  }

  const handleResume = async (cartId: string) => {
    if (!selectedRegisterId || !user) return

    if (cart && cart.lines.length > 0) {
      const proceed = window.confirm('This holds your current sale so you can resume the other one. Continue?')
      if (!proceed) return
      await saveCart({ ...cart, status: 'held' })
    } else if (cart) {
      await deleteCart(cart.id)
    }

    const target = heldCarts.find((c) => c.id === cartId)
    if (!target) return
    const resumed: Cart = { ...target, status: 'active' }
    await saveCart(resumed)
    setCart(resumed)
    await refreshHeldCarts(selectedRegisterId)
  }

  const handleDiscardHeld = async (cartId: string) => {
    if (!selectedRegisterId) return
    await deleteCart(cartId)
    await refreshHeldCarts(selectedRegisterId)
  }

  const totals = cart ? calculateCartTotals(cart) : null

  const submitSale = async (payments: PaymentInput[], discountApprovalToken: string | null) => {
    if (!cart || !selectedRegisterId) return
    setCompleting(true)
    setCompleteError(null)
    try {
      const result = await completeSale({
        registerId: selectedRegisterId,
        customerId: cart.customerId,
        items: cart.lines.map((l) => ({
          productId: l.product.id,
          unitId: l.unitId,
          quantity: l.quantity,
          discountAmount: l.discountAmount,
        })),
        cartDiscountAmount: cart.cartDiscountAmount,
        discountApprovalToken,
        payments,
      })

      setReceipt(result)
      setPaymentModalOpen(false)
      setPendingApproval(null)
      await deleteCart(cart.id)
      if (user) {
        const fresh = createEmptyCart(selectedRegisterId, user.id)
        await saveCart(fresh)
        setCart(fresh)
      }
    } catch (err) {
      if (err instanceof DiscountApprovalRequiredError) {
        setPendingApproval({ payments, message: err.message })
        setPaymentModalOpen(false)
      } else {
        setCompleteError(getErrorMessage(err, 'Could not complete the sale. Try again.'))
      }
    } finally {
      setCompleting(false)
    }
  }

  const handleApproveDiscount = async (email: string, password: string) => {
    if (!pendingApproval) return
    setApproving(true)
    setApprovalError(null)
    try {
      const token = await approveDiscount(email, password)
      await submitSale(pendingApproval.payments, token)
    } catch (err) {
      setApprovalError(getErrorMessage(err, 'Approval failed. Check the credentials and try again.'))
    } finally {
      setApproving(false)
    }
  }

  if (registersError) {
    return <div className="checkout-screen checkout-screen--error">{registersError}</div>
  }

  if (!cart || !totals) {
    return <div className="checkout-screen">Loading checkout…</div>
  }

  return (
    <div className="checkout-screen">
      <div className="checkout-header">
        <h1>Checkout</h1>

        {isManagerOrAdmin && registers.length > 1 ? (
          <select
            className="checkout-register-picker"
            value={selectedRegisterId ?? ''}
            onChange={(e) => setRegisterOverride(e.target.value)}
          >
            {registers.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name} — till {r.isTillOpen ? 'open' : 'closed'}
              </option>
            ))}
          </select>
        ) : (
          selectedRegister && (
            <span className="checkout-register-indicator">
              {selectedRegister.name} — till {selectedRegister.isTillOpen ? 'open' : 'closed'}
            </span>
          )
        )}
      </div>

      {selectedRegister && !selectedRegister.isTillOpen && (
        <div className="checkout-till-warning">
          <span>This register's till is closed — a sale can't be completed until it's opened.</span>
          <button type="button" className="checkout-till-action" onClick={() => setTillOpenModalOpen(true)}>
            Open till
          </button>
        </div>
      )}

      {selectedRegister?.isTillOpen && currentTillSession && (
        <div className="checkout-till-status">
          <span>
            Till open since {new Date(currentTillSession.openedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })} by{' '}
            {currentTillSession.openedByName} · opening float {formatKes(currentTillSession.openingFloat)}
          </span>
          <button type="button" className="checkout-till-action" onClick={() => setTillCloseModalOpen(true)}>
            Close till
          </button>
        </div>
      )}

      <div className="checkout-body">
        <div className="checkout-main">
          <ProductSearchBar onAdd={handleAddProduct} disabled={completing} />
          <HeldSalesList
            heldCarts={heldCarts}
            onResume={(id) => void handleResume(id)}
            onDiscard={(id) => void handleDiscardHeld(id)}
          />
        </div>

        <div className="checkout-side">
          <CartPanel
            cart={cart}
            totals={totals}
            onQuantityChange={handleQuantityChange}
            onLineDiscountChange={handleLineDiscountChange}
            onRemoveLine={handleRemoveLine}
            onCartDiscountChange={handleCartDiscountChange}
            disabled={completing}
          />

          <div className="checkout-actions">
            <button type="button" disabled={cart.lines.length === 0 || completing} onClick={() => void handleHold()}>
              Hold sale
            </button>
            <button
              type="button"
              className="checkout-complete-btn"
              disabled={cart.lines.length === 0 || completing || !selectedRegister?.isTillOpen}
              onClick={() => setPaymentModalOpen(true)}
            >
              Complete sale
            </button>
          </div>

          {completeError && <div className="checkout-error">{completeError}</div>}
        </div>
      </div>

      {paymentModalOpen && (
        <PaymentModal
          totalDue={totals.total}
          submitting={completing}
          errorMessage={completeError}
          onCancel={() => setPaymentModalOpen(false)}
          onSubmit={(payments) => void submitSale(payments, null)}
        />
      )}

      {pendingApproval && (
        <DiscountApprovalModal
          discountAmount={totals.discountTotal}
          message={pendingApproval.message}
          submitting={approving}
          errorMessage={approvalError}
          onCancel={() => setPendingApproval(null)}
          onApprove={(email, password) => void handleApproveDiscount(email, password)}
        />
      )}

      {receipt && (
        <div className="checkout-modal-backdrop" role="dialog" aria-modal="true">
          <div className="checkout-modal">
            <h2>Sale complete</h2>
            <p className="checkout-modal__subtitle">Total: {receipt.total.toFixed(2)} KES</p>
            <ul className="receipt-items">
              {receipt.items.map((item) => (
                <li key={item.productId}>
                  {item.quantity} × {item.productName} — {item.lineTotal.toFixed(2)}
                </li>
              ))}
            </ul>
            <div className="checkout-modal__actions">
              <button type="button" onClick={() => setReceipt(null)}>
                New sale
              </button>
            </div>
          </div>
        </div>
      )}
      {tillOpenModalOpen && selectedRegister && (
        <TillOpenModal
          registerName={selectedRegister.name}
          submitting={tillActionSubmitting}
          errorMessage={tillActionError}
          onCancel={() => {
            setTillOpenModalOpen(false)
            setTillActionError(null)
          }}
          onOpen={(openingFloat) => void handleOpenTill(openingFloat)}
        />
      )}

      {tillCloseModalOpen && selectedRegister && (
        <TillCloseModal
          registerName={selectedRegister.name}
          submitting={tillActionSubmitting}
          errorMessage={tillActionError}
          onCancel={() => {
            setTillCloseModalOpen(false)
            setTillActionError(null)
          }}
          onClose={(countedCashAtClose) => void handleCloseTill(countedCashAtClose)}
        />
      )}

      {tillReconciliation && (
        <div className="checkout-modal-backdrop" role="dialog" aria-modal="true">
          <div className="checkout-modal">
            <h2>Till closed</h2>
            <p className="checkout-modal__subtitle">
              {new Date(tillReconciliation.openedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })} –{' '}
              {new Date(tillReconciliation.closedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
            </p>

            <div className="till-reconciliation">
              <div className="till-reconciliation__row">
                <span>Opening float</span>
                <span>{formatKes(tillReconciliation.openingFloat)}</span>
              </div>
              <div className="till-reconciliation__row">
                <span>Cash sales</span>
                <span>{formatKes(tillReconciliation.cashSalesTotal)}</span>
              </div>
              <div className="till-reconciliation__row till-reconciliation__row--strong">
                <span>Expected cash</span>
                <span>{formatKes(tillReconciliation.expectedCashAtClose)}</span>
              </div>
              <div className="till-reconciliation__row">
                <span>Counted cash</span>
                <span>{formatKes(tillReconciliation.countedCashAtClose)}</span>
              </div>
              <div
                className={`till-reconciliation__row till-reconciliation__row--variance ${
                  Math.abs(tillReconciliation.variance) < 0.01
                    ? 'till-reconciliation__row--ok'
                    : tillReconciliation.variance > 0
                      ? 'till-reconciliation__row--over'
                      : 'till-reconciliation__row--short'
                }`}
              >
                <span>Variance</span>
                <span>
                  {tillReconciliation.variance > 0.005 && `${formatKes(tillReconciliation.variance)} over`}
                  {tillReconciliation.variance < -0.005 && `${formatKes(Math.abs(tillReconciliation.variance))} short`}
                  {Math.abs(tillReconciliation.variance) < 0.01 && 'Exact'}
                </span>
              </div>

              {(tillReconciliation.mpesaSalesTotal > 0 || tillReconciliation.cardSalesTotal > 0) && (
                <p className="till-reconciliation__note">
                  Also collected {formatKes(tillReconciliation.mpesaSalesTotal)} via M-Pesa and{' '}
                  {formatKes(tillReconciliation.cardSalesTotal)} via card this shift (not part of the cash count).
                </p>
              )}
            </div>

            <div className="checkout-modal__actions">
              <button type="button" onClick={() => setTillReconciliation(null)}>
                Done
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
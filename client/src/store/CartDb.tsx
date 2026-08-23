import Dexie, { type Table } from 'dexie'
import type { Cart } from '../types/sale'

/**
 * Local-first cart persistence for Step 24 (checkout). This is deliberately built on Dexie
 * (already a project dependency, unused until now) rather than plain localStorage, because
 * Dexie is what Phase 7 (steps 34-36, the offline data layer / sync engine) is expected to
 * use for the full offline-first store. That means this module is a genuine down-payment on
 * Phase 7, not throwaway scaffolding: when Phase 7 lands, it should extend this database
 * (add tables for the sync queue, other offline-capable entities) rather than replace it.
 *
 * Scope today: an in-progress cart survives a page reload or a lost connection, and a
 * cashier can hold one sale and resume it later without losing state. What this does NOT
 * do yet (Phase 7's job): sync a completed-while-offline sale to the server once
 * connectivity returns, or resolve conflicts between devices.
 */
class CartDatabase extends Dexie {
  carts!: Table<Cart, string>

  constructor() {
    super('AyiyaPosCartDb')
    this.version(1).stores({
      // Indexed on registerId + status so "show this register's held sales" is a fast
      // lookup rather than a full-table scan as the held list grows over a shift.
      carts: 'id, registerId, status, updatedAt',
    })
  }
}

export const cartDb = new CartDatabase()

function nowIso(): string {
  return new Date().toISOString()
}

function newId(): string {
  return crypto.randomUUID()
}

export function createEmptyCart(registerId: string, cashierId: string): Cart {
  const timestamp = nowIso()
  return {
    id: newId(),
    registerId,
    cashierId,
    customerId: null,
    heldLabel: null,
    status: 'active',
    lines: [],
    cartDiscountAmount: 0,
    createdAt: timestamp,
    updatedAt: timestamp,
  }
}

export async function saveCart(cart: Cart): Promise<void> {
  await cartDb.carts.put({ ...cart, updatedAt: nowIso() })
}

export async function getCart(id: string): Promise<Cart | undefined> {
  return cartDb.carts.get(id)
}

export async function deleteCart(id: string): Promise<void> {
  await cartDb.carts.delete(id)
}

/** Every held sale for this register, most recently held first — for the "resume a sale" list. */
export async function listHeldCarts(registerId: string): Promise<Cart[]> {
  const carts = await cartDb.carts
    .where('registerId')
    .equals(registerId)
    .and((c) => c.status === 'held')
    .toArray()
  return carts.sort((a, b) => b.updatedAt.localeCompare(a.updatedAt))
}

/** The single in-progress (not held) cart for this register, if any — a cashier has at
 * most one active cart open at a time; everything else is parked as 'held'. */
export async function getActiveCart(registerId: string): Promise<Cart | undefined> {
  const carts = await cartDb.carts
    .where('registerId')
    .equals(registerId)
    .and((c) => c.status === 'active')
    .toArray()
  return carts[0]
}
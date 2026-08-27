import Dexie, { type Table } from "dexie";
import type { ProductSummary } from "../types/product";
import type { CompleteSaleRequest } from "../types/sale";

// ---------- Local cache tables (Step 34) ----------

// Extends the real ProductSummary DTO rather than redeclaring its fields, so the cache
// can't silently drift out of sync with the server contract (that's exactly how the
// `price` vs `salePrice` and missing-`taxClass` bugs happened). Only offline-specific
// bookkeeping fields are added on top.
export interface CachedProduct extends ProductSummary {
  isSerialized: boolean;
  updatedAt: string; // ISO timestamp from the server, used to decide if a refresh is needed
}

export interface CachedUnit {
  id: string; // matches backend Unit.Id (serialized items — IMEI/serial-tracked)
  productId: string;
  serialNumber: string;
  status: "InStock" | "Sold" | "Reserved"; // local view only — server is authoritative
}

// ---------- Action queue (Step 34/35) ----------

export type QueuedActionType = "Sale" | "StockAdjustment";
export type QueuedActionStatus = "Pending" | "Syncing" | "Synced" | "Conflict" | "Failed" | "NeedsApproval";

export interface QueuedAction {
  // clientTransactionId is the whole reason sync can be safe to retry: it's generated
  // once, client-side, at the moment the action happens (not at sync time), and the
  // backend uses it as an idempotency key. If a sync request succeeds but the response
  // never reaches the client (dropped connection right as it comes back online), retrying
  // the same action with the same clientTransactionId is a no-op on the server instead of
  // a duplicate sale.
  clientTransactionId: string;
  type: QueuedActionType;
  payload: CompleteSaleRequest | StockAdjustmentActionPayload;
  status: QueuedActionStatus;
  createdAt: string; // ISO — this is the ordering key for replay (Step 35)
  registerId: string;
  syncAttempts: number;
  lastError?: string;
  conflictReason?: string; // populated only when status === "Conflict" (Step 36)
}

// NOTE: SaleLineItem/SaleActionPayload below are no longer what QueuedAction.payload
// uses for Sale actions (that's CompleteSaleRequest now, from types/sale — see above).
// They look like an earlier placeholder shape from before types/sale.ts was fully
// built out. Left in place rather than deleted since a sync-engine file (Step 35/36)
// may still reference them and I haven't seen that code — worth confirming before
// removing.
export interface SaleLineItem {
  productId: string;
  unitId?: string; // present only for serialized items
  quantity: number;
  unitPrice: number;
}

export interface SaleActionPayload {
  lineItems: SaleLineItem[];
  paymentMethod: "Cash" | "MPesa" | "Card" | "Credit";
  customerId?: string; // present for credit sales (Deni ledger)
  totalAmount: number;
}

export interface StockAdjustmentActionPayload {
  productId: string;
  quantityDelta: number; // e.g. -1 for a sale-driven decrement done outside a full sale record, +N for a manual recount
  reason: string;
}

class PosOfflineDatabase extends Dexie {
  products!: Table<CachedProduct, string>;
  units!: Table<CachedUnit, string>;
  actionQueue!: Table<QueuedAction, string>; // keyed by clientTransactionId

  constructor() {
    super("pos-offline-db");

    // Version 1 schema. Never edit this block once it's shipped to any register — add a
    // new .version() instead, or existing local data on registers already running v1
    // breaks on upgrade.
    this.version(1).stores({
      products: "id, sku, categoryId, updatedAt",
      units: "id, productId, serialNumber, status",
      actionQueue: "clientTransactionId, status, createdAt, type",
    });

    // Version 2: adds a `barcode` index (for offline barcode-scan lookups) and renames
    // `price` -> `salePrice` on cached products to match ProductSummary, plus backfills
    // `taxClass` so CartMath's VAT calc works correctly on products added from the
    // offline cache. The upgrade function fixes up any product rows a register already
    // has cached from v1 — without it, those rows would sit without a correct
    // salePrice/taxClass/barcode until the next successful refreshCatalogCache, which is
    // exactly the moment they might be needed (offline).
    this.version(2)
      .stores({
        products: "id, sku, barcode, categoryId, updatedAt",
        units: "id, productId, serialNumber, status",
        actionQueue: "clientTransactionId, status, createdAt, type",
      })
      .upgrade((tx) =>
        tx
          .table("products")
          .toCollection()
          .modify((product: Record<string, unknown>) => {
            if ("price" in product && !("salePrice" in product)) {
              product.salePrice = product.price;
              delete product.price;
            }
            if (typeof product.barcode !== "string") {
              product.barcode = "";
            }
            if (product.taxClass === undefined) {
              // Best-effort default so pre-v2 cached rows don't compute zero tax by
              // omission. This is a stopgap, not a source of truth — it's overwritten by
              // the real value on the next refreshCatalogCache while online.
              product.taxClass = "Standard";
            }
            if (typeof product.categoryName !== "string") {
              product.categoryName = "";
            }
            if (product.imageUrl === undefined) {
              product.imageUrl = null;
            }
          })
      );
  }
}

export const db = new PosOfflineDatabase();
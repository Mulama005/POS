import { db, type CachedProduct, type CachedUnit } from "./db";
import type { ProductSummary } from "../types/product";

// Adjust these to your actual API client/base URL setup (axios instance, etc.) — shown
// here with plain fetch to stay dependency-free.
const API_BASE = import.meta.env.VITE_API_BASE_URL;

/**
 * Full refresh of the local catalog cache. Call this on login and periodically while
 * online (e.g. every few minutes) NOT on every screen render. The whole point of this
 * cache is that the register can go fully offline and still have a recent, usable
 * snapshot; refreshing too aggressively just wastes bandwidth without improving the
 * offline experience.
 */
export async function refreshCatalogCache(accessToken: string): Promise<void> {
  const response = await fetch(`${API_BASE}/api/products/catalog-snapshot`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });

  if (!response.ok) {
    // Deliberately non-fatal — if this fails (e.g. already offline), the existing
    // cached data just stays as-is. A failed refresh should never wipe what's there.
    console.warn("Catalog refresh failed; continuing with existing local cache.");
    return;
  }

  const data: { products: CachedProduct[]; units: CachedUnit[] } = await response.json();

  // Replace wholesale inside one transaction — avoids a half-updated cache if this gets
  // interrupted partway through (e.g. connection drops mid-refresh).
  await db.transaction("rw", db.products, db.units, async () => {
    await db.products.clear();
    await db.units.clear();
    await db.products.bulkPut(data.products);
    await db.units.bulkPut(data.units);
  });
}

/** Local-only stock check used at the point of sale while potentially offline. */
export async function getLocalStock(productId: string): Promise<number> {
  const product = await db.products.get(productId);
  return product?.stockQuantity ?? 0;
}

/**
 * Optimistically decrements the LOCAL cache the moment a sale is queued, so the next
 * cashier action (or the same cashier ringing up two of the same low-stock item back to
 * back while offline) sees an accurate-so-far count. This is a local optimistic update
 * only — the server's number is what actually matters and gets reconciled on sync
 * (Step 35/36); this just keeps the offline UI from looking obviously wrong in the
 * meantime.
 */
export async function decrementLocalStock(productId: string, quantity: number): Promise<void> {
  const product = await db.products.get(productId);
  if (!product) return;
  await db.products.update(productId, {
    stockQuantity: Math.max(0, product.stockQuantity - quantity),
  });
}

// CachedProduct extends ProductSummary with a couple of offline-only bookkeeping
// fields (isSerialized, updatedAt) that ProductSearchBar / CartMutations have no use
// for — strip them so callers get exactly the shape they already know how to handle.
function toProductSummary(cached: CachedProduct): ProductSummary {
  return {
    id: cached.id,
    name: cached.name,
    sku: cached.sku,
    salePrice: cached.salePrice,
    stockQuantity: cached.stockQuantity,
  } as ProductSummary;
}

/**
 * Offline fallback for ProductsService.searchProducts. Same "name or SKU contains the
 * query" behavior, just run against the local cache instead of the server. This is a
 * full table scan (Dexie has no built-in substring index) — fine for a single store's
 * catalog size, but if the product count grows into the tens of thousands this should
 * move to a prefix-indexed lookup instead.
 */
export async function searchLocalProducts(query: string): Promise<ProductSummary[]> {
  const lower = query.toLowerCase();
  const matches = await db.products
    .filter((p) => p.name.toLowerCase().includes(lower) || p.sku.toLowerCase().includes(lower))
    .limit(20) // same reasoning as any live search — don't dump the entire catalog into the dropdown
    .toArray();

  return matches.map(toProductSummary);
}

/**
 * Offline fallback for ProductsService.lookupProductByBarcode. Uses the `barcode`
 * index added in db.ts v2, so this stays fast even as the catalog grows.
 */
export async function lookupLocalProductByBarcode(barcode: string): Promise<ProductSummary | null> {
  const match = await db.products.where("barcode").equals(barcode).first();
  return match ? toProductSummary(match) : null;
}
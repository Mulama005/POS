import { apiClient } from "../services/apiClient";
import type { ProductSummary } from "../types/product";
import { db, type CachedProduct, type CachedUnit } from "./db";

/**
 * Full refresh of the local catalog cache. Call this on login and periodically while
 * online — NOT on every screen render. Uses apiClient (not raw fetch) so it
 * automatically gets the Authorization header and 401-refresh-and-retry handling that
 * every other request in this app already relies on — a raw fetch call here would
 * silently skip all of that.
 */
export async function refreshCatalogCache(): Promise<void> {
  let data: { products: CachedProduct[]; units: CachedUnit[] };

  try {
    const response = await apiClient.get<{ products: CachedProduct[]; units: CachedUnit[] }>(
      "/api/products/catalog-snapshot"
    );
    data = response.data;
  } catch {
    // Deliberately non-fatal — if this fails (e.g. offline, or the request 401s and
    // even the refresh attempt fails), the existing cached data just stays as-is. A
    // failed refresh should never wipe what's already there.
    console.warn("Catalog refresh failed; continuing with existing local cache.");
    return;
  }

  await db.transaction("rw", db.products, db.units, async () => {
    await db.products.clear();
    await db.units.clear();
    await db.products.bulkPut(data.products);
    await db.units.bulkPut(data.units);
  });
}

export async function getLocalStock(productId: string): Promise<number> {
  const product = await db.products.get(productId);
  return product?.stockQuantity ?? 0;
}

export async function decrementLocalStock(productId: string, quantity: number): Promise<void> {
  const product = await db.products.get(productId);
  if (!product) return;
  await db.products.update(productId, {
    stockQuantity: Math.max(0, product.stockQuantity - quantity),
  });
}

function toProductSummary(cached: CachedProduct): ProductSummary {
  const { isSerialized: _isSerialized, updatedAt: _updatedAt, ...summary } = cached;
  return summary;
}

/** Offline fallback for ProductsService.searchProducts — same "name or SKU contains
 * the query" behavior, run against the local cache instead of the server. */
export async function searchLocalProducts(query: string, limit = 20): Promise<ProductSummary[]> {
  const trimmed = query.trim().toLowerCase();
  if (!trimmed) return [];
  const matches = await db.products
    .filter((p) => p.name.toLowerCase().includes(trimmed) || p.sku.toLowerCase().includes(trimmed))
    .limit(limit)
    .toArray();
  return matches.map(toProductSummary);
}

/** Offline fallback for ProductsService.lookupProductByBarcode — uses the `barcode`
 * index added in db.ts v2, so this stays fast as the catalog grows. */
export async function lookupLocalProductByBarcode(barcode: string): Promise<ProductSummary | null> {
  const match = await db.products.where("barcode").equals(barcode).first();
  return match ? toProductSummary(match) : null;
}
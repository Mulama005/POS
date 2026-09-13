import { isAxiosError } from "axios";
import { apiClient } from "../services/apiClient";
import { db, type QueuedAction } from "./db";
import { refreshCatalogCache } from "./catalogCache";

let syncInProgress = false;

/**
 * The ordering here is not incidental — it's the Step 35 requirement made literal:
 * sales sync FIRST (chronological order), THEN stock adjustments, THEN a full catalog
 * refresh to pick up whatever changed as a result — including any conflicts flagged
 * during sale sync.
 *
 * No accessToken parameter — apiClient's own interceptor already attaches the current
 * Authorization header (and transparently refreshes it on 401) for every request, the
 * same as every other call in this app. Passing a token here separately would just be
 * a second, easy-to-desync source of truth for something apiClient already owns.
 */
export async function runSync(): Promise<void> {
  if (syncInProgress) return;
  syncInProgress = true;

  try {
    await syncActionsOfType("Sale");
    await syncActionsOfType("StockAdjustment");
    await refreshCatalogCache();
  } finally {
    syncInProgress = false;
  }
}

async function syncActionsOfType(type: QueuedAction["type"]): Promise<void> {
  const pending = await db.actionQueue
    .where("status")
    .anyOf(["Pending", "Failed"])
    .and((a) => a.type === type)
    .sortBy("createdAt");

  for (const action of pending) {
    await syncOneAction(action);
  }
}

async function syncOneAction(action: QueuedAction): Promise<void> {
  await db.actionQueue.update(action.clientTransactionId, { status: "Syncing" });

  // Sales replay through the SAME /api/sales endpoint a live checkout uses — your real
  // SalesController.Complete, not a separate sync-only route. action.payload IS already
  // the full, correct request body (a real CompleteSaleRequest) — sent as-is.
  const endpoint = action.type === "Sale" ? "/api/sales" : "/api/sync/stock-adjustments";

  try {
    await apiClient.post(endpoint, action.payload);

    // Success — including the case where the server recognizes this
    // clientTransactionId as already-processed (idempotent replay) and returns the
    // original result without redoing the work.
    await db.actionQueue.update(action.clientTransactionId, { status: "Synced" });
  } catch (err) {
    if (isAxiosError(err) && err.response?.status === 409) {
      // Step 36 conflict — not deleted, not silently retried forever, flagged for a
      // human. SalesController.Complete has already logged it server-side too.
      await db.actionQueue.update(action.clientTransactionId, {
        status: "Conflict",
        conflictReason: err.response.data?.message ?? "Conflict detected during sync.",
      });
      return;
    }

    if (isAxiosError(err) && err.response?.status === 428) {
      // Discount exceeded the approval threshold — can't be resolved by a background
      // retry, since approving it needs a live Manager/Admin login.
      await db.actionQueue.update(action.clientTransactionId, {
        status: "NeedsApproval",
        conflictReason: "This sale's discount needs Manager/Admin approval before it can sync.",
      });
      return;
    }

    await db.actionQueue.update(action.clientTransactionId, {
      status: "Failed",
      syncAttempts: action.syncAttempts + 1,
      lastError: err instanceof Error ? err.message : String(err),
    });
    // Stop this type's queue here rather than skipping ahead — attempting the next
    // action out of order defeats the point of ordered replay.
    throw err;
  }
}

/**
 * Registers a listener that runs a sync every time the browser comes back online.
 * Returns a cleanup function — call it from a useEffect's return so repeated
 * calls (e.g. on every accessToken change) don't stack up duplicate listeners.
 */
export function scheduleSyncOnReconnect(): () => void {
  const handler = () => {
    runSync().catch((e) => console.error("Sync run failed:", e));
  };
  window.addEventListener("online", handler);
  return () => window.removeEventListener("online", handler);
}
import { db, type QueuedAction } from "./db";
import { refreshCatalogCache } from "./catalogCache";

const API_BASE = import.meta.env.VITE_API_BASE_URL;

let syncInProgress = false;

/**
 * The ordering here is not incidental — it's the Step 35 requirement made literal:
 * sales sync FIRST (in chronological order, since a sale queued at 9am and one at 9:05am
 * both draw from the same stock and must be resolved in the order they actually
 * happened), THEN stock adjustments, THEN a full catalog refresh to pick up whatever
 * changed as a result — including any conflicts flagged during sale sync.
 */
export async function runSync(accessToken: string): Promise<void> {
  if (syncInProgress) return;
  syncInProgress = true;

  try {
    await syncActionsOfType(accessToken, "Sale");
    await syncActionsOfType(accessToken, "StockAdjustment");
    await refreshCatalogCache(accessToken);
  } finally {
    syncInProgress = false;
  }
}

async function syncActionsOfType(accessToken: string, type: QueuedAction["type"]): Promise<void> {
  const pending = await db.actionQueue
    .where("status")
    .anyOf(["Pending", "Failed"])
    .and((a) => a.type === type)
    .sortBy("createdAt");

  for (const action of pending) {
    await syncOneAction(accessToken, action);
  }
}

async function syncOneAction(accessToken: string, action: QueuedAction): Promise<void> {
  await db.actionQueue.update(action.clientTransactionId, { status: "Syncing" });

  // Sales replay through the SAME /api/sales endpoint a live checkout uses — your real
  // SalesController.Complete, not a separate sync-only route. action.payload IS already
  // the full, correct request body (a real CompleteSaleRequest, including its own
  // registerId and clientTransactionId) — sent as-is, no wrapping, no extra fields.
  // Wrapping it again here was the bug: it doesn't match what SalesController.Complete
  // actually binds from the request body.
  const endpoint = action.type === "Sale" ? "/api/sales" : "/api/sync/stock-adjustments";

  try {
    const response = await fetch(`${API_BASE}${endpoint}`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${accessToken}`,
      },
      body: JSON.stringify(action.payload),
    });

    if (response.status === 409) {
      // Step 36 conflict — the backend detected this action can't be applied as-is
      // (insufficient stock, or a serialized unit already sold on another register).
      // Not deleted, not silently retried forever — flagged for a human, and
      // SalesController.Complete has already logged it to SaleConflicts server-side.
      const body = await response.json();
      await db.actionQueue.update(action.clientTransactionId, {
        status: "Conflict",
        conflictReason: body.message ?? "Conflict detected during sync.",
      });
      return;
    }

    if (response.status === 428) {
      // Discount exceeded the approval threshold and no valid token was included.
      // Can't be resolved by a background retry — approving a discount needs a live
      // Manager/Admin login, which a silent sync can't prompt for. Surfaced distinctly
      // so staff know this needs a person, not just "try again later."
      await db.actionQueue.update(action.clientTransactionId, {
        status: "NeedsApproval",
        conflictReason: "This sale's discount needs Manager/Admin approval before it can sync.",
      });
      return;
    }

    if (!response.ok) {
      throw new Error(`Sync failed with status ${response.status}`);
    }

    // Success — including the case where the server recognizes this
    // clientTransactionId as already-processed (idempotent replay) and returns the
    // original result without redoing the work.
    await db.actionQueue.update(action.clientTransactionId, { status: "Synced" });
  } catch (err) {
    await db.actionQueue.update(action.clientTransactionId, {
      status: "Failed",
      syncAttempts: action.syncAttempts + 1,
      lastError: err instanceof Error ? err.message : String(err),
    });
    // Deliberately stop processing this action type's queue on the first failure rather
    // than skipping ahead — if action #3 failed because the connection just dropped
    // again, attempting #4 out of order defeats the entire point of ordered replay.
    throw err;
  }
}

/** Call this once at app startup, and again whenever useOnlineStatus flips to true. */
export function scheduleSyncOnReconnect(getAccessToken: () => string | null): void {
  window.addEventListener("online", () => {
    const token = getAccessToken();
    if (token) runSync(token).catch((e) => console.error("Sync run failed:", e));
  });
}
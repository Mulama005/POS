import { useEffect, useState } from "react";

/**
 * navigator.onLine alone is unreliable — it reports "online" as soon as SOME network
 * interface is up, even if the actual backend is unreachable (captive portal, backend
 * down, DNS issue). This adds a lightweight real-connectivity check on top, so the
 * offline indicator reflects "can I actually reach my server" rather than "does this
 * device have a network adapter."
 */
export function useOnlineStatus(pingUrl: string, intervalMs = 15000): boolean {
  const [isOnline, setIsOnline] = useState(navigator.onLine);

  useEffect(() => {
    let cancelled = false;

    async function checkConnectivity() {
      if (!navigator.onLine) {
        if (!cancelled) setIsOnline(false);
        return;
      }
      try {
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 5000);
        const response = await fetch(pingUrl, { method: "HEAD", signal: controller.signal });
        clearTimeout(timeout);
        if (!cancelled) setIsOnline(response.ok);
      } catch {
        if (!cancelled) setIsOnline(false);
      }
    }

    checkConnectivity();
    const interval = setInterval(checkConnectivity, intervalMs);
    window.addEventListener("online", checkConnectivity);
    window.addEventListener("offline", checkConnectivity);

    return () => {
      cancelled = true;
      clearInterval(interval);
      window.removeEventListener("online", checkConnectivity);
      window.removeEventListener("offline", checkConnectivity);
    };
  }, [pingUrl, intervalMs]);

  return isOnline;
}

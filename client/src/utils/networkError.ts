import { isAxiosError } from "axios";

/**
 * True only when the request never reached the server at all — no response object on
 * the axios error means the browser couldn't complete the HTTP request (offline, DNS
 * failure, timeout, server unreachable). A 400/409/428/500 all HAVE a response — those
 * are the server correctly telling you something about the sale, not a connectivity
 * problem, and should keep surfacing as real errors, not silently queue.
 */
export function isNetworkError(err: unknown): boolean {
  return isAxiosError(err) && !err.response;
}

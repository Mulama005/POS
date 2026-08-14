import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'

/**
 * Central axios instance. Two things make this the only place these rules
 * should ever need to be written:
 *
 * 1. `withCredentials: true` — required for the browser to send/receive the
 *    httpOnly `posRefreshToken` cookie from Step 8, since client and API run
 *    on different ports (different origins) in dev.
 * 2. The response interceptor below handles an expired access token by
 *    silently calling /api/auth/refresh once and retrying the original
 *    request — callers never need to think about token expiry themselves.
 */
export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? 'http://localhost:5179',
  withCredentials: true,
})

// Set by AuthProvider once it's mounted — lets the interceptor read the
// current access token and hand back a refreshed one, without this module
// needing to import the context directly (would create a circular import:
// AuthContext uses apiClient, apiClient would use AuthContext).
let currentAccessToken: string | null = null
let onTokenRefreshed: ((token: string) => void) | null = null
let onRefreshFailed: (() => void) | null = null

export function registerAuthHandlers(handlers: {
  getAccessToken: () => string | null
  onTokenRefreshed: (token: string) => void
  onRefreshFailed: () => void
}) {
  currentAccessToken = handlers.getAccessToken()
  onTokenRefreshed = handlers.onTokenRefreshed
  onRefreshFailed = handlers.onRefreshFailed
}

export function setCurrentAccessToken(token: string | null) {
  currentAccessToken = token
}

apiClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  if (currentAccessToken) {
    config.headers.Authorization = `Bearer ${currentAccessToken}`
  }
  return config
})

let refreshInFlight: Promise<string | null> | null = null

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as (InternalAxiosRequestConfig & { _retried?: boolean }) | undefined

    const isAuthEndpoint = originalRequest?.url?.includes('/api/auth/login')
      || originalRequest?.url?.includes('/api/auth/refresh')
      || originalRequest?.url?.includes('/api/auth/mfa/verify')

    if (error.response?.status !== 401 || !originalRequest || originalRequest._retried || isAuthEndpoint) {
      return Promise.reject(error)
    }

    originalRequest._retried = true

    // Multiple requests can 401 at once (e.g. a page firing several queries
    // on load) — share one in-flight refresh instead of racing several.
    if (!refreshInFlight) {
      refreshInFlight = apiClient
        .post('/api/auth/refresh')
        .then((res) => {
          const token = res.data.accessToken as string
          setCurrentAccessToken(token)
          onTokenRefreshed?.(token)
          return token
        })
        .catch(() => {
          onRefreshFailed?.()
          return null
        })
        .finally(() => {
          refreshInFlight = null
        })
    }

    const newToken = await refreshInFlight
    if (!newToken) {
      return Promise.reject(error)
    }

    originalRequest.headers.Authorization = `Bearer ${newToken}`
    return apiClient(originalRequest)
  },
)
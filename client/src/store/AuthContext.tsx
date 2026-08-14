import { createContext, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { login as apiLogin, logout as apiLogout, refresh as apiRefresh, verifyMfa as apiVerifyMfa } from '../services/authService'
import { isMfaRequired } from '../types/auth'
import type { AuthUser, LoginMfaRequiredResponse } from '../types/auth'
import { registerAuthHandlers, setCurrentAccessToken } from '../services/apiClient'

type AuthStatus = 'loading' | 'authenticated' | 'unauthenticated'


type LoginStage = 'password' | 'awaiting-mfa'

export interface AuthContextValue {
  status: AuthStatus
  user: AuthUser | null
  loginStage: LoginStage
  pendingChallenge: LoginMfaRequiredResponse | null
  login: (email: string, password: string) => Promise<{ mfaRequired: boolean }>
  submitMfaCode: (code: string) => Promise<void>
  cancelMfaStep: () => void
  logout: () => Promise<void>
  /**
   * Re-fetches the current user via /api/auth/refresh and applies it — used
   * after MfaSetupPage successfully enables MFA, so `user.mfaEnabled` flips
   * to true in the app's state immediately, without needing a full page
   * reload. Deliberately reuses the same refresh call as initial rehydration
   * rather than a separate "patch user" mutator, so there's one source of
   * truth for "what does the server say the current session looks like."
   */
  refreshSession: () => Promise<void>
}

// Exported (not just used internally) so hooks/useAuth.ts can consume it via
// useContext without this file needing to also own the hook — keeps the
// provider component and the consumer hook in the folders this project
// actually uses (store/ vs hooks/) instead of bundled in one file.
// eslint-disable-next-line react-refresh/only-export-components
export const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('loading')
  const [user, setUser] = useState<AuthUser | null>(null)
  const [accessToken, setAccessTokenState] = useState<string | null>(null)
  const [loginStage, setLoginStage] = useState<LoginStage>('password')
  const [pendingChallenge, setPendingChallenge] = useState<LoginMfaRequiredResponse | null>(null)

  const applySession = useCallback((token: string, sessionUser: AuthUser) => {
    setCurrentAccessToken(token)
    setAccessTokenState(token)
    setUser(sessionUser)
    setStatus('authenticated')
    setLoginStage('password')
    setPendingChallenge(null)
  }, [])

  const clearSession = useCallback(() => {
    setCurrentAccessToken(null)
    setAccessTokenState(null)
    setUser(null)
    setStatus('unauthenticated')
  }, [])

  // Rehydrate on load: the access token only ever lives in memory (never
  // localStorage — an XSS bug shouldn't be able to walk off with a usable
  // token), so a full page refresh always starts with no token in hand.
  // The httpOnly refresh cookie is what makes this work: if it's present
  // and valid, /api/auth/refresh hands back a fresh token + the user object
  // in one call, no separate /me round-trip needed.
  useEffect(() => {
    let cancelled = false
    apiRefresh()
      .then((res) => {
        if (!cancelled) applySession(res.accessToken, res.user)
      })
      .catch(() => {
        if (!cancelled) clearSession()
      })
    return () => {
      cancelled = true
    }
  }, [applySession, clearSession])

  useEffect(() => {
    registerAuthHandlers({
      getAccessToken: () => accessToken,
      onTokenRefreshed: (token) => setAccessTokenState(token),
      onRefreshFailed: () => clearSession(),
    })
  }, [accessToken, clearSession])

  const login = useCallback(async (email: string, password: string) => {
    const response = await apiLogin(email, password)

    if (isMfaRequired(response)) {
      setPendingChallenge(response)
      setLoginStage('awaiting-mfa')
      return { mfaRequired: true }
    }

    applySession(response.accessToken, response.user)
    return { mfaRequired: false }
  }, [applySession])

  const submitMfaCode = useCallback(async (code: string) => {
    if (!pendingChallenge) {
      throw new Error('No MFA challenge in progress.')
    }
    // On failure, the backend has already consumed the challenge token
    // (single-use, see Step 10) — the caller (LoginPage) is responsible
    // for catching the rejection and calling cancelMfaStep() to send the
    // user back to the password screen rather than letting them retry
    // the same challenge token.
    const result = await apiVerifyMfa(pendingChallenge.challengeToken, code)
    applySession(result.accessToken, result.user)
  }, [pendingChallenge, applySession])

  const cancelMfaStep = useCallback(() => {
    setPendingChallenge(null)
    setLoginStage('password')
  }, [])

  const logout = useCallback(async () => {
    try {
      await apiLogout()
    } finally {
      clearSession()
    }
  }, [clearSession])

  const refreshSession = useCallback(async () => {
    const res = await apiRefresh()
    applySession(res.accessToken, res.user)
  }, [applySession])

  const value = useMemo<AuthContextValue>(() => ({
    status,
    user,
    loginStage,
    pendingChallenge,
    login,
    submitMfaCode,
    cancelMfaStep,
    logout,
    refreshSession,
  }), [status, user, loginStage, pendingChallenge, login, submitMfaCode, cancelMfaStep, logout, refreshSession])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
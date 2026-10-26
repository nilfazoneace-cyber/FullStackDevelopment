import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { api, configureApi } from '../api'
import { useNotice } from '../notice/notice-context'
import type { User } from '../types'
import { AuthContext } from './auth-context'

interface Session {
  token: string
  expiresAt: number // epoch milliseconds
  user: User
}

/**
 * Holds the JWT and the user IN MEMORY (React state). Deliberately NOT localStorage/sessionStorage:
 * any script injected by an XSS attack can read those, but cannot read a closure variable.
 * Trade-off: a page refresh logs you out (accepted by the assignment).
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const { notify } = useNotice()
  const [session, setSession] = useState<Session | null>(null)

  // The API module needs the token synchronously, even in the same tick as login(). A ref is
  // updated immediately, whereas state only updates on the next render.
  const tokenRef = useRef<string | null>(null)

  const logout = useCallback(() => {
    tokenRef.current = null
    setSession(null) // ProtectedRoute notices user === null and redirects to /login
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const response = await api.login(email, password) // throws ApiError(401) on bad credentials
    tokenRef.current = response.accessToken
    setSession({
      token: response.accessToken,
      expiresAt: new Date(response.expiresAt).getTime(),
      user: response.user,
    })
  }, [])

  // Connect the API module to this provider. useLayoutEffect runs BEFORE any child's useEffect,
  // so the very first data fetch of a page already has the handlers in place.
  useLayoutEffect(() => {
    configureApi({
      getToken: () => tokenRef.current,
      onUnauthorized: () => {
        if (tokenRef.current) {
          // A 401 on a protected call = token expired/invalid: wipe the session.
          logout()
          notify('Your session has expired. Please sign in again.', 'error')
        }
      },
      onForbidden: (message) => notify(message, 'error'), // stay on the page, just explain
    })
  }, [logout, notify])

  // Sign out automatically when the token's lifetime ends (instead of waiting for a failing request).
  useEffect(() => {
    if (!session) return
    const msLeft = Math.max(0, session.expiresAt - Date.now())
    const timer = window.setTimeout(() => {
      logout()
      notify('Your session has expired. Please sign in again.', 'error')
    }, Math.min(msLeft, 2_147_483_647)) // setTimeout max delay is ~24.8 days
    return () => window.clearTimeout(timer)
  }, [session, logout, notify])

  const value = useMemo(() => ({ user: session?.user ?? null, login, logout }), [session, login, logout])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

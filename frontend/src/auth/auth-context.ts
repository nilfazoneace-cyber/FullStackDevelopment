import { createContext, useContext } from 'react'
import type { User } from '../types'

export interface AuthContextValue {
  /** The signed-in user, or null when signed out. Held in memory ONLY. */
  user: User | null
  login: (email: string, password: string) => Promise<void>
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>')
  return ctx
}

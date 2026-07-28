import { createContext } from 'react'

export interface AuthUser {
  username: string
}

export interface AuthContextValue {
  user: AuthUser | null
  isAuthenticated: boolean
  signIn: (username: string, password: string) => Promise<void>
  signOut: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

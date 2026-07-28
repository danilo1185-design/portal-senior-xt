import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { getStoredToken, setStoredToken, setUnauthorizedHandler } from '../api/client'
import { login as loginRequest, logout as logoutRequest } from '../api/auth'
import { AuthContext } from './authContext'
import type { AuthContextValue, AuthUser } from './authContext'

/** Lê o "sub" do JWT e trata token expirado como inexistente. */
function readUsernameFromToken(token: string): string | null {
  try {
    const payload = token.split('.')[1]
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'))
    const claims = JSON.parse(json) as { sub?: string; exp?: number }
    if (claims.exp && claims.exp * 1000 <= Date.now()) {
      return null
    }
    return claims.sub ?? null
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => {
    const token = getStoredToken()
    if (!token) {
      return null
    }
    const username = readUsernameFromToken(token)
    if (!username) {
      setStoredToken(null)
      return null
    }
    return { username }
  })

  const signIn = useCallback(async (username: string, password: string) => {
    const result = await loginRequest(username, password)
    setStoredToken(result.token)
    setUser({ username: result.username })
  }, [])

  const signOut = useCallback(() => {
    // Best-effort: avisa o servidor para descartar as credenciais do ERP guardadas na sessão.
    void logoutRequest().catch(() => {})
    setStoredToken(null)
    setUser(null)
  }, [])

  useEffect(() => {
    // 401 numa chamada autenticada = sessão do ERP/token expirou: limpa o estado local.
    setUnauthorizedHandler(() => {
      setStoredToken(null)
      setUser(null)
    })
    return () => setUnauthorizedHandler(null)
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({ user, isAuthenticated: user !== null, signIn, signOut }),
    [user, signIn, signOut],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

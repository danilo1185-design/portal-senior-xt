import axios from 'axios'

const TOKEN_KEY = 'portal-senior.token'

/** Lê o token JWT do portal guardado no navegador. */
export function getStoredToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

/** Grava (ou remove, com null) o token JWT do portal. */
export function setStoredToken(token: string | null): void {
  if (token) {
    localStorage.setItem(TOKEN_KEY, token)
  } else {
    localStorage.removeItem(TOKEN_KEY)
  }
}

// A camada de auth registra aqui o que fazer quando o servidor devolve 401 numa
// chamada autenticada (sessão do ERP/token expirou): normalmente deslogar e voltar ao login.
let unauthorizedHandler: (() => void) | null = null
export function setUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler
}

// Em produção (backend no Render), o front chama a URL absoluta da API via VITE_API_BASE,
// definida no build. Em dev fica "/api" e o proxy do Vite encaminha ao backend local.
const baseURL = (import.meta.env.VITE_API_BASE as string | undefined) ?? '/api'
export const api = axios.create({ baseURL })

api.interceptors.request.use((config) => {
  const token = getStoredToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

api.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      // 401 no próprio login é "usuário/senha inválidos" — não deve disparar logout global.
      const url = error.config?.url ?? ''
      if (!url.includes('/auth/login')) {
        unauthorizedHandler?.()
      }
    }
    return Promise.reject(error)
  },
)

/** Extrai a mensagem amigável do corpo { message } devolvido pela API, com fallback. */
export function extractErrorMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError(error)) {
    if (error.code === 'ERR_NETWORK') {
      return 'Não foi possível conectar ao servidor. Verifique se a API está no ar.'
    }
    const data = error.response?.data as { message?: string } | undefined
    return data?.message ?? fallback
  }
  if (error instanceof Error && error.message) {
    return error.message
  }
  return fallback
}

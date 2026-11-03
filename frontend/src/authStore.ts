import { create } from 'zustand'
import type { AuthResponse } from './types'

interface AuthState {
  auth: AuthResponse | null
  isHydrating: boolean
  setAuth: (auth: AuthResponse) => void
  hydrate: () => Promise<void>
  refresh: () => Promise<boolean>
  logout: () => void
}

const storageKey = 'billares-auth'
const baseUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'
let storedAuth: AuthResponse | null = null

try {
  const value = localStorage.getItem(storageKey)
  storedAuth = value ? JSON.parse(value) as AuthResponse : null
} catch {
  localStorage.removeItem(storageKey)
}

let hydrationStarted = false

export const useAuthStore = create<AuthState>((set) => ({
  auth: null,
  isHydrating: Boolean(storedAuth?.refreshToken),
  setAuth: (auth) => {
    localStorage.setItem(storageKey, JSON.stringify(auth))
    set({ auth, isHydrating: false })
  },
  hydrate: async () => {
    if (hydrationStarted) return
    hydrationStarted = true
    if (!storedAuth?.refreshToken) {
      set({ auth: null, isHydrating: false })
      return
    }
    await useAuthStore.getState().refresh()
    set({ isHydrating: false })
  },
  refresh: async () => {
    const refreshToken = useAuthStore.getState().auth?.refreshToken ?? storedAuth?.refreshToken
    if (!refreshToken) return false
    try {
      const response = await fetch(baseUrl + '/api/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken })
      })
      if (!response.ok) throw new Error('refresh failed')
      const auth = await response.json() as AuthResponse
      localStorage.setItem(storageKey, JSON.stringify(auth))
      storedAuth = auth
      set({ auth, isHydrating: false })
      return true
    } catch {
      localStorage.removeItem(storageKey)
      storedAuth = null
      set({ auth: null, isHydrating: false })
      return false
    }
  },
  logout: () => {
    localStorage.removeItem(storageKey)
    storedAuth = null
    set({ auth: null, isHydrating: false })
  }
}))
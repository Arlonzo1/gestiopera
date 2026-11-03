import { Navigate, Route, Routes } from 'react-router-dom'
import { useEffect } from 'react'
import type { ReactNode } from 'react'
import { isPushConfigured, listenToForegroundMessages } from './firebasePush'
import { useAuthStore } from './authStore'
import { applyBranding, onBrandingChanged, saveBranding } from './branding'
import { getTenantBranding } from './api'
import LoginPage from './pages/LoginPage'
import TablesPage from './pages/TablesPage'
import CashPage from './pages/CashPage'
import DashboardPage from './pages/DashboardPage'
import PosPage from './pages/PosPage'
import ProductsPage from './pages/ProductsPage'
import PurchasesPage from './pages/PurchasesPage'
import InventoryPage from './pages/InventoryPage'
import PhysicalCountPage from './pages/PhysicalCountPage'
import ReportsPage from './pages/ReportsPage'
import EmployeesPage from './pages/EmployeesPage'
import AuditPage from './pages/AuditPage'
import NotificationsPage from './pages/NotificationsPage'
import SettingsPage from './pages/SettingsPage'

function AuthLoading() {
  return <main className="auth-loading" aria-live="polite"><div className="loading-spinner" /><span>Verificando sesión...</span></main>
}

function ProtectedRoute({ children, roles }: { children: ReactNode; roles?: string[] }) {
  const auth = useAuthStore((state) => state.auth)
  const isHydrating = useAuthStore((state) => state.isHydrating)
  if (isHydrating) return <AuthLoading />
  if (!auth) return <Navigate to="/login" replace />
  if (roles && !auth.user.roles.some((role) => roles.includes(role))) return <Navigate to="/tables" replace />
  return children
}

export default function App() {
  const auth = useAuthStore((state) => state.auth)
  const isHydrating = useAuthStore((state) => state.isHydrating)
  const hydrate = useAuthStore((state) => state.hydrate)

  useEffect(() => { void hydrate() }, [hydrate])

  useEffect(() => {
    if (auth) void getTenantBranding().then(saveBranding).catch(() => undefined)
  }, [auth])

  useEffect(() => {
    const updateBranding = () => applyBranding()
    updateBranding()
    return onBrandingChanged(updateBranding)
  }, [])

  useEffect(() => {
    if (!auth || !isPushConfigured()) return
    let unsubscribe: (() => void) | undefined
    void listenToForegroundMessages((title, body) => {
      if (Notification.permission === 'granted') new Notification(title, { body })
    }).then(value => { unsubscribe = value ?? undefined })
    return () => unsubscribe?.()
  }, [auth])

  if (isHydrating) return <AuthLoading />

  return <Routes>
    <Route path="/login" element={auth ? <Navigate to="/tables" replace /> : <LoginPage />} />
    <Route path="/inventory" element={<ProtectedRoute roles={['OWNER', 'ADMINISTRATOR']}><InventoryPage /></ProtectedRoute>} />
    <Route path="/inventory/count" element={<ProtectedRoute roles={['OWNER', 'ADMINISTRATOR']}><PhysicalCountPage /></ProtectedRoute>} />
    <Route path="/purchases" element={<ProtectedRoute roles={['OWNER', 'ADMINISTRATOR']}><PurchasesPage /></ProtectedRoute>} />
    <Route path="/products" element={<ProtectedRoute><ProductsPage /></ProtectedRoute>} />
    <Route path="/pos" element={<ProtectedRoute><PosPage /></ProtectedRoute>} />
    <Route path="/employees" element={<ProtectedRoute roles={['OWNER', 'ADMINISTRATOR']}><EmployeesPage /></ProtectedRoute>} />
    <Route path="/audit" element={<ProtectedRoute roles={['OWNER', 'ADMINISTRATOR']}><AuditPage /></ProtectedRoute>} />
    <Route path="/notifications" element={<ProtectedRoute><NotificationsPage /></ProtectedRoute>} />
    <Route path="/settings" element={<ProtectedRoute roles={['OWNER', 'ADMINISTRATOR']}><SettingsPage /></ProtectedRoute>} />
    <Route path="/reports" element={<ProtectedRoute roles={['OWNER', 'ADMINISTRATOR']}><ReportsPage /></ProtectedRoute>} />
    <Route path="/dashboard" element={<ProtectedRoute roles={["OWNER", "ADMINISTRATOR"]}><DashboardPage /></ProtectedRoute>} />
    <Route path="/tables" element={<ProtectedRoute><TablesPage /></ProtectedRoute>} />
    <Route path="/cash" element={<ProtectedRoute><CashPage /></ProtectedRoute>} />
    <Route path="*" element={<Navigate to={auth ? '/dashboard' : '/login'} replace />} />
  </Routes>
}



import { Banknote, BarChart3, Bell, ClipboardCheck, UserRound, ClipboardList, History, LayoutDashboard, LogOut, Menu, PanelsTopLeft, Package, Settings2, ShoppingCart, X } from 'lucide-react'
import { NavLink } from 'react-router-dom'
import { useAuthStore } from '../authStore'
import { getNotifications } from '../api'
import { useQuery } from '@tanstack/react-query'
import { useEffect, useState, type ReactNode } from 'react'
import { getBranding, onBrandingChanged } from '../branding'

type AppLayoutProps = { eyebrow: string; title: string; actions?: ReactNode; children: ReactNode }

export default function AppLayout({ eyebrow, title, actions, children }: AppLayoutProps) {
  const auth = useAuthStore(state => state.auth)
  const logout = useAuthStore(state => state.logout)
  const [mobileOpen, setMobileOpen] = useState(false)
  const [branding, setBranding] = useState(getBranding)
  useEffect(() => onBrandingChanged(() => setBranding(getBranding())), [])
  useEffect(() => {
    if (!mobileOpen) return
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.body.style.overflow = previousOverflow }
  }, [mobileOpen])
  const canAdminister = auth?.user.roles.some(role => role === 'OWNER' || role === 'ADMINISTRATOR') ?? false
  const unreadNotifications = useQuery({ queryKey: ['notifications-unread'], queryFn: () => getNotifications(true), refetchInterval: 15000 })
  const navClass = ({ isActive }: { isActive: boolean }) => isActive ? 'nav-item active' : 'nav-item'

  return <div className="app-frame">
    <aside className={mobileOpen ? 'sidebar mobile-open' : 'sidebar'}>
      <div className="sidebar-brand">{branding.logoDataUrl ? <img className="brand-logo" src={branding.logoDataUrl} alt="Logo del negocio" /> : <span className="brand-symbol">B</span>}<div><strong>{branding.name}</strong><span>Operacion diaria</span></div></div>
      <nav className="sidebar-nav" aria-label="Navegación principal" onClick={() => setMobileOpen(false)}>
        <p className="nav-label">Operación</p>
        {canAdminister && <NavLink to="/dashboard" className={navClass}><LayoutDashboard size={18} />Resumen</NavLink>}
        <NavLink to="/pos" className={navClass}><ShoppingCart size={18} />Ventas</NavLink>
        <NavLink to="/tables" className={navClass}><PanelsTopLeft size={18} />Mesas de billar</NavLink>
        <NavLink to="/cash" className={navClass}><Banknote size={18} />Caja</NavLink>
        <p className="nav-label secondary-label">Inventario</p>
        <NavLink to="/products" className={navClass}><Package size={18} />Productos</NavLink>
        {canAdminister && <><NavLink to="/purchases" className={navClass}><ClipboardList size={18} />Compras</NavLink><NavLink to="/inventory" className={navClass}><History size={18} />Movimientos</NavLink><NavLink to="/inventory/count" className={navClass}><ClipboardCheck size={18} />Conteo físico</NavLink></>}
        <p className="nav-label secondary-label">Supervisión</p>
        <NavLink to="/notifications" className={navClass}><Bell size={18} />Notificaciones{(unreadNotifications.data?.length ?? 0) > 0 && <span className="notification-badge">{unreadNotifications.data!.length > 99 ? '99+' : unreadNotifications.data!.length}</span>}</NavLink>
        {canAdminister && <><NavLink to="/reports" className={navClass}><BarChart3 size={18} />Reportes</NavLink><NavLink to="/audit" className={navClass}><History size={18} />Auditoría</NavLink><p className="nav-label secondary-label">Administración</p><NavLink to="/employees" className={navClass}><UserRound size={18} />Empleados</NavLink><NavLink to="/settings" className={navClass}><Settings2 size={18} />Configuración</NavLink></>}
      </nav>
      <div className="sidebar-user"><div className="avatar">{auth?.user.fullName.slice(0, 1).toUpperCase()}</div><div><strong>{auth?.user.fullName}</strong><span>{auth?.user.roles[0] ?? 'Usuario'}</span></div><button className="icon-button" title="Cerrar sesión" onClick={logout}><LogOut size={17} /></button></div>
    </aside>
    {mobileOpen && <button className="mobile-sidebar-backdrop" aria-label="Cerrar menú" onClick={() => setMobileOpen(false)} />}
    <main className="app-main">
      <header className="page-header"><div className="page-title"><button className="mobile-nav-toggle icon-button" aria-label={mobileOpen ? 'Cerrar menú' : 'Abrir menú'} onClick={() => setMobileOpen(value => !value)}>{mobileOpen ? <X size={20} /> : <Menu size={20} />}</button><div><p className="eyebrow">{eyebrow}</p><h1>{title}</h1></div></div><div className="page-header-right"><NavLink to="/notifications" className={(unreadNotifications.data?.length ?? 0) > 0 ? 'notification-bell has-unread' : 'notification-bell'} aria-label="Ver notificaciones" title="Ver notificaciones"><Bell size={20} />{(unreadNotifications.data?.length ?? 0) > 0 && <span className="notification-badge">{unreadNotifications.data!.length > 99 ? '99+' : unreadNotifications.data!.length}</span>}</NavLink><div className="page-actions">{actions}</div></div></header>
      {children}
    </main>
  </div>
}






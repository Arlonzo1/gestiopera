import { useQuery } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Activity, ArrowRight, Banknote, Bell, CircleAlert, LayoutDashboard, Package, RefreshCw, ShoppingCart, Table2, TrendingUp } from 'lucide-react'
import AppLayout from '../components/AppLayout'
import { getDashboard } from '../api'

const money = (value: number) => new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(value)
const time = (value: string) => new Intl.DateTimeFormat('es-CO', { hour: '2-digit', minute: '2-digit' }).format(new Date(value))

export default function DashboardPage() {
  const query = useQuery({ queryKey: ['dashboard'], queryFn: getDashboard, refetchInterval: 30000 })
  const data = query.data

  return <AppLayout eyebrow="SUPERVISIÓN" title="Resumen del día" actions={<button className="secondary-button" onClick={() => void query.refetch()}><RefreshCw size={16} />Actualizar</button>}>
    <section className="content dashboard-content">
      {query.isLoading && <div className="empty-state">Cargando resumen...</div>}
      {query.isError && <div className="error-box">No se pudo cargar el resumen. Verifica que la API esté disponible.</div>}
      {data && <>
        <div className="dashboard-intro"><div><p className="muted">Operación de hoy</p><h2>{data.businessDate}</h2></div><span className="live-indicator"><Activity size={15} />Datos actualizados automáticamente</span></div><div className="dashboard-quick-actions"><Link to="/pos"><ShoppingCart size={17} /><span><strong>Nueva venta</strong><small>Registrar cobro</small></span><ArrowRight size={16} /></Link><Link to="/tables"><Table2 size={17} /><span><strong>Ver mesas</strong><small>Revisar operación</small></span><ArrowRight size={16} /></Link><Link to="/products"><Package size={17} /><span><strong>Inventario</strong><small>Revisar existencias</small></span><ArrowRight size={16} /></Link><Link to="/notifications"><Bell size={17} /><span><strong>Notificaciones</strong><small>Ver pendientes</small></span><ArrowRight size={16} /></Link></div>
        <div className="metric-grid">
          <Metric icon={<TrendingUp size={19} />} label="Ventas del día" value={money(data.grossSales)} accent />
          <Metric icon={<Table2 size={19} />} label="Ingresos de billar" value={money(data.billiardRevenue)} />
          <Metric icon={<Banknote size={19} />} label="Efectivo esperado" value={money(data.expectedCash)} />
          <Metric icon={<CircleAlert size={19} />} label="Productos por revisar" value={String(data.lowStockProducts)} warning={data.lowStockProducts > 0} />
        </div>
        <div className="dashboard-grid">
          <section className="dashboard-panel sales-panel"><div className="panel-heading"><div><p className="eyebrow">ACTIVIDAD</p><h2>Ventas recientes</h2></div></div>{data.recentSales.length === 0 ? <p className="muted">Todavía no hay ventas hoy.</p> : <div className="dashboard-list">{data.recentSales.map((sale) => <div className="dashboard-row" key={sale.id}><div><strong>Venta #{sale.sequentialNumber}</strong><span className="muted">{time(sale.createdAt)}</span>{sale.billiardTableName && <span className="muted">{sale.billiardTableName} · {sale.billiardMinutes} min · Billar {money(sale.billiardAmount ?? 0)}</span>}</div><strong>{money(sale.total)}</strong></div>)}</div>}</section>
          <section className="dashboard-panel"><div className="panel-heading"><div><p className="eyebrow">OPERACIÓN</p><h2>Mesas activas</h2></div><strong className="panel-number">{data.activeTables}<span>/{data.totalTables}</span></strong></div><div className="table-progress"><span style={{ width: `${data.totalTables ? Math.min(100, data.activeTables / data.totalTables * 100) : 0}%` }} /></div><p className="muted">Mesas ocupadas en este momento</p><Link className="text-link" to="/tables">Ver mesas de billar</Link></section>
          <section className="dashboard-panel"><div className="panel-heading"><div><p className="eyebrow">INVENTARIO</p><h2>Stock bajo</h2></div></div>{data.lowStockItems.length === 0 ? <p className="muted">No hay productos bajo el mínimo.</p> : <div className="dashboard-list">{data.lowStockItems.slice(0, 5).map((item) => <div className="dashboard-row" key={item.id}><div><strong>{item.name}</strong><span className="muted">Mínimo {item.minimumStock}</span></div><span className={item.currentStock <= item.criticalStock ? 'stock-critical' : 'stock-low'}>{item.currentStock}</span></div>)}</div>}<Link className="text-link" to="/products">Revisar inventario</Link></section>
          <section className="dashboard-panel"><div className="panel-heading"><div><p className="eyebrow">RECAUDO</p><h2>Medios de pago</h2></div></div>{data.paymentBreakdown.length === 0 ? <p className="muted">Sin pagos registrados hoy.</p> : <div className="dashboard-list">{data.paymentBreakdown.map((item) => <div className="dashboard-row" key={item.method}><span>{paymentLabel(item.method)}</span><strong>{money(item.amount)}</strong></div>)}</div>}<div className="dashboard-secondary"><span>Productos</span><strong>{money(data.productRevenue)}</strong></div><div className="dashboard-secondary"><span>Gastos</span><strong>{money(data.expenses)}</strong></div></section>
        </div>
      </>}
    </section>
  </AppLayout>
}

function Metric({ icon, label, value, accent, warning }: { icon: ReactNode; label: string; value: string; accent?: boolean; warning?: boolean }) {
  return <article className={`metric-card ${accent ? 'accent' : ''} ${warning ? 'warning' : ''}`}><span className="metric-icon">{icon}</span><span className="muted">{label}</span><strong>{value}</strong></article>
}

function paymentLabel(method: string) { return ({ Cash: 'Efectivo', Nequi: 'Nequi', Daviplata: 'Daviplata', Card: 'Tarjeta', BankTransfer: 'Transferencia', Other: 'Otro' } as Record<string, string>)[method] ?? method }


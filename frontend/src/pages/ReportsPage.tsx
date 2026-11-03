import { useMemo, useState, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { BarChart3, RefreshCw } from 'lucide-react'
import AppLayout from '../components/AppLayout'
import { EmptyState, Feedback, Panel, SectionHeading } from '../components/ui'
import { downloadReportPdf, getReport } from '../api'
import type { ReportMetrics } from '../types'

const money = (value: number) => new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(value)
const number = (value: number) => new Intl.NumberFormat('es-CO', { maximumFractionDigits: 2 }).format(value)
const paymentLabel = (method: string) => ({ Cash: 'Efectivo', Nequi: 'Nequi', Daviplata: 'Daviplata', Card: 'Tarjeta', BankTransfer: 'Transferencia', Other: 'Otro' } as Record<string, string>)[method] ?? method

export default function ReportsPage() {
  const today = useMemo(() => new Date().toISOString().slice(0, 10), [])
  const [from, setFrom] = useState(today)
  const [to, setTo] = useState(today)
  const query = useQuery({ queryKey: ['reports', from, to], queryFn: () => getReport(from, to), enabled: Boolean(from && to) })
  const invalidRange = Boolean(from && to && from > to)

  return <AppLayout eyebrow="SUPERVISIÓN" title="Reportes" actions={<><button className="secondary-button" onClick={() => void query.refetch()}><RefreshCw size={16} />Actualizar</button><button className="secondary-button" disabled={invalidRange} onClick={() => void downloadReportPdf(from, to)}>Descargar PDF</button></>}>
    <section className="content reports-content">
      <Panel className="report-filters"><SectionHeading eyebrow="PERÍODO" title="Resumen financiero" icon={<BarChart3 size={21} />} /><div className="date-filters"><label>Desde<input type="date" value={from} onChange={event => setFrom(event.target.value)} /></label><label>Hasta<input type="date" value={to} onChange={event => setTo(event.target.value)} /></label></div>{invalidRange && <Feedback kind="error">La fecha inicial no puede ser posterior a la fecha final.</Feedback>}</Panel>
      {query.isLoading && <EmptyState>Cargando reporte...</EmptyState>}
      {query.isError && <Feedback kind="error">No se pudo cargar el reporte. Verifica el rango de fechas.</Feedback>}
      {query.data && !invalidRange && <ReportBody current={query.data.current} previous={query.data.previous} from={query.data.from} to={query.data.to} previousFrom={query.data.previousFrom} previousTo={query.data.previousTo} />}
    </section>
  </AppLayout>
}

function ReportBody({ current, previous, from, to, previousFrom, previousTo }: { current: ReportMetrics; previous: ReportMetrics; from: string; to: string; previousFrom: string; previousTo: string }) {
  return <><div className="report-period muted">Período actual: {from} a {to} · Comparado con: {previousFrom} a {previousTo}</div>
    <div className="metric-grid report-metrics"><Metric label="Ventas brutas" current={current.grossSales} previous={previous.grossSales} /><Metric label="Costo de ventas" current={current.costOfGoodsSold} previous={previous.costOfGoodsSold} /><Metric label="Utilidad bruta" current={current.grossProfit} previous={previous.grossProfit} /><Metric label="Gastos" current={current.expenses} previous={previous.expenses} /><Metric label="Resultado operativo" current={current.operatingResult} previous={previous.operatingResult} /><Metric label="Horas de billar" current={current.billiardHours} previous={previous.billiardHours} /></div>
    <div className="dashboard-grid"><ReportPanel title="Medios de pago"><div className="dashboard-list">{current.paymentBreakdown.length === 0 ? <p className="muted">Sin pagos en el período.</p> : current.paymentBreakdown.map(item => <div className="dashboard-row" key={item.method}><span>{paymentLabel(item.method)}</span><strong>{money(item.amount)}</strong></div>)}</div><div className="dashboard-secondary"><span>Efectivo</span><strong>{money(current.cashPayments)}</strong></div><div className="dashboard-secondary"><span>Electrónico</span><strong>{money(current.electronicPayments)}</strong></div></ReportPanel>
      <ReportPanel title="Ventas por empleado">{current.salesByEmployee.length === 0 ? <p className="muted">Sin ventas.</p> : <div className="dashboard-list">{current.salesByEmployee.map(item => <div className="dashboard-row" key={item.userId}><div><strong>{item.employeeName}</strong><span className="muted">{item.salesCount} ventas</span></div><strong>{money(item.amount)}</strong></div>)}</div>}</ReportPanel>
      <ReportPanel title="Productos más vendidos">{current.salesByProduct.length === 0 ? <p className="muted">Sin productos vendidos.</p> : <div className="dashboard-list">{current.salesByProduct.slice(0, 8).map(item => <div className="dashboard-row" key={item.productId || item.productName}><div><strong>{item.productName}</strong><span className="muted">{number(item.quantity)} unidades</span></div><strong>{money(item.revenue)}</strong></div>)}</div>}</ReportPanel>
      <ReportPanel title="Ventas por día">{current.salesByDay.length === 0 ? <p className="muted">Sin ventas.</p> : <div className="dashboard-list">{current.salesByDay.map(item => <div className="dashboard-row" key={item.date}><span>{item.date}</span><strong>{money(item.amount)}</strong></div>)}</div>}</ReportPanel></div>
    </>
}

function ReportPanel({ title, children }: { title: string; children: ReactNode }) { return <Panel className="dashboard-panel"><SectionHeading title={title} />{children}</Panel> }
function Metric({ label, current, previous }: { label: string; current: number; previous: number }) {
  const change = previous === 0 ? null : ((current - previous) / previous) * 100
  return <article className="metric-card"><span className="muted">{label}</span><strong>{label === 'Horas de billar' ? number(current) : money(current)}</strong>{change === null ? <small className="muted">Sin período anterior</small> : <small className={change >= 0 ? 'stock-ok' : 'stock-critical'}>{change >= 0 ? '+' : ''}{change.toFixed(1)}% vs. anterior</small>}</article>
}
import { useMemo, useState, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Award, BarChart3, CalendarCheck, CalendarDays, Clock, Package, Percent, Receipt, RefreshCw, Table2, TrendingUp, UserCheck } from 'lucide-react'
import AppLayout from '../components/AppLayout'
import { EmptyState, Feedback, Panel, SectionHeading } from '../components/ui'
import { downloadReportPdf, getReport } from '../api'
import type { ReportMetrics } from '../types'

const money = (value: number) => new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(value)
const number = (value: number) => new Intl.NumberFormat('es-CO', { maximumFractionDigits: 2 }).format(value)
const weekdays = ['Domingo', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado']
const parseDay = (value: string) => { const [year, month, day] = value.split('-').map(Number); return new Date(year, month - 1, day) }
const dayLabel = (value: string) => new Intl.DateTimeFormat('es-CO', { weekday: 'short', day: 'numeric', month: 'short' }).format(parseDay(value))
const hourLabel = (hour: number) => new Intl.DateTimeFormat('es-CO', { hour: 'numeric' }).format(new Date(2000, 0, 1, hour))
const plural = (count: number, singular: string, pluralForm: string) => `${count} ${count === 1 ? singular : pluralForm}`
const duration = (hours: number) => { const totalMinutes = Math.round(hours * 60); const h = Math.floor(totalMinutes / 60); const m = totalMinutes % 60; return h === 0 ? `${m} min` : m === 0 ? `${h} h` : `${h} h ${m} min` }
const isoDay = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
const shortHour = (hour: number) => `${hour % 12 === 0 ? 12 : hour % 12}${hour < 12 ? 'a' : 'p'}`
const shortDay = (value: string, withMonth = true) => { const date = parseDay(value); return withMonth ? `${date.getDate()} ${new Intl.DateTimeFormat('es-CO', { month: 'short' }).format(date).replace('.', '')}` : String(date.getDate()) }
const rangeLabel = (from: string, to: string) => from === to ? shortDay(from) : from.slice(0, 7) === to.slice(0, 7) ? `${shortDay(from, false)}–${shortDay(to)}` : `${shortDay(from)} – ${shortDay(to)}`
const percent = (value: number) => new Intl.NumberFormat('es-CO', { minimumFractionDigits: 1, maximumFractionDigits: 1, signDisplay: 'always' }).format(value)
const maxBy = <T,>(items: T[], value: (item: T) => number) => items.reduce<T | null>((best, item) => best === null || value(item) > value(best) ? item : best, null)
const paymentLabel = (method: string) => ({ Cash: 'Efectivo', Nequi: 'Nequi', Daviplata: 'Daviplata', Card: 'Tarjeta', BankTransfer: 'Transferencia', Other: 'Otro' } as Record<string, string>)[method] ?? method

type RangePreset = { key: string; label: string; range: () => [string, string] }
const daysAgo = (days: number) => { const date = new Date(); date.setDate(date.getDate() - days); return isoDay(date) }
const presets: RangePreset[] = [
  { key: 'today', label: 'Hoy', range: () => [daysAgo(0), daysAgo(0)] },
  { key: 'yesterday', label: 'Ayer', range: () => [daysAgo(1), daysAgo(1)] },
  { key: '7d', label: 'Últimos 7 días', range: () => [daysAgo(6), daysAgo(0)] },
  { key: '30d', label: 'Últimos 30 días', range: () => [daysAgo(29), daysAgo(0)] },
  { key: 'month', label: 'Este mes', range: () => { const now = new Date(); return [isoDay(new Date(now.getFullYear(), now.getMonth(), 1)), daysAgo(0)] } }
]

export default function ReportsPage() {
  const [[from, to], setRange] = useState<[string, string]>(() => presets[2].range())
  const query = useQuery({ queryKey: ['reports', from, to], queryFn: () => getReport(from, to), enabled: Boolean(from && to) })
  const invalidRange = Boolean(from && to && from > to)
  const activePreset = presets.find(preset => { const [presetFrom, presetTo] = preset.range(); return presetFrom === from && presetTo === to })?.key

  return <AppLayout eyebrow="SUPERVISIÓN" title="Reportes" actions={<><button className="secondary-button" onClick={() => void query.refetch()}><RefreshCw size={16} />Actualizar</button><button className="secondary-button" disabled={invalidRange} onClick={() => void downloadReportPdf(from, to)}>Descargar PDF</button></>}>
    <section className="content reports-content report-stack">
      <Panel className="report-filters">
        <div className="report-toolbar">
          <div className="range-chips" role="group" aria-label="Período rápido">{presets.map(preset => <button key={preset.key} type="button" className={`range-chip ${activePreset === preset.key ? 'active' : ''}`} onClick={() => setRange(preset.range())}>{preset.label}</button>)}</div>
          <div className="date-filters"><label>Desde<input type="date" value={from} onChange={event => setRange([event.target.value, to])} /></label><label>Hasta<input type="date" value={to} onChange={event => setRange([from, event.target.value])} /></label></div>
        </div>
        {query.data && !invalidRange && <p className="report-period muted">Comparado con el período anterior: {dayLabel(query.data.previousFrom)} a {dayLabel(query.data.previousTo)}</p>}
        {invalidRange && <Feedback kind="error">La fecha inicial no puede ser posterior a la fecha final.</Feedback>}
      </Panel>
      {query.isLoading && <EmptyState>Cargando reporte...</EmptyState>}
      {query.isError && <Feedback kind="error">No se pudo cargar el reporte. Verifica el rango de fechas.</Feedback>}
      {query.data && !invalidRange && <ReportBody current={query.data.current} previous={query.data.previous} from={query.data.from} to={query.data.to} compareLabel={rangeLabel(query.data.previousFrom, query.data.previousTo)} />}
    </section>
  </AppLayout>
}

function ReportBody({ current, previous, from, to, compareLabel }: { current: ReportMetrics; previous: ReportMetrics; from: string; to: string; compareLabel: string }) {
  const [showAllProducts, setShowAllProducts] = useState(false)
  const products = soldProducts(current)
  return <>
    <div className="metric-grid report-summary"><Metric label="Ventas brutas" current={current.grossSales} previous={previous.grossSales} compareLabel={compareLabel} accent /><Metric label="Utilidad bruta" current={current.grossProfit} previous={previous.grossProfit} compareLabel={compareLabel} /><Metric label="Resultado operativo" current={current.operatingResult} previous={previous.operatingResult} compareLabel={compareLabel} /><Metric label="Costo de ventas" current={current.costOfGoodsSold} previous={previous.costOfGoodsSold} compareLabel={compareLabel} lowerIsBetter /><Metric label="Gastos" current={current.expenses} previous={previous.expenses} compareLabel={compareLabel} lowerIsBetter /><Metric label="Horas de billar" current={current.billiardHours} previous={previous.billiardHours} compareLabel={compareLabel} /></div>
    <ReportPanel title="Ventas por día" icon={<BarChart3 size={21} />}>{current.salesByDay.length === 0 ? <p className="muted">Sin ventas en el período.</p> : <ColumnChart columns={dailyColumns(current, from, to)} />}</ReportPanel>
    <KeyIndicators current={current} />
    <div className="report-grid">
      <div className="report-column">
        <ReportPanel title="Ventas por hora">{current.salesByHour.length === 0 ? <p className="muted">Sin ventas.</p> : <ColumnChart columns={Array.from({ length: 24 }, (_, hour) => { const row = current.salesByHour.find(item => item.hour === hour); return { key: String(hour), label: shortHour(hour), title: `${hourLabel(hour)} – ${hourLabel((hour + 1) % 24)}: ${money(row?.amount ?? 0)} · ${plural(row?.salesCount ?? 0, 'venta', 'ventas')}`, heading: `${hourLabel(hour)} – ${hourLabel((hour + 1) % 24)}`, detail: `${money(row?.amount ?? 0)} · ${plural(row?.salesCount ?? 0, 'venta', 'ventas')}`, value: row?.amount ?? 0 } })} />}</ReportPanel>
        <ReportPanel title="Productos más vendidos">{products.length === 0 ? <p className="muted">Sin productos vendidos.</p> : <><BarList rows={products.slice(0, showAllProducts ? 10 : 5).map(item => ({ key: item.productId ?? item.productName, label: item.productName, detail: plural(item.quantity, 'unidad', 'unidades'), value: item.revenue, display: money(item.revenue) }))} />{products.length > 5 && <button type="button" className="text-button report-more" onClick={() => setShowAllProducts(value => !value)}>{showAllProducts ? 'Ver menos' : 'Ver top 10'}</button>}</>}</ReportPanel>
      </div>
      <div className="report-column">
        <ReportPanel title="Uso de mesas de billar">{current.billiardByTable.length === 0 ? <p className="muted">Sin mesas usadas.</p> : <BarList highlightMax rows={current.billiardByTable.map(item => ({ key: item.tableId, label: item.tableName, detail: plural(item.sessions, 'sesión', 'sesiones'), value: item.hours, display: duration(item.hours) }))} />}</ReportPanel>
        <ReportPanel title="Medios de pago">{current.paymentBreakdown.length === 0 ? <p className="muted">Sin pagos en el período.</p> : <BarList rows={current.paymentBreakdown.map(item => ({ key: item.method, label: paymentLabel(item.method), value: item.amount, display: money(item.amount) }))} />}<div className="dashboard-secondary"><span>Efectivo</span><strong>{money(current.cashPayments)}</strong></div><div className="dashboard-secondary"><span>Electrónico</span><strong>{money(current.electronicPayments)}</strong></div></ReportPanel>
        <ReportPanel title="Ventas por empleado">{current.salesByEmployee.length === 0 ? <p className="muted">Sin ventas.</p> : <div className="dashboard-list">{current.salesByEmployee.map(item => <div className="dashboard-row" key={item.userId}><div><strong>{item.employeeName}</strong><span className="muted">{plural(item.salesCount, 'venta', 'ventas')}</span></div><strong>{money(item.amount)}</strong></div>)}</div>}</ReportPanel>
      </div>
    </div>
  </>
}

function soldProducts(current: ReportMetrics) { return current.salesByProduct.filter(item => item.productId !== null) }

function KeyIndicators({ current }: { current: ReportMetrics }) {
  const products = soldProducts(current)
  const bestDay = maxBy(current.salesByDay, item => item.amount)
  const weekdayTotals = current.salesByDay.reduce<number[]>((totals, item) => { totals[parseDay(item.date).getDay()] += item.amount; return totals }, Array(7).fill(0))
  const bestWeekday = current.salesByDay.length === 0 ? null : weekdayTotals.indexOf(Math.max(...weekdayTotals))
  const peakHour = maxBy(current.salesByHour, item => item.amount)
  const topRevenue = maxBy(products, item => item.revenue)
  const topQuantity = maxBy(products, item => item.quantity)
  const topProfit = maxBy(products, item => item.grossProfit)
  const topTable = maxBy(current.billiardByTable, item => item.hours)
  const topEmployee = maxBy(current.salesByEmployee, item => item.amount)
  const empty = 'Sin datos'
  return <section className="kpi-section">
    <SectionHeading eyebrow="INDICADORES CLAVE" title="Lo más destacado del período" />
    <div className="kpi-grid">
      <Kpi icon={<CalendarCheck size={19} />} label="Mejor día en ventas" value={bestDay ? dayLabel(bestDay.date) : empty} detail={bestDay ? money(bestDay.amount) : undefined} accent />
      <Kpi icon={<CalendarDays size={19} />} label="Mejor día de la semana" value={bestWeekday === null ? empty : weekdays[bestWeekday]} detail={bestWeekday === null ? undefined : money(weekdayTotals[bestWeekday])} />
      <Kpi icon={<Clock size={19} />} label="Hora pico" value={peakHour ? `${hourLabel(peakHour.hour)} – ${hourLabel((peakHour.hour + 1) % 24)}` : empty} detail={peakHour ? `${money(peakHour.amount)} · ${plural(peakHour.salesCount, 'venta', 'ventas')}` : undefined} />
      <Kpi icon={<Award size={19} />} label="Producto estrella (ingresos)" value={topRevenue?.productName ?? empty} detail={topRevenue ? money(topRevenue.revenue) : undefined} accent />
      <Kpi icon={<Package size={19} />} label="Producto más vendido" value={topQuantity?.productName ?? empty} detail={topQuantity ? plural(topQuantity.quantity, 'unidad', 'unidades') : undefined} />
      <Kpi icon={<TrendingUp size={19} />} label="Producto más rentable" value={topProfit?.productName ?? empty} detail={topProfit ? `${money(topProfit.grossProfit)} de utilidad` : undefined} />
      <Kpi icon={<Table2 size={19} />} label="Mesa más usada" value={topTable?.tableName ?? empty} detail={topTable ? `${duration(topTable.hours)} · ${plural(topTable.sessions, 'sesión', 'sesiones')}` : undefined} />
      <Kpi icon={<Receipt size={19} />} label="Ticket promedio" value={current.completedSales === 0 ? empty : money(current.grossSales / current.completedSales)} detail={plural(current.completedSales, 'venta completada', 'ventas completadas')} />
      <Kpi icon={<UserCheck size={19} />} label="Empleado destacado" value={topEmployee?.employeeName ?? empty} detail={topEmployee ? `${money(topEmployee.amount)} · ${plural(topEmployee.salesCount, 'venta', 'ventas')}` : undefined} />
      <Kpi icon={<Percent size={19} />} label="Margen bruto" value={current.grossSales === 0 ? empty : `${(current.grossProfit / current.grossSales * 100).toFixed(1)}%`} detail={`Utilidad ${money(current.grossProfit)}`} />
    </div>
  </section>
}

function Kpi({ icon, label, value, detail, accent }: { icon: ReactNode; label: string; value: string; detail?: string; accent?: boolean }) {
  return <article className={`metric-card kpi-card ${accent ? 'accent' : ''}`}><span className="metric-icon">{icon}</span><span className="muted">{label}</span><strong>{value}</strong>{detail && <small className="muted">{detail}</small>}</article>
}

function BarList({ rows, highlightMax }: { rows: Array<{ key: string; label: string; detail?: string; value: number; display: string }>; highlightMax?: boolean }) {
  const max = Math.max(0, ...rows.map(row => row.value))
  return <div className="bar-list">{rows.map(row => <div className={`bar-row ${highlightMax && row.value === max && max > 0 ? 'best' : ''}`} key={row.key}>
    <div className="bar-labels"><span><strong>{row.label}</strong>{row.detail && <span className="muted"> · {row.detail}</span>}</span><strong>{row.display}</strong></div>
    <div className="bar-track"><span style={{ width: `${max > 0 ? Math.max(2, row.value / max * 100) : 0}%` }} /></div>
  </div>)}</div>
}

function dailyColumns(current: ReportMetrics, from: string, to: string) {
  const amounts = new Map(current.salesByDay.map(item => [item.date, item.amount]))
  const days: string[] = []
  for (let day = parseDay(from); isoDay(day) <= to && days.length <= 62; day.setDate(day.getDate() + 1)) days.push(isoDay(day))
  const keys = days.length > 62 ? current.salesByDay.map(item => item.date) : days
  return keys.map((date, index) => ({ key: date, label: index === 0 || date.endsWith('-01') ? new Intl.DateTimeFormat('es-CO', { day: 'numeric', month: 'short' }).format(parseDay(date)).replace(' de ', ' ').replace('.', '') : String(parseDay(date).getDate()), title: `${dayLabel(date)}: ${money(amounts.get(date) ?? 0)}`, heading: dayLabel(date), detail: money(amounts.get(date) ?? 0), value: amounts.get(date) ?? 0 }))
}

const plotHeight = 180

function ColumnChart({ columns }: { columns: Array<{ key: string; label: string; title: string; heading: string; detail: string; value: number }> }) {
  const [active, setActive] = useState<number | null>(null)
  const max = Math.max(0, ...columns.map(column => column.value))
  const labelEvery = columns.length <= 16 ? 1 : Math.ceil(columns.length / 12)
  const height = (value: number) => max > 0 && value > 0 ? Math.max(3, value / max * 100) : 0
  const hovered = active === null ? null : columns[active]
  return <div className="column-chart">
    <div className="column-plot" onMouseLeave={() => setActive(null)}>
      {columns.map((column, index) => <button type="button" className={`column ${column.value === max && max > 0 ? 'best' : ''} ${active === index ? 'active' : ''}`} key={column.key} aria-label={column.title} onMouseEnter={() => setActive(index)} onFocus={() => setActive(index)} onBlur={() => setActive(null)} onClick={() => setActive(current => current === index ? null : index)}><span style={{ height: `${height(column.value)}%` }} /></button>)}
      {hovered && active !== null && <div className="column-tooltip" role="status" style={{ left: `${(active + 0.5) / columns.length * 100}%`, bottom: `${height(hovered.value) / 100 * plotHeight + 8}px`, transform: `translateX(${active < columns.length * 0.15 ? '-15%' : active > columns.length * 0.85 ? '-85%' : '-50%'})` }}><span>{hovered.heading}</span><strong>{hovered.detail}</strong></div>}
    </div>
    <div className="column-axis">{columns.map((column, index) => <span key={column.key}>{index % labelEvery === 0 ? column.label : ''}</span>)}</div>
    {max > 0 && <p className="column-caption muted">★ Máximo: {columns.find(column => column.value === max)?.title}</p>}
  </div>
}

function ReportPanel({ title, icon, children }: { title: string; icon?: ReactNode; children: ReactNode }) { return <Panel className="dashboard-panel"><SectionHeading title={title} icon={icon} />{children}</Panel> }
function Metric({ label, current, previous, compareLabel, accent, lowerIsBetter }: { label: string; current: number; previous: number; compareLabel: string; accent?: boolean; lowerIsBetter?: boolean }) {
  const change = previous === 0 ? null : ((current - previous) / Math.abs(previous)) * 100
  const good = change !== null && (lowerIsBetter ? change <= 0 : change >= 0)
  const comparison = change !== null ? `${percent(change)}% vs. ${compareLabel}`
    : current === 0 ? `Igual que ${compareLabel}`
    : `Sin datos en ${compareLabel} para comparar`
  return <article className={`metric-card ${accent ? 'accent' : ''}`}><span className="muted">{label}</span><strong>{label === 'Horas de billar' ? number(current) : money(current)}</strong><small className={change === null ? 'muted' : good ? 'stock-ok' : 'stock-critical'}>{comparison}</small></article>
}
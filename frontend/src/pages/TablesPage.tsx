import { useEffect, useMemo, useState } from 'react'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { Pause, Play, RotateCcw, Square, Plus, X, Trash2 } from 'lucide-react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import AppLayout from '../components/AppLayout'
import { addConsumption, closeSession, deleteConsumption, getProducts, getSession, getTables, pauseTable, resumeTable, startTable } from '../api'
import { useAuthStore } from '../authStore'
import type { Product, SessionDetail, Table } from '../types'
import { EmptyState, Feedback, SectionHeading } from '../components/ui'

const apiUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'
function formatMoney(value: number) { return new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(value) }
function elapsed(table: Table) {
  if (!table.startedAtUtc) return 0
  const pausedNow = table.pausedAtUtc ? Math.max(0, Math.floor((Date.now() - Date.parse(table.pausedAtUtc)) / 1000)) : 0
  return Math.max(0, Math.floor((Date.now() - Date.parse(table.startedAtUtc)) / 1000) - table.totalPausedSeconds - pausedNow)
}
function formatTime(seconds: number) { const h = Math.floor(seconds / 3600); const m = Math.floor((seconds % 3600) / 60); const s = seconds % 60; return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}` }

export default function TablesPage() {
  const auth = useAuthStore((state) => state.auth)
  const logout = useAuthStore((state) => state.logout)
  const client = useQueryClient()
  const [now, setNow] = useState(Date.now())
  const [message, setMessage] = useState('')
  const [selected, setSelected] = useState<Table | null>(null)
  const { data: tables = [], isLoading, isError, refetch } = useQuery({ queryKey: ['tables'], queryFn: getTables })
  const { data: products = [] } = useQuery({ queryKey: ['products'], queryFn: getProducts })
  const sessionId = selected?.activeSessionId ?? ''
  const { data: detail, refetch: refetchDetail } = useQuery<SessionDetail>({ queryKey: ['session', sessionId], queryFn: () => getSession(sessionId), enabled: Boolean(sessionId), refetchInterval: 5000 })

  useEffect(() => { const timer = setInterval(() => setNow(Date.now()), 1000); return () => clearInterval(timer) }, [])
  useEffect(() => {
    const connection = new HubConnectionBuilder().withUrl(`${apiUrl}/hubs/billiards`, { accessTokenFactory: () => auth?.accessToken ?? '' }).withAutomaticReconnect().configureLogging(LogLevel.Warning).build()
    connection.on('tableUpdated', () => { client.invalidateQueries({ queryKey: ['tables'] }); if (selected?.activeSessionId) void refetchDetail() })
    connection.start().catch(() => undefined)
    return () => { void connection.stop() }
  }, [auth?.accessToken, client, refetchDetail, selected?.activeSessionId])

  const occupied = useMemo(() => tables.filter((table) => table.status !== 'Available').length, [tables])
  async function action(task: () => Promise<unknown>, success: string) { setMessage(''); try { await task(); await refetch(); setMessage(success) } catch (err) { setMessage(err instanceof Error ? err.message : 'No se pudo completar la operación') } }

  void now

  return <AppLayout
    eyebrow="OPERACIÓN"
    title="Mesas de billar">
    <section className="content">
      <div className="summary-row"><div><p className="muted">Estado en tiempo real</p><strong>{occupied} de {tables.length} mesas activas</strong></div><button className="secondary-button" onClick={() => void refetch()}><RotateCcw size={16} />Actualizar</button></div>
      {message && <Feedback>{message}</Feedback>}
      {isLoading && <EmptyState>Cargando mesas...</EmptyState>}
      {isError && <div className="error-box">No se pudo conectar con la API. Verifica que esté ejecutándose en el puerto 5080.</div>}
      <div className="table-grid">{tables.map((table) => <TableCard key={table.id} table={table} onAction={action} onManage={setSelected} />)}</div>
      {!isLoading && tables.length === 0 && <div className="empty-state">No hay mesas configuradas.</div>}
    </section>
    {selected && detail && <SessionPanel table={selected} detail={detail} products={products} onClose={() => setSelected(null)} onChanged={() => { void refetchDetail(); void refetch() }} />}
  </AppLayout>
}

function TableCard({ table, onAction, onManage }: { table: Table; onAction: (task: () => Promise<unknown>, success: string) => Promise<void>; onManage: (table: Table) => void }) {
  const active = table.status !== 'Available'
  return <article className={`table-card ${table.status.toLowerCase()}`}>
    <div className="card-heading"><div><span className={`status-dot ${table.status.toLowerCase()}`} /><span className="status-text">{table.status === 'Available' ? 'Disponible' : table.status === 'Occupied' ? 'Ocupada' : 'Pausada'}</span></div><span className="table-rate">{formatMoney(table.hourlyRate)} / hora</span></div>
    <h2>{table.name}</h2>
    <div className="timer">{active ? formatTime(elapsed(table)) : '00:00:00'}</div>
    <p className="muted">Incremento de {table.billingIncrementMinutes} minutos</p>
    <div className="card-actions">
      {!active && <button className="primary-button" onClick={() => void onAction(() => startTable(table.id), 'Mesa iniciada')}><Play size={16} />Iniciar</button>}
      {table.status === 'Occupied' && <button className="secondary-button" onClick={() => void onAction(() => pauseTable(table.id), 'Mesa pausada')}><Pause size={16} />Pausar</button>}
      {table.status === 'Paused' && <button className="secondary-button" onClick={() => void onAction(() => resumeTable(table.id), 'Mesa reanudada')}><Play size={16} />Reanudar</button>}
      {active && <button className="secondary-button" onClick={() => onManage(table)}><Square size={16} />Gestionar</button>}
    </div>
  </article>
}

function SessionPanel({ table, detail, products, onClose, onChanged }: { table: Table; detail: SessionDetail; products: Product[]; onClose: () => void; onChanged: () => void }) {
  const [productId, setProductId] = useState(products[0]?.id ?? '')
  const [quantity, setQuantity] = useState('1')
  const [amount, setAmount] = useState(String(detail.total))
  const [customerName, setCustomerName] = useState('')
  useEffect(() => { setAmount(String(detail.total)) }, [detail.total])
  const [error, setError] = useState('')
  const available = products.filter((product) => product.isActive && product.currentStock > 0)
  async function add() {
    setError('')
    try { await addConsumption(detail.sessionId, productId, Number(quantity)); setQuantity('1'); onChanged() }
    catch (err) { setError(err instanceof Error ? err.message : 'No se pudo agregar el consumo') }
  }
  async function remove(consumptionId: string) {
    setError('')
    try { await deleteConsumption(detail.sessionId, consumptionId); onChanged() }
    catch (err) { setError(err instanceof Error ? err.message : 'No se pudo eliminar el consumo') }
  }
  async function close() {
    setError('')
    try { await closeSession(detail.sessionId, Number(amount), customerName); onClose(); onChanged() }
    catch (err) { setError(err instanceof Error ? err.message : 'No se pudo cerrar la mesa') }
  }
  return <div className="modal-backdrop"><section className="modal session-modal">
    <SectionHeading eyebrow="SESIÓN ACTIVA" title={table.name} action={<button className="icon-button" onClick={onClose} title="Cerrar panel"><X size={19} /></button>} />
    <div className="session-summary"><div><span className="muted">Tiempo</span><strong>{formatTime(detail.elapsedSeconds)}</strong></div><div><span className="muted">Mesa</span><strong>{formatMoney(detail.billiardCharge)}</strong></div><div><span className="muted">Total</span><strong>{formatMoney(detail.total)}</strong></div></div>
    <div className="consumption-header"><h3>Consumos</h3><span className="muted">{formatMoney(detail.consumptionTotal)}</span></div>
    {detail.consumptions.length === 0 ? <p className="muted">Todavía no hay productos agregados.</p> : <div className="consumption-list">{detail.consumptions.map((item) => <div className="consumption-line" key={item.id}><span>{item.quantity} x {item.descriptionSnapshot}</span><span className="consumption-actions"><strong>{formatMoney(item.subtotal)}</strong><button className="icon-button danger-icon" title="Eliminar consumo" onClick={() => void remove(item.id)}><Trash2 size={16} /></button></span></div>)}</div>}
    <div className="add-consumption"><select value={productId} onChange={(e) => setProductId(e.target.value)}><option value="">Selecciona un producto</option>{available.map((product) => <option key={product.id} value={product.id}>{product.name} - {formatMoney(product.salePrice)} - stock {product.currentStock}</option>)}</select><input type="number" min="1" step="1" value={quantity} onChange={(e) => setQuantity(e.target.value)} /><button className="primary-button" disabled={!productId} onClick={() => void add()}><Plus size={16} />Agregar</button></div>
    <div className="close-section"><label>Nombre para la venta o factura<input value={customerName} maxLength={160} placeholder="Opcional" onChange={(e) => setCustomerName(e.target.value)} /></label><label>Total recibido (COP)<input type="number" min="0" value={amount} onChange={(e) => setAmount(e.target.value)} /></label><button className="danger-button" onClick={() => void close()}>Cerrar mesa y cobrar</button></div>
    {error && <div className="error-box">{error}</div>}
  </section></div>
}


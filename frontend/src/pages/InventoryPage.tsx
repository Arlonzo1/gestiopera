import { useMemo, useState } from 'react'
import { ArrowDownUp, History, RefreshCw } from 'lucide-react'
import { useQuery } from '@tanstack/react-query'
import AppLayout from '../components/AppLayout'
import { EmptyState, Feedback, Panel, SectionHeading } from '../components/ui'
import { getInventoryMovements } from '../api'

const number = (value: number) => new Intl.NumberFormat('es-CO', { maximumFractionDigits: 3 }).format(value)
const date = (value: string) => new Intl.DateTimeFormat('es-CO', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))
const movementLabel = (value: string) => ({ Sale: 'Venta', Purchase: 'Compra', Adjustment: 'Ajuste', InitialStock: 'Inventario inicial', Return: 'Devolución', Waste: 'Merma', PhysicalCountAdjustment: 'Ajuste por conteo físico' } as Record<string, string>)[value] ?? value

export default function InventoryPage() {
  const query = useQuery({ queryKey: ['inventory-movements'], queryFn: getInventoryMovements })
  const [filter, setFilter] = useState('all')
  const movements = query.data ?? []
  const filtered = useMemo(() => filter === 'all' ? movements : movements.filter(item => item.movementType === filter), [filter, movements])
  const filters = Array.from(new Set(movements.map(item => item.movementType)))

  return <AppLayout eyebrow="INVENTARIO" title="Movimientos" actions={<button className="secondary-button" onClick={() => void query.refetch()}><RefreshCw size={16} />Actualizar</button>}>
    <section className="content inventory-content">
      <Panel>
        <SectionHeading eyebrow="TRAZABILIDAD" title="Historial de inventario" icon={<History size={21} />} />
        <div className="inventory-toolbar"><p className="muted">Últimos 100 movimientos registrados. Las entradas y salidas no se eliminan.</p><label>Filtrar por tipo<select value={filter} onChange={event => setFilter(event.target.value)}><option value="all">Todos los movimientos</option>{filters.map(value => <option key={value} value={value}>{movementLabel(value)}</option>)}</select></label></div>
        {query.isLoading && <EmptyState>Cargando movimientos...</EmptyState>}
        {query.isError && <Feedback kind="error">{query.error instanceof Error ? query.error.message : "No se pudo cargar el historial de inventario."}</Feedback>}
        {!query.isLoading && !query.isError && filtered.length === 0 && <EmptyState><ArrowDownUp size={20} />No hay movimientos para este filtro.</EmptyState>}
        {!query.isLoading && !query.isError && filtered.length > 0 && <div className="movement-list">{filtered.map(item => <div className="movement-row" key={item.id}><div><strong>{item.productName}</strong><span className="muted">{date(item.createdAt)} | {movementLabel(item.movementType)}</span></div><strong className={item.quantity >= 0 ? 'stock-ok' : 'stock-critical'}>{item.quantity >= 0 ? '+' : ''}{number(item.quantity)}</strong><span className="muted">{item.notes || 'Sin nota'}</span></div>)}</div>}
      </Panel>
    </section>
  </AppLayout>
}

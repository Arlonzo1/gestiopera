import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ClipboardCheck, RefreshCw } from 'lucide-react'
import AppLayout from '../components/AppLayout'
import { EmptyState, Feedback, Panel, SectionHeading } from '../components/ui'
import { confirmPhysicalCount, getProducts } from '../api'

const number = (value: number) => new Intl.NumberFormat('es-CO', { maximumFractionDigits: 3 }).format(value)

export default function PhysicalCountPage() {
  const queryClient = useQueryClient()
  const productsQuery = useQuery({ queryKey: ['products'], queryFn: getProducts })
  const [quantities, setQuantities] = useState<Record<string, string>>({})
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const mutation = useMutation({ mutationFn: () => confirmPhysicalCount((productsQuery.data ?? []).map(product => ({ productId: product.id, physicalQuantity: Number(quantities[product.id] ?? product.currentStock) }))), onSuccess: async result => { setMessage(`Conteo confirmado. ${result.adjustedItemCount} producto(s) ajustado(s).`); setError(null); setQuantities({}); await queryClient.invalidateQueries({ queryKey: ['products'] }); await queryClient.invalidateQueries({ queryKey: ['inventory-movements'] }); await queryClient.invalidateQueries({ queryKey: ['dashboard'] }); await queryClient.invalidateQueries({ queryKey: ['notifications-unread'] }) }, onError: reason => setError(reason instanceof Error ? reason.message : 'No se pudo confirmar el conteo.') })
  const rows = useMemo(() => (productsQuery.data ?? []).filter(product => product.isActive).map(product => { const entered = quantities[product.id]; const physical = entered === undefined || entered === '' ? product.currentStock : Number(entered); return { product, physical, difference: physical - product.currentStock } }), [productsQuery.data, quantities])
  const hasInvalid = rows.some(row => !Number.isFinite(row.physical) || row.physical < 0)

  return <AppLayout eyebrow="INVENTARIO" title="Conteo físico" actions={<button className="secondary-button" onClick={() => void productsQuery.refetch()}><RefreshCw size={16} />Actualizar</button>}>
    <section className="content inventory-content"><Panel><SectionHeading eyebrow="CONTROL DE EXISTENCIAS" title="Confirmar conteo físico" icon={<ClipboardCheck size={21} />} /><p className="muted">Registra la cantidad encontrada. El sistema conservará la cantidad anterior y creará un ajuste por la diferencia.</p>{message && <Feedback kind="success">{message}</Feedback>}{error && <Feedback kind="error">{error}</Feedback>}{productsQuery.isLoading ? <EmptyState>Cargando productos...</EmptyState> : productsQuery.isError ? <Feedback kind="error">No se pudieron cargar los productos.</Feedback> : <><div className="movement-list">{rows.length === 0 ? <EmptyState>No hay productos activos para contar.</EmptyState> : rows.map(({ product, physical, difference }) => <div className="movement-row" key={product.id}><div><strong>{product.name}</strong><span className="muted">Sistema: {number(product.currentStock)} {product.sku ? `| ${product.sku}` : ''}</span></div><input className="compact-input" type="number" min="0" step="0.001" value={quantities[product.id] ?? product.currentStock} onChange={event => setQuantities(current => ({ ...current, [product.id]: event.target.value }))} aria-label={`Cantidad física de ${product.name}`} /><strong className={difference === 0 ? 'muted' : difference > 0 ? 'stock-ok' : 'stock-critical'}>{difference > 0 ? '+' : ''}{number(difference)}</strong></div>)}</div><div className="form-actions"><button className="primary-button" disabled={mutation.isPending || rows.length === 0 || hasInvalid} onClick={() => { setMessage(null); setError(null); mutation.mutate() }}><ClipboardCheck size={16} />{mutation.isPending ? 'Confirmando...' : 'Confirmar conteo'}</button></div></>}</Panel></section>
  </AppLayout>
}
import { FormEvent, useMemo, useState } from 'react'
import { ClipboardPlus, Plus, RefreshCw, Trash2 } from 'lucide-react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import AppLayout from '../components/AppLayout'
import { EmptyState, Feedback, Panel, SectionHeading } from '../components/ui'
import { createPurchase, getProducts } from '../api'
import type { Product } from '../types'

const money = (value: number) => new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(value)
type PurchaseLine = { product: Product; quantity: string; unitCost: string }

export default function PurchasesPage() {
  const client = useQueryClient()
  const productsQuery = useQuery({ queryKey: ['products'], queryFn: getProducts })
  const [lines, setLines] = useState<PurchaseLine[]>([])
  const [productId, setProductId] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [unitCost, setUnitCost] = useState('')
  const [supplier, setSupplier] = useState('')
  const [invoiceReference, setInvoiceReference] = useState('')
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const total = useMemo(() => lines.reduce((sum, line) => sum + Number(line.quantity) * Number(line.unitCost), 0), [lines])
  const products = productsQuery.data ?? []
  const activeProducts = products.filter(product => product.isActive)
  const mutation = useMutation({
    mutationFn: () => createPurchase(supplier, invoiceReference, lines.map(line => ({ productId: line.product.id, quantity: Number(line.quantity), unitCost: Number(line.unitCost) }))),
    onSuccess: result => {
      setLines([])
      setSupplier('')
      setInvoiceReference('')
      setMessage('Compra registrada por ' + money(result.total))
      setError('')
      void client.invalidateQueries({ queryKey: ['products'] })
      void client.invalidateQueries({ queryKey: ['dashboard'] })
      void client.invalidateQueries({ queryKey: ['inventory-movements'] })
      void client.invalidateQueries({ queryKey: ['notifications-unread'] })
    },
    onError: (err: Error) => { setError(err.message); setMessage('') }
  })

  function addLine() {
    const product = products.find(item => item.id === productId)
    const parsedQuantity = Number(quantity)
    const parsedCost = Number(unitCost)
    if (!product || !Number.isFinite(parsedQuantity) || parsedQuantity <= 0 || !Number.isFinite(parsedCost) || parsedCost < 0) {
      setError('Selecciona un producto e ingresa una cantidad y un costo válidos.')
      return
    }
    setLines(current => [...current, { product, quantity: String(parsedQuantity), unitCost: String(parsedCost) }])
    setProductId('')
    setQuantity('1')
    setUnitCost('')
    setError('')
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    if (lines.length === 0) { setError('Agrega al menos un producto a la compra.'); return }
    mutation.mutate()
  }

  return <AppLayout eyebrow="INVENTARIO" title="Compras" actions={<button className="secondary-button" onClick={() => void productsQuery.refetch()}><RefreshCw size={16} />Actualizar</button>}>
    <section className="content purchase-content">
      <div className="purchase-layout">
        <Panel className="products-panel">
          <SectionHeading eyebrow="ENTRADA DE STOCK" title="Nueva compra" icon={<ClipboardPlus size={21} />} />
          {message && <Feedback kind="success">{message}</Feedback>}
          {error && <Feedback kind="error">{error}</Feedback>}
          {productsQuery.isLoading && <EmptyState>Cargando productos...</EmptyState>}
          {productsQuery.isError && <Feedback kind="error">No se pudieron cargar los productos disponibles.</Feedback>}
          {!productsQuery.isLoading && !productsQuery.isError && activeProducts.length === 0 && <EmptyState>Primero crea un producto activo para registrar una compra.</EmptyState>}
          {!productsQuery.isLoading && !productsQuery.isError && activeProducts.length > 0 && <form className="form-stack compact" onSubmit={submit}>
            <div className="purchase-meta"><label>Proveedor<input value={supplier} placeholder="Nombre del proveedor" onChange={event => setSupplier(event.target.value)} /></label><label>Referencia de factura<input value={invoiceReference} placeholder="Opcional" onChange={event => setInvoiceReference(event.target.value)} /></label></div>
            <div className="purchase-add-line"><label>Producto<select value={productId} onChange={event => setProductId(event.target.value)}><option value="">Selecciona un producto</option>{activeProducts.map(product => <option value={product.id} key={product.id}>{product.name} | stock {product.currentStock}</option>)}</select></label><label>Cantidad<input type="number" min="0.001" step="0.001" value={quantity} onChange={event => setQuantity(event.target.value)} /></label><label>Costo unitario<input type="number" min="0" step="0.01" value={unitCost} onChange={event => setUnitCost(event.target.value)} /></label><button type="button" className="secondary-button" disabled={!productId || !unitCost} onClick={addLine}><Plus size={16} />Agregar</button></div>
            <div className="purchase-lines">{lines.length === 0 ? <EmptyState>Aún no hay productos en la compra.</EmptyState> : lines.map((line, index) => <div className="purchase-line" key={line.product.id + '-' + index}><div><strong>{line.product.name}</strong><span className="muted">{line.quantity} unidades | {money(Number(line.unitCost))} cada una</span></div><strong>{money(Number(line.quantity) * Number(line.unitCost))}</strong><button type="button" title="Quitar producto" onClick={() => setLines(current => current.filter((_, itemIndex) => itemIndex !== index))}><Trash2 size={16} /></button></div>)}</div>
            <div className="purchase-total"><span>Total de compra</span><strong>{money(total)}</strong></div>
            <button className="primary-button" disabled={lines.length === 0 || mutation.isPending}>{mutation.isPending ? 'Registrando...' : 'Completar compra'}</button>
          </form>}
        </Panel>
      </div>
    </section>
  </AppLayout>
}

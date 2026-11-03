import { FormEvent, useMemo, useState } from 'react'
import { ClipboardPlus, PackagePlus, Plus, RefreshCw, Trash2 } from 'lucide-react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import AppLayout from '../components/AppLayout'
import { EmptyState, Feedback, Panel, SectionHeading } from '../components/ui'
import { createProduct, createPurchase, getProducts } from '../api'
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
  const [isNewProduct, setIsNewProduct] = useState(false)
  const [newProduct, setNewProduct] = useState({ name: '', sku: '', salePrice: '', minimumStock: '0', criticalStock: '0' })
  const [creatingProduct, setCreatingProduct] = useState(false)
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

  async function addNewProductLine() {
    const parsedQuantity = Number(quantity)
    const parsedCost = Number(unitCost)
    const salePrice = Number(newProduct.salePrice)
    const minimumStock = Number(newProduct.minimumStock)
    const criticalStock = Number(newProduct.criticalStock)
    if (!newProduct.name.trim() || !Number.isFinite(parsedQuantity) || parsedQuantity <= 0 || !Number.isInteger(parsedCost) || parsedCost < 0 || !Number.isInteger(salePrice) || salePrice < 0) {
      setError('Ingresa el nombre, una cantidad válida y un costo y precio de venta en pesos enteros.')
      return
    }
    if (!Number.isInteger(minimumStock) || !Number.isInteger(criticalStock) || minimumStock < 0 || criticalStock < 0 || criticalStock > minimumStock) {
      setError('Los stocks mínimo y crítico deben ser enteros y el crítico no puede superar al mínimo.')
      return
    }
    setCreatingProduct(true)
    try {
      const product = await createProduct(newProduct.name.trim(), newProduct.sku.trim(), parsedCost, salePrice, minimumStock, criticalStock)
      setLines(current => [...current, { product, quantity: String(parsedQuantity), unitCost: String(parsedCost) }])
      setNewProduct({ name: '', sku: '', salePrice: '', minimumStock: '0', criticalStock: '0' })
      setIsNewProduct(false)
      setQuantity('1')
      setUnitCost('')
      setError('')
      setMessage('Producto ' + product.name + ' creado y agregado a la compra')
      void client.invalidateQueries({ queryKey: ['products'] })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo crear el producto')
    } finally {
      setCreatingProduct(false)
    }
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
          {!productsQuery.isLoading && !productsQuery.isError && activeProducts.length === 0 && !isNewProduct && <EmptyState>Aún no hay productos. Usa “Producto nuevo” para crearlo desde esta compra.</EmptyState>}
          {!productsQuery.isLoading && !productsQuery.isError && <form className="form-stack compact" onSubmit={submit}>
            <div className="purchase-meta"><label>Proveedor<input value={supplier} placeholder="Nombre del proveedor" onChange={event => setSupplier(event.target.value)} /></label><label>Referencia de factura<input value={invoiceReference} placeholder="Opcional" onChange={event => setInvoiceReference(event.target.value)} /></label></div>
            <div className="purchase-add-line">{isNewProduct ? <label>Nombre del producto nuevo<input value={newProduct.name} maxLength={160} placeholder="Ej. Cerveza Club Colombia" onChange={event => setNewProduct(current => ({ ...current, name: event.target.value }))} /></label> : <label>Producto<select value={productId} onChange={event => setProductId(event.target.value)}><option value="">Selecciona un producto</option>{activeProducts.map(product => <option value={product.id} key={product.id}>{product.name} | stock {product.currentStock}</option>)}</select></label>}<label>Cantidad<input type="number" min="0.001" step="0.001" value={quantity} onChange={event => setQuantity(event.target.value)} /></label><label>Costo unitario<input type="number" min="0" step={isNewProduct ? '1' : '0.01'} value={unitCost} onChange={event => setUnitCost(event.target.value)} /></label>{isNewProduct ? <button type="button" className="secondary-button" disabled={!newProduct.name.trim() || !unitCost || !newProduct.salePrice || creatingProduct} onClick={() => void addNewProductLine()}><Plus size={16} />{creatingProduct ? 'Creando...' : 'Crear y agregar'}</button> : <button type="button" className="secondary-button" disabled={!productId || !unitCost} onClick={addLine}><Plus size={16} />Agregar</button>}</div>
            {isNewProduct && <div className="purchase-new-product"><label>SKU<input value={newProduct.sku} placeholder="Opcional" onChange={event => setNewProduct(current => ({ ...current, sku: event.target.value }))} /></label><label>Precio de venta<input type="number" min="0" step="1" value={newProduct.salePrice} onChange={event => setNewProduct(current => ({ ...current, salePrice: event.target.value }))} /></label><label>Stock mínimo<input type="number" min="0" step="1" value={newProduct.minimumStock} onChange={event => setNewProduct(current => ({ ...current, minimumStock: event.target.value }))} /></label><label>Stock crítico<input type="number" min="0" step="1" value={newProduct.criticalStock} onChange={event => setNewProduct(current => ({ ...current, criticalStock: event.target.value }))} /></label></div>}
            <button type="button" className="text-button" onClick={() => { setIsNewProduct(current => !current); setProductId(''); setError('') }}><PackagePlus size={15} />{isNewProduct ? 'Elegir un producto existente' : 'Producto nuevo (no está en el catálogo)'}</button>
            <div className="purchase-lines">{lines.length === 0 ? <EmptyState>Aún no hay productos en la compra.</EmptyState> : lines.map((line, index) => <div className="purchase-line" key={line.product.id + '-' + index}><div><strong>{line.product.name}</strong><span className="muted">{line.quantity} unidades | {money(Number(line.unitCost))} cada una</span></div><strong>{money(Number(line.quantity) * Number(line.unitCost))}</strong><button type="button" title="Quitar producto" onClick={() => setLines(current => current.filter((_, itemIndex) => itemIndex !== index))}><Trash2 size={16} /></button></div>)}</div>
            <div className="purchase-total"><span>Total de compra</span><strong>{money(total)}</strong></div>
            <button className="primary-button" disabled={lines.length === 0 || mutation.isPending}>{mutation.isPending ? 'Registrando...' : 'Completar compra'}</button>
          </form>}
        </Panel>
      </div>
    </section>
  </AppLayout>
}

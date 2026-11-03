import { FormEvent, useMemo, useState } from 'react'
import { PackagePlus, Pencil, RefreshCw, Settings2, Trash2 } from 'lucide-react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import AppLayout from '../components/AppLayout'
import { EmptyState, Feedback, Panel, SectionHeading } from '../components/ui'
import { adjustInventory, createProduct, deactivateProduct, getProducts, updateProduct } from '../api'
import type { Product } from '../types'

const money = (value: number) => new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(value)

export default function ProductsPage() {
  const client = useQueryClient()
  const query = useQuery({ queryKey: ['products'], queryFn: getProducts })
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<'all' | 'low' | 'out' | 'ok'>('all')
  const [selected, setSelected] = useState<Product | null>(null)
  const [editProduct, setEditProduct] = useState<Product | null>(null)
  const [confirmProduct, setConfirmProduct] = useState<Product | null>(null)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const products = query.data ?? []
  const filtered = useMemo(() => products.filter(product => {
    const matchesSearch = product.name.toLowerCase().includes(search.toLowerCase()) || product.sku?.toLowerCase().includes(search.toLowerCase())
    const matchesStatus = status === 'all' || status === 'out' && product.currentStock <= 0 || status === 'low' && product.currentStock > 0 && product.currentStock <= product.minimumStock || status === 'ok' && product.currentStock > product.minimumStock
    return product.isActive && matchesSearch && matchesStatus
  }), [products, search, status])
  const lowCount = products.filter(product => product.isActive && product.currentStock > 0 && product.currentStock <= product.minimumStock).length
  const outCount = products.filter(product => product.isActive && product.currentStock <= 0).length
  const refresh = () => { void client.invalidateQueries({ queryKey: ['products'] }); void client.invalidateQueries({ queryKey: ['dashboard'] }); void client.invalidateQueries({ queryKey: ['notifications-unread'] }) }
  const deactivate = useMutation({ mutationFn: (id: string) => deactivateProduct(id), onSuccess: () => { setMessage('Producto desactivado'); setError(''); setConfirmProduct(null); refresh() }, onError: (err: Error) => setError(err.message) })
  const update = useMutation({ mutationFn: ({ id, value }: { id: string; value: { name: string; sku: string; unitCost: number; salePrice: number; minimumStock: number; criticalStock: number } }) => updateProduct(id, value), onSuccess: () => { setMessage('Producto actualizado correctamente'); setError(''); setEditProduct(null); refresh() }, onError: (err: Error) => setError(err.message) })

  async function submitCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const formElement = event.currentTarget
    const form = new FormData(formElement)
    try {
      await createProduct(String(form.get('name') ?? ''), String(form.get('sku') || ''), Number(form.get('unitCost')), Number(form.get('salePrice')), Number(form.get('minimumStock')), Number(form.get('criticalStock')))
      formElement.reset()
      setMessage('Producto creado correctamente')
      setError('')
      refresh()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo crear el producto')
    }
  }

  return <AppLayout eyebrow="INVENTARIO" title="Productos" actions={<button className="secondary-button" onClick={() => void query.refetch()}><RefreshCw size={16} />Actualizar</button>}>
    <section className="content products-content">
      {message && <Feedback>{message}</Feedback>}
      {error && <Feedback kind="error">{error}</Feedback>}
      <div className="inventory-metrics">
        <button className={status === 'all' ? 'inventory-metric active' : 'inventory-metric'} onClick={() => setStatus('all')}><span>Productos activos</span><strong>{products.filter(product => product.isActive).length}</strong></button>
        <button className={status === 'low' ? 'inventory-metric warning active' : 'inventory-metric warning'} onClick={() => setStatus('low')}><span>Stock bajo</span><strong>{lowCount}</strong></button>
        <button className={status === 'out' ? 'inventory-metric danger active' : 'inventory-metric danger'} onClick={() => setStatus('out')}><span>Sin stock</span><strong>{outCount}</strong></button>
      </div>
      <div className="products-layout">
        <Panel className="products-panel">
          <div className="products-toolbar"><SectionHeading eyebrow="CATÁLOGO ACTIVO" title="Productos" icon={<Settings2 size={21} />} /><div className="products-filters"><input aria-label="Buscar producto" placeholder="Buscar por nombre o SKU..." value={search} onChange={event => setSearch(event.target.value)} /><select aria-label="Filtrar productos" value={status} onChange={event => setStatus(event.target.value as typeof status)}><option value="all">Todos los estados</option><option value="ok">Stock normal</option><option value="low">Stock bajo</option><option value="out">Sin stock</option></select></div></div>
          {query.isLoading && <EmptyState>Cargando productos...</EmptyState>}
          {query.isError && <Feedback kind="error">No se pudieron cargar los productos.</Feedback>}
          {!query.isLoading && !query.isError && filtered.length === 0 && <EmptyState>No hay productos para este filtro.</EmptyState>}
          {!query.isLoading && !query.isError && filtered.length > 0 && <div className="product-table"><div className="product-table-head"><span>Producto</span><span>Existencia</span><span>Precio</span><span>Acciones</span></div>{filtered.map(product => <div className="product-row" key={product.id}><div><strong>{product.name}</strong><span className="muted">{product.sku || 'Sin SKU'} | Costo {money(product.averageCost)}</span></div><span className={product.currentStock <= 0 ? 'stock-critical' : product.currentStock <= product.criticalStock ? 'stock-critical' : product.currentStock <= product.minimumStock ? 'stock-low' : 'stock-ok'}>{product.currentStock <= 0 ? 'Sin stock' : product.currentStock + ' unidades'}</span><strong>{money(product.salePrice)}</strong><div className="row-actions"><button title="Editar producto" onClick={() => setEditProduct(product)}><Pencil size={16} /></button><button title="Ajustar inventario" onClick={() => setSelected(product)}><Settings2 size={16} /></button><button title="Desactivar producto" onClick={() => setConfirmProduct(product)}><Trash2 size={16} /></button></div></div>)}</div>}
        </Panel>
        <Panel className="products-panel product-form-panel">
          <SectionHeading eyebrow="NUEVO PRODUCTO" title="Agregar producto" icon={<PackagePlus size={21} />} />
          <form className="form-stack compact" onSubmit={submitCreate}><label>Nombre<input name="name" required /></label><label>SKU<input name="sku" /></label><label>Costo unitario (COP)<input name="unitCost" type="number" min="0" step="1" required /></label><label>Precio de venta (COP)<input name="salePrice" type="number" min="0" step="1" required /></label><label>Stock mínimo<input name="minimumStock" type="number" min="0" step="1" defaultValue="10" required /></label><label>Stock crítico<input name="criticalStock" type="number" min="0" step="1" defaultValue="3" required /></label><button className="primary-button"><PackagePlus size={16} />Crear producto</button></form>
        </Panel>
      </div>
      {editProduct && <ProductEdit product={editProduct} pending={update.isPending} onClose={() => setEditProduct(null)} onSave={(value) => update.mutate({ id: editProduct.id, value })} />}
      {selected && <InventoryAdjust product={selected} onClose={() => setSelected(null)} onSaved={() => { setSelected(null); setMessage('Inventario actualizado'); refresh() }} onError={setError} />}
      {confirmProduct && <ConfirmDeactivate product={confirmProduct} pending={deactivate.isPending} onCancel={() => setConfirmProduct(null)} onConfirm={() => deactivate.mutate(confirmProduct.id)} />}
    </section>
  </AppLayout>
}

function InventoryAdjust({ product, onClose, onSaved, onError }: { product: Product; onClose: () => void; onSaved: () => void; onError: (message: string) => void }) {
  const [quantity, setQuantity] = useState('')
  const [notes, setNotes] = useState('')
  const invalidQuantity = quantity !== '' && !Number.isInteger(Number(quantity))
  const mutation = useMutation({ mutationFn: () => adjustInventory(product.id, Number(quantity), notes), onSuccess: onSaved, onError: (err: Error) => onError(err.message) })
  return <div className="modal-backdrop"><section className="modal inventory-modal"><SectionHeading eyebrow="AJUSTE CONTROLADO" title={product.name} action={<button className="icon-button" onClick={onClose} title="Cerrar">×</button>} /><p className="muted">Stock actual: {product.currentStock}. Usa valores enteros positivos para entradas y negativos para salidas.</p><label>Cantidad<input type="number" step="1" value={quantity} onChange={event => setQuantity(event.target.value)} />{invalidQuantity && <span className="field-error">La cantidad debe ser un número entero.</span>}</label><label>Motivo<input value={notes} onChange={event => setNotes(event.target.value)} /></label><div className="modal-actions"><button className="secondary-button" onClick={onClose}>Cancelar</button><button className="primary-button" disabled={!quantity || invalidQuantity || mutation.isPending} onClick={() => mutation.mutate()}>{mutation.isPending ? 'Guardando...' : 'Confirmar ajuste'}</button></div></section></div>
}

function ConfirmDeactivate({ product, pending, onCancel, onConfirm }: { product: Product; pending: boolean; onCancel: () => void; onConfirm: () => void }) {
  return <div className="modal-backdrop"><section className="modal inventory-modal"><SectionHeading eyebrow="DESACTIVAR PRODUCTO" title={product.name} action={<button className="icon-button" onClick={onCancel} title="Cerrar">×</button>} /><p className="muted">El producto dejará de aparecer en ventas, pero conservará su historial de inventario.</p><div className="modal-actions"><button className="secondary-button" onClick={onCancel}>Cancelar</button><button className="danger-button" disabled={pending} onClick={onConfirm}>{pending ? 'Desactivando...' : 'Desactivar producto'}</button></div></section></div>
}

function ProductEdit({ product, pending, onClose, onSave }: { product: Product; pending: boolean; onClose: () => void; onSave: (value: { name: string; sku: string; unitCost: number; salePrice: number; minimumStock: number; criticalStock: number }) => void }) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    onSave({ name: String(form.get('name') ?? ''), sku: String(form.get('sku') ?? ''), unitCost: Number(form.get('unitCost')), salePrice: Number(form.get('salePrice')), minimumStock: Number(form.get('minimumStock')), criticalStock: Number(form.get('criticalStock')) })
  }
  return <div className="modal-backdrop"><section className="modal inventory-modal" role="dialog" aria-modal="true" aria-labelledby="edit-product-title"><SectionHeading eyebrow="CONFIGURACIÓN DEL PRODUCTO" title={product.name} action={<button type="button" className="icon-button" onClick={onClose} title="Cerrar">×</button>} /><form className="form-stack compact" onSubmit={submit}><label>Nombre<input name="name" defaultValue={product.name} required /></label><label>SKU<input name="sku" defaultValue={product.sku ?? ''} /></label><label>Costo unitario (COP)<input name="unitCost" type="number" min="0" step="1" defaultValue={product.averageCost} required /></label><label>Precio de venta (COP)<input name="salePrice" type="number" min="0" step="1" defaultValue={product.salePrice} required /></label><label>Stock mínimo<input name="minimumStock" type="number" min="0" step="1" defaultValue={product.minimumStock} required /></label><label>Stock crítico<input name="criticalStock" type="number" min="0" step="1" defaultValue={product.criticalStock} required /></label><div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Cancelar</button><button className="primary-button" disabled={pending}>{pending ? 'Guardando...' : 'Guardar cambios'}</button></div></form></section></div>
}
import { useMemo, useState } from 'react'
import { Minus, Plus, RefreshCw, ShoppingCart, Trash2 } from 'lucide-react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import AppLayout from '../components/AppLayout'
import { createSale, downloadSaleReceipt, getProducts } from '../api'
import { EmptyState, Feedback, Panel, SectionHeading } from '../components/ui'
import type { Product } from '../types'

type CartLine = Product & { quantity: number }
const money = (value: number) => new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(value)

export default function PosPage() {
  const client = useQueryClient()
  const productsQuery = useQuery({ queryKey: ['products'], queryFn: getProducts })
  const [cart, setCart] = useState<CartLine[]>([])
  const [search, setSearch] = useState('')
  const [discount, setDiscount] = useState('0')
  const [method, setMethod] = useState('Cash')
  const [customerName, setCustomerName] = useState('')
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [lastSaleId, setLastSaleId] = useState<string | null>(null)
  const subtotal = useMemo(() => cart.reduce((total, item) => total + item.salePrice * item.quantity, 0), [cart])
  const total = Math.max(0, subtotal - Math.min(Number(discount || 0), subtotal))
  const mutation = useMutation({
    mutationFn: () => createSale(cart.map(item => ({ productId: item.id, quantity: item.quantity })), Number(discount || 0), method, total, customerName),
    onSuccess: (sale) => {
      setCart([])
      setDiscount('0')
      setCustomerName('')
      setLastSaleId(sale.id)
      setMessage('Venta #' + sale.sequentialNumber + ' registrada correctamente')
      setError('')
      void client.invalidateQueries({ queryKey: ['products'] })
      void client.invalidateQueries({ queryKey: ['dashboard'] })
      void client.invalidateQueries({ queryKey: ['notifications-unread'] })
    },
    onError: (err: Error) => { setError(err.message); setMessage('') }
  })
  const filtered = (productsQuery.data ?? []).filter(product => product.isActive && product.name.toLowerCase().includes(search.toLowerCase()))

  function add(product: Product) {
    setCart(current => {
      const found = current.find(item => item.id === product.id)
      if (found) return current.map(item => item.id === product.id ? { ...item, quantity: Math.min(item.quantity + 1, product.currentStock) } : item)
      return [...current, { ...product, quantity: 1 }]
    })
  }

  function change(id: string, delta: number) {
    setCart(current => current.map(item => item.id === id ? { ...item, quantity: Math.min(item.currentStock, item.quantity + delta) } : item).filter(item => item.quantity > 0))
  }

  return <AppLayout eyebrow="OPERACIÓN" title="Punto de venta" actions={<button className="secondary-button" onClick={() => void productsQuery.refetch()}><RefreshCw size={16} />Actualizar</button>}>
    <section className="content pos-content">
      {message && <Feedback>{message} {lastSaleId && <button className="secondary-button" onClick={() => void downloadSaleReceipt(lastSaleId)}>Descargar comprobante</button>}</Feedback>}
      {error && <Feedback kind="error">{error}</Feedback>}
      <div className="pos-layout">
        <Panel className="pos-products">
          <div className="pos-toolbar">
            <SectionHeading eyebrow="PRODUCTOS DISPONIBLES" title="Catálogo" icon={<ShoppingCart size={21} />} />
            <input aria-label="Buscar producto" placeholder="Buscar producto..." value={search} onChange={event => setSearch(event.target.value)} />
          </div>
          {productsQuery.isLoading && <EmptyState>Cargando productos...</EmptyState>}
          {productsQuery.isError && <Feedback kind="error">No se pudieron cargar los productos. Intenta actualizar.</Feedback>}
          {!productsQuery.isLoading && !productsQuery.isError && filtered.length === 0 && <EmptyState>No hay productos disponibles para esta búsqueda.</EmptyState>}
          {!productsQuery.isLoading && !productsQuery.isError && filtered.length > 0 && <div className="pos-product-grid">{filtered.map(product => <button className="product-tile" key={product.id} disabled={product.currentStock <= 0} onClick={() => add(product)}><strong>{product.name}</strong><span>{money(product.salePrice)}</span><small>{product.currentStock > 0 ? 'Stock ' + product.currentStock : 'Sin stock'}</small></button>)}</div>}
        </Panel>

        <Panel as="aside" className="pos-cart">
          <SectionHeading eyebrow="VENTA ACTUAL" title="Carrito" icon={<ShoppingCart size={21} />} />
          {cart.length === 0 ? <EmptyState>Selecciona productos para comenzar.</EmptyState> : <div className="cart-list">{cart.map(item => <div className="cart-line" key={item.id}><div><strong>{item.name}</strong><span>{money(item.salePrice)} c/u</span></div><div className="cart-controls"><button type="button" title="Disminuir cantidad" onClick={() => change(item.id, -1)}><Minus size={14} /></button><b>{item.quantity}</b><button type="button" title="Aumentar cantidad" disabled={item.quantity >= item.currentStock} onClick={() => change(item.id, 1)}><Plus size={14} /></button><button type="button" title="Eliminar producto" onClick={() => setCart(current => current.filter(line => line.id !== item.id))}><Trash2 size={15} /></button></div></div>)}</div>}
          <div className="pos-total">
            <label>Descuento<input type="number" min="0" max={subtotal} value={discount} onChange={event => setDiscount(event.target.value)} /></label>
            <div><span>Subtotal</span><strong>{money(subtotal)}</strong></div>
            <div className="total-line"><span>Total</span><strong>{money(total)}</strong></div>
          </div>
          <label>Nombre para la venta o factura<input value={customerName} maxLength={160} placeholder="Opcional" onChange={event => setCustomerName(event.target.value)} /></label>
          <label>Medio de pago<select value={method} onChange={event => setMethod(event.target.value)}><option value="Cash">Efectivo</option><option value="Nequi">Nequi</option><option value="Daviplata">Daviplata</option><option value="Card">Tarjeta</option><option value="BankTransfer">Transferencia</option><option value="Other">Otro</option></select></label>
          <button className="primary-button pos-submit" disabled={cart.length === 0 || mutation.isPending} onClick={() => mutation.mutate()}>{mutation.isPending ? 'Procesando...' : 'Cobrar venta'}</button>
        </Panel>
      </div>
    </section>
  </AppLayout>
}

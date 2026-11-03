import { useQuery } from '@tanstack/react-query'
import { History, RefreshCw } from 'lucide-react'
import AppLayout from '../components/AppLayout'
import { EmptyState, Feedback, Panel, SectionHeading } from '../components/ui'
import { getAuditLogs } from '../api'

const actionLabels: Record<string, string> = {
  LOGIN: 'Inicio de sesión', USER_CREATED: 'Empleado creado', USER_ROLE_CHANGED: 'Rol de empleado cambiado', USER_ACTIVATED: 'Empleado reactivado', USER_DEACTIVATED: 'Empleado desactivado',
  SALE_CREATED: 'Venta registrada', SALE_CANCELLED: 'Venta anulada', PURCHASE_CREATED: 'Compra registrada', INVENTORY_ADJUSTED: 'Movimiento de inventario', PRODUCT_PRICE_CHANGED: 'Precio de producto cambiado',
  EXPENSE_CREATED: 'Gasto registrado', CASH_SHIFT_OPENED: 'Turno de caja abierto', CASH_SHIFT_CLOSED: 'Turno de caja cerrado', BILLIARD_SESSION_STARTED: 'Mesa abierta', BILLIARD_SESSION_CLOSED: 'Mesa cerrada'
}
const entityLabels: Record<string, string> = { User: 'Usuario', Sale: 'Venta', Purchase: 'Compra', InventoryMovement: 'Inventario', Product: 'Producto', Expense: 'Gasto', CashShift: 'Turno de caja', BilliardSession: 'Sesión de mesa' }
const date = (value: string) => new Intl.DateTimeFormat('es-CO', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))

export default function AuditPage() {
  const query = useQuery({ queryKey: ['audit'], queryFn: getAuditLogs })
  return <AppLayout eyebrow="CONTROL" title="Auditoría" actions={<button className="secondary-button" onClick={() => void query.refetch()}><RefreshCw size={16} />Actualizar</button>}><section className="content inventory-content"><Panel><SectionHeading eyebrow="TRAZABILIDAD" title="Operaciones sensibles" icon={<History size={21} />} /><p className="muted">Los registros son inmutables y no se pueden eliminar desde la aplicación.</p>{query.isLoading ? <EmptyState>Cargando auditoría...</EmptyState> : query.isError ? <Feedback kind="error">No se pudo cargar la auditoría.</Feedback> : !query.data?.length ? <EmptyState>No hay registros de auditoría.</EmptyState> : <div className="movement-list">{query.data.map(item => <div className="movement-row" key={item.id}><div><strong>{actionLabels[item.action] ?? item.action}</strong><span className="muted">{entityLabels[item.entityType] ?? item.entityType} · {item.entityId ?? 'Sin entidad'}</span></div><span className="muted">{date(item.createdAt)}</span><span className="muted">{item.ipAddress ?? 'IP no disponible'}</span></div>)}</div>}</Panel></section></AppLayout>
}
import { Bell, Check, Mail, Save, Smartphone, X } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import AppLayout from '../components/AppLayout'
import { EmptyState, Feedback, Panel, SectionHeading } from '../components/ui'
import { getNotificationPreferences, getNotifications, markNotificationRead, registerNotificationDevice, updateNotificationPreferences } from '../api'
import { isPushConfigured, requestPushToken } from '../firebasePush'
import { getSaleDetails } from '../api'
import { useAuthStore } from '../authStore'

const dateFormatter = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })

export default function NotificationsPage() {
  const queryClient = useQueryClient()
  const notifications = useQuery({ queryKey: ['notifications'], queryFn: () => getNotifications() })
  const canManage = useAuthStore(state => state.auth?.user.roles.some(role => role === 'OWNER' || role === 'ADMINISTRATOR') ?? false)
  const preferences = useQuery({ queryKey: ['notification-preferences'], queryFn: getNotificationPreferences, enabled: canManage })
  const [mode, setMode] = useState<'Never' | 'AllSales' | 'AboveAmount'>('AllSales')
  const [amount, setAmount] = useState('0')
  const [pushEnabled, setPushEnabled] = useState(true)
  const [whatsAppEnabled, setWhatsAppEnabled] = useState(false)
  const [whatsAppPhoneNumber, setWhatsAppPhoneNumber] = useState('')
  const [emailAddress, setEmailAddress] = useState('')
  const [pushMessage, setPushMessage] = useState('')
  const [filter, setFilter] = useState<'all' | 'unread'>('all')
  const [detailId, setDetailId] = useState<string | null>(null)
  const saleDetail = useQuery({ queryKey: ['sale-detail', detailId], queryFn: () => getSaleDetails(detailId!), enabled: Boolean(detailId) })

  useEffect(() => { if (preferences.data) { setMode(preferences.data.saleNotificationMode); setAmount(String(preferences.data.saleNotificationAmount)); setPushEnabled(preferences.data.pushEnabled); setWhatsAppEnabled(preferences.data.whatsAppEnabled); setWhatsAppPhoneNumber(preferences.data.whatsAppPhoneNumber ?? ''); setEmailAddress(preferences.data.emailAddress ?? '') } }, [preferences.data])
  const markRead = useMutation({ mutationFn: markNotificationRead, onSuccess: () => { void queryClient.invalidateQueries({ queryKey: ['notifications'] }); void queryClient.invalidateQueries({ queryKey: ['notifications-unread'] }) } })
  const savePreferences = useMutation({ mutationFn: updateNotificationPreferences, onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['notification-preferences'] }) })
  const enablePush = useMutation({
    mutationFn: async () => {
      const token = await requestPushToken()
      if (!token) throw new Error('No fue posible obtener permiso o token de Firebase.')
      const deviceId = localStorage.getItem('billares-pos-device-id') ?? crypto.randomUUID()
      localStorage.setItem('billares-pos-device-id', deviceId)
      await registerNotificationDevice(token, deviceId)
    },
    onSuccess: () => setPushMessage('Avisos push activados en este dispositivo.'),
    onError: (error) => setPushMessage((error as Error).message)
  })
  const items = notifications.data ?? []
  const unread = useMemo(() => items.filter(item => !item.readAt).length, [items])
  const visible = filter === 'unread' ? items.filter(item => !item.readAt) : items

  return <AppLayout eyebrow="SUPERVISIÓN" title="Notificaciones" actions={<button className="secondary-button" onClick={() => void notifications.refetch()}><Bell size={16} />Actualizar</button>}>
    <section className="content notification-content">
      {canManage && <Panel className="notification-settings">
        <SectionHeading eyebrow="PREFERENCIAS" title="Canales de aviso" icon={<Bell size={21} />} />
        <div className="notification-preference-form">
          <label>Recibir ventas<select value={mode} onChange={event => setMode(event.target.value as typeof mode)}><option value="AllSales">Todas las ventas</option><option value="AboveAmount">Solo sobre un monto</option><option value="Never">No recibir</option></select></label>
          {mode === 'AboveAmount' && <label>Monto mínimo (COP)<input type="number" min="0" step="1000" value={amount} onChange={event => setAmount(event.target.value)} /></label>}
          <label className="checkbox-row"><input type="checkbox" checked={pushEnabled} onChange={event => setPushEnabled(event.target.checked)} /> Permitir avisos push</label>
          <label className="checkbox-row"><input type="checkbox" checked={whatsAppEnabled} onChange={event => setWhatsAppEnabled(event.target.checked)} /> Permitir avisos de WhatsApp</label>
          {whatsAppEnabled && <label>Número de WhatsApp<input type="tel" placeholder="+573001234567" value={whatsAppPhoneNumber} onChange={event => setWhatsAppPhoneNumber(event.target.value)} /></label>}
          <label><span><Mail size={15} /> Correo para recibir notificaciones</span><input type="email" placeholder="dueno@negocio.com" value={emailAddress} onChange={event => setEmailAddress(event.target.value)} /><small className="field-hint">Si lo dejas vacío, se usará el correo de acceso del propietario.</small></label>
          <button className="primary-button" disabled={savePreferences.isPending} onClick={() => savePreferences.mutate({ saleNotificationMode: mode, saleNotificationAmount: Number(amount) || 0, pushEnabled, whatsAppEnabled, whatsAppPhoneNumber: whatsAppPhoneNumber || null, emailAddress: emailAddress.trim() || null })}><Save size={16} />{savePreferences.isPending ? 'Guardando...' : 'Guardar preferencias'}</button>
          {isPushConfigured() && <button className="secondary-button" disabled={enablePush.isPending} onClick={() => enablePush.mutate()}><Smartphone size={16} />{enablePush.isPending ? 'Activando...' : 'Activar avisos push'}</button>}
        </div>
        {savePreferences.isSuccess && <Feedback kind="success">Preferencias guardadas.</Feedback>}
        {pushMessage && <Feedback kind={enablePush.isError ? 'error' : 'notice'}>{pushMessage}</Feedback>}
      </Panel>}
      <Panel>
        <SectionHeading eyebrow="BANDEJA" title="Actividad reciente" action={<div className="notification-filter"><button className={filter === 'all' ? 'filter-button active' : 'filter-button'} onClick={() => setFilter('all')}>Todas ({items.length})</button><button className={filter === 'unread' ? 'filter-button active' : 'filter-button'} onClick={() => setFilter('unread')}>No leídas ({unread})</button></div>} />
        {notifications.isLoading && <EmptyState>Cargando notificaciones...</EmptyState>}
        {notifications.isError && <Feedback kind="error">{(notifications.error as Error).message}</Feedback>}
        {!notifications.isLoading && !notifications.isError && visible.length === 0 && <EmptyState>No hay notificaciones en este filtro.</EmptyState>}
        <div className="notification-list notification-list-scroll">{visible.map(notification => <article className={notification.readAt ? 'notification-item read' : 'notification-item'} key={notification.id}><div className="notification-icon"><Bell size={18} /></div><div className="notification-content"><strong>{notification.title}</strong><p>{notification.body}</p><time>{dateFormatter.format(new Date(notification.createdAt))}</time></div><div className="notification-actions">{notification.referenceType === 'Sale' && notification.referenceId && <button className="secondary-button notification-detail" onClick={() => setDetailId(notification.referenceId!)}>Detalle</button>}{!notification.readAt && <button className="secondary-button notification-read" disabled={markRead.isPending} onClick={() => markRead.mutate(notification.id)} title="Marcar como leída"><Check size={16} />Leída</button>}</div></article>)}</div>
      </Panel>
    </section>
    {detailId && saleDetail.data && <div className="modal-backdrop" role="presentation" onClick={() => setDetailId(null)}>
      <section className="sale-detail-modal" role="dialog" aria-modal="true" aria-labelledby="sale-detail-title" onClick={event => event.stopPropagation()}>
        <div className="modal-heading"><div><p className="eyebrow">DETALLE DE VENTA</p><h2 id="sale-detail-title">Venta #{saleDetail.data.sequentialNumber}</h2></div><button className="icon-button" onClick={() => setDetailId(null)} aria-label="Cerrar"><X size={20} /></button></div>
        <div className="sale-detail-meta"><span>Fecha: {dateFormatter.format(new Date(saleDetail.data.createdAt))}</span><span>A nombre de: {saleDetail.data.customerName ?? 'No registrado'}</span><span>Cajero: {saleDetail.data.cashierName ?? 'No registrado'}</span>{saleDetail.data.tableName && <span>Mesa: {saleDetail.data.tableName}</span>}{saleDetail.data.billiardStartedAt && <span>Inicio: {dateFormatter.format(new Date(saleDetail.data.billiardStartedAt))}</span>}{saleDetail.data.billiardEndedAt && <span>Final: {dateFormatter.format(new Date(saleDetail.data.billiardEndedAt))}</span>}{saleDetail.data.billiardMinutes !== null && <span>Tiempo: {saleDetail.data.billiardMinutes} min</span>}</div>
        <div className="sale-detail-items">{saleDetail.data.items.map((item, index) => <div className="sale-detail-row" key={index}><span>{item.quantity} x {item.description}</span><strong>{item.subtotal.toLocaleString('es-CO')} COP</strong></div>)}</div>
        <div className="sale-detail-total"><span>Total</span><strong>{saleDetail.data.total.toLocaleString('es-CO')} COP</strong></div>
        <div className="sale-detail-payments"><strong>Medios de pago</strong>{saleDetail.data.payments.map((payment, index) => <div key={index}><span>{({ Cash: 'Efectivo', Nequi: 'Nequi', Daviplata: 'Daviplata', Card: 'Tarjeta', BankTransfer: 'Transferencia', Other: 'Otro' } as Record<string, string>)[payment.method] ?? payment.method}</span><span>{payment.amount.toLocaleString('es-CO')} COP</span></div>)}</div>
      </section>
    </div>}  </AppLayout>
}





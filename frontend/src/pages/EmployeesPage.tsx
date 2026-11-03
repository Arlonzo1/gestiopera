import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Power, Plus, RefreshCw, Users, X } from 'lucide-react'
import AppLayout from '../components/AppLayout'
import { EmptyState, Feedback, Panel, SectionHeading } from '../components/ui'
import { changeEmployeeRole, changeEmployeeStatus, createEmployee, getEmployees } from '../api'
import { useAuthStore } from '../authStore'
import type { EmployeeResponse } from '../types'

const emptyForm = { email: '', fullName: '', password: '', role: 'CASHIER' }

export default function EmployeesPage() {
  const client = useQueryClient()
  const currentUserId = useAuthStore(state => state.auth?.user.id)
  const isOwner = useAuthStore(state => state.auth?.user.roles.includes('OWNER') ?? false)
  const canEdit = (user: EmployeeResponse) => user.id !== currentUserId && (isOwner || user.roles.every(value => value === 'CASHIER'))
  const query = useQuery({ queryKey: ['employees'], queryFn: getEmployees })
  const [form, setForm] = useState(emptyForm)
  const [error, setError] = useState<string | null>(null)
  const [pendingStatus, setPendingStatus] = useState<EmployeeResponse | null>(null)
  const create = useMutation({ mutationFn: () => createEmployee(form.email, form.fullName, form.password, form.role), onSuccess: async () => { setForm(emptyForm); setError(null); await client.invalidateQueries({ queryKey: ['employees'] }) }, onError: reason => setError(reason instanceof Error ? reason.message : 'No se pudo crear el empleado.') })
  const role = useMutation({ mutationFn: ({ id, value }: { id: string; value: string }) => changeEmployeeRole(id, value), onSuccess: async () => { setError(null); await client.invalidateQueries({ queryKey: ['employees'] }) }, onError: reason => setError(reason instanceof Error ? reason.message : 'No se pudo cambiar el rol.') })
  const status = useMutation({ mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => changeEmployeeStatus(id, isActive), onSuccess: async () => { setError(null); setPendingStatus(null); await client.invalidateQueries({ queryKey: ['employees'] }) }, onError: reason => setError(reason instanceof Error ? reason.message : 'No se pudo cambiar el estado del empleado.') })

  return <AppLayout eyebrow="ADMINISTRACIÓN" title="Empleados" actions={<button className="secondary-button" onClick={() => void query.refetch()}><RefreshCw size={16} />Actualizar</button>}>
    <section className="content users-content">
      <Panel><SectionHeading eyebrow="NUEVO ACCESO" title="Crear empleado" icon={<Plus size={21} />} />{error && <Feedback kind="error">{error}</Feedback>}<div className="employee-form"><label>Nombre<input value={form.fullName} onChange={e => setForm({ ...form, fullName: e.target.value })} /></label><label>Correo<input type="email" value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} /></label><label>Contraseña<input type="password" value={form.password} onChange={e => setForm({ ...form, password: e.target.value })} /></label><label>Rol<select value={form.role} onChange={e => setForm({ ...form, role: e.target.value })}><option value="CASHIER">Cajero</option>{isOwner && <><option value="ADMINISTRATOR">Administrador</option><option value="OWNER">Propietario</option></>}</select></label></div>{!isOwner && <p className="field-hint">Como administrador puedes crear y gestionar cajeros. Los administradores y propietarios los gestiona el propietario.</p>}<button className="primary-button" disabled={create.isPending} onClick={() => create.mutate()}><Plus size={16} />{create.isPending ? 'Creando...' : 'Crear empleado'}</button></Panel>
      <Panel><SectionHeading eyebrow="PERSONAL DEL NEGOCIO" title="Usuarios" icon={<Users size={21} />} />{query.isLoading ? <EmptyState>Cargando empleados...</EmptyState> : query.isError ? <Feedback kind="error">No se pudieron cargar los empleados.</Feedback> : !query.data?.length ? <EmptyState>No hay empleados registrados.</EmptyState> : <div className="movement-list">{query.data.map(user => <div className="movement-row employee-row" key={user.id}><div><strong>{user.fullName}</strong><span className="muted">{user.email}</span></div><select value={user.roles[0] ?? 'CASHIER'} onChange={e => role.mutate({ id: user.id, value: e.target.value })} disabled={role.isPending || !canEdit(user)}><option value="CASHIER">Cajero</option>{(isOwner || user.roles[0] !== 'CASHIER') && <><option value="ADMINISTRATOR">Administrador</option><option value="OWNER">Propietario</option></>}</select><span className={user.isActive ? 'stock-ok' : 'stock-critical'}>{user.isActive ? 'Activo' : 'Inactivo'}</span><button className={user.isActive ? 'icon-button danger-icon' : 'icon-button'} title={user.isActive ? 'Desactivar usuario' : 'Reactivar usuario'} disabled={!canEdit(user) || status.isPending} onClick={() => setPendingStatus(user)}><Power size={16} /></button></div>)}</div>}</Panel>
    </section>
    {pendingStatus && <StatusModal user={pendingStatus} pending={status.isPending} onCancel={() => setPendingStatus(null)} onConfirm={() => status.mutate({ id: pendingStatus.id, isActive: !pendingStatus.isActive })} />}
  </AppLayout>
}

function StatusModal({ user, pending, onCancel, onConfirm }: { user: EmployeeResponse; pending: boolean; onCancel: () => void; onConfirm: () => void }) {
  const activate = !user.isActive
  return <div className="modal-backdrop"><section className="modal inventory-modal" role="dialog" aria-modal="true" aria-labelledby="status-title"><SectionHeading eyebrow={activate ? 'REACTIVAR USUARIO' : 'DESACTIVAR USUARIO'} title={user.fullName} action={<button className="icon-button" onClick={onCancel} title="Cerrar"><X size={17} /></button>} /><p className="muted">{activate ? 'El usuario podrá iniciar sesión nuevamente.' : 'El usuario no podrá iniciar sesión, pero conservará su historial.'}</p><div className="modal-actions"><button className="secondary-button" onClick={onCancel}>Cancelar</button><button className={activate ? 'primary-button' : 'danger-button'} disabled={pending} onClick={onConfirm}>{pending ? 'Guardando...' : activate ? 'Reactivar usuario' : 'Desactivar usuario'}</button></div></section></div>
}
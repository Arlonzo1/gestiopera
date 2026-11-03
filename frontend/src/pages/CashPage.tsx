import { FormEvent, useState } from 'react'
import { Banknote, Plus, RotateCcw } from 'lucide-react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import AppLayout from '../components/AppLayout'
import { closeCashShift, createExpense, getCashHistory, getCurrentCash, openCashShift } from '../api'
import { useAuthStore } from '../authStore'
import type { ExpensePaymentMethod } from '../types'
import { Feedback, Panel, SectionHeading } from '../components/ui'

const money = (value: number) => new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(value)

export default function CashPage() {
  const logout = useAuthStore((state) => state.logout)
  const queryClient = useQueryClient()
  const { data: shift, isLoading, refetch } = useQuery({ queryKey: ['cash'], queryFn: getCurrentCash })
  const { data: history = [] } = useQuery({ queryKey: ['cash-history'], queryFn: getCashHistory })
 const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [opening, setOpening] = useState('')
  const [counted, setCounted] = useState('')
  const [closing, setClosing] = useState(false)
  const [expenseAmount, setExpenseAmount] = useState('')
  const [category, setCategory] = useState('')
  const [description, setDescription] = useState('')
  const [paymentMethod, setPaymentMethod] = useState<ExpensePaymentMethod>('Cash')

  async function run(task: () => Promise<unknown>, success: string) { setError(''); setMessage(''); try { await task(); await queryClient.invalidateQueries({ queryKey: ['cash'] }); setMessage(success) } catch (err) { setError(err instanceof Error ? err.message : 'No se pudo completar la operación') } }
  async function submitExpense(event: FormEvent) { event.preventDefault(); await run(() => createExpense(Number(expenseAmount), paymentMethod, category, description), 'Gasto registrado'); setExpenseAmount(''); setCategory(''); setDescription('') }
  async function close() {
    if (!shift || closing) return
    setClosing(true)
    setError('')
    setMessage('')
    try {
      await closeCashShift(Number(counted))
      setCounted('')
      queryClient.setQueryData(['cash'], null)
      setMessage('Turno cerrado correctamente')
      void queryClient.invalidateQueries({ queryKey: ['cash'] })
      void queryClient.invalidateQueries({ queryKey: ['cash-history'] })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cerrar el turno')
    } finally {
      setClosing(false)
    }
  }

  return <AppLayout eyebrow="CONTROL FINANCIERO" title="Caja">
    <section className="content cash-content">
      {message && <Feedback>{message}</Feedback>}{error && <Feedback kind="error">{error}</Feedback>}
      {isLoading ? <div className="empty-state">Cargando caja...</div> : shift ? <>
        <section className="cash-summary"><div><span className="muted">Efectivo inicial</span><strong>{money(shift.openingCash)}</strong></div><div><span className="muted">Ventas en efectivo</span><strong>{money(shift.cashSales)}</strong></div><div><span className="muted">Gastos en efectivo</span><strong>{money(shift.cashExpenses)}</strong></div><div className="expected"><span className="muted">Efectivo esperado</span><strong>{money(shift.expectedCash)}</strong></div></section>
        <div className="cash-columns"><Panel className="cash-panel"><SectionHeading eyebrow="TURNO ACTIVO" title="Cerrar caja" icon={<Banknote size={22} />} /><p className="muted">Cuenta el efectivo físico y confirma el cierre del turno.</p><label>Efectivo contado (COP)<input type="number" min="0" step="0.01" value={counted} onChange={(e) => setCounted(e.target.value)} /></label><button className="danger-button cash-action" disabled={!counted || closing} onClick={() => void close()}>{closing ? 'Cerrando...' : 'Cerrar turno'}</button></Panel><ExpenseForm amount={expenseAmount} setAmount={setExpenseAmount} category={category} setCategory={setCategory} description={description} setDescription={setDescription} method={paymentMethod} setMethod={setPaymentMethod} onSubmit={submitExpense} /></div>
      </> : <OpenCash opening={opening} setOpening={setOpening} onOpen={() => void run(() => openCashShift(Number(opening)), 'Turno abierto correctamente')} />}
      {history.length > 0 && <CashHistory history={history} />}
     <button className="secondary-button refresh-cash" onClick={() => void refetch()}><RotateCcw size={16} />Actualizar caja</button>
    </section>
  </AppLayout>
}


function CashHistory({ history }: { history: Array<{ id: string; openedAt: string; closedAt: string | null; openingCash: number; cashSales: number; cashExpenses: number; expectedCash: number; countedCash: number | null; difference: number | null }> }) {
  const date = (value: string | null) => value ? new Intl.DateTimeFormat('es-CO', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)) : '-';
  return <Panel className="cash-panel cash-history"><SectionHeading eyebrow="HISTORIAL" title="Turnos cerrados" /><div className="history-list">{history.map((item) => <div className="history-row" key={item.id}><div><strong>{date(item.closedAt)}</strong><span className="muted">Inicial {money(item.openingCash)} · Ventas {money(item.cashSales)} · Gastos {money(item.cashExpenses)}</span></div><div><strong>Esperado {money(item.expectedCash)}</strong><span className={item.difference === null || item.difference === 0 ? 'muted' : item.difference > 0 ? 'difference-positive' : 'difference-negative'}>Diferencia {item.difference === null ? '-' : money(item.difference)}</span></div></div>)}</div></Panel>
}
function OpenCash({ opening, setOpening, onOpen }: { opening: string; setOpening: (value: string) => void; onOpen: () => void }) { return <Panel className="cash-panel narrow-panel"><SectionHeading eyebrow="INICIO DE TURNO" title="Abrir caja" icon={<Banknote size={22} />} /><p className="muted">Registra el efectivo disponible antes de comenzar la operación.</p><label>Efectivo inicial (COP)<input type="number" min="0" step="0.01" value={opening} onChange={(e) => setOpening(e.target.value)} /></label><button className="primary-button cash-action" disabled={!opening} onClick={onOpen}><Plus size={16} />Abrir turno</button></Panel> }

function ExpenseForm({ amount, setAmount, category, setCategory, description, setDescription, method, setMethod, onSubmit }: { amount: string; setAmount: (v: string) => void; category: string; setCategory: (v: string) => void; description: string; setDescription: (v: string) => void; method: ExpensePaymentMethod; setMethod: (v: ExpensePaymentMethod) => void; onSubmit: (e: FormEvent) => void }) { return <Panel className="cash-panel"><div className="panel-heading"><div><p className="eyebrow">SALIDA DE DINERO</p><h2>Registrar gasto</h2></div></div><form className="form-stack compact" onSubmit={onSubmit}><label>Valor<input type="number" min="0.01" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} /></label><label>Método<select value={method} onChange={(e) => setMethod(e.target.value as ExpensePaymentMethod)}><option value="Cash">Efectivo</option><option value="Nequi">Nequi</option><option value="Daviplata">Daviplata</option><option value="Card">Tarjeta</option><option value="BankTransfer">Transferencia</option><option value="Other">Otro</option></select></label><label>Categoría<input value={category} onChange={(e) => setCategory(e.target.value)} /></label><label>Descripción<input value={description} onChange={(e) => setDescription(e.target.value)} /></label><button className="primary-button cash-action" disabled={!amount || !category}><Plus size={16} />Registrar gasto</button></form></Panel> }



import { FormEvent, useState } from 'react'
import { LogIn, ShieldCheck } from 'lucide-react'
import { login } from '../api'
import { useAuthStore } from '../authStore'

export default function LoginPage() {
  const setAuth = useAuthStore((state) => state.setAuth)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault(); setError(''); setLoading(true)
    try { setAuth(await login(email, password)) }
    catch (err) { setError(err instanceof Error ? err.message : 'No se pudo iniciar sesión') }
    finally { setLoading(false) }
  }

  return <main className="login-shell">
    <section className="login-side"><div className="login-panel">
      <div className="brand-mark"><ShieldCheck size={22} /></div>
      <p className="eyebrow">BILLARES POS</p>
      <h1>Control operativo del establecimiento</h1>
      <p className="muted">Inicia sesión para supervisar tus mesas y operaciones.</p>
      <form onSubmit={submit} className="form-stack">
        <label>Correo <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} /></label>
        <label>Contraseña <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} /></label>
        {error && <div className="error-box">{error}</div>}
        <button className="primary-button" disabled={loading}><LogIn size={17} />{loading ? 'Ingresando...' : 'Ingresar'}</button>
      </form>
    </div></section>
    <aside className="login-visual">
      <img src="/login-billar.jpg" alt="Bolas de billar ordenadas sobre una mesa de paño azul" />
      <div className="login-visual-text"><p>Mesas · Consumos · Caja</p><strong>Todo tu billar en un solo lugar.</strong></div>
      <a className="photo-credit" href="https://commons.wikimedia.org/wiki/File:DSCF0497_Rack_em_up_a_colorful_set_of_billiard_balls_ready_for_a_night_of_pool_under_cool_blue_bar_lights.jpg" target="_blank" rel="noreferrer">Foto: PattayaPatrol · CC BY-SA 4.0</a>
    </aside>
  </main>
}

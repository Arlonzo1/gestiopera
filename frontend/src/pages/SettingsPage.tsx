import { FormEvent, useEffect, useState, type ChangeEvent } from "react"
import { Pencil, Plus, Settings2 } from "lucide-react"
import { useMutation, useQuery } from "@tanstack/react-query"
import AppLayout from "../components/AppLayout"
import { createBilliardTable, getTables, updateBilliardTable, getTenantBranding, updateTenantBranding } from "../api"
import { useAuthStore } from "../authStore"
import type { Table, TableKind } from "../types"
import { getBranding, saveBranding } from "../branding"

const money = (value: number) => new Intl.NumberFormat("es-CO", { style: "currency", currency: "COP", maximumFractionDigits: 0 }).format(value)

export default function SettingsPage() {
  const auth = useAuthStore((state) => state.auth)
  const [message, setMessage] = useState("")
  const [editTable, setEditTable] = useState<Table | null>(null)
  const [newKind, setNewKind] = useState<TableKind>("Billiard")
  const initialBranding = getBranding()
  const [brandName, setBrandName] = useState(initialBranding.name)
  const [logoDataUrl, setLogoDataUrl] = useState<string | null>(initialBranding.logoDataUrl)
  const [brandingMessage, setBrandingMessage] = useState("")
  const { data: tables = [], isLoading, refetch } = useQuery({ queryKey: ["tables"], queryFn: getTables })
  const canManage = auth?.user.roles.some((role) => role === "OWNER" || role === "ADMINISTRATOR")
  const isOwner = auth?.user.roles.includes("OWNER") ?? false
  const update = useMutation({
    mutationFn: ({ id, value }: { id: string; value: { name: string; hourlyRate: number; billingIncrementMinutes: number } }) => updateBilliardTable(id, value),
    onSuccess: async () => { setEditTable(null); setMessage("Mesa actualizada correctamente"); await refetch() },
    onError: (err: Error) => setMessage(err.message)
  })

  useEffect(() => {
    void getTenantBranding().then((value) => {
      setBrandName(value.name)
      setLogoDataUrl(value.logoDataUrl)
      saveBranding(value)
    }).catch(() => undefined)
  }, [])

  async function saveBrandingSettings(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    try {
      const value = await updateTenantBranding({ name: brandName.trim() || "Billar POS", logoDataUrl })
      saveBranding(value)
      setBrandName(value.name)
      setLogoDataUrl(value.logoDataUrl)
      setBrandingMessage("Marca actualizada correctamente")
    } catch (err) {
      setBrandingMessage(err instanceof Error ? err.message : "No se pudo guardar la identidad")
    }
  }

  function handleLogo(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    if (!file) return
    if (!file.type.startsWith("image/") || file.size > 5 * 1024 * 1024) {
      setBrandingMessage("Selecciona una imagen PNG, JPG o WEBP de máximo 5 MB.")
      return
    }
    const reader = new FileReader()
    reader.onload = () => {
      const image = new Image()
      image.onload = () => {
        const maxSize = 256
        const scale = Math.min(1, maxSize / Math.max(image.width, image.height))
        const canvas = document.createElement("canvas")
        canvas.width = Math.max(1, Math.round(image.width * scale))
        canvas.height = Math.max(1, Math.round(image.height * scale))
        canvas.getContext("2d")?.drawImage(image, 0, 0, canvas.width, canvas.height)
        setLogoDataUrl(canvas.toDataURL("image/webp", 0.82))
        setBrandingMessage("Imagen lista. Pulsa Guardar identidad.")
      }
      image.onerror = () => setBrandingMessage("No se pudo procesar la imagen seleccionada.")
      image.src = String(reader.result)
    }
    reader.onerror = () => setBrandingMessage("No se pudo leer la imagen seleccionada.")
    reader.readAsDataURL(file)
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const formElement = event.currentTarget
    const form = new FormData(formElement)
    setMessage("")
    try {
      if (newKind === "Regular") await createBilliardTable(String(form.get("name") ?? ""), 0, 15, "Regular")
      else await createBilliardTable(String(form.get("name") ?? ""), Number(form.get("hourlyRate")), Number(form.get("billingIncrementMinutes")))
      formElement.reset()
      setNewKind("Billiard")
      setMessage("Mesa creada correctamente")
      await refetch()
    } catch (err) {
      setMessage(err instanceof Error ? err.message : "No se pudo crear la mesa")
    }
  }

  return <AppLayout eyebrow="ADMINISTRACIÓN" title="Configuración">
    <section className="content">
      {isOwner && <section className="table-config-panel branding-panel">
        <div className="panel-heading"><div><p className="eyebrow">IDENTIDAD DEL NEGOCIO</p><h2>Marca del establecimiento</h2></div></div>
        <p className="muted">Personaliza el nombre y el logo que aparecen en la esquina superior del menu.</p>
        <form className="form-stack compact" onSubmit={saveBrandingSettings}>
          <label>Nombre del negocio<input value={brandName} onChange={(event) => setBrandName(event.target.value)} maxLength={80} required /></label>
          <label>Logo del negocio<input type="file" accept="image/png,image/jpeg,image/webp" onChange={handleLogo} /></label>
          {logoDataUrl && <img className="branding-preview" src={logoDataUrl} alt="Vista previa del logo" />}
          <div className="modal-actions"><button className="primary-button">Guardar identidad</button>{logoDataUrl && <button type="button" className="secondary-button" onClick={() => setLogoDataUrl(null)}>Quitar logo</button>}</div>
        </form>
        {brandingMessage && <div className="notice">{brandingMessage}</div>}
      </section>}
      <section className="table-config-panel">
        <div className="panel-heading"><div><p className="eyebrow">MESAS DE BILLAR</p><h2>Configuración de mesas</h2></div><Settings2 size={21} /></div>
        <p className="muted">Configura cuántas mesas tiene tu establecimiento y define cómo se cobra el tiempo de juego.</p>
        {canManage ? <form className="table-config-form" onSubmit={submit}>
          <label>Tipo de mesa<select value={newKind} onChange={(event) => setNewKind(event.target.value as TableKind)}><option value="Billiard">Mesa de billar (cobra tiempo)</option><option value="Regular">Mesa de bar (solo consumo)</option></select></label>
          <label>Nombre de la mesa<input name="name" placeholder={newKind === "Regular" ? "Mesa bar 1" : "Mesa 1"} required /></label>
          {newKind === "Billiard" && <><label>Tarifa por hora (COP)<input name="hourlyRate" type="number" min="0" step="1" placeholder="30000" required /></label>
          <label>Facturación cada (minutos)<select name="billingIncrementMinutes" defaultValue="15"><option value="1">Cada minuto</option><option value="5">Cada 5 minutos</option><option value="10">Cada 10 minutos</option><option value="15">Cada 15 minutos</option><option value="30">Cada 30 minutos</option><option value="60">Cada hora</option></select></label></>}
          <button className="primary-button"><Plus size={16} />Agregar mesa</button>
        </form> : <div className="notice">Solo el propietario o un administrador puede modificar esta configuración.</div>}
        {message && <div className="notice">{message}</div>}
      </section>
      <section className="products-panel">
        <div className="panel-heading"><div><p className="eyebrow">CATÁLOGO ACTIVO</p><h2>Mesas configuradas</h2></div><strong>{tables.length}</strong></div>
        {isLoading ? <p className="muted">Cargando mesas...</p> : <div className="settings-table-list">{tables.map((table) => <div className="settings-table-row" key={table.id}>
          <div><strong>{table.name}</strong><span className="muted">{table.kind === "Regular" ? "Mesa de bar" : table.billingIncrementMinutes === 60 ? "Cobro por hora" : "Cobro cada " + table.billingIncrementMinutes + " minutos"}</span></div>
          <strong>{table.kind === "Regular" ? "Solo consumo" : `${money(table.hourlyRate)} / hora`}</strong>
          {canManage && <button className="icon-button" title="Editar mesa" onClick={() => setEditTable(table)}><Pencil size={16} /></button>}
        </div>)}</div>}
      </section>
    </section>
    {editTable && <EditTable table={editTable} pending={update.isPending} onClose={() => setEditTable(null)} onSave={(value) => update.mutate({ id: editTable.id, value })} />}
  </AppLayout>
}

function EditTable({ table, pending, onClose, onSave }: { table: Table; pending: boolean; onClose: () => void; onSave: (value: { name: string; hourlyRate: number; billingIncrementMinutes: number }) => void }) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    onSave({ name: String(form.get("name") ?? ""), hourlyRate: Number(form.get("hourlyRate")), billingIncrementMinutes: Number(form.get("billingIncrementMinutes")) })
  }
  return <div className="modal-backdrop"><section className="modal inventory-modal" role="dialog" aria-modal="true">
    <div className="panel-heading"><div><p className="eyebrow">CONFIGURACIÓN DE MESA</p><h2>Editar {table.name}</h2></div><button type="button" className="icon-button" onClick={onClose} title="Cerrar">×</button></div>
    <form className="form-stack compact" onSubmit={submit}>
      <label>Nombre de la mesa<input name="name" defaultValue={table.name} required /></label>
      {table.kind !== "Regular" && <><label>Tarifa por hora (COP)<input name="hourlyRate" type="number" min="0" step="1" defaultValue={table.hourlyRate} required /></label>
      <label>Facturación cada (minutos)<select name="billingIncrementMinutes" defaultValue={String(table.billingIncrementMinutes)}><option value="1">Cada minuto</option><option value="5">Cada 5 minutos</option><option value="10">Cada 10 minutos</option><option value="15">Cada 15 minutos</option><option value="30">Cada 30 minutos</option><option value="60">Cada hora</option></select></label></>}
      <div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Cancelar</button><button className="primary-button" disabled={pending}>{pending ? "Guardando..." : "Guardar cambios"}</button></div>
    </form>
  </section></div>
}



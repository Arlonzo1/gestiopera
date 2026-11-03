export type Branding = { name: string; logoDataUrl: string | null }

const storageKey = "billares-pos-branding"
const brandingEvent = "billares-pos-branding-updated"
const fallback: Branding = { name: "Billar POS", logoDataUrl: null }

export function getBranding(): Branding {
  try {
    const stored = localStorage.getItem(storageKey)
    if (!stored) return fallback
    const value = JSON.parse(stored) as Partial<Branding>
    return { name: value.name?.trim() || fallback.name, logoDataUrl: value.logoDataUrl || null }
  } catch {
    return fallback
  }
}

export function saveBranding(value: Branding) {
  localStorage.setItem(storageKey, JSON.stringify(value))
  window.dispatchEvent(new Event(brandingEvent))
}

export function onBrandingChanged(listener: () => void) {
  window.addEventListener(brandingEvent, listener)
  return () => window.removeEventListener(brandingEvent, listener)
}
export function applyBranding(value: Branding = getBranding()) {
  document.title = value.name
  let icon = document.querySelector<HTMLLinkElement>('link[rel="icon"]')
  if (!icon) {
    icon = document.createElement("link")
    icon.rel = "icon"
    document.head.appendChild(icon)
  }
  icon.href = value.logoDataUrl || "data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 64 64%22><rect width=%2264%22 height=%2264%22 rx=%2212%22 fill=%22%231b819c%22/><text x=%2232%22 y=%2244%22 text-anchor=%22middle%22 font-size=%2236%22 fill=%22white%22>B</text></svg>"
}

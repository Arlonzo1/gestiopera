import type { ReactNode } from 'react'

type PanelProps = {
  as?: 'section' | 'aside'
  className?: string
  children: ReactNode
}

export function Panel({ as: Tag = 'section', className = '', children }: PanelProps) {
  return <Tag className={['ui-panel', className].filter(Boolean).join(' ')}>{children}</Tag>
}

export function SectionHeading({ eyebrow, title, icon, action }: { eyebrow?: string; title: string; icon?: ReactNode; action?: ReactNode }) {
  return <div className="section-heading">
    <div>{eyebrow && <p className="eyebrow">{eyebrow}</p>}<h2>{title}</h2></div>
    {action ?? icon}
  </div>
}

export function Feedback({ kind = 'notice', children }: { kind?: 'notice' | 'error' | 'success'; children: ReactNode }) {
  return <div className={kind === 'error' ? 'error-box' : kind === 'success' ? 'success-box' : 'notice'} role={kind === 'error' ? 'alert' : 'status'}>{children}</div>
}

export function EmptyState({ children }: { children: ReactNode }) {
  return <div className="empty-state">{children}</div>
}

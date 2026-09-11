import { useEffect, useRef, useState } from 'react'
import { Info } from 'lucide-react'

/**
 * Alan etiketinin sağındaki bilgi ikonu: tıklanınca açıklama balonu açılır; dışarı tıklama / Esc kapatır.
 * ⚠ <label> İÇİNE konmaz — label'ın örtük kontrolü tıklamayı yutar (bkz. feedback_label_implicit_button_control_trap).
 *   Etiket metniyle KARDEŞ eleman olarak kullanılır: <div className="flbl">Ad <InfoTip text="…" /></div>
 */
export function InfoTip({ text }: { text: string }) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLSpanElement>(null)

  useEffect(() => {
    if (!open) return
    const kapat = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false) }
    const esc = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpen(false) }
    document.addEventListener('mousedown', kapat)
    document.addEventListener('keydown', esc)
    return () => { document.removeEventListener('mousedown', kapat); document.removeEventListener('keydown', esc) }
  }, [open])

  return (
    <span ref={ref} className="relative inline-flex items-center align-middle ml-1">
      <button type="button" aria-label="Açıklama" aria-expanded={open}
        onClick={(e) => { e.preventDefault(); e.stopPropagation(); setOpen(o => !o) }}
        className="inline-flex items-center justify-center rounded-full"
        style={{ width: 16, height: 16, color: open ? 'var(--brand)' : 'var(--text-s)', background: 'transparent', border: 0, cursor: 'pointer', padding: 0 }}>
        <Info size={14} />
      </button>
      {open && (
        <span role="tooltip"
          className="absolute left-0 top-full mt-1 z-50 rounded-lg px-3 py-2 text-xs font-normal normal-case whitespace-normal shadow-lg"
          style={{ width: 260, background: 'var(--surface)', color: 'var(--text)', border: '1px solid var(--border)', lineHeight: 1.45 }}>
          {text}
        </span>
      )}
    </span>
  )
}

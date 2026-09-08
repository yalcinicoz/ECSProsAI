import { useEffect, useRef, useState } from 'react'
import { ChevronDown, ChevronUp, Columns3, Lock, RotateCcw } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { GridColumn, GridPrefs } from './types'

interface Props<T> {
  columns: GridColumn<T>[]          // kullanıcı sırasıyla (tanım + prefs.order)
  visibleKeys: Set<string>
  prefs: GridPrefs
  setPrefs: (patch: Partial<GridPrefs> | ((p: GridPrefs) => GridPrefs)) => void
  resetPrefs: () => void
  frozenSupported: boolean
}

/** "Kolonlar" menüsü (K4, E5): göster/gizle, ▲/▼ sıra, sabit kolon aç/kapa, varsayılana dön. Sürükle-bırak yok. */
export function ColumnsMenu<T>({ columns, visibleKeys, prefs, setPrefs, resetPrefs, frozenSupported }: Props<T>) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const onDoc = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false) }
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpen(false) }
    document.addEventListener('mousedown', onDoc); document.addEventListener('keydown', onKey)
    return () => { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onKey) }
  }, [open])

  const toggle = (key: string, show: boolean) => setPrefs(p => ({
    ...p,
    manualVisible: show ? Array.from(new Set([...(p.manualVisible ?? []), key])) : (p.manualVisible ?? []).filter(k => k !== key),
    manualHidden: show ? (p.manualHidden ?? []).filter(k => k !== key) : Array.from(new Set([...(p.manualHidden ?? []), key])),
  }))

  const move = (key: string, delta: -1 | 1) => setPrefs(p => {
    const order = columns.map(c => c.key)
    const i = order.indexOf(key); const j = i + delta
    if (i < 0 || j < 0 || j >= order.length) return p
    const next = [...order]; next.splice(i, 1); next.splice(j, 0, key)
    return { ...p, order: next }
  })

  const customized = !!(prefs.order?.length || prefs.manualVisible?.length || prefs.manualHidden?.length || prefs.frozen === 'off')

  return (
    <div className="relative" ref={ref}>
      <button type="button" onClick={() => setOpen(o => !o)} aria-haspopup="menu" aria-expanded={open}
        className={cn('inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm transition-colors hover:bg-[var(--surface2)]')}
        style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>
        <Columns3 size={15} /> <span className="mob-hide">Kolonlar</span>
        {customized && <span className="w-1.5 h-1.5 rounded-full" style={{ background: 'var(--brand)' }} aria-label="kişiselleştirildi" />}
      </button>
      {open && (
        <div role="menu" className="absolute right-0 mt-1 w-72 max-h-[70vh] overflow-y-auto thin-scroll rounded-xl shadow-lg z-40 p-2"
          style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}>
          <div className="px-2 py-1 text-[11px] font-semibold uppercase tracking-wide" style={{ color: 'var(--text-s)' }}>Görünür kolonlar ve sıra</div>
          {columns.map((c, i) => {
            const on = visibleKeys.has(c.key)
            return (
              <div key={c.key} className="flex items-center gap-2 px-2 py-1 rounded-lg hover:bg-[var(--surface2)]">
                <label className="flex items-center gap-2 flex-1 min-w-0 text-sm cursor-pointer" style={{ color: 'var(--text)' }}>
                  <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={on} disabled={!!c.lockVisible}
                    onChange={e => toggle(c.key, e.target.checked)} />
                  <span className="truncate">{c.header || c.key}</span>
                  {c.lockVisible && <Lock size={12} style={{ color: 'var(--text-s)' }} aria-label="kritik kolon, gizlenemez" />}
                </label>
                <button type="button" onClick={() => move(c.key, -1)} disabled={i === 0} aria-label="Yukarı taşı"
                  className="p-0.5 rounded disabled:opacity-30 hover:bg-[var(--surface)]" style={{ color: 'var(--text-m)' }}><ChevronUp size={14} /></button>
                <button type="button" onClick={() => move(c.key, 1)} disabled={i === columns.length - 1} aria-label="Aşağı taşı"
                  className="p-0.5 rounded disabled:opacity-30 hover:bg-[var(--surface)]" style={{ color: 'var(--text-m)' }}><ChevronDown size={14} /></button>
              </div>
            )
          })}
          {frozenSupported && (
            <label className="flex items-center gap-2 px-2 py-1.5 mt-1 text-sm cursor-pointer" style={{ color: 'var(--text)', borderTop: '1px solid var(--border)' }}>
              <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={prefs.frozen !== 'off'}
                onChange={e => setPrefs({ frozen: e.target.checked ? 'auto' : 'off' })} />
              Kritik kolonları sabit tut
            </label>
          )}
          <button type="button" onClick={() => { resetPrefs(); setOpen(false) }} disabled={!customized}
            className="flex items-center gap-1.5 w-full px-2 py-1.5 mt-1 rounded-lg text-sm disabled:opacity-40 hover:bg-[var(--surface2)]"
            style={{ color: 'var(--text-m)', borderTop: '1px solid var(--border)' }}>
            <RotateCcw size={13} /> Varsayılana dön
          </button>
        </div>
      )}
    </div>
  )
}

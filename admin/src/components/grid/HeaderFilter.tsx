import { useEffect, useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { Filter, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { GridStateApi } from './useGridState'
import type { GridFilterField } from './filterUtils'
import { FieldRow } from './FilterBar'

// Sütun başlığı filtresi (kullanıcı kararı 2026-09-08): "Gelişmiş" paneli yerine filtrelenebilir her başlıkta ikon; tıklayınca
// o kolonun alan(lar)ı portal açılır pencerede (kaydırma kabı kırpmasın). Aktif filtre ikonu marka renginde + sayı.

interface Props {
  fields: GridFilterField[]
  grid: GridStateApi
  header: string
}

export function HeaderFilterButton({ fields, grid, header }: Props) {
  const [open, setOpen] = useState(false)
  const [anchorEl, setAnchorEl] = useState<HTMLButtonElement | null>(null)   // callback ref: render sırasında ref okunmaz
  const active = fields.filter(f => grid.state.filters.some(x => x.field === f.key)).length
  return (
    <>
      <button ref={setAnchorEl} type="button" aria-label={`${header} filtresi`} aria-haspopup="dialog" aria-expanded={open}
        onClick={e => { e.stopPropagation(); setOpen(o => !o) }}
        className={cn('inline-flex items-center gap-0.5 p-0.5 rounded transition-colors hover:bg-[var(--surface)]', active ? 'opacity-100' : 'opacity-50 hover:opacity-100')}
        style={{ color: active ? 'var(--brand)' : 'var(--text-s)' }}>
        <Filter size={12} fill={active ? 'currentColor' : 'none'} />
        {active > 1 && <span className="text-[10px] font-semibold">{active}</span>}
      </button>
      {open && (
        <HeaderPopover anchor={anchorEl} title={header} onClose={() => setOpen(false)}>
          <div className="space-y-2">
            {fields.map(f => <FieldRow key={f.key} field={f} grid={grid} stacked />)}
            <div className="flex items-center justify-between pt-1">
              <button type="button" disabled={!active} onClick={() => { fields.forEach(f => grid.removeFilter(f.key)) }}
                className="text-xs underline disabled:opacity-40" style={{ color: 'var(--text-s)' }}>Temizle</button>
              <button type="button" onClick={() => setOpen(false)} className="px-3 py-1 rounded-lg text-xs font-medium" style={{ background: 'var(--brand)', color: '#fff' }}>Tamam</button>
            </div>
          </div>
        </HeaderPopover>
      )}
    </>
  )
}

/** Başlığa hizalı, body'ye portal edilen küçük pencere; viewport taşmasını sola/yukarı kaydırarak engeller. */
function HeaderPopover({ anchor, title, children, onClose }: { anchor: HTMLElement | null; title: string; children: ReactNode; onClose: () => void }) {
  const ref = useRef<HTMLDivElement>(null)
  const [pos, setPos] = useState<{ top: number; left: number } | null>(null)

  useLayoutEffect(() => {
    if (!anchor) return
    const place = () => {
      const r = anchor.getBoundingClientRect()
      const w = ref.current?.offsetWidth ?? 300, h = ref.current?.offsetHeight ?? 200
      let left = r.left, top = r.bottom + 6
      if (left + w > window.innerWidth - 8) left = Math.max(8, window.innerWidth - w - 8)
      if (top + h > window.innerHeight - 8 && r.top - h - 6 > 8) top = r.top - h - 6
      setPos({ top, left })
    }
    const raf = requestAnimationFrame(place)
    window.addEventListener('resize', place); window.addEventListener('scroll', place, true)
    return () => { cancelAnimationFrame(raf); window.removeEventListener('resize', place); window.removeEventListener('scroll', place, true) }
  }, [anchor])

  useEffect(() => {
    const onDoc = (e: MouseEvent) => { const t = e.target as Node; if (ref.current && !ref.current.contains(t) && !(anchor && anchor.contains(t))) onClose() }
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    document.addEventListener('mousedown', onDoc); document.addEventListener('keydown', onKey)
    return () => { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onKey) }
  }, [anchor, onClose])

  return createPortal(
    <div ref={ref} role="dialog" aria-label={`${title} filtresi`} data-header-filter=""
      className="fixed z-[80] w-[300px] max-w-[calc(100vw-16px)] rounded-xl shadow-lg p-3"
      style={{ top: pos?.top ?? -9999, left: pos?.left ?? -9999, background: 'var(--surface)', border: '1px solid var(--border)', visibility: pos ? 'visible' : 'hidden' }}
      onClick={e => e.stopPropagation()}>
      <div className="flex items-center justify-between mb-2">
        <span className="text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--text-s)' }}>{title}</span>
        <button type="button" aria-label="Kapat" onClick={onClose} className="p-0.5 rounded hover:bg-[var(--surface2)]" style={{ color: 'var(--text-s)' }}><X size={14} /></button>
      </div>
      {children}
    </div>,
    document.body,
  )
}

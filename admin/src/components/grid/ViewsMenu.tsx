import { useEffect, useRef, useState } from 'react'
import { Bookmark, ChevronDown, Star, Trash2 } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { GridViewsApi } from './useGridViews'

// "Görünüm ▾" seçici (plan §2.9): kaydedilmiş görünümler listesi (uygula / varsayılan yap / sil), geçerli durumu kaydet/güncelle.
// Filtre içeren görünüm uygulanınca çipler FilterBar'da zaten görünür; aktif görünüm adı düğmede yazar.

export function ViewsMenu({ views }: { views: GridViewsApi }) {
  const [open, setOpen] = useState(false)
  const [naming, setNaming] = useState(false)
  const [name, setName] = useState('')
  const [err, setErr] = useState<string | null>(null)
  const ref = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (!open) return
    const onDoc = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) { setOpen(false); setNaming(false) } }
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') { setOpen(false); setNaming(false) } }
    document.addEventListener('mousedown', onDoc); document.addEventListener('keydown', onKey)
    return () => { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onKey) }
  }, [open])
  useEffect(() => { if (naming) inputRef.current?.focus() }, [naming])

  const active = views.activeView
  const run = async (fn: () => Promise<unknown>) => { setErr(null); try { await fn() } catch { setErr('Kaydedilemedi. Tekrar deneyin.') } }

  return (
    <div className="relative" ref={ref}>
      <button type="button" onClick={() => setOpen(o => !o)} aria-haspopup="menu" aria-expanded={open} aria-label="Görünüm"
        className={cn('inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm max-w-[220px] transition-colors hover:bg-[var(--surface2)]')}
        style={{ border: '1px solid var(--border)', color: 'var(--text)', background: active ? 'var(--surface2)' : undefined }}>
        <Bookmark size={15} />
        <span className="truncate">{active ? active.name : 'Görünüm'}</span>
        {views.views.length > 0 && !active && <span className="text-[11px] px-1.5 rounded-full" style={{ background: 'var(--surface2)', color: 'var(--text-s)' }}>{views.views.length}</span>}
        <ChevronDown size={13} />
      </button>
      {open && (
        <div role="menu" className="absolute left-0 mt-1 w-80 max-h-[70vh] overflow-y-auto thin-scroll rounded-xl shadow-lg z-40 p-2"
          style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}>
          <div className="px-2 py-1 text-[11px] font-semibold uppercase tracking-wide" style={{ color: 'var(--text-s)' }}>Kaydedilmiş görünümler (kişisel)</div>
          {views.views.length === 0 && <div className="px-2 py-2 text-sm" style={{ color: 'var(--text-s)' }}>Henüz görünüm yok. Filtre/kolon ayarlarını yapıp aşağıdan kaydedin.</div>}
          {views.views.map(v => {
            const isDefault = views.defaultViewId === v.id
            const isActive = active?.id === v.id
            return (
              <div key={v.id} className={cn('flex items-center gap-1 px-2 py-1 rounded-lg hover:bg-[var(--surface2)]', isActive && 'bg-[var(--surface2)]')}>
                <button type="button" role="menuitem" onClick={() => { views.apply(v); setOpen(false) }}
                  className="flex-1 min-w-0 text-left text-sm truncate" style={{ color: 'var(--text)' }} title={Object.entries(v.params).map(([k, x]) => `${k}=${x}`).join('\n') || 'filtresiz'}>
                  {v.name}{isDefault && <span className="ml-1 text-[11px]" style={{ color: 'var(--text-s)' }}>(varsayılan)</span>}
                </button>
                <button type="button" aria-label={isDefault ? 'Varsayılanı kaldır' : 'Varsayılan yap'} title={isDefault ? 'Varsayılanı kaldır' : 'Listeye girişte bu görünümle aç'}
                  onClick={() => run(() => views.setDefault(isDefault ? null : v.id))}
                  className="p-1 rounded hover:bg-[var(--surface)]" style={{ color: isDefault ? 'var(--brand)' : 'var(--text-s)' }}>
                  <Star size={14} fill={isDefault ? 'currentColor' : 'none'} />
                </button>
                {isActive && (
                  <button type="button" aria-label="Görünümü güncelle" title="Geçerli filtre/kolon durumuyla güncelle" onClick={() => run(() => views.update(v.id))}
                    className="px-1.5 py-0.5 rounded text-[11px] hover:bg-[var(--surface)]" style={{ color: 'var(--text-m)', border: '1px solid var(--border)' }}>güncelle</button>
                )}
                <button type="button" aria-label={`Görünümü sil: ${v.name}`} onClick={() => { if (window.confirm(`"${v.name}" görünümü silinsin mi?`)) run(() => views.remove(v.id)) }}
                  className="p-1 rounded hover:bg-[var(--surface)]" style={{ color: 'var(--text-s)' }}><Trash2 size={14} /></button>
              </div>
            )
          })}
          <div className="mt-1 pt-1" style={{ borderTop: '1px solid var(--border)' }}>
            {!naming ? (
              <button type="button" role="menuitem" onClick={() => { setName(''); setNaming(true) }} disabled={views.saving}
                className="w-full text-left px-2 py-1.5 rounded-lg text-sm hover:bg-[var(--surface2)] disabled:opacity-50" style={{ color: 'var(--text)' }}>
                + Geçerli durumu görünüm olarak kaydet
              </button>
            ) : (
              <form className="flex items-center gap-1 px-1 py-1" onSubmit={e => { e.preventDefault(); if (!name.trim()) return; run(async () => { await views.save(name); setNaming(false); setOpen(false) }) }}>
                <input ref={inputRef} className="inp text-sm !py-1 !px-2 !h-auto flex-1" placeholder="Görünüm adı (örn. Depo, Bekleyen)" value={name} onChange={e => setName(e.target.value)} aria-label="Görünüm adı" maxLength={60} />
                <button type="submit" disabled={!name.trim() || views.saving} className="px-2 py-1 rounded-lg text-sm font-medium disabled:opacity-50" style={{ background: 'var(--brand)', color: '#fff' }}>Kaydet</button>
              </form>
            )}
            {err && <div role="alert" className="px-2 py-1 text-xs" style={{ color: '#be123c' }}>{err}</div>}
          </div>
        </div>
      )}
    </div>
  )
}

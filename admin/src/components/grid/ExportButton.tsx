import { useEffect, useRef, useState } from 'react'
import { ChevronDown, FileSpreadsheet } from 'lucide-react'
import api from '@/api/client'
import { apiErrorMessage } from '@/lib/api-error'
import type { GridStateApi } from './useGridState'

// Excel export (plan §2.8, E4/E12/E13): aynı GridState (search/sort/dir/filters) + kolon listesi POST gövdesiyle gider;
// sayfalama gönderilmez → aktif filtreye uyan TÜM kayıtlar. "Tüm kolonlar" / "Yalnız görünür kolonlar" seçeneği.
// Sunucu: 100.000 satır tavanı (400) ve kullanıcı bazlı 5/dk (429) — mesajlar düğme altında gösterilir.

export interface GridExportConfig {
  /** POST ucu, örn. '/orders/export' */
  endpoint: string
  /** ucun adlandırılmış parametreleri (örn. statuses sekmesi) — sunucu "named" sözlüğü */
  named?: () => Record<string, string | undefined>
  /** dosya adı sunucudan (Content-Disposition) gelmezse yedek */
  fallbackFileName?: string
}

interface Props {
  grid: GridStateApi
  config: GridExportConfig
  /** o anki görünür + exportable kolon anahtarları (tanım sırasıyla) */
  visibleExportKeys: string[]
  /** tüm exportable kolon anahtarları */
  allExportKeys: string[]
}

export function ExportButton({ grid, config, visibleExportKeys, allExportKeys }: Props) {
  const [open, setOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const [msg, setMsg] = useState<string | null>(null)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const onDoc = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false) }
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpen(false) }
    document.addEventListener('mousedown', onDoc); document.addEventListener('keydown', onKey)
    return () => { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onKey) }
  }, [open])

  async function run(mode: 'all' | 'visible') {
    setOpen(false); setMsg(null); setBusy(true)
    const { state } = grid
    const named: Record<string, string> = {}
    for (const [k, v] of Object.entries(config.named?.() ?? {})) if (v) named[k] = v
    const body = {
      search: state.search || null,
      sort: state.sort, dir: state.dir,
      filters: state.filters.map(f => ({ field: f.field, op: f.op === 'auto' ? null : f.op, value: f.value })),
      columns: mode === 'visible' ? visibleExportKeys : [],   // boş → sunucunun TÜM export kolonları (E13)
      named,
    }
    try {
      const res = await api.post(config.endpoint, body, { responseType: 'blob', timeout: 180_000 })
      const cd: string = res.headers['content-disposition'] ?? ''
      const m = /filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i.exec(cd)
      const name = m ? decodeURIComponent(m[1] ?? m[2]) : (config.fallbackFileName ?? 'export.xlsx')
      const url = URL.createObjectURL(res.data as Blob)
      const a = document.createElement('a'); a.href = url; a.download = name; a.rel = 'noopener'
      document.body.appendChild(a); a.click(); a.remove()
      setTimeout(() => URL.revokeObjectURL(url), 10_000)
    } catch (e) {
      // blob yanıtındaki JSON hata gövdesini oku (400 tavan / 429 limit)
      const err = e as { response?: { data?: unknown; status?: number } }
      let text: string | null = null
      if (err.response?.data instanceof Blob) {
        try { text = (JSON.parse(await err.response.data.text()) as { error?: string }).error ?? null } catch { /* düz metin değil */ }
      }
      setMsg(text ?? apiErrorMessage(e, 'Dışa aktarma başarısız oldu.'))
    } finally { setBusy(false) }
  }

  return (
    <div className="relative" ref={ref}>
      <button type="button" onClick={() => setOpen(o => !o)} disabled={busy} aria-haspopup="menu" aria-expanded={open}
        className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm transition-colors hover:bg-[var(--surface2)] disabled:opacity-60"
        style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>
        <FileSpreadsheet size={15} /> <span className="mob-hide">{busy ? 'Hazırlanıyor…' : 'Excel\'e aktar'}</span> <ChevronDown size={13} />
      </button>
      {open && (
        <div role="menu" className="absolute right-0 mt-1 w-64 rounded-xl shadow-lg z-40 p-1" style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}>
          <button type="button" role="menuitem" onClick={() => run('visible')} className="w-full text-left px-3 py-2 rounded-lg text-sm hover:bg-[var(--surface2)]" style={{ color: 'var(--text)' }}>
            Yalnız görünür kolonlar
            <div className="text-[11px]" style={{ color: 'var(--text-s)' }}>{visibleExportKeys.length} kolon</div>
          </button>
          <button type="button" role="menuitem" onClick={() => run('all')} className="w-full text-left px-3 py-2 rounded-lg text-sm hover:bg-[var(--surface2)]" style={{ color: 'var(--text)' }}>
            Tüm kolonlar
            <div className="text-[11px]" style={{ color: 'var(--text-s)' }}>sunucudaki tüm alanlar (ekranda olmayanlar dahil{allExportKeys.length ? `, en az ${allExportKeys.length}` : ''}) · filtreye uyan tüm kayıtlar</div>
          </button>
        </div>
      )}
      {msg && (
        <div role="alert" className="absolute right-0 mt-1 w-72 rounded-lg px-3 py-2 text-xs z-40" style={{ background: 'var(--surface)', border: '1px solid #fecdd3', color: '#be123c' }}>
          {msg} <button type="button" className="underline ml-1" onClick={() => setMsg(null)}>kapat</button>
        </div>
      )}
    </div>
  )
}

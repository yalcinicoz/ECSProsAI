import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { RefreshCw } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { StoreLogo, timeAgo } from './MarketplacesPage'

// ── Types ─────────────────────────────────────────────────────────────────────

interface RefSyncRun {
  id: string
  marketplace: string
  scope: string
  status: 'running' | 'completed' | 'failed'
  startedAt: string
  finishedAt: string | null
  totalCategories: number | null
  processedCategories: number
  addedCount: number
  changedCount: number
  removedCount: number
  unchangedCount: number
  error: string | null
}

interface RefSummary {
  marketplace: string
  categoryCount: number
  attributeCount: number
  valueCount: number
  removedCategoryCount: number
  lastRun: RefSyncRun | null
}

const SCOPE_LABEL: Record<string, string> = {
  categories: 'Kategoriler',
  attributes: 'Özellikler + Değerler',
}

const MP_NAME: Record<string, string> = {
  trendyol: 'Trendyol',
  hepsiburada: 'Hepsiburada',
  n11: 'n11',
  amazon: 'Amazon',
  ciceksepeti: 'Çiçeksepeti',
  pazarama: 'Pazarama',
}

function RunStatusBadge({ run }: { run: RefSyncRun }) {
  if (run.status === 'running') {
    const progress =
      run.totalCategories != null && run.totalCategories > 0
        ? ` ${run.processedCategories}/${run.totalCategories}`
        : ''
    return (
      <Badge variant="warning">
        <RefreshCw size={10} className="animate-spin inline mr-1" />
        Sürüyor{progress}
      </Badge>
    )
  }
  if (run.status === 'failed') return <Badge variant="danger">Başarısız</Badge>
  return <Badge variant="success">Tamamlandı</Badge>
}

/**
 * Pazaryeri referans verisi (kategori/özellik/değer) senkron modalı:
 * pazaryeri başına özet sayılar + koşu başlatma + son koşuların canlı izlenmesi.
 * Referans verisi marketplace_ref ayrı DB'sinde tutulur (yeniden indirilebilir cache).
 */
export function ReferenceSyncModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [marketplace, setMarketplace] = useState('trendyol')
  const [scope, setScope] = useState('categories')
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)

  const { data: summaryResp } = useQuery<{ rows: RefSummary[]; configured: boolean }>({
    queryKey: ['mp-ref-summary'],
    queryFn: async () => {
      const { data } = await api.get('/marketplaces/reference-sync/summary')
      return { rows: data.data ?? [], configured: data.configured !== false }
    },
    enabled: open,
    refetchInterval: (q) =>
      q.state.data?.rows.some((r) => r.lastRun?.status === 'running') ? 4000 : false,
  })

  const { data: runs = [] } = useQuery<RefSyncRun[]>({
    queryKey: ['mp-ref-runs'],
    queryFn: async () => {
      const { data } = await api.get('/marketplaces/reference-sync/runs?limit=10')
      return data.data ?? []
    },
    enabled: open,
    refetchInterval: (q) => (q.state.data?.some((r) => r.status === 'running') ? 4000 : false),
  })

  const start = useMutation({
    mutationFn: async () => {
      const { data } = await api.post('/marketplaces/reference-sync', { marketplace, scope })
      return data.data
    },
    onSuccess: () => {
      setMessage({ ok: true, text: 'Senkron başlatıldı — ilerleme aşağıdaki listede.' })
      queryClient.invalidateQueries({ queryKey: ['mp-ref-runs'] })
      queryClient.invalidateQueries({ queryKey: ['mp-ref-summary'] })
    },
    onError: (err: unknown) => {
      const e = err as { response?: { data?: { error?: string } } }
      setMessage({ ok: false, text: e.response?.data?.error ?? 'Senkron başlatılamadı.' })
    },
  })

  const summary = summaryResp?.rows ?? []
  const supported = summary.map((s) => s.marketplace)

  return (
    <Modal open={open} onClose={onClose} title="Pazaryeri Referans Verisi" size="lg" footer={null}>
      <p className="text-xs mb-4" style={{ color: 'var(--text-s)' }}>
        Pazaryerlerinin kategori / özellik / değer ağaçları ayrı referans veritabanında saklanır ve
        buradan güncellenir. Değişiklikler (silinen kategori, zorunlu olan özellik…) kayıt altına
        alınır; eşleme sağlığını besler. Özellik senkronu kategori sayısına göre uzun sürebilir —
        arka planda çalışır, bu pencereyi kapatabilirsiniz.
      </p>

      {summaryResp?.configured === false ? (
        <div
          className="rounded-lg px-4 py-3 mb-4 text-sm"
          style={{ background: '#fef2f2', color: '#b91c1c', border: '1px solid #fecaca' }}
        >
          Referans veritabanı yapılandırılmamış (ConnectionStrings:MarketplaceRef). Sunucu
          ayarlarını kontrol edin.
        </div>
      ) : (
        <div className="mb-4">
          {/* Özet: pazaryeri başına sayılar */}
          <div className="rounded-lg overflow-hidden mb-4" style={{ border: '1px solid var(--border)' }}>
            <table className="w-full text-sm">
              <thead>
                <tr style={{ background: 'var(--surface2)' }}>
                  {['Pazaryeri', 'Kategori', 'Özellik', 'Değer', 'Son Senkron'].map((h, i) => (
                    <th
                      key={h}
                      className={`px-3 py-2 text-xs font-semibold ${i === 0 ? 'text-left' : i === 4 ? 'text-left' : 'text-right'}`}
                      style={{ color: 'var(--text-s)' }}
                    >
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {summary.map((s) => (
                  <tr key={s.marketplace} style={{ borderTop: '1px solid var(--border)' }}>
                    <td className="px-3 py-2">
                      <div className="flex items-center gap-2">
                        <StoreLogo code={s.marketplace} size={22} />
                        <span className="font-medium" style={{ color: 'var(--text)' }}>
                          {MP_NAME[s.marketplace] ?? s.marketplace}
                        </span>
                      </div>
                    </td>
                    <td className="px-3 py-2 text-right tabular-nums" style={{ color: 'var(--text)' }}>
                      {s.categoryCount.toLocaleString('tr-TR')}
                      {s.removedCategoryCount > 0 && (
                        <span className="text-[11px] ml-1" style={{ color: 'var(--text-s)' }}>
                          (+{s.removedCategoryCount} kaldırılmış)
                        </span>
                      )}
                    </td>
                    <td className="px-3 py-2 text-right tabular-nums" style={{ color: 'var(--text)' }}>
                      {s.attributeCount.toLocaleString('tr-TR')}
                    </td>
                    <td className="px-3 py-2 text-right tabular-nums" style={{ color: 'var(--text)' }}>
                      {s.valueCount.toLocaleString('tr-TR')}
                    </td>
                    <td className="px-3 py-2">
                      {s.lastRun ? (
                        <div className="flex items-center gap-2">
                          <RunStatusBadge run={s.lastRun} />
                          <span className="text-xs" style={{ color: 'var(--text-s)' }}>
                            {SCOPE_LABEL[s.lastRun.scope] ?? s.lastRun.scope} ·{' '}
                            {timeAgo(s.lastRun.startedAt)}
                          </span>
                        </div>
                      ) : (
                        <span className="text-xs" style={{ color: 'var(--text-s)' }}>
                          Henüz senkron yapılmadı
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Başlatma kontrolleri */}
          <div className="flex items-end gap-2 flex-wrap">
            <div>
              <label className="text-xs font-semibold block mb-1" style={{ color: 'var(--text-s)' }}>
                Pazaryeri
              </label>
              <select className="inp" value={marketplace} onChange={(e) => setMarketplace(e.target.value)}>
                {(supported.length > 0 ? supported : ['trendyol']).map((m) => (
                  <option key={m} value={m}>
                    {MP_NAME[m] ?? m}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="text-xs font-semibold block mb-1" style={{ color: 'var(--text-s)' }}>
                Kapsam
              </label>
              <select className="inp" value={scope} onChange={(e) => setScope(e.target.value)}>
                <option value="categories">Kategoriler</option>
                <option value="attributes">Özellikler + Değerler (tüm yaprak kategoriler)</option>
              </select>
            </div>
            <Button size="sm" onClick={() => start.mutate()} disabled={start.isPending}>
              <RefreshCw size={13} className={start.isPending ? 'animate-spin' : ''} /> Senkronu
              Başlat
            </Button>
            <div className="text-xs min-h-[18px]" style={{ color: message ? (message.ok ? 'var(--brand)' : '#ef4444') : 'var(--text-s)' }}>
              {message?.text ?? ''}
            </div>
          </div>
        </div>
      )}

      {/* Son koşular */}
      <p className="text-xs font-semibold mb-1.5" style={{ color: 'var(--text-s)' }}>
        SON KOŞULAR
      </p>
      <div className="rounded-lg overflow-hidden" style={{ border: '1px solid var(--border)' }}>
        {runs.length === 0 ? (
          <p className="text-xs px-3 py-3" style={{ color: 'var(--text-s)' }}>
            Henüz koşu yok.
          </p>
        ) : (
          <table className="w-full text-sm">
            <tbody>
              {runs.map((r) => (
                <tr key={r.id} style={{ borderTop: '1px solid var(--border)' }}>
                  <td className="px-3 py-2">
                    <div className="flex items-center gap-2">
                      <StoreLogo code={r.marketplace} size={20} />
                      <span className="text-xs font-medium" style={{ color: 'var(--text)' }}>
                        {SCOPE_LABEL[r.scope] ?? r.scope}
                      </span>
                    </div>
                  </td>
                  <td className="px-3 py-2">
                    <RunStatusBadge run={r} />
                  </td>
                  <td className="px-3 py-2 text-xs tabular-nums" style={{ color: 'var(--text-m)' }}>
                    +{r.addedCount.toLocaleString('tr-TR')} yeni · ~
                    {r.changedCount.toLocaleString('tr-TR')} değişen · −
                    {r.removedCount.toLocaleString('tr-TR')} kaldırılan
                  </td>
                  <td className="px-3 py-2 text-xs text-right" style={{ color: 'var(--text-s)' }}>
                    {timeAgo(r.startedAt)}
                    {r.error ? (
                      <span className="block max-w-[260px] truncate" title={r.error} style={{ color: '#ef4444' }}>
                        {r.error}
                      </span>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </Modal>
  )
}

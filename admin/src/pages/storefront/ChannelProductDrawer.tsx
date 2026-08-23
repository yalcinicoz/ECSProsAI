/**
 * F3 sağ çekmece (docs/satis-kanali-ortak-kurgu.md §4.1 madde 3.5): satır tıklanınca ürünün bu kanaldaki
 * tam resmi — kanal kararı aksiyonları, listeleme durumu + sebepler (her biri "Düzelt" hedefli),
 * pazaryeri varyant satırları (ham hata dahil) ve push aksiyonları (Gönder / Yeniden dene / Hazırlığı
 * yeniden hesapla). "Listeden düşür" backend'i (deactivate batch) henüz yok — buton konmadı (plan notu).
 */
import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { X, ExternalLink, RefreshCw, UploadCloud } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'

export interface DrawerProduct {
  productId: string
  code: string
  name: string
  mainImageUrl: string | null
  isSelected: boolean
  isStoppedNow: boolean
  saleStoppedUntil: string | null
}

interface ReasonDto { code: string; label: string }
interface VariantDto { variantId: string; sku: string | null; externalId: string | null; syncStatus: string; lastErrorCode: string | null; lastSyncError: string | null; lastSyncedAt: string | null }
interface DetailDto { status: string; reasons: ReasonDto[]; isPushChannel: boolean; marketplaceCode: string | null; variants: VariantDto[] }

const STATUS_META: Record<string, { label: string; variant: 'success' | 'info' | 'warning' | 'danger' | 'neutral' }> = {
  published: { label: 'Yayında', variant: 'success' },
  ready: { label: 'Hazır', variant: 'info' },
  pending: { label: 'Bekliyor', variant: 'info' },
  missing_info: { label: 'Eksik bilgi', variant: 'warning' },
  blocked: { label: 'Engelli', variant: 'neutral' },
  failed: { label: 'Hatalı', variant: 'danger' },
  deactivated: { label: 'Düşürüldü', variant: 'neutral' },
}
const SYNC_META: Record<string, { label: string; variant: 'success' | 'info' | 'danger' | 'neutral' }> = {
  synced: { label: 'Yüklü', variant: 'success' },
  pending: { label: 'Bekliyor', variant: 'info' },
  failed: { label: 'Hatalı', variant: 'danger' },
  deactivated: { label: 'Pasif', variant: 'neutral' },
}

/** Sebep → Düzelt hedefi (plan §4.3). null → link yok (aksiyon çekmecedeki butonlarda). */
function fixTarget(code: string, productCode: string): { to: string; label: string } | null {
  if (['price_zero', 'no_channel_price', 'sale_closed'].includes(code))
    return { to: `/catalog/products/${productCode}`, label: 'Ürün detayı' }
  if (code === 'out_of_stock') return { to: '/inventory/stocks', label: 'Stok sayfası' }
  if (['no_category_mapping', 'pool_assignment_pending', 'rule_no_match', 'broken_mapping', 'value_unmapped', 'attrs_not_synced', 'required_attr_missing'].includes(code))
    return { to: '/marketplaces/eslestirme', label: 'Eşleme sayfası' }
  return null
}

export function ChannelProductDrawer({ channelId, product, onClose, onChannelAction, busy }: {
  channelId: string
  product: DrawerProduct
  onClose: () => void
  /** Kanal kararı aksiyonları üst sayfada (mutasyonlar + cache invalidation orada). */
  onChannelAction: (action: 'select' | 'unselect' | 'stop' | 'start', productId: string) => void
  busy: boolean
}) {
  const qc = useQueryClient()
  const [msg, setMsg] = useState<string | null>(null)

  const { data: detail, isLoading } = useQuery<DetailDto>({
    queryKey: ['listing-detail', channelId, product.productId],
    queryFn: async () => (await api.get(`/navigation/channel-products/${channelId}/listing-detail/${product.productId}`)).data.data,
  })

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['listing-detail', channelId, product.productId] })
    qc.invalidateQueries({ queryKey: ['listing-status', channelId] })
    qc.invalidateQueries({ queryKey: ['listing-summary', channelId] })
  }

  const pushMut = useMutation({
    mutationFn: async () => (await api.post(`/marketplaces/${channelId}/sync-products`, { productIds: [product.productId] })).data.data,
    onSuccess: (d: any) => { setMsg(d?.mode === 'batch' ? `Gönderildi: ${d.submitted} varyant (hazır olmayan ${d.skippedNotReady ?? 0}, değişmeyen ${d.skippedUnchanged ?? 0} atlandı). Sonuç asenkron — birkaç dakika içinde durum güncellenir.` : 'Gönderildi.'); invalidate() },
    onError: (e: any) => setMsg(e?.response?.data?.error ?? 'Gönderilemedi.'),
  })
  const recomputeMut = useMutation({
    mutationFn: async () => (await api.post(`/marketplaces/mapping/readiness/recompute?marketplace=${detail?.marketplaceCode}`, { productIds: [product.productId] })).data.data,
    onSuccess: () => { setMsg('Hazırlık yeniden hesaplandı.'); invalidate() },
    onError: (e: any) => setMsg(e?.response?.data?.error ?? 'Hesaplanamadı.'),
  })

  const sm = detail ? (STATUS_META[detail.status] ?? { label: detail.status, variant: 'neutral' as const }) : null
  const failedVariants = detail?.variants.filter(v => v.syncStatus === 'failed') ?? []

  return (
    <>
      <div className="fixed inset-0 z-40" style={{ background: 'rgba(0,0,0,.25)' }} onClick={onClose} />
      <aside className="fixed right-0 top-0 bottom-0 z-50 w-full max-w-lg overflow-y-auto p-5 space-y-4"
        style={{ background: 'var(--surface)', borderLeft: '1px solid var(--border)', boxShadow: '-8px 0 24px rgba(0,0,0,.12)' }}>
        {/* Başlık */}
        <div className="flex items-start gap-3">
          {product.mainImageUrl
            ? <img src={product.mainImageUrl} alt="" className="w-14 h-14 rounded object-cover shrink-0" style={{ background: 'var(--surface2)' }} />
            : <div className="w-14 h-14 rounded shrink-0" style={{ background: 'var(--surface2)' }} />}
          <div className="min-w-0 flex-1">
            <p className="text-sm font-semibold truncate" style={{ color: 'var(--text)' }}>{product.name}</p>
            <p className="text-xs" style={{ color: 'var(--text-s)' }}>{product.code}</p>
            <Link to={`/catalog/products/${product.code}`} className="text-xs inline-flex items-center gap-1 mt-1 underline" style={{ color: 'var(--brand)' }}>
              Ürün detayına git <ExternalLink size={11} />
            </Link>
          </div>
          <button onClick={onClose} className="p-1 rounded hover:opacity-70"><X size={18} style={{ color: 'var(--text-s)' }} /></button>
        </div>

        {/* Kanal kararı */}
        <div className="rounded-xl p-3 space-y-2" style={{ border: '1px solid var(--border)' }}>
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>Kanal Kararı</span>
            {!product.isSelected
              ? <Badge variant="neutral">Kanaldan çıkarıldı</Badge>
              : product.isStoppedNow
                ? <Badge variant="warning">Durduruldu{product.saleStoppedUntil ? ` — ${new Date(product.saleStoppedUntil).toLocaleDateString('tr-TR')} kadar` : ''}</Badge>
                : <Badge variant="success">Kanalda</Badge>}
          </div>
          <div className="flex flex-wrap gap-2">
            {product.isSelected
              ? <Button size="sm" variant="secondary" disabled={busy} onClick={() => onChannelAction('unselect', product.productId)}>Kanaldan Çıkar</Button>
              : <Button size="sm" variant="secondary" disabled={busy} onClick={() => onChannelAction('select', product.productId)}>Kanala Al</Button>}
            {product.isStoppedNow
              ? <Button size="sm" variant="secondary" disabled={busy} onClick={() => onChannelAction('start', product.productId)}>Satışı Başlat</Button>
              : <Button size="sm" variant="secondary" disabled={busy} onClick={() => onChannelAction('stop', product.productId)}>Satışı Durdur…</Button>}
          </div>
        </div>

        {/* Listeleme durumu + sebepler */}
        <div className="rounded-xl p-3 space-y-2" style={{ border: '1px solid var(--border)' }}>
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>Listeleme Durumu</span>
            {isLoading ? <span className="text-xs" style={{ color: 'var(--text-s)' }}>…</span>
              : sm && <Badge variant={sm.variant}>{sm.label}</Badge>}
          </div>
          {detail && detail.reasons.length === 0 && (
            <p className="text-xs" style={{ color: 'var(--text-s)' }}>Sebep yok — ürün bu kanalda sorunsuz.</p>
          )}
          {detail && detail.reasons.length > 0 && (
            <ul className="space-y-1.5">
              {detail.reasons.map(r => {
                const fix = fixTarget(r.code, product.code)
                return (
                  <li key={r.code} className="flex items-center justify-between gap-2 text-sm">
                    <span style={{ color: 'var(--text)' }}>• {r.label}</span>
                    {fix
                      ? <Link to={fix.to} className="text-xs underline shrink-0" style={{ color: 'var(--brand)' }}>Düzelt → {fix.label}</Link>
                      : r.code === 'readiness_unknown' && detail.marketplaceCode
                        ? <button className="text-xs underline shrink-0" style={{ color: 'var(--brand)' }} disabled={recomputeMut.isPending}
                            onClick={() => recomputeMut.mutate()}>Hesapla</button>
                        : null}
                  </li>
                )
              })}
            </ul>
          )}
        </div>

        {/* Pazaryeri (push) bölümü */}
        {detail?.isPushChannel && (
          <div className="rounded-xl p-3 space-y-2" style={{ border: '1px solid var(--border)' }}>
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>
                Pazaryeri Yüklemesi {detail.marketplaceCode ? `(${detail.marketplaceCode})` : ''}
              </span>
              <div className="flex gap-2">
                <Button size="sm" variant="secondary" loading={recomputeMut.isPending} onClick={() => recomputeMut.mutate()}>
                  <RefreshCw size={13} /> Hazırlığı Hesapla
                </Button>
                <Button size="sm" loading={pushMut.isPending} onClick={() => pushMut.mutate()}>
                  <UploadCloud size={13} /> {failedVariants.length > 0 ? 'Yeniden Dene' : 'Gönder'}
                </Button>
              </div>
            </div>
            {detail.variants.length === 0 ? (
              <p className="text-xs" style={{ color: 'var(--text-s)' }}>Henüz hiç varyant yüklenmemiş.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-xs">
                  <thead>
                    <tr style={{ color: 'var(--text-s)' }}>
                      <th className="text-left py-1 pr-2">SKU</th>
                      <th className="text-left py-1 pr-2">Durum</th>
                      <th className="text-left py-1 pr-2">Dış Id</th>
                      <th className="text-left py-1">Son Senkron</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.variants.map(v => {
                      const m = SYNC_META[v.syncStatus] ?? { label: v.syncStatus, variant: 'neutral' as const }
                      return (
                        <tr key={v.variantId} style={{ borderTop: '1px solid var(--border)' }}>
                          <td className="py-1 pr-2 font-mono">{v.sku ?? '—'}</td>
                          <td className="py-1 pr-2"><Badge variant={m.variant}>{m.label}</Badge></td>
                          <td className="py-1 pr-2 font-mono truncate max-w-[120px]" title={v.externalId ?? ''}>{v.externalId ?? '—'}</td>
                          <td className="py-1">{v.lastSyncedAt ? new Date(v.lastSyncedAt).toLocaleString('tr-TR') : '—'}</td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            )}
            {failedVariants.length > 0 && (
              <div className="rounded-lg p-2 space-y-1" style={{ background: 'var(--surface2)' }}>
                <p className="text-xs font-semibold" style={{ color: '#b91c1c' }}>Ham hata ({failedVariants.length} varyant)</p>
                {failedVariants.slice(0, 3).map(v => (
                  <p key={v.variantId} className="text-xs break-words" style={{ color: 'var(--text-m)' }}>
                    <code>{v.sku}</code>{v.lastErrorCode ? ` [${v.lastErrorCode}]` : ''}: {v.lastSyncError ?? '—'}
                  </p>
                ))}
                {failedVariants.length > 3 && <p className="text-xs" style={{ color: 'var(--text-s)' }}>… +{failedVariants.length - 3} varyant daha</p>}
              </div>
            )}
          </div>
        )}

        {msg && <p className="text-xs" style={{ color: 'var(--text-s)' }}>{msg}</p>}
      </aside>
    </>
  )
}

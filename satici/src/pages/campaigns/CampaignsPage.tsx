import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { pickName, formatDate } from '@/lib/i18n'
import { Button } from '@/components/ui/Button'

/* Kampanyalar (2026-08-11): bana açık (opt-in) kampanyalar — komisyon oranı ve indirim
 * yükü paylaşımı AÇIKÇA görünür; katılım tüm ürünlerle veya seçili ürünlerle yapılır.
 * Partner API POST/DELETE /campaigns/{id}/join ile aynı komutlar. */

interface Campaign {
  campaignId: string
  code: string
  nameI18n: Record<string, string>
  startsAt: string
  endsAt: string | null
  supplierCommissionRate: number | null
  supplierDiscountSharePercent: number
  joined: boolean
  joinedProductIds: string[]
}
interface ProductRow { supplierProductCode: string; productId: string | null; name: Record<string, string>; status: string }

export function CampaignsPage() {
  const queryClient = useQueryClient()
  const [secilen, setSecilen] = useState<Campaign | null>(null)
  const [urunSecimi, setUrunSecimi] = useState<Set<string>>(new Set())
  const [hata, setHata] = useState('')

  const { data: campaigns = [], isLoading } = useQuery<Campaign[]>({
    queryKey: ['supplier-campaigns'],
    queryFn: async () => (await api.get('/supplier/campaigns')).data.data ?? [],
  })
  const { data: urunler = [] } = useQuery<ProductRow[]>({
    queryKey: ['supplier-live-products'],
    queryFn: async () => (await api.get('/supplier/products', { params: { status: 'live', pageSize: 100 } })).data.data?.items ?? [],
    enabled: !!secilen,
  })

  const katil = useMutation({
    mutationFn: async () => {
      const productIds = [...urunSecimi]
      await api.post(`/supplier/campaigns/${secilen!.campaignId}/join`, { productIds })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['supplier-campaigns'] })
      setSecilen(null)
    },
    onError: (e: unknown) => setHata((e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Katılım kaydedilemedi.'),
  })
  const ayril = useMutation({
    mutationFn: async (id: string) => { await api.delete(`/supplier/campaigns/${id}/join`) },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['supplier-campaigns'] }),
  })

  const modalAc = (c: Campaign) => {
    setHata('')
    setUrunSecimi(new Set(c.joinedProductIds))
    setSecilen(c)
  }

  return (
    <div>
      <h1 className="text-lg font-bold mb-1">Kampanyalar</h1>
      <p className="text-xs opacity-70 mb-4">
        Katılım tercihinize bağlı kampanyalar. Kampanya komisyon oranı ve indirim yükünün size düşen payı
        tanımda görünür — katılım bilinçli yapılır; dilediğinizde ayrılabilirsiniz.
      </p>

      {isLoading && <div className="py-8 text-center opacity-60">Yükleniyor…</div>}
      {!isLoading && campaigns.length === 0 && (
        <div className="card p-8 text-center opacity-60">Şu anda size açık kampanya yok.</div>
      )}

      <div className="grid gap-4 sm:grid-cols-2">
        {campaigns.map(c => (
          <div key={c.campaignId} className="card p-5">
            <div className="flex items-start justify-between gap-3">
              <div>
                <div className="font-semibold">{pickName(c.nameI18n) || c.code}</div>
                <div className="text-xs opacity-70 mt-0.5">
                  {formatDate(c.startsAt)} — {c.endsAt ? formatDate(c.endsAt) : 'süresiz'}
                </div>
              </div>
              {c.joined
                ? <span className="badge bg">Katıldınız</span>
                : <span className="badge bx">Katılmadınız</span>}
            </div>
            <div className="text-sm mt-3 grid gap-1">
              <div>Kampanya komisyonu: <strong>{c.supplierCommissionRate == null ? 'sözleşme oranınız geçerli' : `%${c.supplierCommissionRate}`}</strong></div>
              <div>İndirim yükü payınız: <strong>%{c.supplierDiscountSharePercent}</strong></div>
              {c.joined && (
                <div className="text-xs opacity-70">
                  Katılım: {c.joinedProductIds.length === 0 ? 'tüm ürünler' : `${c.joinedProductIds.length} seçili ürün`}
                </div>
              )}
            </div>
            <div className="flex gap-2 mt-4">
              <Button size="sm" onClick={() => modalAc(c)}>{c.joined ? 'Katılımı Düzenle' : 'Katıl'}</Button>
              {c.joined && (
                <Button size="sm" variant="danger" onClick={() => ayril.mutate(c.campaignId)} disabled={ayril.isPending}>
                  Ayrıl
                </Button>
              )}
            </div>
          </div>
        ))}
      </div>

      {secilen && (
        <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={() => setSecilen(null)}>
          <div className="card p-5 w-full max-w-lg max-h-[80vh] overflow-y-auto" style={{ background: 'var(--surface, #fff)' }}
            onClick={e => e.stopPropagation()}>
            <div className="font-semibold mb-1">{pickName(secilen.nameI18n) || secilen.code} — Katılım</div>
            <p className="text-xs opacity-70 mb-3">
              Hiç ürün seçmezseniz kampanya kapsamına giren TÜM ürünlerinizle katılırsınız.
            </p>
            <div className="grid gap-1 mb-4">
              {urunler.filter(u => u.productId).map(u => (
                <label key={u.productId} className="flex items-center gap-2 text-sm cursor-pointer py-0.5">
                  <input type="checkbox" checked={urunSecimi.has(u.productId!)}
                    onChange={e => setUrunSecimi(prev => {
                      const next = new Set(prev)
                      if (e.target.checked) next.add(u.productId!); else next.delete(u.productId!)
                      return next
                    })} />
                  <span className="truncate">{pickName(u.name)} <code className="text-xs opacity-60">{u.supplierProductCode}</code></span>
                </label>
              ))}
              {urunler.filter(u => u.productId).length === 0 && (
                <div className="text-xs opacity-60">Canlı ürününüz yok — katılım tüm (gelecek) ürünler için geçerli olur.</div>
              )}
            </div>
            {hata && <div className="text-xs text-red-600 mb-2">{hata}</div>}
            <div className="flex gap-2 justify-end">
              <Button variant="secondary" onClick={() => setSecilen(null)}>Vazgeç</Button>
              <Button onClick={() => katil.mutate()} disabled={katil.isPending}>
                {katil.isPending ? 'Kaydediliyor…' : (urunSecimi.size === 0 ? 'Tüm Ürünlerle Katıl' : `${urunSecimi.size} Ürünle Katıl`)}
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

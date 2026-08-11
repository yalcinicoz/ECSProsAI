import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import api from '@/api/client'
import { pickName, formatDate } from '@/lib/i18n'

interface VariantAxis { typeName: Record<string, string>; valueName: Record<string, string> }
interface Variant {
  sku: string
  barcode: string | null
  basePrice: number
  isActive: boolean
  axes: VariantAxis[]
}
interface LiveProduct {
  code: string
  name: Record<string, string>
  groupCode: string
  groupName: Record<string, string>
  basePrice: number
  taxRate: number
  isSaleOpen: boolean
  createdAt: string
  updatedAt: string | null
  variants: Variant[]
}
interface Submission {
  id: string
  status: 'pending' | 'approved' | 'rejected'
  variantCount: number
  reviewNote: string | null
  source: 'api' | 'panel'
  submittedAt: string
  reviewedAt: string | null
}
interface Detail {
  supplierProductCode: string
  product: LiveProduct | null
  submissions: Submission[]
}

const SUB_STATUS: Record<Submission['status'], { label: string; cls: string }> = {
  pending: { label: 'Onay Bekliyor', cls: 'ba' },
  approved: { label: 'Onaylandı', cls: 'bg' },
  rejected: { label: 'Reddedildi', cls: 'br' },
}

export function ProductDetailPage() {
  const { code } = useParams<{ code: string }>()
  const navigate = useNavigate()

  const { data, isLoading, error } = useQuery({
    queryKey: ['supplier-product', code],
    queryFn: async () => {
      const { data } = await api.get(`/supplier/products/${encodeURIComponent(code!)}`)
      return data.data as Detail
    },
    enabled: !!code,
  })

  const p = data?.product
  const lastRejected = data?.submissions.find((s) => s.status === 'rejected')

  return (
    <>
      <div className="vh">
        <div className="flex items-center gap-3">
          <button
            className="p-2 -ml-2 rounded-lg hover:bg-[var(--surface2)]"
            style={{ color: 'var(--text-m)' }}
            onClick={() => navigate('/products')}
            aria-label="Geri"
          >
            <ArrowLeft size={18} />
          </button>
          <div>
            <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>
              {p ? pickName(p.name) : (data?.supplierProductCode ?? '…')}
            </h1>
            <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{data?.supplierProductCode}</p>
          </div>
        </div>
      </div>

      <div className="vc space-y-5">
        {isLoading && <div style={{ color: 'var(--text-s)' }}>Yükleniyor…</div>}
        {!!error && (
          <div className="text-sm px-4 py-3 rounded-xl bg-red-50 text-red-600 border border-red-100">
            Ürün bulunamadı ya da yüklenemedi.
          </div>
        )}

        {/* Reddedilme uyarısı — satıcının ilk görmesi gereken şey */}
        {data && !p && lastRejected?.reviewNote && (
          <div className="text-sm px-4 py-3 rounded-xl bg-red-50 text-red-700 border border-red-100">
            <strong>Red nedeni:</strong> {lastRejected.reviewNote}
          </div>
        )}

        {/* Canlı ürün bilgileri */}
        {p && (
          <div className="card p-5">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-sm font-bold uppercase tracking-wide" style={{ color: 'var(--text-s)' }}>Canlı Ürün</h2>
              <div className="flex items-center gap-2">
                <button className="px-3 py-1.5 rounded-lg border text-xs font-semibold" style={{ borderColor: 'var(--border)' }}
                  onClick={() => navigate(`/products/${encodeURIComponent(code!)}/edit`)}>İçeriği Düzenle (onaylı)</button>
                <span className={`badge ${p.isSaleOpen ? 'bg' : 'bx'}`}>{p.isSaleOpen ? 'Satışa Açık' : 'Satışa Kapalı'}</span>
              </div>
            </div>
            <div className="grid gap-x-8 gap-y-2 sm:grid-cols-2 lg:grid-cols-3 text-sm" style={{ color: 'var(--text-m)' }}>
              <div>Katalog Kodu: <span className="font-medium" style={{ color: 'var(--text)' }}>{p.code}</span></div>
              <div>Grup: {pickName(p.groupName)}</div>
              <div>KDV: %{p.taxRate}</div>
              <div>Temel Fiyat: {p.basePrice.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺</div>
              <div>Oluşturma: {formatDate(p.createdAt)}</div>
              <div>Güncelleme: {formatDate(p.updatedAt)}</div>
            </div>
          </div>
        )}

        {/* Varyantlar */}
        {p && (
          <div className="card tbl-wrap">
            <div className="px-5 pt-4 pb-2 text-sm font-bold uppercase tracking-wide" style={{ color: 'var(--text-s)' }}>
              Varyantlar ({p.variants.length})
            </div>
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs uppercase tracking-wide" style={{ color: 'var(--text-s)' }}>
                  <th className="px-5 py-2 font-semibold">SKU</th>
                  <th className="px-5 py-2 font-semibold">Özellikler</th>
                  <th className="px-5 py-2 font-semibold">Barkod</th>
                  <th className="px-5 py-2 font-semibold text-right">Fiyat</th>
                  <th className="px-5 py-2 font-semibold">Durum</th>
                </tr>
              </thead>
              <tbody>
                {p.variants.map((v) => (
                  <tr key={v.sku} className="border-t" style={{ borderColor: 'var(--border)' }}>
                    <td className="px-5 py-2.5 font-medium" style={{ color: 'var(--text)' }}>{v.sku}</td>
                    <td className="px-5 py-2.5" style={{ color: 'var(--text-m)' }}>
                      {v.axes.map((a) => `${pickName(a.typeName)}: ${pickName(a.valueName)}`).join(' · ') || '—'}
                    </td>
                    <td className="px-5 py-2.5" style={{ color: 'var(--text-m)' }}>{v.barcode ?? '—'}</td>
                    <td className="px-5 py-2.5 text-right" style={{ color: 'var(--text-m)' }}>
                      {v.basePrice.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺
                    </td>
                    <td className="px-5 py-2.5">
                      <span className={`badge ${v.isActive ? 'bg' : 'bx'}`}>{v.isActive ? 'Aktif' : 'Pasif'}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Fiyat & stok hızlı güncelleme — onay KAPISIZ (API PUT prices/stock ile aynı komutlar) */}
        {p && <FiyatStokKarti code={code!} variants={p.variants} />}

        {/* Gönderim geçmişi */}
        {data && data.submissions.length > 0 && (
          <div className="card p-5">
            <h2 className="text-sm font-bold uppercase tracking-wide mb-3" style={{ color: 'var(--text-s)' }}>
              Gönderim Geçmişi
            </h2>
            <div className="space-y-3">
              {data.submissions.map((s) => (
                <div key={s.id} className="card2 p-3.5 text-sm">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className={`badge ${SUB_STATUS[s.status].cls}`}>{SUB_STATUS[s.status].label}</span>
                    <span style={{ color: 'var(--text-m)' }}>{s.variantCount} varyant</span>
                    <span className="badge bx">{s.source === 'api' ? 'API' : 'Panel'}</span>
                    <span className="ml-auto" style={{ color: 'var(--text-s)' }}>
                      {formatDate(s.submittedAt)}
                      {s.reviewedAt && ` → ${formatDate(s.reviewedAt)}`}
                    </span>
                  </div>
                  {s.reviewNote && (
                    <div
                      className={`mt-2 px-3 py-2 rounded-lg text-[13px] ${s.status === 'rejected'
                        ? 'bg-red-50 text-red-700 border border-red-100'
                        : 'bg-[var(--surface2)]'}`}
                      style={s.status !== 'rejected' ? { color: 'var(--text-m)' } : undefined}
                    >
                      <strong>İnceleme notu:</strong> {s.reviewNote}
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </>
  )
}

/* Fiyat & stok hızlı güncelleme (2026-08-11): onay beklemeden uygulanır — partner API
 * PUT products/{code}/prices ve /stock ile AYNI komutlar (panel/API çelişmezliği). */
function FiyatStokKarti({ code, variants }: { code: string; variants: Variant[] }) {
  const queryClient = useQueryClient()
  const [fiyatlar, setFiyatlar] = useState<Record<string, string>>({})
  const [stoklar, setStoklar] = useState<Record<string, string>>({})
  const [mesaj, setMesaj] = useState('')
  const [hata, setHata] = useState('')

  const kaydet = useMutation({
    mutationFn: async () => {
      const fiyatKalemleri = Object.entries(fiyatlar)
        .filter(([, v]) => v.trim() !== '' && !isNaN(parseFloat(v)))
        .map(([sku, v]) => ({ sku, price: parseFloat(v) }))
      const stokKalemleri = Object.entries(stoklar)
        .filter(([, v]) => v.trim() !== '' && !isNaN(parseInt(v)))
        .map(([sku, v]) => ({ sku, quantity: parseInt(v) }))
      if (fiyatKalemleri.length === 0 && stokKalemleri.length === 0)
        throw new Error('Değişiklik girilmedi.')
      if (fiyatKalemleri.length > 0)
        await api.put(`/supplier/products/${encodeURIComponent(code)}/prices`, { items: fiyatKalemleri })
      if (stokKalemleri.length > 0)
        await api.put(`/supplier/products/${encodeURIComponent(code)}/stock`, { items: stokKalemleri })
      return { fiyat: fiyatKalemleri.length, stok: stokKalemleri.length }
    },
    onSuccess: (r) => {
      setMesaj(`Güncellendi: ${r.fiyat} fiyat, ${r.stok} stok kalemi.`)
      setHata('')
      setFiyatlar({}); setStoklar({})
      queryClient.invalidateQueries({ queryKey: ['supplier-product', code] })
    },
    onError: (e: unknown) => {
      setMesaj('')
      const d = (e as { response?: { data?: { error?: string; errors?: { message: string }[] } }; message?: string })
      setHata(d.response?.data?.error ?? d.response?.data?.errors?.map(x => x.message).join(' ') ?? d.message ?? 'Güncellenemedi.')
    },
  })

  return (
    <div className="card p-5">
      <h2 className="text-sm font-bold uppercase tracking-wide mb-1" style={{ color: 'var(--text-s)' }}>
        Fiyat &amp; Stok Güncelle
      </h2>
      <p className="text-xs mb-3" style={{ color: 'var(--text-s)' }}>
        Onay beklemez, anında uygulanır. Yalnız doldurduğunuz alanlar güncellenir; stok MUTLAK değerdir.
      </p>
      <div className="tbl-wrap overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs uppercase tracking-wide" style={{ color: 'var(--text-s)' }}>
              <th className="py-1.5 pr-3">SKU</th>
              <th className="py-1.5 pr-3 text-right">Mevcut Fiyat</th>
              <th className="py-1.5 pr-3">Yeni Fiyat (TL)</th>
              <th className="py-1.5 pr-3">Yeni Stok (adet)</th>
            </tr>
          </thead>
          <tbody>
            {variants.map(v => (
              <tr key={v.sku} className="border-t" style={{ borderColor: 'var(--border)' }}>
                <td className="py-1.5 pr-3 font-medium" style={{ color: 'var(--text)' }}>{v.sku}</td>
                <td className="py-1.5 pr-3 text-right" style={{ color: 'var(--text-m)' }}>
                  {v.basePrice.toLocaleString('tr-TR', { minimumFractionDigits: 2 })}
                </td>
                <td className="py-1.5 pr-3">
                  <input className="inp !w-28 text-right" type="number" step="0.01" min="0" placeholder="—"
                    value={fiyatlar[v.sku] ?? ''} onChange={e => setFiyatlar(p => ({ ...p, [v.sku]: e.target.value }))} />
                </td>
                <td className="py-1.5 pr-3">
                  <input className="inp !w-24 text-right" type="number" min="0" placeholder="—"
                    value={stoklar[v.sku] ?? ''} onChange={e => setStoklar(p => ({ ...p, [v.sku]: e.target.value }))} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="flex items-center gap-3 mt-3">
        <button className="px-4 py-2 rounded-xl text-sm font-semibold text-white disabled:opacity-50"
          style={{ background: 'var(--brand, #16a34a)' }}
          disabled={kaydet.isPending} onClick={() => kaydet.mutate()}>
          {kaydet.isPending ? 'Güncelleniyor…' : 'Güncelle'}
        </button>
        {mesaj && <span className="text-xs text-green-600">{mesaj}</span>}
        {hata && <span className="text-xs text-red-600">{hata}</span>}
      </div>
    </div>
  )
}

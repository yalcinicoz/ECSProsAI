import { useEffect, useRef, useState } from 'react'
import { useQuery, useMutation } from '@tanstack/react-query'
import { useNavigate, useParams } from 'react-router-dom'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { PageSpinner } from '@/components/ui/Spinner'
import { errText } from '@/components/ui/DataTable.utils'
import { basariSesi, hataSesi, seslendir } from '@/lib/sesler'

/**
 * OP3 — Ara Ayrıştırma Okutma (sorting, tablet).
 * Toplanan ürünün barkodu okutulur → sistem koli numarası söyler; personel ürünü
 * o numaralı koliye atar. Ürüne ihtiyaç yoksa depo iadesine ayrılır.
 */

interface ScanSonuc {
  boxNumber: number
  orderNumber: string
  siparisKalan: number
  koliSiparisSayisi: number
  yeniKoli: boolean
}

interface PlanDetail { id: string; planNumber: string; status: string; planType: string }

export function SortingScanPage() {
  const { planId } = useParams<{ planId: string }>()
  const navigate = useNavigate()
  const inputRef = useRef<HTMLInputElement>(null)
  const [deger, setDeger] = useState('')
  const [sonuc, setSonuc] = useState<ScanSonuc | null>(null)
  const [hata, setHata] = useState('')
  const [okutulan, setOkutulan] = useState(0)
  const [iade, setIade] = useState(0)

  const { data: plan, isLoading } = useQuery<PlanDetail>({
    queryKey: ['picking-plan-detail', planId],
    queryFn: async () => (await api.get(`/fulfillment/picking-plans/${planId}`)).data.data,
    enabled: !!planId,
  })

  // Odak hep okutma kutusunda kalsın (tablet + el terminali)
  useEffect(() => {
    const t = setInterval(() => {
      if (document.activeElement !== inputRef.current) inputRef.current?.focus()
    }, 800)
    inputRef.current?.focus()
    return () => clearInterval(t)
  }, [])

  const scan = useMutation({
    mutationFn: async (barcode: string) =>
      (await api.post<{ data: ScanSonuc }>(`/fulfillment/sorting/${planId}/scan`, { barcode })).data.data,
    onSuccess: (d) => {
      setHata('')
      setSonuc(d)
      setOkutulan(n => n + 1)
      basariSesi()
      seslendir(String(d.boxNumber))
    },
    onError: (e: unknown) => {
      hataSesi()
      setSonuc(null)
      setHata(errText(e))
      setIade(n => n + 1)
    },
  })

  const okut = () => {
    const barkod = deger.trim()
    setDeger('')
    if (!barkod || scan.isPending) return
    scan.mutate(barkod)
  }

  if (isLoading) return <PageSpinner />

  return (
    <div className="p-4 max-w-3xl mx-auto pb-16">
      {/* ── Üst şerit: plan + oturum sayaçları + koli duvarı ── */}
      <div className="card p-4 mb-4 flex items-center gap-4 flex-wrap">
        <div className="flex-1 min-w-0">
          <div className="text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Ara Ayrıştırma</div>
          <div className="text-xl font-bold font-mono truncate" style={{ color: 'var(--text)' }}>
            {plan?.planNumber ?? planId}
          </div>
        </div>
        <div className="text-center px-4 py-1 rounded-xl" style={{ background: '#f0fdf4' }}>
          <div className="text-3xl font-extrabold leading-none" style={{ color: '#15803d' }}>{okutulan}</div>
          <div className="text-xs mt-1" style={{ color: '#166534' }}>okutulan</div>
        </div>
        <div className="text-center px-4 py-1 rounded-xl" style={{ background: '#fef2f2' }}>
          <div className="text-3xl font-extrabold leading-none" style={{ color: '#dc2626' }}>{iade}</div>
          <div className="text-xs mt-1" style={{ color: '#991b1b' }}>iade</div>
        </div>
        <Button variant="secondary" onClick={() => navigate(`/fulfillment/sorting-wall/${planId}`)}>
          Koli Duvarı
        </Button>
      </div>

      {/* ── Büyük okutma kutusu ── */}
      <div className="card p-5 mb-4">
        <label className="flbl mb-2 block !text-base">Ürün barkodunu okut</label>
        <input
          ref={inputRef}
          value={deger}
          onChange={e => setDeger(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); okut() } }}
          onBlur={() => setTimeout(() => inputRef.current?.focus(), 150)}
          autoFocus
          autoComplete="off"
          autoCapitalize="off"
          placeholder={scan.isPending ? 'İşleniyor...' : 'Barkod bekleniyor ●'}
          className="inp w-full font-mono text-center !text-3xl !py-5 !rounded-2xl"
          aria-label="Barkod okutma alanı"
        />
      </div>

      {/* ── Hata: bu ürüne ihtiyaç yok ── */}
      {hata && (
        <div className="rounded-2xl p-8 mb-4 text-center text-white" style={{ background: '#dc2626' }}>
          <div className="text-6xl mb-3">✋</div>
          <div className="text-4xl font-extrabold leading-tight">İHTİYAÇ YOK</div>
          <div className="text-2xl font-bold mt-2">DEPO İADESİNE AYIR</div>
          <div className="text-base mt-4 opacity-90">{hata}</div>
        </div>
      )}

      {/* ── Başarı: DEV koli numarası ── */}
      {sonuc && !hata && (
        <div className="rounded-2xl mb-4 text-center overflow-hidden"
          style={{ background: '#f0fdf4', border: '3px solid #22c55e' }}>
          {sonuc.yeniKoli && (
            <div className="py-2 text-2xl font-extrabold text-white tracking-widest" style={{ background: '#16a34a' }}>
              ★ YENİ KOLİ ★
            </div>
          )}
          <div className="py-6">
            <div className="text-lg font-bold uppercase" style={{ color: '#166534' }}>Koli</div>
            <div className="font-extrabold leading-none font-mono select-none"
              style={{ color: '#14532d', fontSize: 'clamp(9rem, 40vh, 20rem)' }}>
              {sonuc.boxNumber}
            </div>
          </div>
          <div className="px-6 pb-6 grid gap-2 text-left max-w-md mx-auto">
            <div className="flex justify-between items-baseline">
              <span className="text-lg" style={{ color: '#166534' }}>Sipariş No</span>
              <span className="text-2xl font-extrabold font-mono" style={{ color: '#14532d' }}>{sonuc.orderNumber}</span>
            </div>
            <div className="flex justify-between items-baseline">
              <span className="text-lg" style={{ color: '#166534' }}>Siparişin kalan ürünü</span>
              <span className="text-2xl font-extrabold" style={{ color: '#14532d' }}>{sonuc.siparisKalan}</span>
            </div>
            <div className="flex justify-between items-baseline">
              <span className="text-lg" style={{ color: '#166534' }}>Kolide sipariş</span>
              <span className="text-2xl font-extrabold" style={{ color: '#14532d' }}>{sonuc.koliSiparisSayisi}</span>
            </div>
          </div>
        </div>
      )}

      {!sonuc && !hata && (
        <div className="card p-10 text-center" style={{ color: 'var(--text-s)' }}>
          <div className="text-5xl mb-3">🗂</div>
          <p className="text-lg">
            Toplama arabasındaki ürünü okutun; sistem koli numarasını<br />
            ekranda gösterir ve sesli söyler. Ürünü o koliye atın.
          </p>
        </div>
      )}
    </div>
  )
}

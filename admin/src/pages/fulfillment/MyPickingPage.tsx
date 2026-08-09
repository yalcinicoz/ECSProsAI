import { useEffect, useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { PageSpinner } from '@/components/ui/Spinner'
import { errText } from '@/components/ui/DataTable'
import { basariSesi, hataSesi, seslendir } from '@/lib/sesler'
import { cn } from '@/lib/utils'

/**
 * OP2 — Ürün Toplama (personel, mobil).
 * Telefon/tablette kullanılır: tek kolon, büyük dokunma hedefleri, büyük yazı.
 * HID barkod okuyucu görünmez input'a yazar, Enter ile gönderir.
 */

interface Plan {
  id: string
  planNumber: string
  planType: string
  status: string
}

interface Satir {
  id: string
  orderNumber: string
  displayName: string
  sku: string
  variantBarcode: string
  quantity: number
  pickedQuantity: number
  sourceBinCode?: string | null
  status: string
  routeOrder: number
}

interface PlanVeGorev { plan: Plan; kalan: number }

interface ScanSonuc {
  lineId: string
  orderNumber: string
  displayName: string
  sku: string
  pickedQuantity: number
  quantity: number
  lineStatus: string
  kalanSatir: number
}

const acikMi = (s: Satir) => s.status === 'pending' || s.status === 'assigned'

export function MyPickingPage() {
  const queryClient = useQueryClient()
  const [seciliPlan, setSeciliPlan] = useState<Plan | null>(null)

  const { data: me } = useQuery<{ userId: string }>({
    queryKey: ['auth-me'],
    queryFn: async () => (await api.get('/auth/me')).data.data,
  })
  const userId = me?.userId

  // Bekleyen + toplanmakta olan planlar → her biri için bana atanmış satırlar
  const { data: gorevler, isLoading } = useQuery<PlanVeGorev[]>({
    queryKey: ['my-picking-plans', userId],
    enabled: !!userId,
    refetchInterval: 30_000,
    queryFn: async () => {
      const [p1, p2] = await Promise.all([
        api.get('/fulfillment/picking-plans?status=pending&page=1&pageSize=50'),
        api.get('/fulfillment/picking-plans?status=picking&page=1&pageSize=50'),
      ])
      const plans: Plan[] = [...p1.data.data.items, ...p2.data.data.items]
      const sonuc = await Promise.all(plans.map(async (plan) => {
        const lines: Satir[] = (await api.get(
          `/fulfillment/picking-plans/${plan.id}/lines?assignedTo=${userId}`)).data.data
        return { plan, toplam: lines.length, kalan: lines.filter(acikMi).length }
      }))
      return sonuc.filter(x => x.toplam > 0).map(({ plan, kalan }) => ({ plan, kalan }))
    },
  })

  if (!userId || isLoading) return <PageSpinner />

  if (seciliPlan) {
    return (
      <ToplamaEkrani
        plan={seciliPlan}
        userId={userId}
        onGeri={() => {
          setSeciliPlan(null)
          queryClient.invalidateQueries({ queryKey: ['my-picking-plans'] })
        }}
      />
    )
  }

  return (
    <div className="p-4 max-w-xl mx-auto">
      <h1 className="text-2xl font-bold mb-1" style={{ color: 'var(--text)' }}>Ürün Toplama</h1>
      <p className="text-base mb-5" style={{ color: 'var(--text-s)' }}>
        Sana atanmış toplama görevleri. Birini seçip okutmaya başla.
      </p>
      {(gorevler ?? []).length === 0 && (
        <div className="card p-8 text-center">
          <div className="text-5xl mb-3">📭</div>
          <p className="text-lg font-semibold" style={{ color: 'var(--text)' }}>Sana atanmış görev yok</p>
          <p className="text-sm mt-1" style={{ color: 'var(--text-s)' }}>
            Yeni görev atandığında burada görünür (30 sn'de bir yenilenir).
          </p>
        </div>
      )}
      <div className="flex flex-col gap-3">
        {(gorevler ?? []).map(({ plan, kalan }) => (
          <button key={plan.id} onClick={() => setSeciliPlan(plan)}
            className="card p-5 text-left flex items-center gap-4 active:scale-[0.99] transition-transform">
            <div className="flex-1 min-w-0">
              <div className="text-lg font-bold font-mono" style={{ color: 'var(--text)' }}>{plan.planNumber}</div>
              <div className="text-base mt-0.5" style={{ color: kalan > 0 ? 'var(--text-m)' : '#16a34a' }}>
                {kalan > 0 ? `kalan ${kalan} satır` : 'tüm satırlar bitti ✓'}
              </div>
            </div>
            <span className="text-2xl" style={{ color: 'var(--text-s)' }}>›</span>
          </button>
        ))}
      </div>
    </div>
  )
}

// ── Toplama ekranı (plan seçildi) ─────────────────────────────────────────────

function ToplamaEkrani({ plan, userId, onGeri }: { plan: Plan; userId: string; onGeri: () => void }) {
  const queryClient = useQueryClient()
  const inputRef = useRef<HTMLInputElement>(null)
  const [deger, setDeger] = useState('')
  const [hata, setHata] = useState('')
  const [sonOkutma, setSonOkutma] = useState<ScanSonuc | null>(null)
  const [farkliRaf, setFarkliRaf] = useState(false)
  const [bekleyenRaf, setBekleyenRaf] = useState<string | null>(null)
  const [shortOnay, setShortOnay] = useState(false)

  const { data: lines = [], isLoading } = useQuery<Satir[]>({
    queryKey: ['my-picking-lines', plan.id, userId],
    queryFn: async () =>
      (await api.get(`/fulfillment/picking-plans/${plan.id}/lines?assignedTo=${userId}`)).data.data,
  })

  const acik = lines.filter(acikMi) // rota sıralı gelir
  const siradaki = acik[0]
  const sonrakiler = acik.slice(1, 5)
  const bitti = !isLoading && lines.length > 0 && acik.length === 0

  const yenile = () => {
    queryClient.invalidateQueries({ queryKey: ['my-picking-lines', plan.id, userId] })
  }

  // Input her zaman odakta kalsın (HID okuyucu buraya yazar)
  useEffect(() => {
    const t = setInterval(() => {
      if (document.activeElement !== inputRef.current) inputRef.current?.focus()
    }, 800)
    inputRef.current?.focus()
    return () => clearInterval(t)
  }, [])

  const scan = useMutation({
    mutationFn: async (body: { barcode: string; binBarcode?: string }) =>
      (await api.post<{ data: ScanSonuc }>(`/fulfillment/picking/${plan.id}/scan`, body)).data.data,
    onSuccess: (d) => {
      basariSesi()
      setHata('')
      setSonOkutma(d)
      setBekleyenRaf(null)
      setFarkliRaf(false)
      if (d.kalanSatir === 0) seslendir('Görev tamamlandı')
      yenile()
    },
    onError: (e: unknown) => {
      hataSesi()
      setHata(errText(e))
      setBekleyenRaf(null)
      yenile()
    },
  })

  const short = useMutation({
    mutationFn: async (lineId: string) =>
      api.post(`/fulfillment/picking-lines/${lineId}/short`, {}),
    onSuccess: () => { setShortOnay(false); setHata(''); yenile() },
    onError: (e: unknown) => { hataSesi(); setHata(errText(e)); setShortOnay(false); yenile() },
  })

  const okut = () => {
    const barkod = deger.trim()
    setDeger('')
    if (!barkod) return
    if (farkliRaf && !bekleyenRaf) {
      // İlk okutma raf barkodu — sakla, ikinci okutmayı bekle
      setBekleyenRaf(barkod)
      basariSesi()
      return
    }
    scan.mutate({ barcode: barkod, ...(bekleyenRaf ? { binBarcode: bekleyenRaf } : {}) })
  }

  if (isLoading) return <PageSpinner />

  // ── Kutlama ──
  if (bitti) {
    return (
      <div className="p-4 max-w-xl mx-auto">
        <div className="card p-10 text-center" style={{ background: '#f0fdf4', border: '1px solid #86efac' }}>
          <div className="text-7xl mb-4">🎉</div>
          <p className="text-3xl font-extrabold" style={{ color: '#15803d' }}>Görevin bitti!</p>
          <p className="text-lg mt-2" style={{ color: '#166534' }}>
            {plan.planNumber} planındaki tüm satırların tamamlandı.
          </p>
          <Button size="lg" className="mt-8 w-full !text-lg !py-4" onClick={onGeri}>
            Görev Listesine Dön
          </Button>
        </div>
      </div>
    )
  }

  return (
    <div className="p-4 max-w-xl mx-auto pb-28">
      {/* Görünmez barkod input'u — hep odakta, HID okuyucu Enter'la gönderir */}
      <input
        ref={inputRef}
        value={deger}
        onChange={e => setDeger(e.target.value)}
        onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); okut() } }}
        onBlur={() => setTimeout(() => inputRef.current?.focus(), 150)}
        autoFocus
        autoComplete="off"
        autoCapitalize="off"
        inputMode="none"
        aria-label="Barkod okutma alanı"
        className="fixed opacity-0 pointer-events-none w-px h-px"
        style={{ top: 0, left: 0 }}
      />

      {/* Üst şerit */}
      <div className="flex items-center gap-3 mb-4">
        <button onClick={onGeri} className="text-2xl px-2 py-1 rounded-xl"
          style={{ color: 'var(--text-m)', border: '1px solid var(--border)' }}>←</button>
        <div className="flex-1 min-w-0">
          <div className="text-lg font-bold font-mono truncate" style={{ color: 'var(--text)' }}>{plan.planNumber}</div>
          <div className="text-sm" style={{ color: 'var(--text-s)' }}>kalan {acik.length} satır</div>
        </div>
        <span className="text-xs px-3 py-1.5 rounded-full whitespace-nowrap"
          style={{ background: 'var(--surface2)', color: 'var(--text-s)' }}>
          Okutmaya hazır ●
        </span>
      </div>

      {/* Hata şeridi */}
      {hata && (
        <div className="rounded-2xl p-4 mb-4 text-lg font-bold text-white" style={{ background: '#dc2626' }}>
          ⚠ {hata}
        </div>
      )}

      {/* Son başarılı okutma */}
      {sonOkutma && !hata && (
        <div className="rounded-2xl px-4 py-3 mb-4 text-base font-semibold"
          style={{ background: '#f0fdf4', border: '1px solid #86efac', color: '#15803d' }}>
          ✓ {sonOkutma.displayName} — {sonOkutma.pickedQuantity}/{sonOkutma.quantity}
        </div>
      )}

      {/* ── Sıradaki satır: BÜYÜK kart ── */}
      {siradaki && (
        <div className="card p-5 mb-4" style={{ borderWidth: 2, borderColor: 'var(--brand)' }}>
          <div className="text-sm font-semibold uppercase mb-1" style={{ color: 'var(--text-s)' }}>
            Raf
          </div>
          <div className="font-mono font-extrabold leading-none mb-4"
            style={{ fontSize: 'clamp(2.5rem, 12vw, 4.5rem)', color: 'var(--text)' }}>
            {siradaki.sourceBinCode || '—'}
          </div>
          <div className="text-xl font-bold mb-1" style={{ color: 'var(--text)' }}>{siradaki.displayName}</div>
          <div className="text-base font-mono mb-3" style={{ color: 'var(--text-m)' }}>{siradaki.sku}</div>
          <div className="flex items-center justify-between">
            <span className="text-sm" style={{ color: 'var(--text-s)' }}>Sipariş: {siradaki.orderNumber}</span>
            <span className="text-2xl font-extrabold" style={{ color: 'var(--brand)' }}>
              {siradaki.pickedQuantity}/{siradaki.quantity} adet
            </span>
          </div>

          {/* Farklı raftan alma */}
          <div className="mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
            <label className="flex items-center gap-3 text-base py-1" style={{ color: 'var(--text)' }}>
              <input type="checkbox" className="w-6 h-6" checked={farkliRaf}
                onChange={e => { setFarkliRaf(e.target.checked); setBekleyenRaf(null); inputRef.current?.focus() }} />
              Farklı raftan aldım
            </label>
            {farkliRaf && (
              <p className="text-sm mt-1 rounded-xl px-3 py-2"
                style={{ background: 'var(--surface2)', color: 'var(--text-m)' }}>
                {bekleyenRaf
                  ? <>Raf okundu: <b className="font-mono">{bekleyenRaf}</b> — şimdi ÜRÜN barkodunu okut.</>
                  : 'Önce RAF barkodunu, sonra ÜRÜN barkodunu okut.'}
              </p>
            )}
          </div>

          {/* Bulunamadı */}
          <div className="mt-3">
            {!shortOnay ? (
              <Button variant="secondary" className="w-full !text-base !py-3"
                onClick={() => { setShortOnay(true); inputRef.current?.focus() }}>
                Bulunamadı
              </Button>
            ) : (
              <div className="rounded-xl p-3" style={{ background: '#fff7ed', border: '1px solid #fdba74' }}>
                <p className="text-base font-semibold mb-3" style={{ color: '#9a3412' }}>
                  Bu satır "bulunamadı" olarak işaretlenecek. Emin misin?
                </p>
                <div className="flex gap-2">
                  <Button variant="danger" className="flex-1 !text-base !py-3"
                    loading={short.isPending} onClick={() => short.mutate(siradaki.id)}>
                    Evet, Bulunamadı
                  </Button>
                  <Button variant="secondary" className="flex-1 !text-base !py-3"
                    onClick={() => { setShortOnay(false); inputRef.current?.focus() }}>
                    Vazgeç
                  </Button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* ── Sonraki satırlar (soluk) ── */}
      {sonrakiler.length > 0 && (
        <div className="card p-4 opacity-60">
          <div className="text-xs font-semibold uppercase mb-2" style={{ color: 'var(--text-s)' }}>Sıradakiler</div>
          <div className="flex flex-col gap-2">
            {sonrakiler.map(l => (
              <div key={l.id} className={cn('flex items-center gap-3 text-sm')} style={{ color: 'var(--text-m)' }}>
                <span className="font-mono font-bold w-24 shrink-0" style={{ color: 'var(--text)' }}>
                  {l.sourceBinCode || '—'}
                </span>
                <span className="flex-1 truncate">{l.displayName}</span>
                <span className="shrink-0">{l.quantity} ad.</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

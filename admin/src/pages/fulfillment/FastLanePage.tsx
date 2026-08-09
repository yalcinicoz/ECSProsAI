import { useEffect, useRef, useState } from 'react'
import { useQuery, useMutation } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import api from '@/api/client'
import { PageSpinner } from '@/components/ui/Spinner'
import { errText } from '@/components/ui/DataTable'
import { basariSesi, hataSesi } from '@/lib/sesler'

/**
 * OP2 — Tek Ürünlü Hat (fast lane, paketleme masası).
 * Barkod okut → sipariş eşleşir, paket + fatura oluşur, etiketler otomatik yazdırılır.
 * Tablette/masa bilgisayarında büyük ekran kullanımı.
 */

interface ScanSonuc {
  orderId: string
  orderNumber: string
  packageId: string
  packageNumber: string
  invoiceId?: string
  invoiceNumber?: string
  invoiceError?: string
  printUrls: string[]
}

interface PlanDetail { id: string; planNumber: string; status: string; planType: string }

const otoluUrl = (u: string) => `${u}${u.includes('?') ? '&' : '?'}oto=1`

export function FastLanePage() {
  const { planId } = useParams<{ planId: string }>()
  const inputRef = useRef<HTMLInputElement>(null)
  const [deger, setDeger] = useState('')
  const [sonuc, setSonuc] = useState<ScanSonuc | null>(null)
  const [hata, setHata] = useState('')
  const [paketlenen, setPaketlenen] = useState(0)
  const [hataSayisi, setHataSayisi] = useState(0)
  // Yazdırma kuyruğu: iframe'ler sırayla yüklenir, her onload bir sonrakini başlatır
  const [yazdirmaKuyrugu, setYazdirmaKuyrugu] = useState<string[]>([])

  const { data: plan, isLoading } = useQuery<PlanDetail>({
    queryKey: ['picking-plan-detail', planId],
    queryFn: async () => (await api.get(`/fulfillment/picking-plans/${planId}`)).data.data,
    enabled: !!planId,
  })

  // Odak hep okutma kutusunda kalsın
  useEffect(() => {
    const t = setInterval(() => {
      if (document.activeElement !== inputRef.current) inputRef.current?.focus()
    }, 800)
    inputRef.current?.focus()
    return () => clearInterval(t)
  }, [])

  const scan = useMutation({
    mutationFn: async (barcode: string) =>
      (await api.post<{ data: ScanSonuc }>(`/fulfillment/fast-lane/${planId}/scan`, { barcode })).data.data,
    onSuccess: (d) => {
      basariSesi()
      setHata('')
      setSonuc(d)
      setPaketlenen(n => n + 1)
      if (d.printUrls?.length) {
        setYazdirmaKuyrugu(q => [...q, ...d.printUrls.map(otoluUrl)])
      }
    },
    onError: (e: unknown) => {
      hataSesi()
      setSonuc(null)
      setHata(errText(e))
      setHataSayisi(n => n + 1)
    },
  })

  const okut = () => {
    const barkod = deger.trim()
    setDeger('')
    if (!barkod || scan.isPending) return
    scan.mutate(barkod)
  }

  if (isLoading) return <PageSpinner />

  const aktifYazdirma = yazdirmaKuyrugu[0]

  return (
    <div className="p-4 max-w-2xl mx-auto pb-16">
      {/* Gizli yazdırma iframe'i — sayfa ?oto=1 ile kendini yazdırır; onload sonrası sıradaki */}
      {aktifYazdirma && (
        <iframe
          key={aktifYazdirma + yazdirmaKuyrugu.length}
          src={aktifYazdirma}
          title="Otomatik yazdırma"
          aria-hidden
          style={{ position: 'absolute', width: 0, height: 0, border: 0, visibility: 'hidden' }}
          onLoad={() => setTimeout(() => setYazdirmaKuyrugu(q => q.slice(1)), 1500)}
        />
      )}

      {/* ── Üst şerit: plan + oturum sayaçları ── */}
      <div className="card p-4 mb-4 flex items-center gap-4">
        <div className="flex-1 min-w-0">
          <div className="text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Tek Ürünlü Hat</div>
          <div className="text-xl font-bold font-mono truncate" style={{ color: 'var(--text)' }}>
            {plan?.planNumber ?? planId}
          </div>
        </div>
        <div className="text-center px-4 py-1 rounded-xl" style={{ background: '#f0fdf4' }}>
          <div className="text-3xl font-extrabold leading-none" style={{ color: '#15803d' }}>{paketlenen}</div>
          <div className="text-xs mt-1" style={{ color: '#166534' }}>paketlenen</div>
        </div>
        <div className="text-center px-4 py-1 rounded-xl" style={{ background: '#fef2f2' }}>
          <div className="text-3xl font-extrabold leading-none" style={{ color: '#dc2626' }}>{hataSayisi}</div>
          <div className="text-xs mt-1" style={{ color: '#991b1b' }}>hata</div>
        </div>
        {yazdirmaKuyrugu.length > 0 && (
          <div className="text-center px-3 py-1 rounded-xl" style={{ background: 'var(--surface2)' }}>
            <div className="text-xl font-bold leading-none" style={{ color: 'var(--text)' }}>🖨 {yazdirmaKuyrugu.length}</div>
            <div className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>yazdırılıyor</div>
          </div>
        )}
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
          <div className="text-3xl font-extrabold leading-tight">Bu ürüne ihtiyaç yok</div>
          <div className="text-2xl font-bold mt-2">DEPO İADESİNE AYIR</div>
          <div className="text-base mt-4 opacity-90">{hata}</div>
        </div>
      )}

      {/* ── Başarı kartı ── */}
      {sonuc && !hata && (
        <div className="rounded-2xl p-6 mb-4" style={{ background: '#f0fdf4', border: '2px solid #22c55e' }}>
          <div className="flex items-center gap-3 mb-4">
            <span className="text-4xl">✅</span>
            <span className="text-2xl font-extrabold" style={{ color: '#15803d' }}>Paketlendi</span>
          </div>
          <div className="grid gap-3">
            <div className="flex justify-between items-baseline">
              <span className="text-base" style={{ color: '#166534' }}>Sipariş No</span>
              <span className="text-2xl font-extrabold font-mono" style={{ color: '#14532d' }}>{sonuc.orderNumber}</span>
            </div>
            <div className="flex justify-between items-baseline">
              <span className="text-base" style={{ color: '#166534' }}>Paket No</span>
              <span className="text-2xl font-extrabold font-mono" style={{ color: '#14532d' }}>{sonuc.packageNumber}</span>
            </div>
            {sonuc.invoiceNumber && (
              <div className="flex justify-between items-baseline">
                <span className="text-base" style={{ color: '#166534' }}>Fatura No</span>
                <span className="text-2xl font-extrabold font-mono" style={{ color: '#14532d' }}>{sonuc.invoiceNumber}</span>
              </div>
            )}
          </div>
          {sonuc.printUrls?.length > 0 && (
            <p className="text-sm mt-4" style={{ color: '#166534' }}>
              🖨 {sonuc.printUrls.length} belge yazıcıya gönderildi.
            </p>
          )}
        </div>
      )}

      {/* ── Fatura hatası (akış devam eder) ── */}
      {sonuc && !hata && sonuc.invoiceError && (
        <div className="rounded-2xl p-4 mb-4 text-lg font-bold"
          style={{ background: '#fff7ed', border: '2px solid #fb923c', color: '#9a3412' }}>
          ⚠ Fatura kesilemedi: {sonuc.invoiceError}
        </div>
      )}

      {!sonuc && !hata && (
        <div className="card p-10 text-center" style={{ color: 'var(--text-s)' }}>
          <div className="text-5xl mb-3">📦</div>
          <p className="text-lg">Ürünü okutunca sipariş eşleşir, paket ve fatura oluşur,<br />etiketler otomatik yazdırılır.</p>
        </div>
      )}
    </div>
  )
}

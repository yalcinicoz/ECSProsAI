import { useEffect, useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useParams, useSearchParams } from 'react-router-dom'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { errText } from '@/components/ui/DataTable'
import { basariSesi, hataSesi, seslendir } from '@/lib/sesler'

/**
 * OP4 — Masa Ekranı (son ayrıştırma + son kontrol + paket kapanışı).
 * Ayrıştırma modu: ürün okut → DEV slot numarası + sesli okuma; sipariş tamamlanınca
 * "PAKETLE" uyarısı → son kontrol moduna geçilir.
 * Son kontrol modu: siparişin ürünleri tek tek okutulur; bitince paket + otomatik
 * fatura oluşur, etiketler gizli iframe kuyruğuyla yazdırılır (FastLane kalıbı).
 * Tablet kullanımı — büyük öğeler.
 */

interface DeskSlot {
  slotNumber: number
  orderId: string
  orderNumber: string
  finalSorted: number
  finalScanned: number
  quantity: number
  paketlenebilir: boolean
}

interface Desk {
  deskId: string
  deskNumber: number
  status: string
  openedBy: string
  openedAt: string
  sortingBoxId: string
  boxNumber: number
  koliSiparis: number
  paketlenen: number
  obmSayisi: number
  sonIslem?: string | null
  slotlar: DeskSlot[]
}

interface SortSonuc {
  slotNumber: number
  orderNumber: string
  orderId: string
  siparisKalan: number
  paketle: boolean
}

interface FinalSonuc {
  kalan: number
  tamam: boolean
  packageId?: string
  packageNumber?: string
  orderNumber?: string
  invoiceId?: string
  invoiceNumber?: string
  invoiceError?: string
  printUrls?: string[]
}

interface KapatSonuc { paketlenenSiparis: number; obmSiparis: number }

interface AktifSiparis { orderId: string; orderNumber: string; slotNumber: number }

const otoluUrl = (u: string) => `${u}${u.includes('?') ? '&' : '?'}oto=1`

const saat = (v?: string | null) =>
  v ? new Date(v).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' }) : '—'

export function DeskPage() {
  const { deskId } = useParams<{ deskId: string }>()
  const [searchParams] = useSearchParams()
  const planId = searchParams.get('planId')
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const inputRef = useRef<HTMLInputElement>(null)

  const [mod, setMod] = useState<'sort' | 'final'>('sort')
  const [aktifSiparis, setAktifSiparis] = useState<AktifSiparis | null>(null)
  const [deger, setDeger] = useState('')

  // Ayrıştırma modu durumu
  const [sortSonuc, setSortSonuc] = useState<SortSonuc | null>(null)
  const [sortHata, setSortHata] = useState('')

  // Son kontrol modu durumu
  const [finalSonuc, setFinalSonuc] = useState<FinalSonuc | null>(null)
  const [finalHata, setFinalHata] = useState('')

  // Yazdırma kuyruğu: iframe'ler sırayla yüklenir, her onload bir sonrakini başlatır
  const [yazdirmaKuyrugu, setYazdirmaKuyrugu] = useState<string[]>([])

  // Koli kapatma
  const [kapatModal, setKapatModal] = useState(false)
  const [kapatUyari, setKapatUyari] = useState('')
  const [kapatSonuc, setKapatSonuc] = useState<KapatSonuc | null>(null)

  const { data: masa, isLoading } = useQuery<Desk | undefined>({
    queryKey: ['packing-desk', deskId],
    queryFn: async () =>
      ((await api.get(`/fulfillment/desks?deskId=${deskId}`)).data.data as Desk[])[0],
    enabled: !!deskId,
    refetchInterval: 10_000,
  })

  // Odak hep okutma kutusunda kalsın (modal açıkken bırak)
  useEffect(() => {
    const t = setInterval(() => {
      if (kapatModal) return
      if (document.activeElement !== inputRef.current) inputRef.current?.focus()
    }, 800)
    inputRef.current?.focus()
    return () => clearInterval(t)
  }, [kapatModal])

  const masayiYenile = () => queryClient.invalidateQueries({ queryKey: ['packing-desk', deskId] })

  // ── Ayrıştırma okutması ──
  const sortScan = useMutation({
    mutationFn: async (barcode: string) =>
      (await api.post<{ data: SortSonuc }>(`/fulfillment/desks/${deskId}/sort-scan`, { barcode })).data.data,
    onSuccess: (d) => {
      basariSesi()
      setSortHata('')
      setSortSonuc(d)
      if (d.paketle) seslendir(`Paketle ${d.slotNumber}`)
      else seslendir(String(d.slotNumber))
      masayiYenile()
    },
    onError: (e: unknown) => {
      hataSesi()
      setSortSonuc(null)
      setSortHata(errText(e))
    },
  })

  // ── Son kontrol okutması ──
  const finalScan = useMutation({
    mutationFn: async (barcode: string) =>
      (await api.post<{ data: FinalSonuc }>(`/fulfillment/desks/${deskId}/final-scan`, {
        orderId: aktifSiparis?.orderId, barcode,
      })).data.data,
    onSuccess: (d) => {
      basariSesi()
      setFinalHata('')
      setFinalSonuc(d)
      if (d.tamam) {
        if (d.printUrls?.length) setYazdirmaKuyrugu(q => [...q, ...d.printUrls!.map(otoluUrl)])
        masayiYenile()
      }
    },
    onError: (e: unknown) => {
      hataSesi()
      setFinalHata(errText(e))
    },
  })

  // ── Koli kapatma ──
  const kapat = useMutation({
    mutationFn: async (force: boolean) =>
      (await api.post<{ data: KapatSonuc }>(
        `/fulfillment/sorting-boxes/${masa?.sortingBoxId}/close?force=${force}`, {})).data.data,
    onSuccess: (d) => {
      setKapatUyari('')
      setKapatSonuc(d)
    },
    onError: (e: unknown) => {
      setKapatUyari(errText(e))
    },
  })

  const sonKontroleBasla = (siparis: AktifSiparis) => {
    setMod('final')
    setAktifSiparis(siparis)
    setFinalSonuc(null)
    setFinalHata('')
    setSortSonuc(null)
    setSortHata('')
    setDeger('')
    inputRef.current?.focus()
  }

  const ayristirmayaDon = () => {
    setMod('sort')
    setAktifSiparis(null)
    setFinalSonuc(null)
    setFinalHata('')
    setDeger('')
    inputRef.current?.focus()
  }

  const okut = () => {
    const barkod = deger.trim()
    setDeger('')
    if (!barkod) return
    if (mod === 'sort') {
      if (sortScan.isPending) return
      sortScan.mutate(barkod)
    } else {
      if (finalScan.isPending || !aktifSiparis) return
      if (finalSonuc?.tamam) return // paket kapandı — önce "Ayrıştırmaya Dön"
      finalScan.mutate(barkod)
    }
  }

  const kapatModalAc = () => {
    setKapatUyari('')
    setKapatSonuc(null)
    setKapatModal(true)
  }

  const duvaraDon = () => {
    if (planId) navigate(`/fulfillment/sorting-wall/${planId}`)
    else navigate(-1)
  }

  if (isLoading) return <PageSpinner />

  const aktifYazdirma = yazdirmaKuyrugu[0]
  const slotlar = [...(masa?.slotlar ?? [])].sort((a, b) => a.slotNumber - b.slotNumber)
  const paketleBekliyor = mod === 'sort' && sortSonuc?.paketle === true
  const isleniyor = sortScan.isPending || finalScan.isPending

  return (
    <div className="p-4 pb-16">
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

      {/* ── Üst şerit: masa + koli + sayaçlar + koli kapat ── */}
      <div className="card p-4 mb-4 flex items-center gap-4 flex-wrap">
        <div className="flex-1 min-w-0">
          <div className="text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>
            {mod === 'final'
              ? `SON KONTROL — Slot ${aktifSiparis?.slotNumber} / ${aktifSiparis?.orderNumber}`
              : 'Masa Ekranı — Ayrıştırma'}
          </div>
          <div className="text-2xl font-extrabold truncate" style={{ color: 'var(--text)' }}>
            MASA {masa?.deskNumber ?? '—'}
            <span className="text-lg font-bold ml-3" style={{ color: 'var(--text-m)' }}>
              Koli {masa?.boxNumber ?? '—'}
            </span>
          </div>
        </div>
        <div className="text-center px-4 py-1 rounded-xl" style={{ background: '#f0fdf4' }}>
          <div className="text-3xl font-extrabold leading-none" style={{ color: '#15803d' }}>
            {masa?.paketlenen ?? 0}<span className="text-xl" style={{ color: '#166534' }}>/{masa?.koliSiparis ?? 0}</span>
          </div>
          <div className="text-xs mt-1" style={{ color: '#166534' }}>paketlenen / sipariş</div>
        </div>
        {(masa?.obmSayisi ?? 0) > 0 && (
          <div className="text-center px-4 py-1 rounded-xl" style={{ background: '#fffbeb' }}>
            <div className="text-3xl font-extrabold leading-none" style={{ color: '#b45309' }}>{masa?.obmSayisi}</div>
            <div className="text-xs mt-1" style={{ color: '#92400e' }}>OBM</div>
          </div>
        )}
        {yazdirmaKuyrugu.length > 0 && (
          <div className="text-center px-3 py-1 rounded-xl" style={{ background: 'var(--surface2)' }}>
            <div className="text-xl font-bold leading-none" style={{ color: 'var(--text)' }}>🖨 {yazdirmaKuyrugu.length}</div>
            <div className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>yazdırılıyor</div>
          </div>
        )}
        <Button variant="danger" className="!py-3 !text-lg" onClick={kapatModalAc}>
          Koliyi Kapat
        </Button>
      </div>

      <div className="grid gap-4 items-start lg:grid-cols-[minmax(0,1fr)_360px]">
          {/* ── Sol: okutma alanı ── */}
          <div>
            {/* Büyük okutma kutusu */}
            <div className="card p-5 mb-4">
              <label className="flbl mb-2 block !text-base">
                {mod === 'final'
                  ? `Son kontrol — ${aktifSiparis?.orderNumber} ürünlerini okut`
                  : 'Ürün barkodunu okut'}
              </label>
              <input
                ref={inputRef}
                value={deger}
                onChange={e => setDeger(e.target.value)}
                onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); okut() } }}
                onBlur={() => setTimeout(() => { if (!kapatModal) inputRef.current?.focus() }, 150)}
                autoFocus
                autoComplete="off"
                autoCapitalize="off"
                placeholder={isleniyor ? 'İşleniyor...' : 'Barkod bekleniyor ●'}
                className="inp w-full font-mono text-center !text-3xl !py-5 !rounded-2xl"
                aria-label="Barkod okutma alanı"
              />
            </div>

            {/* ── AYRIŞTIRMA MODU ── */}
            {mod === 'sort' && (
              <>
                {sortHata && (
                  <div className="rounded-2xl p-8 mb-4 text-center text-white" style={{ background: '#dc2626' }}>
                    <div className="text-6xl mb-3">✋</div>
                    <div className="text-3xl font-extrabold leading-tight">ASKIYA AYIRIN</div>
                    <div className="text-base mt-4 opacity-90">{sortHata}</div>
                  </div>
                )}

                {/* Paketle uyarısı — sipariş tamamlandı, son kontrole geç */}
                {paketleBekliyor && sortSonuc && (
                  <div className="rounded-2xl p-10 mb-4 text-center text-white" style={{ background: '#ea580c' }}>
                    <div className="text-4xl font-extrabold mb-2">PAKETLE</div>
                    <div className="font-extrabold font-mono leading-none" style={{ fontSize: 'clamp(6rem, 20vw, 10rem)' }}>
                      {sortSonuc.slotNumber}
                    </div>
                    <div className="text-2xl font-bold mt-3">
                      SLOT {sortSonuc.slotNumber} ({sortSonuc.orderNumber})
                    </div>
                    <Button
                      className="mt-6 !bg-white !text-orange-700 !text-2xl !py-5 !px-10 !rounded-2xl w-full"
                      onClick={() => sonKontroleBasla({
                        orderId: sortSonuc.orderId,
                        orderNumber: sortSonuc.orderNumber,
                        slotNumber: sortSonuc.slotNumber,
                      })}
                    >
                      Son Kontrole Başla
                    </Button>
                  </div>
                )}

                {/* Slot numarası DEV */}
                {sortSonuc && !paketleBekliyor && !sortHata && (
                  <div className="rounded-2xl p-8 mb-4 text-center"
                    style={{ background: '#f0fdf4', border: '2px solid #22c55e' }}>
                    <div className="text-xl font-bold mb-1" style={{ color: '#166534' }}>SLOT</div>
                    <div className="text-9xl font-extrabold font-mono leading-none" style={{ color: '#14532d' }}>
                      {sortSonuc.slotNumber}
                    </div>
                    <div className="text-2xl font-bold mt-4 font-mono" style={{ color: '#15803d' }}>
                      {sortSonuc.orderNumber}
                    </div>
                    <div className="text-xl mt-1" style={{ color: '#166534' }}>
                      Siparişte kalan: <b>{sortSonuc.siparisKalan}</b>
                    </div>
                  </div>
                )}

                {!sortSonuc && !sortHata && (
                  <div className="card p-10 text-center" style={{ color: 'var(--text-s)' }}>
                    <div className="text-5xl mb-3">🗂️</div>
                    <p className="text-lg">Kolideki ürünü okutunca gideceği slot numarası<br />büyük olarak gösterilir ve sesli okunur.</p>
                  </div>
                )}
              </>
            )}

            {/* ── SON KONTROL MODU ── */}
            {mod === 'final' && (
              <>
                {finalHata && (
                  <div className="rounded-2xl p-8 mb-4 text-center text-white" style={{ background: '#dc2626' }}>
                    <div className="text-6xl mb-3">✋</div>
                    <div className="text-3xl font-extrabold leading-tight">BU SİPARİŞE AİT DEĞİL</div>
                    <div className="text-2xl font-bold mt-2">ASKIYA AYIR</div>
                    <div className="text-base mt-4 opacity-90">{finalHata}</div>
                  </div>
                )}

                {/* Paket kapandı — yeşil dev kart */}
                {finalSonuc?.tamam && (
                  <div className="rounded-2xl p-8 mb-4 text-center"
                    style={{ background: '#f0fdf4', border: '3px solid #22c55e' }}>
                    <div className="text-6xl mb-2">✅</div>
                    <div className="text-3xl font-extrabold" style={{ color: '#15803d' }}>PAKET TAMAM</div>
                    <div className="grid gap-2 mt-5 text-left max-w-md mx-auto">
                      <div className="flex justify-between items-baseline">
                        <span className="text-lg" style={{ color: '#166534' }}>Sipariş No</span>
                        <span className="text-2xl font-extrabold font-mono" style={{ color: '#14532d' }}>
                          {finalSonuc.orderNumber ?? aktifSiparis?.orderNumber}
                        </span>
                      </div>
                      <div className="flex justify-between items-baseline">
                        <span className="text-lg" style={{ color: '#166534' }}>Paket No</span>
                        <span className="text-2xl font-extrabold font-mono" style={{ color: '#14532d' }}>
                          {finalSonuc.packageNumber}
                        </span>
                      </div>
                      {finalSonuc.invoiceNumber && (
                        <div className="flex justify-between items-baseline">
                          <span className="text-lg" style={{ color: '#166534' }}>Fatura No</span>
                          <span className="text-2xl font-extrabold font-mono" style={{ color: '#14532d' }}>
                            {finalSonuc.invoiceNumber}
                          </span>
                        </div>
                      )}
                    </div>
                    {(finalSonuc.printUrls?.length ?? 0) > 0 && (
                      <p className="text-sm mt-4" style={{ color: '#166534' }}>
                        🖨 {finalSonuc.printUrls!.length} belge yazıcıya gönderildi.
                      </p>
                    )}
                    <Button className="mt-5 !text-xl !py-4 !px-8 w-full" onClick={ayristirmayaDon}>
                      Ayrıştırmaya Dön
                    </Button>
                  </div>
                )}

                {/* Fatura hatası (akış devam eder) */}
                {finalSonuc?.tamam && finalSonuc.invoiceError && (
                  <div className="rounded-2xl p-4 mb-4 text-lg font-bold"
                    style={{ background: '#fff7ed', border: '2px solid #fb923c', color: '#9a3412' }}>
                    ⚠ Fatura kesilemedi: {finalSonuc.invoiceError}
                  </div>
                )}

                {/* Kalan sayacı */}
                {!finalSonuc?.tamam && !finalHata && (
                  <div className="card p-8 text-center">
                    {finalSonuc ? (
                      <>
                        <div className="text-xl font-bold mb-1" style={{ color: 'var(--text-m)' }}>KALAN ÜRÜN</div>
                        <div className="text-9xl font-extrabold font-mono leading-none" style={{ color: 'var(--text)' }}>
                          {finalSonuc.kalan}
                        </div>
                      </>
                    ) : (
                      <>
                        <div className="text-5xl mb-3">🔎</div>
                        <p className="text-lg" style={{ color: 'var(--text-s)' }}>
                          Slottaki ürünleri tek tek okutun.<br />Hepsi okutulunca paket ve fatura otomatik oluşur.
                        </p>
                      </>
                    )}
                  </div>
                )}

                {!finalSonuc?.tamam && (
                  <div className="mt-4">
                    <Button variant="secondary" className="!py-3 !text-lg w-full" onClick={ayristirmayaDon}>
                      Ayrıştırmaya Dön
                    </Button>
                  </div>
                )}
              </>
            )}
          </div>

          {/* ── Sağ: masa durumu paneli (10 sn'de bir yenilenir) ── */}
          <div className="card p-4">
            <div className="flex items-center justify-between mb-3">
              <div className="text-base font-bold" style={{ color: 'var(--text)' }}>Slotlar</div>
              <div className="text-sm" style={{ color: 'var(--text-s)' }}>
                Son işlem: {saat(masa?.sonIslem)}
              </div>
            </div>
            {slotlar.length === 0 && (
              <p className="text-base py-4 text-center" style={{ color: 'var(--text-s)' }}>
                Henüz slot dolmadı — okutmaya başlayın.
              </p>
            )}
            <div className="grid gap-2">
              {slotlar.map(s => {
                const tamamlandi = s.quantity > 0 && s.finalScanned >= s.quantity
                return (
                  <button
                    key={s.slotNumber}
                    type="button"
                    disabled={!s.paketlenebilir || tamamlandi}
                    onClick={() => sonKontroleBasla({
                      orderId: s.orderId, orderNumber: s.orderNumber, slotNumber: s.slotNumber,
                    })}
                    className="w-full text-left rounded-xl p-3 flex items-center gap-3 transition-all disabled:cursor-default"
                    style={{
                      background: tamamlandi ? '#f0fdf4' : s.paketlenebilir ? '#fff7ed' : 'var(--surface2)',
                      border: `2px solid ${tamamlandi ? '#22c55e' : s.paketlenebilir ? '#fb923c' : 'var(--border)'}`,
                    }}
                  >
                    <div className="rounded-lg px-3 py-1.5 text-2xl font-extrabold font-mono leading-none text-white"
                      style={{
                        background: tamamlandi ? '#16a34a' : s.paketlenebilir ? '#ea580c' : '#64748b',
                        minWidth: 52, textAlign: 'center',
                      }}>
                      {s.slotNumber}
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="text-base font-bold font-mono truncate" style={{ color: 'var(--text)' }}>
                        {s.orderNumber}
                      </div>
                      <div className="text-sm" style={{ color: 'var(--text-m)' }}>
                        Ayrılan {s.finalSorted}/{s.quantity} · Okutulan {s.finalScanned}/{s.quantity}
                      </div>
                    </div>
                    {tamamlandi ? (
                      <span className="px-2 py-1 rounded-lg text-sm font-extrabold text-white" style={{ background: '#16a34a' }}>
                        PAKETLENDİ
                      </span>
                    ) : s.paketlenebilir ? (
                      <span className="px-2 py-1 rounded-lg text-sm font-extrabold text-white" style={{ background: '#ea580c' }}>
                        PAKETLE
                      </span>
                    ) : null}
                  </button>
                )
              })}
            </div>
            <div className="mt-3 pt-3 text-base flex justify-between" style={{ borderTop: '1px solid var(--border)', color: 'var(--text-m)' }}>
              <span>Dolu slot</span>
              <b style={{ color: 'var(--text)' }}>{slotlar.length}</b>
            </div>
            <div className="text-base flex justify-between" style={{ color: 'var(--text-m)' }}>
              <span>Paketlenen / sipariş</span>
              <b style={{ color: 'var(--text)' }}>{masa?.paketlenen ?? 0} / {masa?.koliSiparis ?? 0}</b>
            </div>
          </div>
      </div>

      {/* ── Koli kapatma modalı ── */}
      <Modal open={kapatModal} onClose={() => { if (!kapat.isPending) setKapatModal(false) }} title="Koliyi Kapat">
        {kapatSonuc ? (
          <>
            <div className="rounded-xl p-5 text-center mb-4" style={{ background: '#f0fdf4', border: '2px solid #22c55e' }}>
              <div className="text-4xl mb-2">✅</div>
              <div className="text-xl font-extrabold" style={{ color: '#15803d' }}>Koli kapatıldı</div>
              <div className="grid gap-1 mt-3 text-lg" style={{ color: '#166534' }}>
                <div>Paketlenen sipariş: <b>{kapatSonuc.paketlenenSiparis}</b></div>
                <div>OBM'ye aktarılan: <b>{kapatSonuc.obmSiparis}</b></div>
              </div>
            </div>
            <div className="flex justify-end gap-2 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
              <Button className="!py-3 !text-lg" onClick={duvaraDon}>Koli Duvarına Dön</Button>
            </div>
          </>
        ) : (
          <>
            <p className="text-lg mb-2" style={{ color: 'var(--text)' }}>
              <b>Koli {masa?.boxNumber}</b> ve <b>Masa {masa?.deskNumber}</b> kapatılacak.
            </p>
            <p className="text-base mb-4" style={{ color: 'var(--text-m)' }}>
              Paketlenmemiş siparişler OBM'ye (Ortak Birleştirme Masası) aktarılır.
              Paketlenen: {masa?.paketlenen ?? 0} / {masa?.koliSiparis ?? 0} sipariş.
            </p>
            {kapatUyari && (
              <div className="rounded-xl p-4 mb-4 text-base font-bold"
                style={{ background: '#fff7ed', border: '2px solid #fb923c', color: '#9a3412' }}>
                ⚠ {kapatUyari}
              </div>
            )}
            <div className="flex justify-end gap-2 pt-4 flex-wrap" style={{ borderTop: '1px solid var(--border)' }}>
              <Button variant="secondary" className="!py-3 !text-lg" onClick={() => setKapatModal(false)}>
                Vazgeç
              </Button>
              {kapatUyari ? (
                <Button variant="danger" className="!py-3 !text-lg" loading={kapat.isPending}
                  onClick={() => kapat.mutate(true)}>
                  Yine de kapat (OBM'ye aktar)
                </Button>
              ) : (
                <Button className="!py-3 !text-lg" loading={kapat.isPending}
                  onClick={() => kapat.mutate(false)}>
                  Koliyi Kapat
                </Button>
              )}
            </div>
          </>
        )}
      </Modal>
    </div>
  )
}

import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { PageSpinner } from '@/components/ui/Spinner'

// 2026-07-22: Kargo Bölgeleri — hangi mahalleye hangi kargolar gider + öncelik.
// Üstte firma genelinin GENEL öncelik sırası (ruleType=default kuralları), altta
// il→ilçe→mahalle seçimiyle mahalleye özel atama listesi (ruleType=neighborhood).
// Priority BÜYÜK olan önce: listedeki üst satır en yüksek sayıyı alır (n..1).
// Atanmamış mahallede sitede tüm aktif kargolar genel öncelikle sunulur.

interface Firm { id: string; nameI18n: Record<string, string> }
interface CargoIntegration {
  id: string; serviceCode: string; serviceNameI18n: Record<string, string>
  name?: string; isActive: boolean
}
interface CargoRule {
  id: string; firmPlatformIntegrationId: string; integrationName: string | null
  ruleType: string; neighborhoodId: string | null; priority: number; isActive: boolean
}
interface GeoItem { id: string; nameI18n?: Record<string, string>; name?: string }

const geoAd = (g: GeoItem) => g.nameI18n?.tr ?? Object.values(g.nameI18n ?? {})[0] ?? g.name ?? ''

function apiErrorMessage(error: unknown, fallback: string): string {
  if (typeof error !== 'object' || error === null || !('response' in error)) return fallback
  const response = error.response
  if (typeof response !== 'object' || response === null || !('data' in response)) return fallback
  const data = response.data
  if (typeof data !== 'object' || data === null || !('error' in data)) return fallback
  return typeof data.error === 'string' ? data.error : fallback
}

// Sıralı entegrasyon listesi düzenleyicisi — genel ve mahalle bölümleri paylaşır
function KargoSiraListesi({ liste, degistir, kargolar, bosMesaj }: {
  liste: string[]
  degistir: (v: string[]) => void
  kargolar: CargoIntegration[]
  bosMesaj: string
}) {
  const kargoAd = (id: string) => {
    const k = kargolar.find((x) => x.id === id)
    return k ? (k.name || k.serviceNameI18n?.tr || k.serviceCode) : 'Bilinmeyen kargo'
  }
  const tasi = (i: number, yon: number) => {
    const yeni = [...liste]
    const [alinan] = yeni.splice(i, 1)
    yeni.splice(i + yon, 0, alinan)
    degistir(yeni)
  }
  const eklenebilir = kargolar.filter((k) => k.isActive && !liste.includes(k.id))
  return (
    <div className="space-y-2">
      {liste.map((id, i) => (
        <div key={id} className="flex items-center gap-2 rounded-lg bg-[var(--surface2)] px-3 py-2 text-sm">
          <span className="w-6 text-center text-xs text-[var(--text-s)]">{i + 1}.</span>
          <span className="flex-1 font-medium">{kargoAd(id)}</span>
          {!kargolar.find((k) => k.id === id)?.isActive && <Badge variant="warning">Pasif entegrasyon</Badge>}
          <button className="rounded px-1.5 py-0.5 text-[var(--text-m)] hover:bg-[var(--surface)] disabled:opacity-30"
            disabled={i === 0} onClick={() => tasi(i, -1)} aria-label="Yukarı">↑</button>
          <button className="rounded px-1.5 py-0.5 text-[var(--text-m)] hover:bg-[var(--surface)] disabled:opacity-30"
            disabled={i === liste.length - 1} onClick={() => tasi(i, 1)} aria-label="Aşağı">↓</button>
          <Button size="sm" variant="danger" onClick={() => degistir(liste.filter((x) => x !== id))}>Kaldır</Button>
        </div>
      ))}
      {liste.length === 0 && <p className="text-sm text-[var(--text-m)]">{bosMesaj}</p>}
      {eklenebilir.length > 0 && (
        <div className="max-w-xs">
          <SearchableSelect
            value={null}
            onChange={(v) => { if (v) degistir([...liste, v]) }}
            options={eklenebilir.map((k) => ({ value: k.id, label: k.name || k.serviceNameI18n?.tr || k.serviceCode }))}
            placeholder="+ Kargo Ekle"
          />
        </div>
      )}
    </div>
  )
}

export function CargoZonesPage() {
  const queryClient = useQueryClient()

  const { data: firms = [], isLoading: firmsYukleniyor } = useQuery<Firm[]>({
    queryKey: ['firms'],
    queryFn: async () => (await api.get('/core/firms')).data.data,
  })
  const [firmId, setFirmId] = useState('')
  const selectedFirmId = firmId || firms[0]?.id || ''

  const { data: kargolar = [], isLoading: kargoYukleniyor } = useQuery<CargoIntegration[]>({
    queryKey: ['cargo-integrations', selectedFirmId],
    queryFn: async () => (await api.get(`/core/firms/${selectedFirmId}/integrations?serviceType=cargo`)).data.data,
    enabled: !!selectedFirmId,
  })
  const { data: kurallar = [], isLoading: kuralYukleniyor } = useQuery<CargoRule[]>({
    queryKey: ['cargo-rules', selectedFirmId],
    queryFn: async () => (await api.get(`/core/firms/${selectedFirmId}/cargo-rules`)).data.data,
    enabled: !!selectedFirmId,
  })

  // ── Genel öncelik (ruleType=default) ──
  const [genelDraft, setGenelDraft] = useState<{ firmId: string; liste: string[] } | null>(null)
  const genelSunucu = useMemo(() => kurallar
    .filter((r) => r.ruleType === 'default' && r.isActive)
    .sort((a, b) => b.priority - a.priority)
    .map((r) => r.firmPlatformIntegrationId), [kurallar])
  const genel = genelDraft?.firmId === selectedFirmId ? genelDraft.liste : null
  const genelListe = genel ?? genelSunucu
  const setGenel = (liste: string[]) => setGenelDraft({ firmId: selectedFirmId, liste })

  // ── Mahalle seçimi (il → ilçe → mahalle arama) ──
  const [ilId, setIlId] = useState<string | null>(null)
  const [ilceId, setIlceId] = useState<string | null>(null)
  const [mahalle, setMahalle] = useState<GeoItem | null>(null)

  const { data: ulkeler = [] } = useQuery<{ id: string; code: string }[]>({
    queryKey: ['geo-countries'],
    queryFn: async () => (await api.get('/store/geo/countries')).data.data,
  })
  const ulkeId = ulkeler.find((u) => u.code === 'TR')?.id ?? ulkeler[0]?.id
  const { data: iller = [] } = useQuery<GeoItem[]>({
    queryKey: ['geo-cities', ulkeId],
    queryFn: async () => (await api.get(`/store/geo/cities?countryId=${ulkeId}`)).data.data,
    enabled: !!ulkeId,
  })
  const { data: ilceler = [] } = useQuery<GeoItem[]>({
    queryKey: ['geo-districts', ilId],
    queryFn: async () => (await api.get(`/store/geo/districts?cityId=${ilId}`)).data.data,
    enabled: !!ilId,
  })
  // İlçenin tüm mahalleleri tek seferde gelir (ilçe başına ≤ ~1.5K, endpoint bellekte
  // filtreliyor zaten) — arama SearchableSelect'in kendi filtresiyle yapılır.
  const { data: mahalleler = [] } = useQuery<GeoItem[]>({
    queryKey: ['geo-neighborhoods', ilceId],
    queryFn: async () => (await api.get(`/store/geo/neighborhoods?districtId=${ilceId}&limit=2000`)).data.data,
    enabled: !!ilceId,
  })

  // ── Seçili mahallenin atamaları (ruleType=neighborhood) ──
  const [mahalleDraft, setMahalleDraft] = useState<{ mahalleId: string; liste: string[] } | null>(null)
  const mahalleSunucu = useMemo(() => kurallar
    .filter((r) => r.ruleType === 'neighborhood' && r.neighborhoodId === mahalle?.id && r.isActive)
    .sort((a, b) => b.priority - a.priority)
    .map((r) => r.firmPlatformIntegrationId), [kurallar, mahalle])
  const mahalleListe = mahalleDraft && mahalleDraft.mahalleId === mahalle?.id ? mahalleDraft.liste : null
  const mahalleAtama = mahalleListe ?? mahalleSunucu
  const setMahalleListe = (liste: string[]) => {
    if (mahalle) setMahalleDraft({ mahalleId: mahalle.id, liste })
  }

  // Mahalle başına atama sayısı — arama listesinde rozet olarak gösterilir
  const atamaSayilari = useMemo(() => {
    const m = new Map<string, number>()
    for (const r of kurallar) {
      if (r.ruleType === 'neighborhood' && r.neighborhoodId && r.isActive)
        m.set(r.neighborhoodId, (m.get(r.neighborhoodId) ?? 0) + 1)
    }
    return m
  }, [kurallar])

  const [kayitDurum, setKayitDurum] = useState('')
  const kaydet = useMutation({
    mutationFn: async (p: { ruleType: string; neighborhoodId: string | null; liste: string[] }) =>
      api.put(`/core/firms/${selectedFirmId}/cargo-rules`, {
        ruleType: p.ruleType,
        neighborhoodId: p.neighborhoodId,
        // Üst satır en yüksek önceliği alır (sunucu DESC sıralar)
        items: p.liste.map((id, i) => ({ firmPlatformIntegrationId: id, priority: p.liste.length - i })),
      }),
    onSuccess: () => {
      setKayitDurum('Kaydedildi ✓')
      setTimeout(() => setKayitDurum(''), 2500)
      queryClient.invalidateQueries({ queryKey: ['cargo-rules', selectedFirmId] })
      setGenelDraft(null); setMahalleDraft(null)
    },
    onError: (error: unknown) => {
      setKayitDurum(apiErrorMessage(error, 'Kaydedilemedi.'))
    },
  })

  if (firmsYukleniyor) return <PageSpinner />

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">Kargo Bölgeleri</h1>
        <div className="flex items-center gap-3">
          {kayitDurum && <span className="text-sm text-[var(--text-m)]">{kayitDurum}</span>}
          {firms.length > 1 && (
            <div className="w-56">
              <SearchableSelect value={selectedFirmId || null} onChange={(v) => {
                if (!v) return
                setFirmId(v)
                setGenelDraft(null)
                setMahalleDraft(null)
              }}
                options={firms.map((f) => ({ value: f.id, label: f.nameI18n?.tr ?? f.id }))} placeholder="Firma" />
            </div>
          )}
        </div>
      </div>

      {/* Kargo entegrasyonu yok uyarısı — her zaman mount, display ile gizli (insertBefore tuzağı) */}
      <div className="rounded-lg border border-amber-200 bg-amber-50 p-3"
        style={{ display: !kargoYukleniyor && selectedFirmId && !kargolar.some((k) => k.isActive) ? undefined : 'none' }}>
        <p className="text-sm text-amber-800">
          Bu firmada tanımlı aktif kargo entegrasyonu yok — kargo bölgeleri tanımlamak için önce{' '}
          <Link to={`/settings/firms/${selectedFirmId}`} className="font-medium underline">firma entegrasyonları</Link>{' '}
          sayfasından bir kargo servisi ekleyin.
        </p>
      </div>

      {/* Genel öncelik */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
        <div className="mb-1 flex items-center justify-between">
          <h2 className="text-sm font-semibold">Genel Öncelik (tüm firma)</h2>
          <Button size="sm" onClick={() => kaydet.mutate({ ruleType: 'default', neighborhoodId: null, liste: genelListe })}
            disabled={kaydet.isPending || genel === null}>Genel Sırayı Kaydet</Button>
        </div>
        <p className="mb-3 text-xs text-[var(--text-s)]">
          Mahalleye özel atama olmayan adreslerde kargolar bu sırayla sunulur. Buraya eklenmemiş aktif kargolar listenin sonunda ada göre yer alır.
        </p>
        {kuralYukleniyor ? <PageSpinner /> : (
          <KargoSiraListesi liste={genelListe} degistir={setGenel} kargolar={kargolar}
            bosMesaj="Genel sıra tanımlanmadı — aktif kargolar ada göre sunulur." />
        )}
      </div>

      {/* Mahalle atamaları */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
        <h2 className="mb-1 text-sm font-semibold">Mahalle Atamaları</h2>
        <p className="mb-3 text-xs text-[var(--text-s)]">
          Mahalleye kargo atanırsa o adreste YALNIZ atanan kargolar, buradaki öncelik sırasıyla sunulur. Atama yoksa genel öncelik geçerlidir.
        </p>
        <div className="grid gap-3 md:grid-cols-3">
          <div>
            <label className="mb-1 block text-sm text-[var(--text-m)]">İl</label>
            <SearchableSelect value={ilId} onChange={(v) => {
              setIlId(v); setIlceId(null); setMahalle(null); setMahalleDraft(null)
            }}
              options={iller.map((i) => ({ value: i.id, label: geoAd(i) }))} placeholder="İl seç" />
          </div>
          <div>
            <label className="mb-1 block text-sm text-[var(--text-m)]">İlçe</label>
            <SearchableSelect value={ilceId} onChange={(v) => {
              setIlceId(v); setMahalle(null); setMahalleDraft(null)
            }}
              options={ilceler.map((i) => ({ value: i.id, label: geoAd(i) }))} placeholder="İlçe seç" />
          </div>
          <div>
            <label className="mb-1 block text-sm text-[var(--text-m)]">Mahalle</label>
            <SearchableSelect
              value={mahalle?.id ?? null}
              onChange={(v) => {
                setMahalle(mahalleler.find((m) => m.id === v) ?? null)
                setMahalleDraft(null)
              }}
              options={mahalleler.map((m) => ({
                value: m.id,
                label: geoAd(m) + ((atamaSayilari.get(m.id) ?? 0) > 0 ? ` · ${atamaSayilari.get(m.id)} kargo` : ''),
              }))}
              placeholder={ilceId ? 'Mahalle seç' : 'Önce ilçe seçin'}
              disabled={!ilceId}
            />
          </div>
        </div>

        {mahalle && (
          <div className="mt-4 rounded-lg border border-[var(--border)] p-3">
            <div className="mb-2 flex items-center justify-between">
              <h3 className="text-sm font-semibold">
                {geoAd(mahalle)}
                {mahalleAtama.length === 0 && <span className="ml-2 text-xs font-normal text-[var(--text-s)]">(atama yok — genel öncelik geçerli)</span>}
              </h3>
              <Button size="sm" onClick={() => kaydet.mutate({ ruleType: 'neighborhood', neighborhoodId: mahalle.id, liste: mahalleAtama })}
                disabled={kaydet.isPending || mahalleListe === null}>Mahalle Atamasını Kaydet</Button>
            </div>
            <KargoSiraListesi liste={mahalleAtama} degistir={setMahalleListe} kargolar={kargolar}
              bosMesaj="Bu mahalleye özel atama yok — eklerseniz yalnız eklenenler sunulur." />
          </div>
        )}
      </div>
    </div>
  )
}

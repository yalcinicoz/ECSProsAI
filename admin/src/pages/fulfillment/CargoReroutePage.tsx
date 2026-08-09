import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { PageSpinner } from '@/components/ui/Spinner'
import { errText, tarihSaat } from '@/components/ui/DataTable'
import { cn } from '@/lib/utils'

// OP5: Kargo Yönlendirme — kargo outbox kuyruğunu taşıyıcı bazlı gruplu gösterir;
// bekleyen/hatalı bildirimler seçilip başka taşıyıcıya yönlendirilebilir.
// Gönderim worker'ı KG1'e kadar kapalı olduğundan kuyruk burada izlenir/yönetilir.

interface CargoOutboxItem {
  id: string
  packageId: string
  packageNumber: string
  orderId: string
  cargoIntegrationId: string | null
  cargoName: string | null
  status: 'pending' | 'sent' | 'failed' | 'cancelled'
  attemptCount: number
  lastError: string | null
  sentAt: string | null
  createdAt: string
}

interface Firm { id: string; nameI18n: Record<string, string> }
interface CargoIntegration {
  id: string
  serviceCode?: string
  serviceNameI18n?: Record<string, string>
  name?: string
  isActive?: boolean
}

interface HedefTasiyici { id: string; label: string; name: string }

const TABLAR: [('pending' | 'failed' | 'sent'), string][] = [
  ['pending', 'Bekleyen'],
  ['failed', 'Hatalı'],
  ['sent', 'Gönderilen'],
]

const BOS_MESAJ: Record<string, string> = {
  pending: 'Bekleyen bildirim yok.',
  failed: 'Hatalı bildirim yok.',
  sent: 'Gönderilmiş bildirim yok.',
}

export function CargoReroutePage() {
  const queryClient = useQueryClient()
  const [tab, setTab] = useState<'pending' | 'failed' | 'sent'>('pending')
  const [secili, setSecili] = useState<Set<string>>(new Set())
  const [hedefId, setHedefId] = useState<string | null>(null)
  const [onayAcik, setOnayAcik] = useState(false)
  const [hata, setHata] = useState('')
  const [basari, setBasari] = useState('')

  // Kuyruk — 30 sn'de bir tazelenir
  const { data: kayitlar = [], isLoading } = useQuery<CargoOutboxItem[]>({
    queryKey: ['cargo-outbox', tab],
    queryFn: async () => (await api.get(`/fulfillment/cargo-outbox?status=${tab}`)).data.data,
    refetchInterval: 30_000,
  })

  // Hedef taşıyıcılar — tüm firmaların cargo entegrasyonları düz listede (firma adı prefix'li)
  const { data: hedefler = [] } = useQuery<HedefTasiyici[]>({
    queryKey: ['cargo-reroute-targets'],
    queryFn: async () => {
      const firmalar: Firm[] = (await api.get('/core/firms')).data.data
      const listeler = await Promise.all(firmalar.map(async (f) => {
        const entegrasyonlar: CargoIntegration[] =
          (await api.get(`/core/firms/${f.id}/integrations?serviceType=cargo`)).data.data
        const firmaAd = f.nameI18n?.tr ?? Object.values(f.nameI18n ?? {})[0] ?? ''
        return entegrasyonlar
          .filter((k) => k.isActive !== false)
          .map((k) => {
            const ad = k.name || k.serviceNameI18n?.tr || k.serviceCode || ''
            return { id: k.id, name: ad, label: `${firmaAd} — ${ad}` }
          })
      }))
      return listeler.flat()
    },
  })

  // Taşıyıcı bazlı gruplama
  const gruplar = useMemo(() => {
    const map = new Map<string, { ad: string; satirlar: CargoOutboxItem[] }>()
    for (const k of kayitlar) {
      const anahtar = k.cargoIntegrationId ?? '-'
      const grup = map.get(anahtar) ?? { ad: k.cargoName ?? 'Taşıyıcı atanmamış', satirlar: [] }
      grup.satirlar.push(k)
      map.set(anahtar, grup)
    }
    return [...map.entries()].map(([key, g]) => ({ key, ...g }))
  }, [kayitlar])

  const secilebilir = tab !== 'sent'
  const seciliSatirlar = kayitlar.filter((k) => secili.has(k.id))
  const hedef = hedefler.find((h) => h.id === hedefId) ?? null

  const tabDegistir = (t: 'pending' | 'failed' | 'sent') => {
    setTab(t)
    setSecili(new Set())
    setHata('')
    setBasari('')
  }

  const satirSec = (id: string) => {
    setSecili((eski) => {
      const yeni = new Set(eski)
      if (yeni.has(id)) yeni.delete(id)
      else yeni.add(id)
      return yeni
    })
  }

  const grupSec = (satirlar: CargoOutboxItem[], hepsiSeciliMi: boolean) => {
    setSecili((eski) => {
      const yeni = new Set(eski)
      for (const s of satirlar) {
        if (hepsiSeciliMi) yeni.delete(s.id)
        else yeni.add(s.id)
      }
      return yeni
    })
  }

  const yonlendir = useMutation({
    mutationFn: async () => {
      if (!hedef) throw new Error('Hedef taşıyıcı seçilmedi')
      const orderIds = [...new Set(seciliSatirlar.map((s) => s.orderId))]
      return (await api.post('/fulfillment/cargo-outbox/reroute', {
        outboxIds: [...secili],
        targetIntegrationId: hedef.id,
        targetName: hedef.name,
        orderIds,
      })).data.data
    },
    onSuccess: (data: { rerouted: number }) => {
      setOnayAcik(false)
      setSecili(new Set())
      setHata('')
      setBasari(`${data?.rerouted ?? seciliSatirlar.length} paket ${hedef?.name ?? 'hedef taşıyıcıya'} yönlendirildi.`)
      queryClient.invalidateQueries({ queryKey: ['cargo-outbox'] })
    },
    onError: (e: unknown) => {
      setOnayAcik(false)
      setBasari('')
      setHata(errText(e))
    },
  })

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Kargo Yönlendirme</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          Kargo bildirim kuyruğu — {kayitlar.length} kayıt
        </p>
      </div>

      {/* Durum sekmeleri */}
      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABLAR.map(([v, l]) => (
          <button key={v} className={cn('stab', tab === v && 'active')} onClick={() => tabDegistir(v)}>
            {l}
          </button>
        ))}
      </div>

      {hata && <p className="text-sm text-red-500 mb-3">{hata}</p>}
      {basari && <p className="text-sm text-green-600 mb-3">{basari}</p>}

      {/* Üst şerit: seçim + hedef taşıyıcı + yönlendir */}
      {secilebilir && (
        <div className="card p-4 mb-4 flex flex-wrap items-end gap-4">
          <div className="text-sm font-medium" style={{ color: 'var(--text)' }}>
            {secili.size > 0 ? `${secili.size} paket seçildi` : 'Paket seçin'}
          </div>
          <div className="min-w-[260px] flex-1 max-w-sm">
            <label className="flbl">Hedef Taşıyıcı</label>
            <SearchableSelect
              value={hedefId}
              onChange={setHedefId}
              options={hedefler.map((h) => ({ value: h.id, label: h.label }))}
              placeholder="— Taşıyıcı seçin —"
              clearable
            />
          </div>
          <Button
            size="sm"
            disabled={secili.size === 0 || !hedef}
            onClick={() => { setHata(''); setBasari(''); setOnayAcik(true) }}
          >
            Seçilenleri Yönlendir
          </Button>
        </div>
      )}

      {/* Gruplu liste */}
      {isLoading ? (
        <PageSpinner />
      ) : gruplar.length === 0 ? (
        <div className="card p-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>
          {BOS_MESAJ[tab]}
        </div>
      ) : (
        <div className="space-y-4">
          {gruplar.map((grup) => {
            const hepsiSecili = grup.satirlar.every((s) => secili.has(s.id))
            return (
              <div key={grup.key} className="card overflow-hidden">
                <div
                  className="flex items-center justify-between px-4 py-3 border-b"
                  style={{ borderColor: 'var(--border)', background: 'var(--surface2)' }}
                >
                  <div className="text-sm font-semibold" style={{ color: 'var(--text)' }}>
                    🚚 {grup.ad} ({grup.satirlar.length} paket)
                  </div>
                  {secilebilir && (
                    <label className="flex items-center gap-2 text-xs cursor-pointer" style={{ color: 'var(--text-m)' }}>
                      <input
                        type="checkbox"
                        checked={hepsiSecili}
                        onChange={() => grupSec(grup.satirlar, hepsiSecili)}
                      />
                      Tümünü Seç
                    </label>
                  )}
                </div>
                <div>
                  {grup.satirlar.map((s) => (
                    <div
                      key={s.id}
                      className="flex flex-wrap items-center gap-x-4 gap-y-1 px-4 py-2.5 border-b last:border-b-0 text-sm"
                      style={{ borderColor: 'var(--border)' }}
                    >
                      {secilebilir && (
                        <input
                          type="checkbox"
                          checked={secili.has(s.id)}
                          onChange={() => satirSec(s.id)}
                        />
                      )}
                      <code className="text-xs font-mono" style={{ color: 'var(--text)' }}>{s.packageNumber}</code>
                      <Link
                        to={`/orders/${s.orderId}`}
                        className="text-xs underline"
                        style={{ color: 'var(--brand)' }}
                      >
                        Sipariş
                      </Link>
                      <span className="text-xs" style={{ color: 'var(--text-m)' }}>
                        Deneme: {s.attemptCount}
                      </span>
                      {s.lastError && (
                        <span
                          className={cn('text-xs flex-1 min-w-[160px] truncate', s.status === 'failed' && 'text-red-500')}
                          style={s.status === 'failed' ? undefined : { color: 'var(--text-s)' }}
                          title={s.lastError}
                        >
                          {s.lastError}
                        </span>
                      )}
                      <span className="text-xs ml-auto" style={{ color: 'var(--text-s)' }}>
                        {tab === 'sent'
                          ? `Gönderim: ${tarihSaat(s.sentAt)}`
                          : tarihSaat(s.createdAt)}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )
          })}
        </div>
      )}

      {/* Bilgi notu */}
      <div
        className="card p-4 mt-6 text-xs"
        style={{ color: 'var(--text-m)', background: 'var(--surface2)' }}
      >
        ℹ️ Gönderim worker'ı gerçek taşıyıcı entegrasyonları devreye alınana dek kapalıdır;
        kuyruk birikir, KG1'de otomatik gönderilir.
      </div>

      {/* Onay modalı */}
      <Modal
        open={onayAcik}
        onClose={() => setOnayAcik(false)}
        title="Yönlendirmeyi Onayla"
        size="sm"
        footer={
          <>
            <Button variant="secondary" size="sm" onClick={() => setOnayAcik(false)}>Vazgeç</Button>
            <Button size="sm" loading={yonlendir.isPending} onClick={() => yonlendir.mutate()}>Yönlendir</Button>
          </>
        }
      >
        <p className="text-sm" style={{ color: 'var(--text)' }}>
          <strong>{secili.size}</strong> paket <strong>{hedef?.label}</strong> taşıyıcısına yönlendirilecek.
          Devam edilsin mi?
        </p>
      </Modal>
    </div>
  )
}

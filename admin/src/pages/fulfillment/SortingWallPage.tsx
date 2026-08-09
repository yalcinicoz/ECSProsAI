import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useParams } from 'react-router-dom'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { errText } from '@/components/ui/DataTable'
import { useAuthStore } from '@/store/auth'

/**
 * OP3 — Koli Duvarı (sorting wall).
 * Ara ayrıştırmadaki aktif kolilerin canlı durumu: doluluk, tamamlanma rengi,
 * zimmet (paketleme masası) bilgisi. 10 sn'de bir yenilenir. Tablet kullanımı.
 */

interface KoliKart {
  boxId: string
  boxNumber: number
  generation: number
  status: 'open' | 'taken'
  takenBy?: string | null
  takenAt?: string | null
  stationNumber?: number | null
  siparisSayisi: number
  tamamSiparis: number
  girenUrun: number
  gerekenUrun: number
  tamamlanmaYuzde: number
  renk: 'green' | 'yellow' | 'red'
  sonOkutma?: string | null
}

interface Duvar {
  koliler: KoliKart[]
  kolisizSiparis: number
  kapaliKoli: number
  yesilEsik: number
  sariEsik: number
}

interface PlanDetail { id: string; planNumber: string; status: string; planType: string }
interface IamUser { id: string; username: string; firstName: string; lastName: string }

const RENK: Record<KoliKart['renk'], string> = {
  green: '#16a34a',
  yellow: '#eab308',
  red: '#dc2626',
}

const saat = (v?: string | null) =>
  v ? new Date(v).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' }) : '—'

export function SortingWallPage() {
  const { planId } = useParams<{ planId: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const me = useAuthStore((s) => s.user)
  const [alinacak, setAlinacak] = useState<KoliKart | null>(null)
  const [hata, setHata] = useState('')

  const { data: plan } = useQuery<PlanDetail>({
    queryKey: ['picking-plan-detail', planId],
    queryFn: async () => (await api.get(`/fulfillment/picking-plans/${planId}`)).data.data,
    enabled: !!planId,
  })

  const { data: duvar, isLoading } = useQuery<Duvar>({
    queryKey: ['sorting-wall', planId],
    queryFn: async () => (await api.get(`/fulfillment/sorting/${planId}/wall`)).data.data,
    enabled: !!planId,
    refetchInterval: 10_000,
  })

  const { data: users = [] } = useQuery<IamUser[]>({
    queryKey: ['iam-users-select'],
    queryFn: async () => (await api.get('/iam/users?page=1&pageSize=200')).data.data.items,
  })
  const kullaniciAd = (uid?: string | null) => {
    if (!uid) return '—'
    const u = users.find(x => x.id === uid)
    return u ? (`${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.username) : uid.slice(0, 8)
  }

  const zimmet = useMutation({
    mutationFn: async (boxId: string) => {
      setHata('')
      await api.post(`/fulfillment/sorting-boxes/${boxId}/take`, {})
    },
    onSuccess: () => {
      setAlinacak(null)
      queryClient.invalidateQueries({ queryKey: ['sorting-wall', planId] })
    },
    onError: (e: unknown) => {
      setAlinacak(null)
      setHata(errText(e))
      queryClient.invalidateQueries({ queryKey: ['sorting-wall', planId] })
    },
  })

  // OP4: zimmetli koli için masa aç (zaten açıksa mevcut masa döner) → masa ekranına git
  const masaAc = useMutation({
    mutationFn: async (boxId: string) => {
      setHata('')
      return (await api.post<{ data: { deskId: string; deskNumber: number; boxNumber: number } }>(
        `/fulfillment/sorting-boxes/${boxId}/open-desk`, {})).data.data
    },
    onSuccess: (d) => {
      navigate(`/fulfillment/desk/${d.deskId}?planId=${planId}`)
    },
    onError: (e: unknown) => {
      setHata(errText(e))
      queryClient.invalidateQueries({ queryKey: ['sorting-wall', planId] })
    },
  })

  if (isLoading || !duvar) return <PageSpinner />

  const koliler = [...duvar.koliler].sort((a, b) => a.boxNumber - b.boxNumber)

  return (
    <div className="p-4 pb-16">
      {/* ── Üst şerit: plan + özet ── */}
      <div className="card p-4 mb-4 flex items-center gap-4 flex-wrap">
        <div className="flex-1 min-w-0">
          <div className="text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Koli Duvarı</div>
          <div className="text-xl font-bold font-mono truncate" style={{ color: 'var(--text)' }}>
            {plan?.planNumber ?? planId}
          </div>
        </div>
        <div className="text-center px-4 py-1 rounded-xl" style={{ background: 'var(--surface2)' }}>
          <div className="text-3xl font-extrabold leading-none" style={{ color: 'var(--text)' }}>{koliler.length}</div>
          <div className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>aktif koli</div>
        </div>
        <div className="text-center px-4 py-1 rounded-xl" style={{ background: '#fffbeb' }}>
          <div className="text-3xl font-extrabold leading-none" style={{ color: '#b45309' }}>{duvar.kolisizSiparis}</div>
          <div className="text-xs mt-1" style={{ color: '#92400e' }}>kolisiz sipariş</div>
        </div>
        <div className="text-center px-4 py-1 rounded-xl" style={{ background: '#f0fdf4' }}>
          <div className="text-3xl font-extrabold leading-none" style={{ color: '#15803d' }}>{duvar.kapaliKoli}</div>
          <div className="text-xs mt-1" style={{ color: '#166534' }}>kapalı koli</div>
        </div>
        <Button variant="secondary" onClick={() => navigate(`/fulfillment/sorting/${planId}`)}>
          Okutma Ekranı
        </Button>
      </div>

      {hata && (
        <div className="rounded-xl p-4 mb-4 text-lg font-bold"
          style={{ background: '#fef2f2', border: '2px solid #dc2626', color: '#991b1b' }}>
          ⚠ {hata}
        </div>
      )}

      {koliler.length === 0 && (
        <div className="card p-10 text-center" style={{ color: 'var(--text-s)' }}>
          <div className="text-5xl mb-3">📦</div>
          <p className="text-lg">Henüz açık koli yok. Okutma başlayınca koliler burada belirir.</p>
        </div>
      )}

      {/* ── Koli kartları ── */}
      <div className="grid gap-4" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))' }}>
        {koliler.map(k => {
          const renk = RENK[k.renk] ?? RENK.red
          return (
            <div key={k.boxId} className="card overflow-hidden"
              style={{ borderLeft: `10px solid ${renk}` }}>
              <div className="flex items-center gap-3 p-3" style={{ borderBottom: '1px solid var(--border)' }}>
                <div className="rounded-xl px-4 py-2 text-white text-4xl font-extrabold font-mono leading-none"
                  style={{ background: renk, minWidth: 72, textAlign: 'center' }}>
                  {k.boxNumber}
                </div>
                <div className="min-w-0">
                  <div className="text-sm font-semibold" style={{ color: 'var(--text-m)' }}>(gen.{k.generation})</div>
                  {k.status === 'taken' && (
                    <span className="inline-block mt-1 px-2 py-0.5 rounded-lg text-sm font-extrabold text-white"
                      style={{ background: '#7c3aed' }}>
                      MASADA{k.stationNumber != null ? ` — Masa ${k.stationNumber}` : ''}
                    </span>
                  )}
                </div>
              </div>

              <div className="p-3 grid gap-1.5 text-lg" style={{ color: 'var(--text)' }}>
                <div className="flex justify-between">
                  <span style={{ color: 'var(--text-m)' }}>Sipariş</span>
                  <b>{k.siparisSayisi} sipariş ({k.tamamSiparis} tamam)</b>
                </div>
                <div className="flex justify-between">
                  <span style={{ color: 'var(--text-m)' }}>Ürün</span>
                  <b>{k.girenUrun} / {k.gerekenUrun}</b>
                </div>
                <div className="flex items-center gap-2">
                  <div className="flex-1 h-3 rounded-full overflow-hidden" style={{ background: 'var(--surface2)' }}>
                    <div className="h-full rounded-full transition-all"
                      style={{ width: `${Math.min(100, Math.max(0, k.tamamlanmaYuzde))}%`, background: renk }} />
                  </div>
                  <b className="text-base" style={{ color: renk }}>%{k.tamamlanmaYuzde}</b>
                </div>
                <div className="flex justify-between text-base" style={{ color: 'var(--text-s)' }}>
                  <span>Son okutma</span>
                  <span>{saat(k.sonOkutma)}</span>
                </div>
                {k.status === 'taken' && (
                  <div className="flex justify-between text-base" style={{ color: 'var(--text-s)' }}>
                    <span>Zimmet</span>
                    <span><b style={{ color: 'var(--text)' }}>{kullaniciAd(k.takenBy)}</b> · {saat(k.takenAt)}</span>
                  </div>
                )}
              </div>

              {k.status === 'open' && (
                <div className="p-3 pt-0">
                  <Button className="w-full !py-3 !text-lg" onClick={() => { setHata(''); setAlinacak(k) }}>
                    Zimmete Al
                  </Button>
                </div>
              )}

              {/* OP4: kendi zimmetindeki koli için masa aç */}
              {k.status === 'taken' && !!me && k.takenBy === me.id && (
                <div className="p-3 pt-0">
                  <Button className="w-full !py-3 !text-lg" loading={masaAc.isPending}
                    onClick={() => masaAc.mutate(k.boxId)}>
                    Masa Aç
                  </Button>
                </div>
              )}
            </div>
          )
        })}
      </div>

      {/* ── Zimmet onayı ── */}
      <Modal open={!!alinacak} onClose={() => setAlinacak(null)} title="Koliyi Zimmete Al">
        {alinacak && (
          <>
            <p className="text-lg mb-2" style={{ color: 'var(--text)' }}>
              <b>Koli {alinacak.boxNumber}</b> (gen.{alinacak.generation}) zimmetinize alınacak.
            </p>
            <p className="text-base mb-4" style={{ color: 'var(--text-m)' }}>
              {alinacak.siparisSayisi} sipariş ({alinacak.tamamSiparis} tamam) · {alinacak.girenUrun}/{alinacak.gerekenUrun} ürün.
              Paketleme bitene kadar koli sizde kalır.
            </p>
            <div className="flex justify-end gap-2 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
              <Button variant="secondary" className="!py-3 !text-lg" onClick={() => setAlinacak(null)}>Vazgeç</Button>
              <Button className="!py-3 !text-lg" loading={zimmet.isPending}
                onClick={() => zimmet.mutate(alinacak.boxId)}>
                Zimmete Al
              </Button>
            </div>
          </>
        )}
      </Modal>
    </div>
  )
}

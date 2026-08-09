import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import api from '@/api/client'
import { PageSpinner } from '@/components/ui/Spinner'

/**
 * OP4 — Masa İzleme.
 * Tüm açık masaların canlı durumu: koli, personel, ilerleme, slot doluluğu.
 * 10 sn'de bir yenilenir; kart tıklanınca masa ekranına gidilir. Tablet kullanımı.
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

interface IamUser { id: string; username: string; firstName: string; lastName: string }

const saat = (v?: string | null) =>
  v ? new Date(v).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' }) : '—'

export function DesksMonitorPage() {
  const navigate = useNavigate()

  const { data: masalar, isLoading } = useQuery<Desk[]>({
    queryKey: ['packing-desks-monitor'],
    queryFn: async () => (await api.get('/fulfillment/desks')).data.data,
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

  if (isLoading || !masalar) return <PageSpinner />

  const sirali = [...masalar].sort((a, b) => a.deskNumber - b.deskNumber)
  const toplamPaketlenen = sirali.reduce((t, m) => t + m.paketlenen, 0)
  const toplamSiparis = sirali.reduce((t, m) => t + m.koliSiparis, 0)

  return (
    <div className="p-4 pb-16">
      {/* ── Üst şerit: özet ── */}
      <div className="card p-4 mb-4 flex items-center gap-4 flex-wrap">
        <div className="flex-1 min-w-0">
          <div className="text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Masa İzleme</div>
          <div className="text-xl font-bold" style={{ color: 'var(--text)' }}>Açık Paketleme Masaları</div>
        </div>
        <div className="text-center px-4 py-1 rounded-xl" style={{ background: 'var(--surface2)' }}>
          <div className="text-3xl font-extrabold leading-none" style={{ color: 'var(--text)' }}>{sirali.length}</div>
          <div className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>açık masa</div>
        </div>
        <div className="text-center px-4 py-1 rounded-xl" style={{ background: '#f0fdf4' }}>
          <div className="text-3xl font-extrabold leading-none" style={{ color: '#15803d' }}>
            {toplamPaketlenen}<span className="text-xl" style={{ color: '#166534' }}>/{toplamSiparis}</span>
          </div>
          <div className="text-xs mt-1" style={{ color: '#166534' }}>paketlenen / sipariş</div>
        </div>
      </div>

      {sirali.length === 0 && (
        <div className="card p-10 text-center" style={{ color: 'var(--text-s)' }}>
          <div className="text-5xl mb-3">🛠️</div>
          <p className="text-lg">Şu anda açık masa yok. Koli duvarından "Masa Aç" ile masa açılır.</p>
        </div>
      )}

      {/* ── Masa kartları ── */}
      <div className="grid gap-4" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))' }}>
        {sirali.map(m => {
          const yuzde = m.koliSiparis > 0 ? Math.round((m.paketlenen / m.koliSiparis) * 100) : 0
          const doluSlot = m.slotlar?.length ?? 0
          return (
            <button
              key={m.deskId}
              type="button"
              onClick={() => navigate(`/fulfillment/desk/${m.deskId}`)}
              className="card overflow-hidden text-left transition-all hover:shadow-lg"
              style={{ cursor: 'pointer' }}
            >
              <div className="flex items-center gap-3 p-3" style={{ borderBottom: '1px solid var(--border)' }}>
                <div className="rounded-xl px-4 py-2 text-white text-3xl font-extrabold leading-none"
                  style={{ background: 'var(--brand)', minWidth: 96, textAlign: 'center' }}>
                  MASA {m.deskNumber}
                </div>
                <div className="min-w-0">
                  <div className="text-lg font-extrabold" style={{ color: 'var(--text)' }}>Koli {m.boxNumber}</div>
                  <div className="text-sm truncate" style={{ color: 'var(--text-m)' }}>{kullaniciAd(m.openedBy)}</div>
                </div>
              </div>

              <div className="p-3 grid gap-1.5 text-lg" style={{ color: 'var(--text)' }}>
                <div className="flex justify-between">
                  <span style={{ color: 'var(--text-m)' }}>Paketlenen</span>
                  <b>{m.paketlenen} / {m.koliSiparis} sipariş</b>
                </div>
                <div className="flex items-center gap-2">
                  <div className="flex-1 h-3 rounded-full overflow-hidden" style={{ background: 'var(--surface2)' }}>
                    <div className="h-full rounded-full transition-all"
                      style={{ width: `${Math.min(100, Math.max(0, yuzde))}%`, background: '#16a34a' }} />
                  </div>
                  <b className="text-base" style={{ color: '#16a34a' }}>%{yuzde}</b>
                </div>
                <div className="flex justify-between">
                  <span style={{ color: 'var(--text-m)' }}>Dolu slot</span>
                  <b>{doluSlot}</b>
                </div>
                {m.obmSayisi > 0 && (
                  <div className="flex justify-between">
                    <span style={{ color: 'var(--text-m)' }}>OBM</span>
                    <b style={{ color: '#b45309' }}>{m.obmSayisi}</b>
                  </div>
                )}
                <div className="flex justify-between text-base" style={{ color: 'var(--text-s)' }}>
                  <span>Açılış</span>
                  <span>{saat(m.openedAt)}</span>
                </div>
                <div className="flex justify-between text-base" style={{ color: 'var(--text-s)' }}>
                  <span>Son işlem</span>
                  <span>{saat(m.sonIslem)}</span>
                </div>
              </div>
            </button>
          )
        })}
      </div>
    </div>
  )
}

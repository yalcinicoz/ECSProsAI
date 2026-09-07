// Müşteri İlişkileri bildirim çanı (2026-09-07, K2): bekleyen (görülmemiş ya da kayda girilmemiş) bildirim varsa çan
// yanıp söner; liste açılınca listelenenler "gördü" olur; tıklanınca "kayda girdi" işlenir ve kayda gidilir.
// Bildirim gördü ama kayda girmediyse listede kırmızı "kayda girmediniz" yazar ve sayaçta kalır.
import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Bell } from 'lucide-react'
import api from '@/api/client'
import { useTicketAlertStore } from '@/store/ticketAlerts'

interface Bildirim { id: string; ticketId: string; trackingNo: number; kind: string; message: string; createdAt: string; seenAt: string | null; openedAt: string | null }

export function TicketBell() {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const pending = useTicketAlertStore((s) => s.pendingCount)
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  const { data } = useQuery<{ items: Bildirim[]; totalCount: number; pendingCount: number }>({
    queryKey: ['ticket-notifications', 'bell'],
    queryFn: async () => (await api.get('/crm/tickets/notifications', { params: { pageSize: 15 } })).data.data,
    enabled: open,
  })

  // liste açıkken görülmemişleri "gördü" işaretle (eski: satır render edilince Goruldu=2)
  useEffect(() => {
    if (!open || !data) return
    const ids = data.items.filter((n) => !n.seenAt).map((n) => n.id)
    if (ids.length === 0) return
    api.post('/crm/tickets/notifications/seen', ids).then(() => {
      qc.invalidateQueries({ queryKey: ['ticket-notifications-pending'] })
    }).catch(() => {})
  }, [open, data, qc])

  useEffect(() => {
    if (!open) return
    const h = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false) }
    document.addEventListener('mousedown', h)
    return () => document.removeEventListener('mousedown', h)
  }, [open])

  async function ac(n: Bildirim) {
    setOpen(false)
    try { await api.post(`/crm/tickets/notifications/${n.id}/opened`) } catch { /* kayda yine gidilir */ }
    qc.invalidateQueries({ queryKey: ['ticket-notifications'] })
    qc.invalidateQueries({ queryKey: ['ticket-notifications-pending'] })
    navigate(`/crm/tickets/${n.trackingNo}`)
  }

  return (
    <div className="relative" ref={ref}>
      <style>{`@keyframes msBellBlink{0%,100%{opacity:1}50%{opacity:.25}} .ms-bell-blink{animation:msBellBlink 1s infinite}`}</style>
      <button type="button" onClick={() => setOpen((o) => !o)}
        className="relative w-9 h-9 flex items-center justify-center rounded-xl transition-colors"
        style={{ color: pending > 0 ? '#ef4444' : 'var(--text-m)' }}
        onMouseEnter={(e) => (e.currentTarget.style.background = 'var(--surface2)')}
        onMouseLeave={(e) => (e.currentTarget.style.background = '')}
        title={pending > 0 ? `${pending} bekleyen Müşteri İlişkileri bildirimi` : 'Bildirimler'}>
        <Bell size={16} className={pending > 0 ? 'ms-bell-blink' : undefined} />
        {pending > 0 && (
          <span className="absolute -top-0.5 -right-0.5 min-w-[16px] h-4 px-1 rounded-full text-[10px] font-bold flex items-center justify-center" style={{ background: '#ef4444', color: '#fff' }}>{pending > 99 ? '99+' : pending}</span>
        )}
      </button>
      {open && (
        <div className="absolute right-0 mt-1 w-[380px] max-w-[calc(100vw-2rem)] rounded-xl shadow-xl z-50 overflow-hidden" style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}>
          <div className="px-3 py-2 text-xs font-semibold flex items-center justify-between" style={{ borderBottom: '1px solid var(--border)', color: 'var(--text)' }}>
            Müşteri İlişkileri bildirimleri
            <button className="underline font-normal" style={{ color: 'var(--text-s)' }} onClick={() => { setOpen(false); navigate('/crm/tickets?unreadByMe=true') }}>okumadıklarım</button>
          </div>
          <div className="max-h-[420px] overflow-y-auto">
            {(data?.items ?? []).map((n) => {
              const bekliyor = !n.openedAt
              return (
                <button key={n.id} type="button" onClick={() => ac(n)} className="w-full text-left px-3 py-2 hover:opacity-90"
                  style={{ borderBottom: '1px solid var(--border)', background: bekliyor ? '#ef44440d' : undefined }}>
                  <div className="text-sm" style={{ color: 'var(--text)' }}>{n.message}</div>
                  <div className="text-[11px] mt-0.5 flex items-center gap-2" style={{ color: 'var(--text-s)' }}>
                    <span>{new Date(n.createdAt).toLocaleString('tr-TR')}</span>
                    {n.openedAt ? <span style={{ color: '#15803d' }}>kayda girdiniz</span>
                      : n.seenAt ? <span className="font-semibold" style={{ color: '#ef4444' }}>gördünüz, kayda girmediniz</span>
                      : <span className="font-semibold" style={{ color: '#ef4444' }}>yeni</span>}
                  </div>
                </button>
              )
            })}
            {data && data.items.length === 0 && <div className="px-3 py-6 text-center text-xs" style={{ color: 'var(--text-s)' }}>Bildirim yok.</div>}
            {!data && <div className="px-3 py-6 text-center text-xs" style={{ color: 'var(--text-s)' }}>Yükleniyor…</div>}
          </div>
        </div>
      )}
    </div>
  )
}

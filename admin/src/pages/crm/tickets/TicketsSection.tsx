// Üye / sipariş detayına gömülü "Müşteri İlişkileri" bloğu: ilgili kayıtlar + yeni kayıt kısayolu (sipariş no ön dolu).
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { fmtTarih, type TicketPage } from './ticketShared'

export function TicketsSection({ memberId, orderId, orderNumber }: { memberId?: string; orderId?: string; orderNumber?: string }) {
  const { data } = useQuery<TicketPage>({
    queryKey: ['tickets', { memberId, orderId, embed: true }],
    queryFn: async () => (await api.get('/crm/tickets', { params: { memberId, orderId, pageSize: 10, includeHidden: true } })).data.data,
    enabled: !!(memberId || orderId),
  })
  return (
    <div className="card p-4">
      <div className="flex items-center justify-between mb-3">
        <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>Müşteri İlişkileri ({data?.totalCount ?? 0})</h2>
        <Link to={`/crm/tickets/new${orderNumber ? `?orderNumber=${encodeURIComponent(orderNumber)}` : ''}`} className="text-xs underline" style={{ color: 'var(--brand)' }}>+ Yeni kayıt</Link>
      </div>
      {(data?.items ?? []).length === 0 && <p className="text-xs" style={{ color: 'var(--text-s)' }}>Kayıt yok.</p>}
      {(data?.items ?? []).map((t) => (
        <Link key={t.id} to={`/crm/tickets/${t.trackingNo}`} className="block text-xs py-1.5 hover:underline" style={{ borderTop: '1px solid var(--border)', color: 'var(--text)' }}>
          <span className="font-mono">{t.trackingNo}</span> · {t.subjectName} · <span style={{ color: t.statusColor }}>{t.statusName}</span> · {t.createdByName} · <span style={{ color: 'var(--text-s)' }}>{fmtTarih(t.createdAt)}</span>
        </Link>
      ))}
    </div>
  )
}

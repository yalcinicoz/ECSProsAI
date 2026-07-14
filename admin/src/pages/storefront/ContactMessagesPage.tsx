import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { cn } from '@/lib/utils'
import { useFirmPlatforms } from '@/pages/cms/CmsPagesPage'

interface ContactMessage {
  id: string
  firmPlatformId: string
  memberId?: string
  name: string
  email: string
  phone?: string
  subject?: string
  message: string
  status: string
  createdAt: string
}

interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

const TABS = [
  { key: 'new',  label: 'Yeni' },
  { key: 'read', label: 'Okundu' },
  { key: '',     label: 'Tümü' },
]

function MessageModal({ msg, platformName, onClose }: {
  msg: ContactMessage
  platformName: (pid?: string) => string
  onClose: () => void
}) {
  const queryClient = useQueryClient()
  const setStatus = useMutation({
    mutationFn: async (status: string) =>
      api.patch(`/contact-messages/${msg.id}/status`, { status }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['contact-messages'] }),
  })

  return (
    <Modal open onClose={onClose} title={msg.subject?.trim() || 'İletişim Mesajı'}>
      <div className="space-y-3">
        <div className="grid grid-cols-2 gap-3 text-sm">
          <div>
            <div className="flbl">Gönderen</div>
            <div style={{ color: 'var(--text)' }}>{msg.name}</div>
          </div>
          <div>
            <div className="flbl">Tarih</div>
            <div style={{ color: 'var(--text)' }}>{new Date(msg.createdAt).toLocaleString('tr-TR')}</div>
          </div>
          <div>
            <div className="flbl">E-posta</div>
            <a href={`mailto:${msg.email}`} className="underline" style={{ color: 'var(--brand)' }}>{msg.email}</a>
          </div>
          <div>
            <div className="flbl">Telefon</div>
            <div style={{ color: 'var(--text)' }}>{msg.phone || '—'}</div>
          </div>
          <div>
            <div className="flbl">Platform</div>
            <div style={{ color: 'var(--text)' }}>{platformName(msg.firmPlatformId)}</div>
          </div>
          <div>
            <div className="flbl">Üye</div>
            <div style={{ color: 'var(--text)' }}>
              {msg.memberId ? <code className="text-xs">{msg.memberId.slice(0, 8)}…</code> : 'Misafir'}
            </div>
          </div>
        </div>
        <div>
          <div className="flbl">Mesaj</div>
          <div className="text-sm whitespace-pre-wrap rounded-lg p-3"
            style={{ background: 'var(--surface2)', color: 'var(--text)' }}>
            {msg.message}
          </div>
        </div>
      </div>
      <div className="flex justify-between gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        <Button variant="secondary" size="sm" loading={setStatus.isPending}
          onClick={() => setStatus.mutate(msg.status === 'read' ? 'new' : 'read')}>
          {msg.status === 'read' ? 'Yeni olarak işaretle' : 'Okundu işaretle'}
        </Button>
        <Button variant="secondary" onClick={onClose}>Kapat</Button>
      </div>
    </Modal>
  )
}

export function ContactMessagesPage() {
  const [tab, setTab] = useState('new')
  const [platformId, setPlatformId] = useState('')
  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<ContactMessage | null>(null)

  const queryClient = useQueryClient()
  const { data: platforms = [] } = useFirmPlatforms()
  const platformName = (pid?: string) =>
    platforms.find(p => p.id === pid)?.nameI18n?.['tr'] ?? '—'

  const { data, isLoading } = useQuery<PagedResult<ContactMessage>>({
    queryKey: ['contact-messages', tab, platformId, appliedSearch, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (tab) params.set('status', tab)
      if (platformId) params.set('firmPlatformId', platformId)
      if (appliedSearch) params.set('search', appliedSearch)
      return (await api.get(`/contact-messages?${params}`)).data.data
    },
  })

  // Gelen kutusu davranışı: yeni mesaj açılınca otomatik okundu olur.
  const markRead = useMutation({
    mutationFn: async (id: string) => api.patch(`/contact-messages/${id}/status`, { status: 'read' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['contact-messages'] }),
  })

  function openMessage(m: ContactMessage) {
    setSelected(m)
    if (m.status === 'new') markRead.mutate(m.id)
  }

  const messages = data?.items ?? []
  const totalPages = Math.ceil((data?.totalCount ?? 0) / 20)

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>İletişim Mesajları</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {data?.totalCount ?? 0} kayıt — site iletişim formundan gelen mesajlar
          </p>
        </div>
        <select className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 180 }}
          value={platformId} onChange={e => { setPlatformId(e.target.value); setPage(1) }}>
          <option value="">Tüm platformlar</option>
          {platforms.map(p => (
            <option key={p.id} value={p.id}>{p.nameI18n?.['tr'] ?? p.id}</option>
          ))}
        </select>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABS.map(t => (
          <button key={t.key} className={cn('stab', tab === t.key && 'active')}
            onClick={() => { setTab(t.key); setPage(1) }}>{t.label}</button>
        ))}
      </div>

      <div className="flex items-center gap-2 mb-4">
        <input className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 220 }}
          placeholder="Ad, e-posta veya konu ara…" value={search}
          onChange={e => setSearch(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') { setAppliedSearch(search.trim()); setPage(1) } }} />
        <button onClick={() => { setAppliedSearch(search.trim()); setPage(1) }}
          className="px-3 py-1.5 rounded-lg text-sm"
          style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Ara</button>
      </div>

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['TARİH', 'GÖNDEREN', 'E-POSTA', 'KONU', 'PLATFORM', 'DURUM'].map(h => (
                <th key={h} className="px-4 py-3 text-xs font-semibold text-left"
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={6} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
            )}
            {!isLoading && messages.length === 0 && (
              <tr><td colSpan={6} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                {tab === 'new' ? 'Okunmamış mesaj yok.' : 'Mesaj yok.'}
              </td></tr>
            )}
            {messages.map(m => (
              <tr key={m.id} onClick={() => openMessage(m)}
                className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3 text-xs whitespace-nowrap" style={{ color: 'var(--text-s)' }}>
                  {new Date(m.createdAt).toLocaleString('tr-TR')}
                </td>
                <td className={cn('px-4 py-3 text-sm', m.status === 'new' && 'font-semibold')}
                  style={{ color: 'var(--text)' }}>{m.name}</td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>{m.email}</td>
                <td className={cn('px-4 py-3 text-sm max-w-xs truncate', m.status === 'new' && 'font-medium')}
                  style={{ color: 'var(--text)' }}>
                  {m.subject?.trim() || <span style={{ color: 'var(--text-s)' }}>{m.message.slice(0, 60)}…</span>}
                </td>
                <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>{platformName(m.firmPlatformId)}</td>
                <td className="px-4 py-3">
                  <Badge variant={m.status === 'new' ? 'warning' : 'neutral'}>
                    {m.status === 'new' ? 'Yeni' : 'Okundu'}
                  </Badge>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 mt-4">
          <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}
            className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>← Önceki</button>
          <span className="text-sm" style={{ color: 'var(--text-s)' }}>{page} / {totalPages}</span>
          <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}
            className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Sonraki →</button>
        </div>
      )}

      {selected && (
        <MessageModal
          msg={selected}
          platformName={platformName}
          onClose={() => setSelected(null)}
        />
      )}
    </div>
  )
}

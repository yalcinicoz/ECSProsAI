import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { cn } from '@/lib/utils'
import { useFirmPlatforms } from '@/pages/cms/cmsPageShared'

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
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (ContactMessageGrid.Schema) + Excel + görünümler.
  // Y3: kanal kapsamı sunucuda kullanıcının yetkisinden çözülür.
  const [sp] = useSearchParams()
  const tab = sp.get('status') ?? 'new'
  const platformId = sp.get('firmPlatformId') ?? ''
  const grid = useGridState('contact-messages', { defaultPageSize: 20, defaultSort: 'createdAt', defaultDir: 'desc' })
  const [selected, setSelected] = useState<ContactMessage | null>(null)

  const queryClient = useQueryClient()
  const { data: platforms = [] } = useFirmPlatforms()
  const platformName = (pid?: string) =>
    platforms.find(p => p.id === pid)?.nameI18n?.['tr'] ?? '—'

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult<ContactMessage>>({
    queryKey: ['contact-messages', tab, platformId, ...grid.queryKey],
    queryFn: async () =>
      (await api.get(`/contact-messages?${grid.toParams({ status: tab || undefined, firmPlatformId: platformId || undefined })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
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

  const columns: GridColumn<ContactMessage>[] = [
    { key: 'createdAt', header: 'TARİH', priority: 1, frozen: true, sortable: true, minWidth: 150,
      filter: { type: 'date', label: 'Tarih', quick: true },
      cell: m => <span className="text-xs whitespace-nowrap" style={{ color: 'var(--text-s)' }}>
        {new Date(m.createdAt).toLocaleString('tr-TR')}</span> },
    { key: 'name', header: 'GÖNDEREN', priority: 1, lockVisible: true, frozen: true, sortable: true,
      filter: { type: 'text', label: 'Gönderen' },
      filters: [{ field: 'isMember', label: 'Üye mesajı', type: 'boolean' }],
      cell: m => <span className={cn('text-sm', m.status === 'new' && 'font-semibold')} style={{ color: 'var(--text)' }}>{m.name}</span> },
    { key: 'email', header: 'E-POSTA', priority: 2, sortable: true, filter: { type: 'text', label: 'E-posta' },
      filters: [{ field: 'phone', label: 'Telefon', type: 'text' }, { field: 'hasPhone', label: 'Telefonu olan', type: 'boolean' }],
      cell: m => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{m.email}</span> },
    { key: 'subject', header: 'KONU', priority: 1, sortable: true, minWidth: 240,
      filter: { type: 'text', label: 'Konu' },
      filters: [{ field: 'message', label: 'Mesaj metni', type: 'text' }],
      cell: m => <span className={cn('text-sm truncate block', m.status === 'new' && 'font-medium')}
        style={{ color: 'var(--text)', maxWidth: 320 }}>
        {m.subject?.trim() || <span style={{ color: 'var(--text-s)' }}>{m.message.slice(0, 60)}…</span>}</span> },
    { key: 'firmPlatformId', header: 'PLATFORM', priority: 2, sortable: false,
      filter: { type: 'enum', label: 'Platform', options: platforms.map(p => ({ value: p.id, label: p.nameI18n?.['tr'] ?? p.id })) },
      cell: m => <span className="text-xs" style={{ color: 'var(--text-s)' }}>{platformName(m.firmPlatformId)}</span> },
    { key: 'status', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Durum', options: [
        { value: 'new', label: 'Yeni' }, { value: 'read', label: 'Okundu' },
        { value: 'answered', label: 'Yanıtlandı' }, { value: 'archived', label: 'Arşiv' }] },
      cell: m => <Badge variant={m.status === 'new' ? 'warning' : 'neutral'}>
        {m.status === 'new' ? 'Yeni' : m.status === 'answered' ? 'Yanıtlandı' : m.status === 'archived' ? 'Arşiv' : 'Okundu'}</Badge> },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>İletişim Mesajları</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''} — site iletişim formundan gelen mesajlar
          </p>
        </div>
        <select className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 180 }} aria-label="Platform"
          value={platformId}
          onChange={e => grid.mutate(n => { if (e.target.value) n.set('firmPlatformId', e.target.value); else n.delete('firmPlatformId') })}>
          <option value="">Tüm platformlar</option>
          {platforms.map(p => (
            <option key={p.id} value={p.id}>{p.nameI18n?.['tr'] ?? p.id}</option>
          ))}
        </select>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABS.map(t => (
          <button key={t.key} className={cn('stab', tab === t.key && 'active')}
            onClick={() => grid.mutate(n => { if (t.key) n.set('status', t.key); else n.delete('status') })}>{t.label}</button>
        ))}
      </div>

      <DataGrid<ContactMessage>
        gridId="contact-messages"
        views
        grid={grid}
        columns={columns}
        rows={messages}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        onRowClick={openMessage}
        empty={tab === 'new' ? 'Okunmamış mesaj yok.' : 'Mesaj yok.'}
        search={{ placeholder: 'Ad, e-posta veya konu ara…' }}
        minWidth={1000}
        export={{ endpoint: '/contact-messages/export', named: () => ({ status: tab || undefined }), fallbackFileName: 'iletisim-mesajlari.xlsx' }}
        compact={{
          title: m => m.name,
          subtitle: m => m.subject?.trim() || m.message.slice(0, 60),
          right: m => new Date(m.createdAt).toLocaleDateString('tr-TR'),
          badge: m => <Badge variant={m.status === 'new' ? 'warning' : 'neutral'}>{m.status === 'new' ? 'Yeni' : 'Okundu'}</Badge>,
        }}
      />

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

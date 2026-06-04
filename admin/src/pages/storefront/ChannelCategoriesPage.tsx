import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useQueries, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, AlertTriangle } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { PageSpinner } from '@/components/ui/Spinner'

interface Firm {
  id: string
  nameI18n: Record<string, string>
}

interface Channel {
  id: string
  nameI18n: Record<string, string>
  code: string
  firmId: string
  firmName: string
}

function getChannelLabel(ch: Channel): string {
  return ch.nameI18n?.['tr'] ?? ch.nameI18n?.[Object.keys(ch.nameI18n)[0]] ?? ch.code
}

interface ChannelCategoryItem {
  id: string
  parentId: string | null
  nameI18n: Record<string, string>
  slug: string
  status: string
  fillType: string
  sortOrder: number
  displayImageUrl: string | null
  badgeLabel: string | null
  productGroupCount: number
}

const STATUS_LABELS: Record<string, { label: string; variant: 'success' | 'warning' | 'neutral' }> = {
  published: { label: 'Yayında',   variant: 'success' },
  draft:     { label: 'Taslak',    variant: 'warning' },
  archived:  { label: 'Arşiv',     variant: 'neutral' },
}

const FILL_LABELS: Record<string, string> = {
  manual: 'Manuel',
  filter: 'Filtre',
  mixed:  'Karma',
}

function getName(i18n: Record<string, string>): string {
  return i18n['tr'] ?? i18n[Object.keys(i18n)[0]] ?? '—'
}

export function ChannelCategoriesPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [selectedChannelId, setSelectedChannelId] = useState<string>('')
  const [createOpen, setCreateOpen] = useState(false)
  const [newSlug, setNewSlug] = useState('')
  const [newName, setNewName] = useState('')

  const { data: firms = [], isLoading: firmsLoading } = useQuery<Firm[]>({
    queryKey: ['firms'],
    queryFn: async () => {
      const { data } = await api.get('/core/firms')
      return data.data ?? []
    },
  })

  const platformQueries = useQueries({
    queries: firms.map(firm => ({
      queryKey: ['firm-platforms', firm.id],
      queryFn: async (): Promise<Channel[]> => {
        const { data } = await api.get(`/core/firms/${firm.id}/platforms`)
        const firmName = firm.nameI18n?.['tr'] ?? firm.nameI18n?.[Object.keys(firm.nameI18n)[0]] ?? ''
        return (data.data ?? []).map((ch: Channel) => ({ ...ch, firmId: firm.id, firmName }))
      },
      enabled: firms.length > 0,
    })),
  })

  const channels: Channel[] = platformQueries.flatMap(q => q.data ?? [])
  const chLoading = firmsLoading || platformQueries.some(q => q.isLoading)

  const channelOptions = channels.map(c => ({
    value: c.id,
    label: `${getChannelLabel(c)} (${c.firmName})`,
  }))

  const { data: categories = [], isLoading: catLoading } = useQuery<ChannelCategoryItem[]>({
    queryKey: ['channel-categories', selectedChannelId],
    queryFn: async () => {
      const { data } = await api.get(`/navigation/channel-categories?firmPlatformId=${selectedChannelId}`)
      return data.data ?? []
    },
    enabled: !!selectedChannelId,
  })

  const createMutation = useMutation({
    mutationFn: async () => {
      const { data } = await api.post('/navigation/channel-categories', {
        firmPlatformId: selectedChannelId,
        nameI18n: { tr: newName },
        slug: newSlug,
        fillType: 'manual',
      })
      return data.data.id as string
    },
    onSuccess: (id) => {
      queryClient.invalidateQueries({ queryKey: ['channel-categories', selectedChannelId] })
      setCreateOpen(false)
      setNewName('')
      setNewSlug('')
      navigate(`/storefront/channel-categories/${id}`)
    },
  })

  // Coverage hesapla: tüm gruplar kapsanıyor mu?
  const uncoveredCount = categories.filter(c => c.productGroupCount === 0 && c.status === 'published').length

  if (chLoading) return <PageSpinner />

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Kanal Kategorileri</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            Kanala özgü ürün listeleme kategorileri — filtre, menü ve banner için
          </p>
        </div>
        <Button onClick={() => setCreateOpen(true)} disabled={!selectedChannelId}>
          <Plus size={14} /> Yeni Kategori
        </Button>
      </div>

      {/* Kanal seçici */}
      <div className="card mb-6">
        <label className="flbl mb-2">Satış Kanalı</label>
        <SearchableSelect
          value={selectedChannelId}
          onChange={(v) => v && setSelectedChannelId(v)}
          options={channelOptions}
          placeholder="Kanal seçin…"
          hasValue={!!selectedChannelId}
        />
      </div>

      {/* Coverage uyarısı */}
      {uncoveredCount > 0 && (
        <div className="flex items-center gap-2 px-4 py-3 rounded-xl mb-4 text-sm"
          style={{ background: '#fef9c3', color: '#854d0e', border: '1px solid #fde68a' }}>
          <AlertTriangle size={15} />
          <span>
            <strong>{uncoveredCount}</strong> yayındaki kategori henüz hiçbir ürün grubundan sorumlu değil.
          </span>
        </div>
      )}

      {/* Tablo */}
      {selectedChannelId && (
        <div className="card overflow-hidden p-0">
          <table className="w-full">
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
                <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Kategori</th>
                <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Dolum</th>
                <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Gruplar</th>
                <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Durum</th>
              </tr>
            </thead>
            <tbody>
              {catLoading && (
                <tr><td colSpan={4} className="py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</td></tr>
              )}
              {!catLoading && categories.length === 0 && (
                <tr><td colSpan={4} className="py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Bu kanalda henüz kategori yok</td></tr>
              )}
              {categories.map(cat => {
                const st = STATUS_LABELS[cat.status] ?? { label: cat.status, variant: 'neutral' as const }
                return (
                  <tr
                    key={cat.id}
                    onClick={() => navigate(`/storefront/channel-categories/${cat.id}`)}
                    className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                    style={{ borderBottom: '1px solid var(--border)' }}
                  >
                    <td className="px-4 py-3">
                      <div className="font-medium text-sm" style={{ color: 'var(--text)' }}>
                        {getName(cat.nameI18n)}
                        {cat.badgeLabel && (
                          <span className="ml-2 text-xs px-1.5 py-0.5 rounded-full"
                            style={{ background: 'var(--brand-bg)', color: 'var(--brand)' }}>
                            {cat.badgeLabel}
                          </span>
                        )}
                      </div>
                      <code className="text-xs" style={{ color: 'var(--text-s)' }}>{cat.slug}</code>
                    </td>
                    <td className="px-4 py-3 text-center text-sm" style={{ color: 'var(--text-m)' }}>
                      {FILL_LABELS[cat.fillType] ?? cat.fillType}
                    </td>
                    <td className="px-4 py-3 text-center">
                      {cat.productGroupCount === 0
                        ? <span className="text-xs" style={{ color: '#f59e0b' }}>⚠ Tanımsız</span>
                        : <span className="text-sm" style={{ color: 'var(--text-m)' }}>{cat.productGroupCount}</span>
                      }
                    </td>
                    <td className="px-4 py-3 text-center">
                      <Badge variant={st.variant}>{st.label}</Badge>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      {/* Create Modal */}
      <Modal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        title="Yeni Kanal Kategorisi"
        footer={
          <>
            <Button variant="secondary" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button
              onClick={() => createMutation.mutate()}
              loading={createMutation.isPending}
              disabled={!newName.trim() || !newSlug.trim()}
            >
              Oluştur
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <div>
            <label className="flbl">Ad (TR)</label>
            <input
              type="text"
              value={newName}
              onChange={(e) => {
                setNewName(e.target.value)
                if (!newSlug) {
                  setNewSlug(e.target.value.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, ''))
                }
              }}
              className="w-full px-3 py-2 rounded-xl text-sm"
              style={{ background: 'var(--surface2)', border: '1px solid var(--border)', color: 'var(--text)' }}
              placeholder="Örn: Erkek Spor"
            />
          </div>
          <div>
            <label className="flbl">Slug</label>
            <input
              type="text"
              value={newSlug}
              onChange={(e) => setNewSlug(e.target.value)}
              className="w-full px-3 py-2 rounded-xl text-sm font-mono"
              style={{ background: 'var(--surface2)', border: '1px solid var(--border)', color: 'var(--text)' }}
              placeholder="erkek-spor"
            />
          </div>
        </div>
      </Modal>
    </div>
  )
}

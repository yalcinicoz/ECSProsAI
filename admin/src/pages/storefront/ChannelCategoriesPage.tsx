import { useState, useEffect } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useQuery, useQueries, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, AlertTriangle } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
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
  // 2026-09-09 (DataGrid): kategoriye bağlı ürün sayısı + oluşturma (kolon/sıralama)
  productCount: number
  createdAt: string
}

interface Sayfali<T> { items: T[]; totalCount: number; page: number; pageSize: number }

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

function slugify(text: string): string {
  const map: Record<string, string> = {
    'ğ':'g','Ğ':'g','ü':'u','Ü':'u','ş':'s','Ş':'s',
    'ı':'i','İ':'i','ö':'o','Ö':'o','ç':'c','Ç':'c',
  }
  return text
    .split('').map(c => map[c] ?? c).join('')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
}

export function ChannelCategoriesPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (ChannelCategoryGrid.Schema) + Excel + görünümler.
  // ★ Ayrı uç: /navigation/channel-categories TAM liste döner (Menü Yerleşimi + Ürün Kartı ekranları);
  // bu ekran sayfalı /channel-categories/grid kullanır. Kanal seçimi URL'de (paylaşılabilir link).
  const [sp] = useSearchParams()
  const grid = useGridState('channel-categories', { defaultPageSize: 30, defaultSort: 'sortOrder', defaultDir: 'asc' })
  const selectedChannelId = sp.get('firmPlatformId') ?? (sessionStorage.getItem('channelCategories.channelId') ?? '')

  useEffect(() => {
    if (selectedChannelId)
      sessionStorage.setItem('channelCategories.channelId', selectedChannelId)
  }, [selectedChannelId])
  const [createOpen, setCreateOpen] = useState(false)
  const [newSlug, setNewSlug] = useState('')
  const [newName, setNewName] = useState('')
  const [slugEdited, setSlugEdited] = useState(false)

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

  const { data, isLoading: catLoading, isFetching, error: listError } = useQuery<Sayfali<ChannelCategoryItem>>({
    queryKey: ['channel-categories-grid', selectedChannelId, ...grid.queryKey],
    queryFn: async () =>
      (await api.get(`/navigation/channel-categories/grid?${grid.toParams({ firmPlatformId: selectedChannelId })}`)).data.data,
    enabled: !!selectedChannelId,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })
  const categories = data?.items ?? []

  // "Tanımsız" uyarısı artık SUNUCUDAN sayılır: sayfalı listede istemci tarafı sayım yanlış olurdu
  // (yalnız açık sayfayı görür). Şemadaki `tanimsiz` filtresi = yayında + hiçbir ürün grubu yok.
  const { data: tanimsizSayim } = useQuery<Sayfali<ChannelCategoryItem>>({
    queryKey: ['channel-categories-uncovered', selectedChannelId],
    queryFn: async () => (await api.get(
      `/navigation/channel-categories/grid?firmPlatformId=${selectedChannelId}&pageSize=1&f.tanimsiz=eq:true`)).data.data,
    enabled: !!selectedChannelId,
    staleTime: 60_000,
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
      setSlugEdited(false)
      navigate(`/storefront/channel-categories/${id}`)
    },
  })

  const uncoveredCount = tanimsizSayim?.totalCount ?? 0

  const columns: GridColumn<ChannelCategoryItem>[] = [
    { key: 'name', header: 'KATEGORİ', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 260,
      filter: { type: 'text', label: 'Kategori adı' },
      filters: [{ field: 'slug', label: 'Slug', type: 'text' }, { field: 'badgeLabel', label: 'Rozet', type: 'text' },
                { field: 'isRoot', label: 'Üst seviye', type: 'boolean' }],
      cell: cat => <div>
        <div className="font-medium text-sm" style={{ color: 'var(--text)' }}>
          {getName(cat.nameI18n)}
          {cat.badgeLabel && (
            <span className="ml-2 text-xs px-1.5 py-0.5 rounded-full"
              style={{ background: 'var(--brand-bg)', color: 'var(--brand)' }}>{cat.badgeLabel}</span>
          )}
        </div>
        <code className="text-xs" style={{ color: 'var(--text-s)' }}>{cat.slug}</code>
      </div> },
    { key: 'fillType', header: 'DOLUM', priority: 1, align: 'center', sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Dolum', options: Object.entries(FILL_LABELS).map(([value, label]) => ({ value, label })) },
      cell: cat => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{FILL_LABELS[cat.fillType] ?? cat.fillType}</span> },
    { key: 'productGroupCount', header: 'GRUPLAR', priority: 1, align: 'center', sortable: true,
      filter: { type: 'number', label: 'Ürün grubu sayısı' },
      filters: [{ field: 'tanimsiz', label: 'Tanımsız (yayında, grubu yok)', type: 'boolean' }],
      cell: cat => cat.productGroupCount === 0
        ? <span className="text-xs" style={{ color: '#f59e0b' }}>⚠ Tanımsız</span>
        : <span className="text-sm" style={{ color: 'var(--text-m)' }}>{cat.productGroupCount}</span> },
    { key: 'productCount', header: 'ÜRÜN', priority: 2, align: 'center', sortable: true,
      filter: { type: 'number', label: 'Ürün sayısı' },
      cell: cat => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{cat.productCount}</span> },
    { key: 'sortOrder', header: 'SIRA', priority: 3, align: 'center', sortable: true, filter: { type: 'number', label: 'Sıra' },
      cell: cat => <span className="text-sm" style={{ color: 'var(--text-s)' }}>{cat.sortOrder}</span> },
    { key: 'status', header: 'DURUM', priority: 1, lockVisible: true, align: 'center', sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Durum', options: Object.entries(STATUS_LABELS).map(([value, v]) => ({ value, label: v.label })) },
      filters: [{ field: 'hasImage', label: 'Görseli var', type: 'boolean' }, { field: 'createdAt', label: 'Oluşturma', type: 'date' }],
      cell: cat => { const st = STATUS_LABELS[cat.status] ?? { label: cat.status, variant: 'neutral' as const }; return <Badge variant={st.variant}>{st.label}</Badge> } },
  ]

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
          onChange={(v) => v && grid.mutate(n => n.set('firmPlatformId', v))}
          options={channelOptions}
          placeholder="Kanal seçin…"
          hasValue={!!selectedChannelId}
        />
      </div>

      {/* Coverage uyarısı */}
      {uncoveredCount > 0 && (
        <button type="button" className="flex items-center gap-2 px-4 py-3 rounded-xl mb-4 text-sm w-full text-left"
          style={{ background: '#fef9c3', color: '#854d0e', border: '1px solid #fde68a' }}
          onClick={() => grid.mutate(n => n.set('f.tanimsiz', 'eq:true'))}>
          <AlertTriangle size={15} />
          <span>
            <strong>{uncoveredCount}</strong> yayındaki kategori henüz hiçbir ürün grubundan sorumlu değil.
            <span className="underline ml-1">Listede göster</span>
          </span>
        </button>
      )}

      {selectedChannelId && (
        <DataGrid<ChannelCategoryItem>
          gridId="channel-categories"
          views
          grid={grid}
          columns={columns}
          rows={categories}
          totalCount={data?.totalCount ?? 0}
          loading={catLoading}
          fetching={isFetching}
          error={listError ? errText(listError) : null}
          onRowClick={cat => navigate(`/storefront/channel-categories/${cat.id}`)}
          empty="Bu kanalda henüz kategori yok."
          search={{ placeholder: 'Kategori adı veya slug ara…' }}
          minWidth={900}
          export={{ endpoint: '/navigation/channel-categories/export', named: () => ({ firmPlatformId: selectedChannelId }), fallbackFileName: 'kanal-kategorileri.xlsx' }}
          compact={{
            title: cat => getName(cat.nameI18n),
            subtitle: cat => `${cat.slug} · ${FILL_LABELS[cat.fillType] ?? cat.fillType}`,
            right: cat => `${cat.productGroupCount} grup`,
            badge: cat => { const st = STATUS_LABELS[cat.status] ?? { label: cat.status, variant: 'neutral' as const }; return <Badge variant={st.variant}>{st.label}</Badge> },
          }}
        />
      )}

      {/* Create Modal */}
      <Modal
        open={createOpen}
        onClose={() => { setCreateOpen(false); setNewName(''); setNewSlug(''); setSlugEdited(false) }}
        title="Yeni Kanal Kategorisi"
        footer={
          <>
            <Button variant="secondary" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button
              onClick={() => createMutation.mutate()}
              loading={createMutation.isPending}
              disabled={!newName.trim()}
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
                if (!slugEdited) setNewSlug(slugify(e.target.value))
              }}
              className="w-full px-3 py-2 rounded-xl text-sm"
              style={{ background: 'var(--surface2)', border: '1px solid var(--border)', color: 'var(--text)' }}
              placeholder="Örn: Erkek Spor"
            />
          </div>
          <div>
            <label className="flbl">URL</label>
            <div className="flex items-center rounded-xl overflow-hidden"
              style={{ border: '1px solid var(--border)', background: 'var(--surface2)' }}>
              <span className="px-3 text-sm select-none" style={{ color: 'var(--text-s)', borderRight: '1px solid var(--border)' }}>/</span>
              <input
                type="text"
                value={newSlug}
                onChange={(e) => { setSlugEdited(true); setNewSlug(e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '')) }}
                className="flex-1 px-3 py-2 text-sm font-mono bg-transparent outline-none"
                style={{ color: 'var(--text)' }}
                placeholder="erkek-spor (boş bırakılırsa isimden üretilir)"
              />
            </div>
          </div>
        </div>
      </Modal>
    </div>
  )
}

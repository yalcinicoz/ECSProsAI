import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueries, useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Input } from '@/components/ui/Input'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { PageSpinner } from '@/components/ui/Spinner'

// G6: vitrin yönetimi — taslak bloklar (yerleşim başına sıralı liste) + Yayınla /
// rollback / yayın geçmişi. Taslak değişiklikler canlıyı ETKİLEMEZ; canlı yalnız
// aktif snapshot'ı okur (spec). Satır tıklanınca blok detayına gider.

interface Firm { id: string; nameI18n: Record<string, string> }
interface Channel { id: string; platformTypeName?: string; name?: string; code?: string; firmId?: string; firmName?: string }
interface CatalogData {
  placements: { code: string; displayName: string }[]
  blockTypes: {
    code: string; displayName: string; ruleLevel: string; supportsItems: boolean
    templates: string[]; requiresProductSource: boolean; requiresCollectionSource: boolean
  }[]
  carouselThemes: string[]
}
interface BlockRow {
  id: string; placement: string; blockType: string; template: string | null
  titleI18n: Record<string, string>; sortOrder: number; isActive: boolean
  priority: number; startAt: string | null; endAt: string | null
  itemCount: number; hasProductSource: boolean
}
interface SnapshotRow { id: string; version: number; publishedAt: string; isActive: boolean; status: string; note: string | null }
interface PublishLogRow { id: string; version: number; previousVersion: number | null; publishedAt: string; status: string; errorMessage: string | null; note: string | null }

const getChannelLabel = (c: Channel) => c.name ?? c.platformTypeName ?? c.code ?? c.id

export function PagesManagementPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [platformId, setPlatformId] = useState<string>(() => sessionStorage.getItem('pages.platformId') ?? '')
  useEffect(() => { if (platformId) sessionStorage.setItem('pages.platformId', platformId) }, [platformId])
  const [placement, setPlacement] = useState('homepage')
  const [createOpen, setCreateOpen] = useState(false)
  const [newType, setNewType] = useState<string | null>(null)
  const [newTemplate, setNewTemplate] = useState<string | null>(null)
  const [newTitle, setNewTitle] = useState('')
  const [publishNote, setPublishNote] = useState('')
  const [publishError, setPublishError] = useState('')

  const { data: firms = [] } = useQuery<Firm[]>({
    queryKey: ['firms'],
    queryFn: async () => (await api.get('/core/firms')).data.data ?? [],
  })
  const platformQueries = useQueries({
    queries: firms.map((firm) => ({
      queryKey: ['firm-platforms', firm.id],
      queryFn: async (): Promise<Channel[]> => {
        const { data } = await api.get(`/core/firms/${firm.id}/platforms`)
        const firmName = firm.nameI18n?.['tr'] ?? Object.values(firm.nameI18n ?? {})[0] ?? ''
        return (data.data ?? []).map((ch: Channel) => ({ ...ch, firmId: firm.id, firmName }))
      },
      enabled: firms.length > 0,
    })),
  })
  const channels: Channel[] = platformQueries.flatMap((q) => q.data ?? [])

  const { data: catalog } = useQuery<CatalogData>({
    queryKey: ['pages-catalog'],
    queryFn: async () => (await api.get('/pages/catalog')).data.data,
  })

  const { data: blocks = [], isLoading } = useQuery<BlockRow[]>({
    queryKey: ['page-blocks', platformId, placement],
    queryFn: async () =>
      (await api.get('/pages/blocks', { params: { firmPlatformId: platformId, placement } })).data.data ?? [],
    enabled: !!platformId,
  })

  const { data: snapshots = [] } = useQuery<SnapshotRow[]>({
    queryKey: ['page-snapshots', platformId],
    queryFn: async () => (await api.get('/pages/snapshots', { params: { firmPlatformId: platformId } })).data.data ?? [],
    enabled: !!platformId,
  })
  const { data: publishLogs = [] } = useQuery<PublishLogRow[]>({
    queryKey: ['publish-logs', platformId],
    queryFn: async () => (await api.get('/pages/publish-logs', { params: { firmPlatformId: platformId } })).data.data ?? [],
    enabled: !!platformId,
  })

  const yenile = () => {
    queryClient.invalidateQueries({ queryKey: ['page-blocks', platformId] })
    queryClient.invalidateQueries({ queryKey: ['page-snapshots', platformId] })
    queryClient.invalidateQueries({ queryKey: ['publish-logs', platformId] })
  }

  const createBlock = useMutation({
    mutationFn: async () => {
      const { data } = await api.post('/pages/blocks', {
        firmPlatformId: platformId, placement, blockType: newType, template: newTemplate,
        titleI18n: { tr: newTitle }, subtitleI18n: null,
        sortOrder: blocks.length + 1, isActive: true,
        startAt: null, endAt: null, priority: 0, ruleJson: null, configJson: null,
      })
      return data.data.id as string
    },
    onSuccess: (id) => {
      setCreateOpen(false); setNewTitle(''); setNewType(null); setNewTemplate(null)
      yenile()
      navigate(`/storefront/pages/${id}?platformId=${platformId}`)
    },
  })

  const reorder = useMutation({
    mutationFn: async (orderedIds: string[]) =>
      api.put('/pages/blocks/order', { firmPlatformId: platformId, placement, orderedIds }),
    onSuccess: yenile,
  })
  const remove = useMutation({
    mutationFn: async (id: string) => api.delete(`/pages/blocks/${id}`, { params: { firmPlatformId: platformId } }),
    onSuccess: yenile,
  })
  const publish = useMutation({
    mutationFn: async () => api.post('/pages/publish', { firmPlatformId: platformId, note: publishNote || null }),
    onSuccess: () => { setPublishNote(''); setPublishError(''); yenile() },
    onError: (e: unknown) => {
      const err = e as { response?: { data?: { error?: string } } }
      setPublishError(err.response?.data?.error ?? 'Yayınlama başarısız.')
    },
  })
  const rollback = useMutation({
    mutationFn: async (targetVersion: number) =>
      api.post('/pages/rollback', { firmPlatformId: platformId, targetVersion }),
    onSuccess: yenile,
  })

  const tasima = (index: number, yon: -1 | 1) => {
    const ids = blocks.map((b) => b.id)
    const hedef = index + yon
    if (hedef < 0 || hedef >= ids.length) return
    ;[ids[index], ids[hedef]] = [ids[hedef], ids[index]]
    reorder.mutate(ids)
  }

  const typeDef = (code: string) => catalog?.blockTypes.find((t) => t.code === code)
  const secilenTip = newType ? typeDef(newType) : null

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Vitrin Yönetimi</h1>
          <p className="text-sm text-[var(--text-m)]">
            Taslak bloklar canlıyı etkilemez — değişiklikler "Yayınla" ile versiyonlu snapshot olarak canlıya alınır.
          </p>
        </div>
        <div className="w-72">
          <SearchableSelect
            value={platformId || null}
            onChange={(v) => setPlatformId(v ?? '')}
            options={channels.map((c) => ({ value: c.id, label: `${getChannelLabel(c)} (${c.firmName})` }))}
            placeholder="Platform seç"
          />
        </div>
      </div>

      {!platformId ? (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-10 text-center text-[var(--text-m)]">
          Blokları yönetmek için platform seçin.
        </div>
      ) : (
        <>
          <div className="flex flex-wrap gap-2">
            {(catalog?.placements ?? []).map((p) => (
              <button
                key={p.code}
                onClick={() => setPlacement(p.code)}
                className={`rounded-lg px-3 py-1.5 text-sm ${
                  placement === p.code
                    ? 'bg-[var(--brand)] text-white'
                    : 'bg-[var(--surface2)] text-[var(--text-m)] hover:text-[var(--text)]'
                }`}
              >
                {p.displayName}
              </button>
            ))}
          </div>

          <div className="flex items-center justify-between">
            <Button size="sm" onClick={() => setCreateOpen(true)}>Yeni Blok</Button>
            <div className="flex items-center gap-2">
              <Input
                value={publishNote}
                onChange={(e) => setPublishNote(e.target.value)}
                placeholder="Yayın notu (opsiyonel)"
                className="w-56"
              />
              <Button size="sm" onClick={() => publish.mutate()} disabled={publish.isPending}>
                Yayınla
              </Button>
            </div>
          </div>
          {publishError && (
            <div className="rounded-lg border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-700">
              {publishError}
            </div>
          )}

          {isLoading ? (
            <PageSpinner />
          ) : (
            <div className="overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--surface)]">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-[var(--border)] text-left text-[var(--text-s)]">
                    <th className="px-4 py-3 w-20">Sıra</th>
                    <th className="px-4 py-3">Başlık</th>
                    <th className="px-4 py-3">Tip</th>
                    <th className="px-4 py-3">Şablon</th>
                    <th className="px-4 py-3">Öğe</th>
                    <th className="px-4 py-3">Durum</th>
                    <th className="px-4 py-3 text-right">İşlem</th>
                  </tr>
                </thead>
                <tbody>
                  {blocks.map((b, i) => (
                    <tr
                      key={b.id}
                      className="cursor-pointer border-b border-[var(--border)] last:border-0 hover:bg-[var(--surface2)]"
                      onClick={() => navigate(`/storefront/pages/${b.id}?platformId=${platformId}`)}
                    >
                      <td className="px-4 py-3" onClick={(e) => e.stopPropagation()}>
                        <div className="flex items-center gap-1">
                          <button className="rounded px-1 text-[var(--text-m)] hover:text-[var(--text)]" onClick={() => tasima(i, -1)} aria-label="Yukarı">↑</button>
                          <span>{b.sortOrder}</span>
                          <button className="rounded px-1 text-[var(--text-m)] hover:text-[var(--text)]" onClick={() => tasima(i, 1)} aria-label="Aşağı">↓</button>
                        </div>
                      </td>
                      <td className="px-4 py-3 font-medium">{b.titleI18n?.tr ?? Object.values(b.titleI18n ?? {})[0] ?? '—'}</td>
                      <td className="px-4 py-3">{typeDef(b.blockType)?.displayName ?? b.blockType}</td>
                      <td className="px-4 py-3 text-[var(--text-m)]">{b.template ?? '—'}</td>
                      <td className="px-4 py-3">{typeDef(b.blockType)?.supportsItems ? b.itemCount : (b.hasProductSource ? 'kaynak' : '—')}</td>
                      <td className="px-4 py-3">
                        <Badge variant={b.isActive ? 'success' : 'neutral'}>{b.isActive ? 'Aktif' : 'Pasif'}</Badge>
                        {(b.startAt || b.endAt) && <span className="ml-2 text-xs text-[var(--text-s)]">tarihli</span>}
                      </td>
                      <td className="px-4 py-3 text-right" onClick={(e) => e.stopPropagation()}>
                        <Button size="sm" variant="danger" onClick={() => { if (confirm('Blok silinsin mi? (Canlı yayın bir sonraki Yayınla\'ya kadar etkilenmez)')) remove.mutate(b.id) }}>
                          Sil
                        </Button>
                      </td>
                    </tr>
                  ))}
                  {blocks.length === 0 && (
                    <tr><td colSpan={7} className="px-4 py-10 text-center text-[var(--text-m)]">Bu yerleşimde taslak blok yok.</td></tr>
                  )}
                </tbody>
              </table>
            </div>
          )}

          <div className="grid gap-4 md:grid-cols-2">
            <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
              <h2 className="mb-3 text-sm font-semibold">Yayın Versiyonları</h2>
              <div className="space-y-2">
                {snapshots.map((s) => (
                  <div key={s.id} className="flex items-center justify-between rounded-lg bg-[var(--surface2)] px-3 py-2 text-sm">
                    <div>
                      <span className="font-medium">v{s.version}</span>
                      <span className="ml-2 text-[var(--text-m)]">{new Date(s.publishedAt).toLocaleString('tr-TR')}</span>
                      {s.note && <span className="ml-2 text-xs text-[var(--text-s)]">{s.note}</span>}
                    </div>
                    <div className="flex items-center gap-2">
                      {s.isActive
                        ? <Badge variant="success">Aktif Yayın</Badge>
                        : <Button size="sm" onClick={() => rollback.mutate(s.version)} disabled={rollback.isPending}>Bu versiyona dön</Button>}
                    </div>
                  </div>
                ))}
                {snapshots.length === 0 && <p className="text-sm text-[var(--text-m)]">Henüz yayın yapılmadı.</p>}
              </div>
            </div>

            <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
              <h2 className="mb-3 text-sm font-semibold">Yayın Geçmişi</h2>
              <div className="max-h-72 space-y-2 overflow-y-auto">
                {publishLogs.map((l) => (
                  <div key={l.id} className="rounded-lg bg-[var(--surface2)] px-3 py-2 text-sm">
                    <div className="flex items-center justify-between">
                      <span>
                        v{l.version}
                        {l.previousVersion != null && <span className="text-[var(--text-m)]"> (önceki v{l.previousVersion})</span>}
                      </span>
                      <Badge variant={l.status === 'success' ? 'success' : l.status === 'rollback' ? 'warning' : 'danger'}>
                        {l.status === 'success' ? 'Yayınlandı' : l.status === 'rollback' ? 'Geri Dönüş' : 'Başarısız'}
                      </Badge>
                    </div>
                    <div className="text-xs text-[var(--text-m)]">{new Date(l.publishedAt).toLocaleString('tr-TR')}{l.note ? ` · ${l.note}` : ''}</div>
                    {l.errorMessage && <div className="mt-1 text-xs text-red-600">{l.errorMessage}</div>}
                  </div>
                ))}
                {publishLogs.length === 0 && <p className="text-sm text-[var(--text-m)]">Kayıt yok.</p>}
              </div>
            </div>
          </div>
        </>
      )}

      <Modal open={createOpen} onClose={() => setCreateOpen(false)} title="Yeni Blok" size="md"
        footer={
          <>
            <Button variant="secondary" onClick={() => setCreateOpen(false)}>Vazgeç</Button>
            <Button onClick={() => createBlock.mutate()} disabled={!newType || !newTitle || createBlock.isPending}>Oluştur</Button>
          </>
        }>
        <div className="space-y-3">
          <div>
            <label className="mb-1 block text-sm text-[var(--text-m)]">Blok tipi</label>
            <SearchableSelect
              value={newType}
              onChange={(v) => { setNewType(v); setNewTemplate(null) }}
              options={(catalog?.blockTypes ?? []).map((t) => ({ value: t.code, label: t.displayName }))}
              placeholder="Tip seç"
            />
          </div>
          {secilenTip && secilenTip.templates.length > 0 && (
            <div>
              <label className="mb-1 block text-sm text-[var(--text-m)]">Şablon</label>
              <SearchableSelect
                value={newTemplate}
                onChange={setNewTemplate}
                options={secilenTip.templates.map((t) => ({ value: t, label: t }))}
                placeholder="Şablon seç"
              />
            </div>
          )}
          <div>
            <label className="mb-1 block text-sm text-[var(--text-m)]">Başlık (TR)</label>
            <Input value={newTitle} onChange={(e) => setNewTitle(e.target.value)} placeholder="Blok başlığı" />
          </div>
          <p className="text-xs text-[var(--text-s)]">
            Yerleşim: {(catalog?.placements ?? []).find((p) => p.code === placement)?.displayName ?? placement}.
            Öğeler ve kaynak yapılandırması detay sayfasında düzenlenir.
          </p>
        </div>
      </Modal>
    </div>
  )
}

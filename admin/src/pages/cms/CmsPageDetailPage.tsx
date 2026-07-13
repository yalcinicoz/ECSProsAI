import { useEffect, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { QuillEditor } from '@/components/QuillEditor'
import { PAGE_TYPE_MAP, useFirmPlatforms, type CmsPageSummary } from './CmsPagesPage'
import { cn } from '@/lib/utils'

export interface PageSectionItem {
  id: string
  itemType: string
  titleI18n?: Record<string, string>
  descriptionI18n?: Record<string, string>
  isActive: boolean
  sortOrder: number
}

export interface PageSection {
  id: string
  sectionTypeCode: string
  name?: string
  titleI18n?: Record<string, string>
  settings: Record<string, unknown>
  isActive: boolean
  sortOrder: number
  updatedAt?: string
  items: PageSectionItem[]
}

export interface CmsPageDetail {
  id: string
  firmPlatformId: string
  code: string
  nameI18n: Record<string, string>
  slugI18n: Record<string, string>
  pageType: string
  targetGender?: string
  metaTitleI18n?: Record<string, string>
  metaDescriptionI18n?: Record<string, string>
  isActive: boolean
  publishAt?: string
  unpublishAt?: string
  createdAt: string
  sections?: PageSection[]
}

function toDateInput(v?: string) {
  return v ? v.slice(0, 10) : ''
}

function errText(e: unknown) {
  const err = e as { response?: { data?: { error?: string } } }
  return err.response?.data?.error ?? 'İşlem başarısız oldu.'
}

// ── rich_text bölüm editörü: WYSIWYG + HTML kaynağı sekmesi (K20) ─────────────
function RichTextEditor({ section, pageId }: { section: PageSection; pageId: string }) {
  const queryClient = useQueryClient()
  const initial = typeof section.settings?.['html'] === 'string' ? (section.settings['html'] as string) : ''
  const [html, setHtml] = useState(initial)
  const [tab, setTab] = useState<'wysiwyg' | 'source'>('wysiwyg')
  const [editorKey, setEditorKey] = useState(0) // HTML sekmesinden dönünce editörü yeniden kur
  const [error, setError] = useState('')
  const [saved, setSaved] = useState(false)

  const save = useMutation({
    mutationFn: async () => {
      setError(''); setSaved(false)
      await api.put(`/cms/sections/${section.id}/content`, { html })
      queryClient.invalidateQueries({ queryKey: ['cms-page', pageId] })
      queryClient.invalidateQueries({ queryKey: ['cms-pages'] })
    },
    onSuccess: () => setSaved(true),
    onError: (e: unknown) => setError(errText(e)),
  })

  return (
    <div className="mt-2">
      <div className="flex gap-1 mb-2" style={{ borderBottom: '1px solid var(--border)' }}>
        <button className={cn('stab', tab === 'wysiwyg' && 'active')}
          onClick={() => { setTab('wysiwyg'); setEditorKey(k => k + 1) }}>Görsel Editör</button>
        <button className={cn('stab', tab === 'source' && 'active')}
          onClick={() => setTab('source')}>HTML Kaynağı</button>
      </div>
      {tab === 'wysiwyg' ? (
        <QuillEditor key={editorKey} initialHtml={html} onChange={setHtml} />
      ) : (
        <textarea className="ta font-mono text-xs" rows={14} value={html}
          onChange={e => setHtml(e.target.value)} spellCheck={false} />
      )}
      {error && <p className="text-sm mt-2 text-red-500">{error}</p>}
      {saved && <p className="text-sm mt-2" style={{ color: 'var(--brand)' }}>
        Kaydedildi ✓ — sözleşme sürüm tarihi güncellendi
      </p>}
      <div className="flex justify-end mt-2">
        <Button size="sm" onClick={() => save.mutate()} loading={save.isPending}>İçeriği Kaydet</Button>
      </div>
    </div>
  )
}

// ── faq bölüm editörü: soru/cevap listesi ─────────────────────────────────────
function FaqEditor({ section, pageId }: { section: PageSection; pageId: string }) {
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<PageSectionItem | 'new' | null>(null)
  const [soru, setSoru] = useState('')
  const [cevap, setCevap] = useState('')
  const [sira, setSira] = useState(0)
  const [aktif, setAktif] = useState(true)
  const [error, setError] = useState('')

  function openEdit(item: PageSectionItem | 'new') {
    setEditing(item)
    setError('')
    if (item === 'new') {
      setSoru(''); setCevap(''); setAktif(true)
      setSira((section.items.length ? Math.max(...section.items.map(i => i.sortOrder)) : 0) + 1)
    } else {
      setSoru(item.titleI18n?.['tr'] ?? '')
      setCevap(item.descriptionI18n?.['tr'] ?? '')
      setSira(item.sortOrder)
      setAktif(item.isActive)
    }
  }

  function invalidate() {
    queryClient.invalidateQueries({ queryKey: ['cms-page', pageId] })
    queryClient.invalidateQueries({ queryKey: ['cms-pages'] })
  }

  const save = useMutation({
    mutationFn: async () => {
      setError('')
      const item = editing !== 'new' ? (editing as PageSectionItem) : null
      const body = {
        titleI18n: { ...(item?.titleI18n ?? {}), tr: soru.trim() },
        descriptionI18n: { ...(item?.descriptionI18n ?? {}), tr: cevap.trim() },
        sortOrder: sira,
        isActive: aktif,
      }
      if (item) await api.put(`/cms/section-items/${item.id}`, body)
      else await api.post(`/cms/sections/${section.id}/items`, body)
    },
    onSuccess: () => { invalidate(); setEditing(null) },
    onError: (e: unknown) => setError(errText(e)),
  })

  const remove = useMutation({
    mutationFn: async (itemId: string) => { await api.delete(`/cms/section-items/${itemId}`) },
    onSuccess: invalidate,
    onError: (e: unknown) => setError(errText(e)),
  })

  return (
    <div className="mt-2">
      <div className="space-y-1 max-h-80 overflow-y-auto">
        {section.items.map(i => (
          <div key={i.id} className="flex items-start gap-2 px-2 py-1.5 rounded-lg hover:bg-[var(--surface2)]">
            <div className="min-w-0 cursor-pointer flex-1" onClick={() => openEdit(i)}>
              <div className="text-sm truncate" style={{ color: i.isActive ? 'var(--text)' : 'var(--text-s)' }}>
                {i.sortOrder}. {i.titleI18n?.['tr'] ?? '—'}
              </div>
              <div className="text-xs truncate" style={{ color: 'var(--text-s)' }}>
                {i.descriptionI18n?.['tr'] ?? ''}
              </div>
            </div>
            {!i.isActive && <Badge variant="neutral">Pasif</Badge>}
            <button className="text-xs shrink-0" style={{ color: 'var(--text-s)' }}
              onClick={() => { if (confirm('Bu soru silinsin mi?')) remove.mutate(i.id) }}>Sil</button>
          </div>
        ))}
        {section.items.length === 0 && (
          <p className="text-sm" style={{ color: 'var(--text-s)' }}>Soru yok.</p>
        )}
      </div>
      {error && <p className="text-sm mt-2 text-red-500">{error}</p>}
      <div className="mt-2">
        <Button size="sm" variant="secondary" onClick={() => openEdit('new')}>+ Soru Ekle</Button>
      </div>

      {editing !== null && (
        <Modal open onClose={() => setEditing(null)} title={editing === 'new' ? 'Yeni Soru' : 'Soruyu Düzenle'}>
          <div className="space-y-3">
            <div>
              <label className="flbl">Soru <span className="text-red-500">*</span></label>
              <input className="inp" value={soru} onChange={e => setSoru(e.target.value)} />
            </div>
            <div>
              <label className="flbl">Cevap <span className="text-red-500">*</span></label>
              <textarea className="ta" rows={5} value={cevap} onChange={e => setCevap(e.target.value)} />
            </div>
            <div className="flex items-center gap-4">
              <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
                Sıra
                <input type="number" className="inp w-20 py-1" value={sira}
                  onChange={e => setSira(parseInt(e.target.value) || 0)} />
              </label>
              <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
                <input type="checkbox" checked={aktif} onChange={e => setAktif(e.target.checked)} />
                Aktif
              </label>
            </div>
            {error && <p className="text-sm text-red-500">{error}</p>}
          </div>
          <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
            <Button variant="secondary" onClick={() => setEditing(null)}>Vazgeç</Button>
            <Button onClick={() => save.mutate()} loading={save.isPending}
              disabled={!soru.trim() || !cevap.trim()}>Kaydet</Button>
          </div>
        </Modal>
      )}
    </div>
  )
}

// ── Diğer platformlara kopyala (K20 — bilinçli, otomatik senkron yok) ─────────
function CopyModal({ page, onClose }: { page: CmsPageDetail; onClose: () => void }) {
  const queryClient = useQueryClient()
  const { data: platforms = [] } = useFirmPlatforms()
  const [selected, setSelected] = useState<string[]>([])
  const [error, setError] = useState('')
  const [done, setDone] = useState<number | null>(null)

  const { data: allPages = [] } = useQuery<CmsPageSummary[]>({
    queryKey: ['cms-pages', '', ''],
    queryFn: async () => (await api.get('/cms/pages?activeOnly=false')).data.data,
  })
  const siblings = allPages.filter(p => p.code === page.code && p.id !== page.id)

  const copy = useMutation({
    mutationFn: async () => {
      setError('')
      const { data } = await api.post(`/cms/pages/${page.id}/copy-content`, { targetPageIds: selected })
      return data.data.copiedSections as number
    },
    onSuccess: (n) => {
      setDone(n)
      queryClient.invalidateQueries({ queryKey: ['cms-pages'] })
      queryClient.invalidateQueries({ queryKey: ['cms-page'] })
    },
    onError: (e: unknown) => setError(errText(e)),
  })

  const platformName = (pid?: string) =>
    platforms.find(p => p.id === pid)?.nameI18n?.['tr'] ?? pid ?? '—'

  return (
    <Modal open onClose={onClose} title="Diğer Platformlara Kopyala">
      <p className="text-xs mb-3" style={{ color: 'var(--text-s)' }}>
        Bu sayfanın içerik bölümleri seçilen platformların aynı sayfasına yazılır —
        oradaki mevcut içerik değişir. Firma/taraf bilgileri platformlara göre farklıysa
        kopyaladıktan sonra hedefte elle düzeltin.
      </p>
      {siblings.length === 0 && (
        <p className="text-sm" style={{ color: 'var(--text-s)' }}>Diğer platformlarda aynı kodlu sayfa yok.</p>
      )}
      <div className="space-y-1">
        {siblings.map(s => (
          <label key={s.id} className="flex items-center gap-2 text-sm px-2 py-1.5 rounded-lg hover:bg-[var(--surface2)]"
            style={{ color: 'var(--text)' }}>
            <input type="checkbox" checked={selected.includes(s.id)}
              onChange={e => setSelected(sel => e.target.checked ? [...sel, s.id] : sel.filter(x => x !== s.id))} />
            {platformName(s.firmPlatformId)}
            <span className="text-xs" style={{ color: 'var(--text-s)' }}>
              (son içerik: {s.lastContentUpdatedAt ? new Date(s.lastContentUpdatedAt).toLocaleDateString('tr-TR') : '—'})
            </span>
          </label>
        ))}
      </div>
      {error && <p className="text-sm mt-2 text-red-500">{error}</p>}
      {done !== null && (
        <p className="text-sm mt-2" style={{ color: 'var(--brand)' }}>{done} bölüm kopyalandı ✓</p>
      )}
      <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        <Button variant="secondary" onClick={onClose}>Kapat</Button>
        <Button onClick={() => copy.mutate()} loading={copy.isPending}
          disabled={selected.length === 0}>Kopyala</Button>
      </div>
    </Modal>
  )
}

export function CmsPageDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const { data: page, isLoading } = useQuery<CmsPageDetail>({
    queryKey: ['cms-page', id],
    queryFn: async () => (await api.get(`/cms/pages/${id}`)).data.data,
    enabled: !!id,
  })
  const { data: platforms = [] } = useFirmPlatforms()

  const [name, setName] = useState('')
  const [slug, setSlug] = useState('')
  const [metaTitle, setMetaTitle] = useState('')
  const [metaDesc, setMetaDesc] = useState('')
  const [isActive, setIsActive] = useState(true)
  const [publishAt, setPublishAt] = useState('')
  const [unpublishAt, setUnpublishAt] = useState('')
  const [error, setError] = useState('')
  const [saved, setSaved] = useState(false)
  const [copyOpen, setCopyOpen] = useState(false)
  const [openSection, setOpenSection] = useState<string | null>(null)

  useEffect(() => {
    if (!page) return
    setName(page.nameI18n?.['tr'] ?? '')
    setSlug(page.slugI18n?.['tr'] ?? '')
    setMetaTitle(page.metaTitleI18n?.['tr'] ?? '')
    setMetaDesc(page.metaDescriptionI18n?.['tr'] ?? '')
    setIsActive(page.isActive)
    setPublishAt(toDateInput(page.publishAt))
    setUnpublishAt(toDateInput(page.unpublishAt))
  }, [page])

  const save = useMutation({
    mutationFn: async () => {
      setError(''); setSaved(false)
      await api.put(`/cms/pages/${id}`, {
        nameI18n: { ...(page?.nameI18n ?? {}), tr: name.trim() },
        slugI18n: { ...(page?.slugI18n ?? {}), tr: slug.trim() },
        metaTitleI18n: metaTitle.trim() ? { ...(page?.metaTitleI18n ?? {}), tr: metaTitle.trim() } : page?.metaTitleI18n ?? null,
        metaDescriptionI18n: metaDesc.trim() ? { ...(page?.metaDescriptionI18n ?? {}), tr: metaDesc.trim() } : page?.metaDescriptionI18n ?? null,
        isActive,
        publishAt: publishAt ? new Date(`${publishAt}T00:00:00`).toISOString() : null,
        unpublishAt: unpublishAt ? new Date(`${unpublishAt}T00:00:00`).toISOString() : null,
        targetGender: page?.targetGender ?? null,
      })
      queryClient.invalidateQueries({ queryKey: ['cms-page', id] })
      queryClient.invalidateQueries({ queryKey: ['cms-pages'] })
    },
    onSuccess: () => setSaved(true),
    onError: (e: unknown) => {
      const err = e as { response?: { data?: { error?: string } } }
      setError(err.response?.data?.error ?? 'Kaydedilemedi.')
    },
  })

  if (isLoading || !page) return <PageSpinner />

  const platform = platforms.find(p => p.id === page.firmPlatformId)

  return (
    <div className="p-6 max-w-4xl">
      <div className="flex flex-wrap items-center gap-3 mb-1">
        <button onClick={() => navigate('/cms/pages')} className="text-sm" style={{ color: 'var(--text-s)' }}>←</button>
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>{page.nameI18n?.['tr'] ?? page.code}</h1>
        <Badge variant={page.isActive ? 'success' : 'neutral'}>{page.isActive ? 'Aktif' : 'Pasif'}</Badge>
        <div className="ml-auto">
          <Button size="sm" variant="secondary" onClick={() => setCopyOpen(true)}>Diğer Platformlara Kopyala</Button>
        </div>
      </div>
      <p className="text-sm mb-5" style={{ color: 'var(--text-s)' }}>
        <code className="text-xs">{page.code}</code>
        {' · '}{PAGE_TYPE_MAP[page.pageType] ?? page.pageType}
        {platform && <> · Platform: {platform.nameI18n?.['tr']}</>}
      </p>

      {/* Sayfa bilgileri */}
      <div className="card p-4 mb-4">
        <h2 className="text-sm font-semibold mb-3" style={{ color: 'var(--text)' }}>Sayfa Bilgileri</h2>
        <div className="grid sm:grid-cols-2 gap-3">
          <div>
            <label className="flbl">Ad (TR) <span className="text-red-500">*</span></label>
            <input className="inp" value={name} onChange={e => setName(e.target.value)} />
          </div>
          <div>
            <label className="flbl">Slug (TR) <span className="text-red-500">*</span></label>
            <input className="inp" value={slug} onChange={e => setSlug(e.target.value)} />
          </div>
          <div>
            <label className="flbl">Meta Başlık</label>
            <input className="inp" value={metaTitle} onChange={e => setMetaTitle(e.target.value)} />
          </div>
          <div>
            <label className="flbl">Meta Açıklama</label>
            <input className="inp" value={metaDesc} onChange={e => setMetaDesc(e.target.value)} />
          </div>
          <div className="flex items-end gap-4">
            <label className="flex items-center gap-2 text-sm pb-2" style={{ color: 'var(--text)' }}>
              <input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} />
              Aktif (sitede yayında)
            </label>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="flbl">Yayın Başlangıcı</label>
              <input type="date" className="inp" value={publishAt} onChange={e => setPublishAt(e.target.value)} />
            </div>
            <div>
              <label className="flbl">Yayın Bitişi</label>
              <input type="date" className="inp" value={unpublishAt} onChange={e => setUnpublishAt(e.target.value)} />
            </div>
          </div>
        </div>
        {error && <p className="text-sm mt-2 text-red-500">{error}</p>}
        {saved && <p className="text-sm mt-2" style={{ color: 'var(--brand)' }}>Kaydedildi ✓</p>}
        <div className="flex justify-end mt-3">
          <Button size="sm" onClick={() => save.mutate()} loading={save.isPending}
            disabled={!name.trim() || !slug.trim()}>Kaydet</Button>
        </div>
      </div>

      {/* Bölümler — tıklayınca editör açılır (P2b) */}
      <div className="card p-4">
        <h2 className="text-sm font-semibold mb-3" style={{ color: 'var(--text)' }}>
          İçerik Bölümleri ({page.sections?.length ?? 0})
        </h2>
        {(page.sections ?? []).map(s => (
          <div key={s.id} className="py-2" style={{ borderBottom: '1px solid var(--border)' }}>
            <div className="flex flex-wrap items-center gap-2 text-sm cursor-pointer"
              onClick={() => setOpenSection(openSection === s.id ? null : s.id)}>
              <Badge variant="neutral">{s.sectionTypeCode}</Badge>
              <span style={{ color: 'var(--text)' }}>{s.name ?? s.titleI18n?.['tr'] ?? '—'}</span>
              {s.sectionTypeCode === 'faq' && (
                <span className="text-xs" style={{ color: 'var(--text-s)' }}>{s.items.length} soru</span>
              )}
              {!s.isActive && <Badge variant="neutral">Pasif</Badge>}
              <span className="text-xs ml-auto" style={{ color: 'var(--text-s)' }}>
                {s.updatedAt ? `Son değişiklik: ${new Date(s.updatedAt).toLocaleString('tr-TR')}` : 'hiç düzenlenmedi'}
                {' '}{openSection === s.id ? '▴' : '▾ düzenle'}
              </span>
            </div>
            {openSection === s.id && (
              s.sectionTypeCode === 'rich_text'
                ? <RichTextEditor section={s} pageId={page.id} />
                : s.sectionTypeCode === 'faq'
                  ? <FaqEditor section={s} pageId={page.id} />
                  : <p className="text-xs mt-2" style={{ color: 'var(--text-s)' }}>
                      Bu bölüm tipi ({s.sectionTypeCode}) için panel editörü yok.
                    </p>
            )}
          </div>
        ))}
        {(page.sections ?? []).length === 0 && (
          <p className="text-sm" style={{ color: 'var(--text-s)' }}>Bu sayfada içerik bölümü yok.</p>
        )}
      </div>

      {copyOpen && <CopyModal page={page} onClose={() => setCopyOpen(false)} />}
    </div>
  )
}

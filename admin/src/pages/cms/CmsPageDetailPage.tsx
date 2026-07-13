import { useEffect, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { PageSpinner } from '@/components/ui/Spinner'
import { PAGE_TYPE_MAP, useFirmPlatforms } from './CmsPagesPage'

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

      {/* Bölümler (içerik düzenleme P2b'de bu listeye bağlanır) */}
      <div className="card p-4">
        <h2 className="text-sm font-semibold mb-3" style={{ color: 'var(--text)' }}>
          İçerik Bölümleri ({page.sections?.length ?? 0})
        </h2>
        {(page.sections ?? []).map(s => (
          <div key={s.id} className="flex flex-wrap items-center gap-2 text-sm py-2"
            style={{ borderBottom: '1px solid var(--border)' }}>
            <Badge variant="neutral">{s.sectionTypeCode}</Badge>
            <span style={{ color: 'var(--text)' }}>{s.name ?? s.titleI18n?.['tr'] ?? '—'}</span>
            {s.sectionTypeCode === 'faq' && (
              <span className="text-xs" style={{ color: 'var(--text-s)' }}>{s.items.length} soru</span>
            )}
            {!s.isActive && <Badge variant="neutral">Pasif</Badge>}
            <span className="text-xs ml-auto" style={{ color: 'var(--text-s)' }}>
              {s.updatedAt ? `Son değişiklik: ${new Date(s.updatedAt).toLocaleString('tr-TR')}` : 'hiç düzenlenmedi'}
            </span>
          </div>
        ))}
        {(page.sections ?? []).length === 0 && (
          <p className="text-sm" style={{ color: 'var(--text-s)' }}>Bu sayfada içerik bölümü yok.</p>
        )}
        <p className="text-xs mt-3" style={{ color: 'var(--text-s)' }}>
          Bölüm içerikleri (metin/SSS) düzenlemesi bir sonraki adımda (P2b) bu listeye bağlanacak.
        </p>
      </div>
    </div>
  )
}

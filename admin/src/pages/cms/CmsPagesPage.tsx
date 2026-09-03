import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { cn } from '@/lib/utils'
import { PAGE_TYPE_MAP, useFirmPlatforms } from './cmsPageShared'

const TABS = [
  { key: 'legal',     label: 'Yasal' },
  { key: 'corporate', label: 'Kurumsal' },
  { key: '',          label: 'Tümü' },
]

export interface CmsPageSummary {
  id: string
  code: string
  nameI18n: Record<string, string>
  slugI18n: Record<string, string>
  pageType: string
  isActive: boolean
  publishAt?: string
  unpublishAt?: string
  firmPlatformId?: string
  lastContentUpdatedAt?: string
}

export function CmsPagesPage() {
  const navigate = useNavigate()
  const [tab, setTab] = useState('legal')
  const [platformId, setPlatformId] = useState('')

  const { data: platforms = [] } = useFirmPlatforms()

  const { data: pages = [], isLoading } = useQuery<CmsPageSummary[]>({
    queryKey: ['cms-pages', tab, platformId],
    queryFn: async () => {
      const params = new URLSearchParams({ activeOnly: 'false' })
      if (tab) params.set('pageType', tab)
      if (platformId) params.set('firmPlatformId', platformId)
      return (await api.get(`/cms/pages?${params}`)).data.data
    },
  })

  const platformName = (pid?: string) =>
    platforms.find(p => p.id === pid)?.nameI18n?.['tr'] ?? '—'

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>İçerik Sayfaları</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {pages.length} kayıt — sözleşme metinleri, kurumsal sayfalar ve SSS içeriği buradan yönetilir
          </p>
        </div>
        <select className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 180 }}
          value={platformId} onChange={e => setPlatformId(e.target.value)}>
          <option value="">Tüm platformlar</option>
          {platforms.map(p => (
            <option key={p.id} value={p.id}>{p.nameI18n?.['tr'] ?? p.id}</option>
          ))}
        </select>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABS.map(t => (
          <button key={t.key} className={cn('stab', tab === t.key && 'active')}
            onClick={() => setTab(t.key)}>{t.label}</button>
        ))}
      </div>

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['AD', 'TÜR', 'PLATFORM', 'AKTİF', 'SON İÇERİK', ''].map(h => (
                <th key={h} className={`px-4 py-3 text-xs font-semibold ${h === '' ? 'w-20' : 'text-left'}`}
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={6} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
            )}
            {!isLoading && pages.length === 0 && (
              <tr><td colSpan={6} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Sayfa bulunamadı.</td></tr>
            )}
            {pages.map(p => (
              <tr key={p.id} onClick={() => navigate(`/cms/pages/${p.id}`)}
                className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3">
                  <div className="text-sm font-medium" style={{ color: 'var(--text)' }}>
                    {p.nameI18n?.['tr'] ?? p.code}
                  </div>
                  <code className="text-xs font-mono" style={{ color: 'var(--text-s)' }}>{p.code}</code>
                </td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>
                  {PAGE_TYPE_MAP[p.pageType] ?? p.pageType}
                </td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>
                  {platformName(p.firmPlatformId)}
                </td>
                <td className="px-4 py-3">
                  <Badge variant={p.isActive ? 'success' : 'neutral'}>{p.isActive ? 'Aktif' : 'Pasif'}</Badge>
                </td>
                <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>
                  {p.lastContentUpdatedAt ? new Date(p.lastContentUpdatedAt).toLocaleString('tr-TR') : '—'}
                </td>
                <td className="px-4 py-3 text-right">
                  <span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

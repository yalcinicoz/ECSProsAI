import { useMemo } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { useQueries, useQuery } from '@tanstack/react-query'
import { ChevronRight, Globe, ShoppingBag } from 'lucide-react'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { PageSpinner } from '@/components/ui/Spinner'
import { CapabilityBadges } from '@/components/channels/ChannelCapabilities'
import type { PlatformType } from './PlatformTypesPage'
import { ChannelForm, type ChannelFormTab, type Firm, type FirmPlatform, type FirmPlatformWithFirm } from './ChannelsPage'
import { getChannelName, getFirmName, getPlatformTypeName } from './channelHelpers'

/**
 * Satış kanalı tanımı — ayrı sayfa, sekmeli (2026-09-10 kullanıcı kararı: kanal ayarları büyüdükçe popup
 * kullanışsız oldu). /settings/channels/new (?firmId=) yeni kanal, /settings/channels/:id düzenleme.
 * Sekme URL'de (?tab=) tutulur; formun kendisi ChannelForm — Firma/Pazaryeri sayfalarındaki modal ile aynı içerik.
 */
export function ChannelDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const isNew = !id
  const tab = searchParams.get('tab') ?? 'genel'
  const firmIdParam = searchParams.get('firmId') ?? undefined

  const { data: firms = [], isLoading: firmsLoading } = useQuery<Firm[]>({
    queryKey: ['firms'],
    queryFn: async () => { const { data } = await api.get('/core/firms'); return data.data ?? [] },
    staleTime: 10 * 60 * 1000,
  })
  const { data: platformTypes = [], isLoading: ptLoading } = useQuery<PlatformType[]>({
    queryKey: ['platform-types', false],
    queryFn: async () => { const { data } = await api.get('/core/platform-types?activeOnly=false'); return data.data ?? [] },
    staleTime: 5 * 60 * 1000,
  })
  // Kanal kaydı: tekil GET ucu yok — liste sayfasıyla aynı sorgular (firma başına), id ile bulunur.
  const channelQueries = useQueries({
    queries: firms.map(firm => ({
      queryKey: ['firm-platforms', firm.id],
      queryFn: async (): Promise<FirmPlatformWithFirm[]> => {
        const { data } = await api.get(`/core/firms/${firm.id}/platforms`)
        return (data.data ?? []).map((ch: FirmPlatform) => ({ ...ch, firmId: firm.id, firmName: getFirmName(firm) }))
      },
      enabled: !isNew && firms.length > 0,
      staleTime: 2 * 60 * 1000,
    })),
  })
  const channelsLoading = !isNew && channelQueries.some(q => q.isLoading)
  const target = useMemo(
    () => (isNew ? null : channelQueries.flatMap(q => q.data ?? []).find(ch => ch.id === id) ?? null),
    [channelQueries, id, isNew])

  function setTab(t: ChannelFormTab) {
    const next = new URLSearchParams(searchParams)
    next.set('tab', t)
    setSearchParams(next, { replace: true })
  }

  if (firmsLoading || ptLoading || channelsLoading) return <PageSpinner />

  if (!isNew && !target) {
    return (
      <div className="p-6 max-w-4xl">
        <div className="card py-16 text-center">
          <p className="text-sm mb-3" style={{ color: 'var(--text-s)' }}>Satış kanalı bulunamadı.</p>
          <Link to="/settings/channels" className="text-sm" style={{ color: 'var(--brand)' }}>← Satış Kanalları</Link>
        </div>
      </div>
    )
  }

  const baslik = target ? getChannelName(target) : 'Yeni Satış Kanalı'
  const firma = target ? firms.find(f => f.id === target.firmId) : firms.find(f => f.id === firmIdParam)

  return (
    <div className="p-6 max-w-4xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-1.5 text-sm mb-4" style={{ color: 'var(--text-s)' }}>
        <Link to="/settings/channels" style={{ color: 'var(--brand)' }}>Satış Kanalları</Link>
        <ChevronRight size={14} />
        <span style={{ color: 'var(--text)' }}>{baslik}</span>
      </div>

      <div className="card">
        {/* Başlık */}
        <div className="flex items-start justify-between gap-3 mb-5 flex-wrap">
          <div className="flex items-center gap-3 min-w-0">
            <div className="w-10 h-10 shrink-0 rounded-lg flex items-center justify-center"
              style={{ background: target?.platformTypeIsMarketplace ? '#fef3c7' : 'var(--surface2)', border: '1px solid var(--border)' }}>
              {target?.platformTypeIsMarketplace
                ? <ShoppingBag size={18} style={{ color: '#d97706' }} />
                : <Globe size={18} style={{ color: 'var(--brand)' }} />}
            </div>
            <div className="min-w-0">
              <div className="flex items-center gap-2 flex-wrap">
                <h1 className="text-lg font-bold truncate" style={{ color: 'var(--text)' }}>{baslik}</h1>
                {target && <Badge variant={target.isActive ? 'success' : 'neutral'}>{target.isActive ? 'Aktif' : 'Pasif'}</Badge>}
              </div>
              <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
                {target ? `${getPlatformTypeName(target)} · ${target.code}` : 'Firma, platform tipi ve kanal adı ile başlayın'}
                {firma ? ` · ${getFirmName(firma)}` : ''}
              </p>
              {target?.capabilities && <div className="mt-1"><CapabilityBadges caps={target.capabilities} /></div>}
            </div>
          </div>
          <Link to="/settings/channels" className="text-sm shrink-0" style={{ color: 'var(--brand)' }}>← Geri</Link>
        </div>

        <ChannelForm
          key={target?.id ?? 'new'}
          platformTypes={platformTypes}
          firms={firms}
          initialFirmId={target ? undefined : firmIdParam}
          target={target}
          layout="tabs"
          tab={tab}
          onTabChange={setTab}
          onClose={() => navigate('/settings/channels')}
          onSuccess={() => navigate('/settings/channels')}
        />
      </div>
    </div>
  )
}

import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { cn } from '@/lib/utils'

interface CampaignType { id: string; nameI18n: Record<string, string> }
interface Campaign {
  id: string; code: string; nameI18n: Record<string, string>
  startsAt: string; endsAt?: string; isActive: boolean; priority: number
  fillType: string; campaignTypeId?: string; campaignTypeCode?: string
}

const tr = (m?: Record<string, string> | null) => m?.['tr'] ?? Object.values(m ?? {})[0] ?? '—'
const FILL_LABEL: Record<string, string> = { all: 'Tüm ürünler', manual: 'Manuel', filter: 'Filtre', mixed: 'Karma' }

export function CampaignsPage() {
  const navigate = useNavigate()
  const [tab, setTab] = useState<'active' | 'all'>('all')

  const { data: campaigns = [], isLoading } = useQuery<Campaign[]>({
    queryKey: ['campaigns', tab],
    queryFn: async () => (await api.get(`/promotion/campaigns?activeOnly=${tab === 'active'}`)).data.data,
  })
  const { data: types = [] } = useQuery<CampaignType[]>({
    queryKey: ['campaign-types', 'all'],
    queryFn: async () => (await api.get('/promotion/campaign-types?activeOnly=false')).data.data,
  })
  const typeName = (tid?: string, tcode?: string) => tr(types.find(t => t.id === tid)?.nameI18n) ?? tcode ?? '—'

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Kampanyalar</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{campaigns.length} kayıt</p>
        </div>
        <Button size="sm" onClick={() => navigate('/promotion/campaigns/new')}>+ Yeni Kampanya</Button>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        <button className={cn('stab', tab === 'active' && 'active')} onClick={() => setTab('active')}>Yayında</button>
        <button className={cn('stab', tab === 'all' && 'active')} onClick={() => setTab('all')}>Tümü</button>
      </div>

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['KOD', 'AD', 'TİP', 'KAPSAM', 'TARİH', 'ÖNCELİK', 'DURUM', ''].map(h => (
                <th key={h} className={`px-4 py-3 text-xs font-semibold ${h === '' ? 'w-20' : 'text-left'}`}
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && <tr><td colSpan={8} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>}
            {!isLoading && campaigns.length === 0 && (
              <tr><td colSpan={8} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Kampanya yok. "+ Yeni Kampanya" ile tanımlayın.
              </td></tr>
            )}
            {campaigns.map(camp => (
              <tr key={camp.id} onClick={() => navigate(`/promotion/campaigns/${camp.id}`)}
                className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3"><code className="text-xs font-mono font-medium" style={{ color: 'var(--text)' }}>{camp.code}</code></td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text)' }}>{tr(camp.nameI18n)}</td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>{typeName(camp.campaignTypeId, camp.campaignTypeCode)}</td>
                <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>{FILL_LABEL[camp.fillType] ?? camp.fillType}</td>
                <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>
                  {new Date(camp.startsAt).toLocaleDateString('tr-TR')} → {camp.endsAt ? new Date(camp.endsAt).toLocaleDateString('tr-TR') : 'süresiz'}
                </td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>{camp.priority}</td>
                <td className="px-4 py-3"><Badge variant={camp.isActive ? 'success' : 'neutral'}>{camp.isActive ? 'Aktif' : 'Pasif'}</Badge></td>
                <td className="px-4 py-3 text-right"><span className="text-xs" style={{ color: 'var(--text-s)' }}>Düzenle →</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

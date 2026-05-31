import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronRight, Plus } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { useQuery as usePlatformTypesQuery } from '@tanstack/react-query'
import type { PlatformType } from './PlatformTypesPage'
import { ChannelForm } from './ChannelsPage'
import type { FirmPlatformWithFirm, Firm } from './ChannelsPage'

// ── Types ─────────────────────────────────────────────────────────────────────

interface FirmDetail {
  id: string
  code: string
  nameI18n: Record<string, string>
  taxOffice: string
  taxNumber: string
  address: string
  phone: string
  email: string
  isMain: boolean
  priceType: string
  priceMultiplier: number | null
  isActive: boolean
  createdAt: string
  platforms: { id: string; code: string; nameI18n: Record<string, string>; isActive: boolean }[]
}

function getI18nName(nameI18n: Record<string, string>) {
  return nameI18n['tr'] ?? nameI18n[Object.keys(nameI18n)[0]] ?? '—'
}

// ── Component ─────────────────────────────────────────────────────────────────

export function FirmDetailPage() {
  const { id } = useParams<{ id: string }>()
  const queryClient = useQueryClient()

  const [modalOpen, setModalOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<FirmPlatformWithFirm | null>(null)

  const { data: firm, isLoading: firmLoading } = useQuery<FirmDetail>({
    queryKey: ['firm-detail', id],
    queryFn: async () => {
      const { data } = await api.get(`/core/firms/${id}`)
      return data.data
    },
    enabled: !!id,
  })

  const { data: firmPlatforms = [], isLoading: platformsLoading } = useQuery<FirmPlatformWithFirm[]>({
    queryKey: ['firm-platforms', id],
    queryFn: async () => {
      const { data } = await api.get(`/core/firms/${id}/platforms`)
      const firmName = firm ? getI18nName(firm.nameI18n) : ''
      return (data.data ?? []).map((ch: FirmPlatformWithFirm) => ({
        ...ch,
        firmId: id!,
        firmName,
      }))
    },
    enabled: !!id,
  })

  const { data: platformTypes = [] } = usePlatformTypesQuery<PlatformType[]>({
    queryKey: ['platform-types', false],
    queryFn: async () => {
      const { data } = await api.get('/core/platform-types?activeOnly=false')
      return data.data
    },
    staleTime: 5 * 60 * 1000,
  })

  function openAdd() {
    setEditTarget(null)
    setModalOpen(true)
  }

  function openEdit(fp: FirmPlatformWithFirm, e: React.MouseEvent) {
    e.stopPropagation()
    setEditTarget(fp)
    setModalOpen(true)
  }

  function closeModal() {
    setModalOpen(false)
    setEditTarget(null)
  }

  function handleSuccess() {
    queryClient.invalidateQueries({ queryKey: ['firm-detail', id] })
    closeModal()
  }

  if (firmLoading) return <PageSpinner />
  if (!firm) return (
    <div className="p-6 text-sm" style={{ color: 'var(--text-s)' }}>Firma bulunamadı.</div>
  )

  const firmAsOption: Firm = {
    id: firm.id,
    code: firm.code,
    nameI18n: firm.nameI18n,
    isMain: firm.isMain,
    isActive: firm.isActive,
  }

  return (
    <div className="p-6 max-w-4xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-1.5 text-sm mb-4" style={{ color: 'var(--text-s)' }}>
        <Link to="/settings/firms" style={{ color: 'var(--brand)' }}>Firmalar</Link>
        <ChevronRight size={14} />
        <span style={{ color: 'var(--text)' }}>{getI18nName(firm.nameI18n)}</span>
      </div>

      {/* Firma bilgileri */}
      <div className="card mb-6">
        <div className="flex items-start justify-between mb-4">
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-lg font-bold" style={{ color: 'var(--text)' }}>{getI18nName(firm.nameI18n)}</h1>
              {firm.isMain && <Badge variant="warning">Ana Firma</Badge>}
              <Badge variant={firm.isActive ? 'success' : 'neutral'}>{firm.isActive ? 'Aktif' : 'Pasif'}</Badge>
            </div>
            <code className="text-xs mt-1 block" style={{ color: 'var(--text-s)' }}>{firm.code}</code>
          </div>
          <Link to="/settings/firms" className="text-sm" style={{ color: 'var(--brand)' }}>← Geri</Link>
        </div>

        <div className="grid grid-cols-2 gap-x-8 gap-y-3 text-sm">
          <div className="flex gap-2">
            <span style={{ color: 'var(--text-s)', minWidth: 120 }}>Vergi Dairesi</span>
            <span style={{ color: 'var(--text)' }}>{firm.taxOffice || '—'}</span>
          </div>
          <div className="flex gap-2">
            <span style={{ color: 'var(--text-s)', minWidth: 120 }}>Vergi No</span>
            <span style={{ color: 'var(--text)' }}>{firm.taxNumber || '—'}</span>
          </div>
          <div className="flex gap-2">
            <span style={{ color: 'var(--text-s)', minWidth: 120 }}>Telefon</span>
            <span style={{ color: 'var(--text)' }}>{firm.phone || '—'}</span>
          </div>
          <div className="flex gap-2">
            <span style={{ color: 'var(--text-s)', minWidth: 120 }}>E-posta</span>
            <span style={{ color: 'var(--text)' }}>{firm.email || '—'}</span>
          </div>
          <div className="flex gap-2 col-span-2">
            <span style={{ color: 'var(--text-s)', minWidth: 120 }}>Adres</span>
            <span style={{ color: 'var(--text)' }}>{firm.address || '—'}</span>
          </div>
          <div className="flex gap-2">
            <span style={{ color: 'var(--text-s)', minWidth: 120 }}>Fiyatlama</span>
            <span style={{ color: 'var(--text)' }}>
              {firm.priceType === 'multiplier'
                ? `Çarpan × ${firm.priceMultiplier}`
                : 'Manuel'}
            </span>
          </div>
        </div>
      </div>

      {/* Platformlar */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-base font-semibold" style={{ color: 'var(--text)' }}>
            Satış Kanalları
            <span className="ml-2 text-sm font-normal" style={{ color: 'var(--text-s)' }}>
              ({firmPlatforms.length})
            </span>
          </h2>
          <Button size="sm" onClick={openAdd}><Plus size={14} /> Kanal Ekle</Button>
        </div>

        <div className="card overflow-hidden p-0">
          {platformsLoading ? (
            <div className="px-4 py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</div>
          ) : (
            <table className="w-full">
              <thead>
                <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
                  {['KOD', 'AD', 'PLATFORM', 'FİYATLAMA', 'DURUM', ''].map(h => (
                    <th key={h}
                      className={`px-4 py-3 text-left text-xs font-semibold tracking-wider ${h === '' ? 'w-24' : ''}`}
                      style={{ color: 'var(--text-s)' }}>
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {firmPlatforms.length === 0 && (
                  <tr>
                    <td colSpan={6} className="px-4 py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                      Henüz satış kanalı eklenmemiş.
                    </td>
                  </tr>
                )}
                {firmPlatforms.map(fp => {
                  const pt = platformTypes.find(t => t.id === fp.platformTypeId)
                  return (
                    <tr key={fp.id} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td className="px-4 py-3">
                        <code className="text-xs px-2 py-0.5 rounded-md font-mono"
                          style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>
                          {fp.code}
                        </code>
                      </td>
                      <td className="px-4 py-3">
                        <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>
                          {fp.nameI18n?.['tr'] ?? fp.code}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        <span className="text-sm" style={{ color: 'var(--text-m)' }}>
                          {pt ? getI18nName(pt.nameI18n) : fp.platformTypeCode}
                        </span>
                        {(pt?.isMarketplace ?? fp.platformTypeIsMarketplace) && (
                          <Badge variant="info" className="ml-2">Pazaryeri</Badge>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <span className="text-sm" style={{ color: 'var(--text-m)' }}>
                          {!fp.priceType ? 'Firmadan al' :
                            fp.priceType === 'multiplier' ? `× ${fp.priceMultiplier}` : 'Manuel'}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        <Badge variant={fp.isActive ? 'success' : 'neutral'}>
                          {fp.isActive ? 'Aktif' : 'Pasif'}
                        </Badge>
                      </td>
                      <td className="px-4 py-3 text-right">
                        <button
                          className="text-xs px-2 py-1 rounded-lg transition-colors"
                          style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
                          onClick={e => openEdit(fp, e)}>
                          Düzenle
                        </button>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {/* Modal — Add / Edit */}
      <Modal
        open={modalOpen}
        onClose={closeModal}
        title={editTarget ? `Kanal Düzenle — ${editTarget.nameI18n?.['tr'] ?? editTarget.code}` : 'Satış Kanalı Ekle'}
        size="md"
        footer={null}
      >
        <ChannelForm
          platformTypes={platformTypes}
          firms={[firmAsOption]}
          initialFirmId={firm.id}
          target={editTarget}
          onClose={closeModal}
          onSuccess={handleSuccess}
        />
      </Modal>
    </div>
  )
}

import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'

interface MemberGroup {
  id: string
  code: string
  nameI18n: Record<string, string>
  isDefault: boolean
  isWholesale: boolean
  requiresApproval: boolean
  showPricesBeforeLogin: boolean
  minOrderAmount?: number
  paymentTermsDays?: number
  isActive: boolean
  sortOrder: number
  memberCount: number
}

function GroupModal({ group, onClose }: { group: MemberGroup | 'new'; onClose: () => void }) {
  const queryClient = useQueryClient()
  const isNew = group === 'new'
  const g = isNew ? undefined : group

  const [code, setCode] = useState(g?.code ?? '')
  const [name, setName] = useState(g?.nameI18n?.['tr'] ?? '')
  const [isWholesale, setIsWholesale] = useState(g?.isWholesale ?? false)
  const [requiresApproval, setRequiresApproval] = useState(g?.requiresApproval ?? false)
  const [showPrices, setShowPrices] = useState(g?.showPricesBeforeLogin ?? true)
  const [minOrder, setMinOrder] = useState(g?.minOrderAmount != null ? String(g.minOrderAmount) : '')
  const [termsDays, setTermsDays] = useState(g?.paymentTermsDays != null ? String(g.paymentTermsDays) : '')
  const [isActive, setIsActive] = useState(g?.isActive ?? true)
  const [sortOrder, setSortOrder] = useState(g?.sortOrder ?? 0)
  const [error, setError] = useState('')

  const save = useMutation({
    mutationFn: async () => {
      setError('')
      const body = {
        code: code.trim().toLowerCase(),
        nameI18n: { ...(g?.nameI18n ?? {}), tr: name.trim() },
        isDefault: g?.isDefault ?? false,
        isWholesale,
        requiresApproval,
        showPricesBeforeLogin: showPrices,
        minOrderAmount: minOrder ? parseFloat(minOrder) : null,
        paymentTermsDays: termsDays ? parseInt(termsDays) : null,
        isActive,
        sortOrder,
      }
      if (isNew) await api.post('/crm/member-groups', body)
      else await api.put(`/crm/member-groups/${g!.id}`, body)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['member-groups'] })
      onClose()
    },
    onError: (e: unknown) => {
      const err = e as { response?: { data?: { error?: string } } }
      setError(err.response?.data?.error ?? 'Kaydedilemedi.')
    },
  })

  return (
    <Modal open onClose={onClose} title={isNew ? 'Yeni Üye Grubu' : `Grup: ${g?.code}`}>
      <div className="space-y-3">
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Kod <span className="text-red-500">*</span></label>
            <input className="inp font-mono" value={code} disabled={!isNew}
              onChange={e => setCode(e.target.value)} placeholder="ör. bayi" />
          </div>
          <div>
            <label className="flbl">Ad <span className="text-red-500">*</span></label>
            <input className="inp" value={name} onChange={e => setName(e.target.value)} />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">En Az Sipariş Tutarı (₺)</label>
            <input type="number" step="0.01" className="inp" value={minOrder}
              onChange={e => setMinOrder(e.target.value)} placeholder="yok" />
          </div>
          <div>
            <label className="flbl">Vade (gün)</label>
            <input type="number" className="inp" value={termsDays}
              onChange={e => setTermsDays(e.target.value)} placeholder="yok" />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-2">
          <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
            <input type="checkbox" checked={isWholesale} onChange={e => setIsWholesale(e.target.checked)} />
            Toptan (B2B)
          </label>
          <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
            <input type="checkbox" checked={requiresApproval} onChange={e => setRequiresApproval(e.target.checked)} />
            Sipariş onay gerektirir
          </label>
          <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
            <input type="checkbox" checked={showPrices} onChange={e => setShowPrices(e.target.checked)} />
            Girişsiz fiyat görünür
          </label>
          <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
            <input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} />
            Aktif
          </label>
        </div>
        <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
          Sıra
          <input type="number" className="inp w-20 py-1" value={sortOrder}
            onChange={e => setSortOrder(parseInt(e.target.value) || 0)} />
        </label>
        {error && <p className="text-sm text-red-500">{error}</p>}
      </div>
      <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
        <Button onClick={() => save.mutate()} loading={save.isPending}
          disabled={!code.trim() || !name.trim()}>Kaydet</Button>
      </div>
    </Modal>
  )
}

export function MemberGroupsPage() {
  const [editing, setEditing] = useState<MemberGroup | 'new' | null>(null)

  const { data: groups = [], isLoading } = useQuery<MemberGroup[]>({
    queryKey: ['member-groups'],
    queryFn: async () => (await api.get('/crm/member-groups?activeOnly=false')).data.data,
  })

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Üye Grupları</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {groups.length} grup — G9 kişiselleştirme segmentleri de üye grubuna bakar
          </p>
        </div>
        <Button size="sm" onClick={() => setEditing('new')}>+ Yeni Grup</Button>
      </div>

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['KOD', 'AD', 'ÜYE', 'ÖZELLİKLER', 'DURUM', ''].map(h => (
                <th key={h} className={`px-4 py-3 text-xs font-semibold ${h === '' ? 'w-20' : 'text-left'}`}
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={6} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
            )}
            {groups.map(g => (
              <tr key={g.id} onClick={() => setEditing(g)}
                className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3">
                  <code className="text-xs font-mono font-medium" style={{ color: 'var(--text)' }}>{g.code}</code>
                </td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text)' }}>
                  {g.nameI18n?.['tr'] ?? '—'}
                  {g.isDefault && <Badge variant="neutral">Varsayılan</Badge>}
                </td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>{g.memberCount}</td>
                <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>
                  {[
                    g.isWholesale && 'B2B',
                    g.requiresApproval && 'onaylı sipariş',
                    g.minOrderAmount != null && `min ${g.minOrderAmount}₺`,
                    g.paymentTermsDays != null && `${g.paymentTermsDays} gün vade`,
                  ].filter(Boolean).join(' · ') || '—'}
                </td>
                <td className="px-4 py-3">
                  <Badge variant={g.isActive ? 'success' : 'neutral'}>{g.isActive ? 'Aktif' : 'Pasif'}</Badge>
                </td>
                <td className="px-4 py-3 text-right">
                  <span className="text-xs" style={{ color: 'var(--text-s)' }}>Düzenle →</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {editing !== null && <GroupModal group={editing} onClose={() => setEditing(null)} />}
    </div>
  )
}

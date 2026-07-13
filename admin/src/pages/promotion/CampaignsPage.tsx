import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { cn } from '@/lib/utils'

interface CampaignType {
  id: string
  code: string
  nameI18n: Record<string, string>
  descriptionI18n?: Record<string, string>
  isStackable: boolean
}

interface Campaign {
  id: string
  code: string
  nameI18n: Record<string, string>
  startsAt: string
  endsAt?: string
  isActive: boolean
  priority: number
  productSelectionType: string
  campaignTypeId?: string
  campaignTypeCode?: string
  descriptionI18n?: Record<string, string>
  settings?: Record<string, unknown>
}

// Tip → ayar alanları (CampaignEngine ile birebir)
const TYPE_FIELDS: Record<string, { key: string; label: string; required?: boolean }[]> = {
  percentage_discount: [
    { key: 'discountRate', label: 'İndirim Oranı (%)', required: true },
    { key: 'maxDiscountAmount', label: 'En Çok İndirim (₺)' },
  ],
  fixed_discount: [
    { key: 'discountAmount', label: 'İndirim Tutarı (₺)', required: true },
    { key: 'minCartTotal', label: 'En Az Sepet Tutarı (₺)' },
  ],
  buy_x_get_y: [
    { key: 'buyQuantity', label: 'Alınacak Adet (X)', required: true },
    { key: 'getQuantity', label: 'Bedava Adet (Y)', required: true },
  ],
  min_cart_discount: [
    { key: 'minCartTotal', label: 'Sepet Eşiği (₺)', required: true },
    { key: 'discountRate', label: 'İndirim Oranı (%)', required: true },
  ],
}

function errText(e: unknown) {
  const err = e as { response?: { data?: { error?: string } } }
  return err.response?.data?.error ?? 'İşlem başarısız oldu.'
}

function CampaignModal({ campaign, types, onClose }: {
  campaign: Campaign | 'new'
  types: CampaignType[]
  onClose: () => void
}) {
  const queryClient = useQueryClient()
  const isNew = campaign === 'new'
  const c = isNew ? undefined : campaign

  const [typeId, setTypeId] = useState(c?.campaignTypeId ?? '')
  const [code, setCode] = useState(c?.code ?? '')
  const [name, setName] = useState(c?.nameI18n?.['tr'] ?? '')
  const [starts, setStarts] = useState((c?.startsAt ?? new Date().toISOString()).slice(0, 10))
  const [ends, setEnds] = useState(c?.endsAt ? c.endsAt.slice(0, 10) : '')
  const [priority, setPriority] = useState(c?.priority ?? 0)
  const [isActive, setIsActive] = useState(c?.isActive ?? true)
  const [settings, setSettings] = useState<Record<string, string>>(() => {
    const out: Record<string, string> = {}
    for (const [k, v] of Object.entries(c?.settings ?? {})) if (v != null) out[k] = String(v)
    return out
  })
  const [error, setError] = useState('')

  const typeCode = isNew
    ? types.find(t => t.id === typeId)?.code ?? ''
    : c?.campaignTypeCode ?? ''
  const fields = TYPE_FIELDS[typeCode] ?? []

  const save = useMutation({
    mutationFn: async () => {
      setError('')
      const settingsBody: Record<string, number> = {}
      for (const f of fields) {
        const v = parseFloat(settings[f.key] ?? '')
        if (!isNaN(v)) settingsBody[f.key] = v
      }
      if (isNew) {
        await api.post('/promotion/campaigns', {
          campaignTypeId: typeId,
          code: code.trim().toLowerCase().replace(/[^a-z0-9_-]+/g, '_'),
          nameI18n: { tr: name.trim() },
          startsAt: new Date(`${starts}T00:00:00`).toISOString(),
          endsAt: ends ? new Date(`${ends}T23:59:59`).toISOString() : null,
          priority,
          productSelectionType: 'all',
          settings: settingsBody,
        })
      } else {
        await api.put(`/promotion/campaigns/${c!.id}`, {
          nameI18n: { ...(c!.nameI18n ?? {}), tr: name.trim() },
          descriptionI18n: c!.descriptionI18n ?? null,
          startsAt: new Date(`${starts}T00:00:00`).toISOString(),
          endsAt: ends ? new Date(`${ends}T23:59:59`).toISOString() : null,
          isActive,
          priority,
          settings: settingsBody,
        })
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['campaigns'] })
      onClose()
    },
    onError: (e: unknown) => setError(errText(e)),
  })

  const requiredOk = fields.filter(f => f.required).every(f => parseFloat(settings[f.key] ?? '') > 0)
  const valid = name.trim() && (isNew ? typeId && code.trim() : true) && requiredOk

  return (
    <Modal open onClose={onClose} title={isNew ? 'Yeni Kampanya' : `Kampanya: ${c?.code}`}>
      <div className="space-y-3">
        {isNew && (
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="flbl">Kampanya Tipi <span className="text-red-500">*</span></label>
              <select className="inp" value={typeId} onChange={e => setTypeId(e.target.value)}>
                <option value="">Tip seçin</option>
                {types.map(t => <option key={t.id} value={t.id}>{t.nameI18n?.['tr'] ?? t.code}</option>)}
              </select>
              {typeCode && (
                <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
                  {types.find(t => t.id === typeId)?.descriptionI18n?.['tr'] ?? ''}
                </p>
              )}
            </div>
            <div>
              <label className="flbl">Kod <span className="text-red-500">*</span></label>
              <input className="inp" value={code} onChange={e => setCode(e.target.value)}
                placeholder="ör. yaz_indirimi" />
            </div>
          </div>
        )}
        <div>
          <label className="flbl">Ad <span className="text-red-500">*</span></label>
          <input className="inp" value={name} onChange={e => setName(e.target.value)} />
        </div>
        {fields.length > 0 && (
          <div className="grid grid-cols-2 gap-3">
            {fields.map(f => (
              <div key={f.key}>
                <label className="flbl">{f.label} {f.required && <span className="text-red-500">*</span>}</label>
                <input type="number" step="0.01" className="inp" value={settings[f.key] ?? ''}
                  onChange={e => setSettings(s => ({ ...s, [f.key]: e.target.value }))} />
              </div>
            ))}
          </div>
        )}
        <div className="grid grid-cols-3 gap-3">
          <div>
            <label className="flbl">Başlangıç</label>
            <input type="date" className="inp" value={starts} onChange={e => setStarts(e.target.value)} />
          </div>
          <div>
            <label className="flbl">Bitiş <span className="text-xs" style={{ color: 'var(--text-s)' }}>(boş = süresiz)</span></label>
            <input type="date" className="inp" value={ends} onChange={e => setEnds(e.target.value)} />
          </div>
          <div>
            <label className="flbl">Öncelik</label>
            <input type="number" className="inp" value={priority}
              onChange={e => setPriority(parseInt(e.target.value) || 0)} />
          </div>
        </div>
        {!isNew && (
          <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
            <input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} />
            Aktif
          </label>
        )}
        <p className="text-xs" style={{ color: 'var(--text-s)' }}>
          Kampanya tüm ürünlere uygulanır; ürün-özel kampanya (specific) tanımı ileri iş.
        </p>
        {error && <p className="text-sm text-red-500">{error}</p>}
      </div>
      <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
        <Button onClick={() => save.mutate()} loading={save.isPending} disabled={!valid}>Kaydet</Button>
      </div>
    </Modal>
  )
}

export function CampaignsPage() {
  const [tab, setTab] = useState<'active' | 'all'>('active')
  const [editing, setEditing] = useState<Campaign | 'new' | null>(null)

  const { data: campaigns = [], isLoading } = useQuery<Campaign[]>({
    queryKey: ['campaigns', tab],
    queryFn: async () =>
      (await api.get(`/promotion/campaigns?activeOnly=${tab === 'active'}`)).data.data,
  })
  const { data: types = [] } = useQuery<CampaignType[]>({
    queryKey: ['campaign-types'],
    queryFn: async () => (await api.get('/promotion/campaign-types')).data.data,
  })

  const typeName = (tid?: string, tcode?: string) =>
    types.find(t => t.id === tid)?.nameI18n?.['tr'] ?? tcode ?? '—'

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Kampanyalar</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{campaigns.length} kayıt</p>
        </div>
        <Button size="sm" onClick={() => setEditing('new')}>+ Yeni Kampanya</Button>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        <button className={cn('stab', tab === 'active' && 'active')} onClick={() => setTab('active')}>Yayında</button>
        <button className={cn('stab', tab === 'all' && 'active')} onClick={() => setTab('all')}>Tümü</button>
      </div>

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['KOD', 'AD', 'TİP', 'TARİH', 'ÖNCELİK', 'DURUM', ''].map(h => (
                <th key={h} className={`px-4 py-3 text-xs font-semibold ${h === '' ? 'w-20' : 'text-left'}`}
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
            )}
            {!isLoading && campaigns.length === 0 && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Kampanya yok. "+ Yeni Kampanya" ile tanımlayın.
              </td></tr>
            )}
            {campaigns.map(camp => (
              <tr key={camp.id} onClick={() => setEditing(camp)}
                className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3">
                  <code className="text-xs font-mono font-medium" style={{ color: 'var(--text)' }}>{camp.code}</code>
                </td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text)' }}>{camp.nameI18n?.['tr'] ?? '—'}</td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>
                  {typeName(camp.campaignTypeId, camp.campaignTypeCode)}
                </td>
                <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>
                  {new Date(camp.startsAt).toLocaleDateString('tr-TR')}
                  {' → '}{camp.endsAt ? new Date(camp.endsAt).toLocaleDateString('tr-TR') : 'süresiz'}
                </td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>{camp.priority}</td>
                <td className="px-4 py-3">
                  <Badge variant={camp.isActive ? 'success' : 'neutral'}>{camp.isActive ? 'Aktif' : 'Pasif'}</Badge>
                </td>
                <td className="px-4 py-3 text-right">
                  <span className="text-xs" style={{ color: 'var(--text-s)' }}>Düzenle →</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {editing !== null && <CampaignModal campaign={editing} types={types} onClose={() => setEditing(null)} />}
    </div>
  )
}

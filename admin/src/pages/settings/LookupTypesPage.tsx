import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { errText, i18nAd } from '@/components/ui/DataTable.utils'
import { cn } from '@/lib/utils'

interface LookupType {
  id: string
  code: string
  nameI18n: Record<string, string>
  description?: string
  isSystem: boolean
}

interface LookupValue {
  id: string
  nameI18n: Record<string, string>
  color?: string
  icon?: string
  isDefault: boolean
  isActive: boolean
  sortOrder: number
}

function TipModal({ onClose }: { onClose: () => void }) {
  const queryClient = useQueryClient()
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [error, setError] = useState('')

  const save = useMutation({
    mutationFn: async () => {
      setError('')
      await api.post('/lookup/types', {
        code: code.trim().toLowerCase().replace(/\s+/g, '_'),
        nameI18n: { tr: name.trim() },
        description: description.trim() || null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['lookup-types'] })
      onClose()
    },
    onError: (e: unknown) => setError(errText(e)),
  })

  const valid = code.trim().length >= 2 && name.trim()

  return (
    <Modal open onClose={onClose} title="Yeni Lookup Tipi">
      <div className="space-y-3">
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Kod <span className="text-red-500">*</span></label>
            <input className="inp font-mono" value={code} onChange={e => setCode(e.target.value)}
              placeholder="cinsiyet" />
          </div>
          <div>
            <label className="flbl">Ad <span className="text-red-500">*</span></label>
            <input className="inp" value={name} onChange={e => setName(e.target.value)} placeholder="Cinsiyet" />
          </div>
        </div>
        <div>
          <label className="flbl">Açıklama</label>
          <input className="inp" value={description} onChange={e => setDescription(e.target.value)} />
        </div>
        {error && <p className="text-sm text-red-500">{error}</p>}
      </div>
      <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
        <Button onClick={() => save.mutate()} loading={save.isPending} disabled={!valid}>Kaydet</Button>
      </div>
    </Modal>
  )
}

function DegerModal({ typeCode, value, onClose }: { typeCode: string; value: LookupValue | 'new'; onClose: () => void }) {
  const queryClient = useQueryClient()
  const isNew = value === 'new'
  const v = isNew ? undefined : value

  const [name, setName] = useState(v?.nameI18n?.['tr'] ?? '')
  const [color, setColor] = useState(v?.color ?? '')
  const [sortOrder, setSortOrder] = useState(v ? String(v.sortOrder) : '0')
  const [isDefault, setIsDefault] = useState(v?.isDefault ?? false)
  const [isActive, setIsActive] = useState(v?.isActive ?? true)
  const [error, setError] = useState('')

  const save = useMutation({
    mutationFn: async () => {
      setError('')
      const body = {
        nameI18n: { ...(v?.nameI18n ?? {}), tr: name.trim() },
        color: color.trim() || null,
        icon: v?.icon ?? null,
        isDefault,
        sortOrder: parseInt(sortOrder) || 0,
      }
      if (isNew) await api.post(`/lookup/types/${typeCode}/values`, body)
      else await api.put(`/lookup/values/${v!.id}`, { ...body, isActive })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['lookup-values', typeCode] })
      onClose()
    },
    onError: (e: unknown) => setError(errText(e)),
  })

  return (
    <Modal open onClose={onClose} title={isNew ? 'Yeni Değer' : `Değer: ${i18nAd(v?.nameI18n)}`}>
      <div className="space-y-3">
        <div className="grid grid-cols-3 gap-3">
          <div className="col-span-2">
            <label className="flbl">Ad <span className="text-red-500">*</span></label>
            <input className="inp" value={name} onChange={e => setName(e.target.value)} />
          </div>
          <div>
            <label className="flbl">Sıra</label>
            <input type="number" className="inp" value={sortOrder} onChange={e => setSortOrder(e.target.value)} />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Renk <span className="text-xs" style={{ color: 'var(--text-s)' }}>(#hex, isteğe bağlı)</span></label>
            <input className="inp font-mono" value={color} onChange={e => setColor(e.target.value)} placeholder="#10b981" />
          </div>
          <div className="flex items-end pb-2 gap-4">
            <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
              <input type="checkbox" checked={isDefault} onChange={e => setIsDefault(e.target.checked)} />
              Varsayılan
            </label>
            {!isNew && (
              <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
                <input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} />
                Aktif
              </label>
            )}
          </div>
        </div>
        {error && <p className="text-sm text-red-500">{error}</p>}
      </div>
      <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
        <Button onClick={() => save.mutate()} loading={save.isPending} disabled={!name.trim()}>Kaydet</Button>
      </div>
    </Modal>
  )
}

export function LookupTypesPage() {
  const [selected, setSelected] = useState<LookupType | null>(null)
  const [newType, setNewType] = useState(false)
  const [editingValue, setEditingValue] = useState<LookupValue | 'new' | null>(null)

  const { data: types, isLoading } = useQuery<LookupType[]>({
    queryKey: ['lookup-types'],
    queryFn: async () => (await api.get('/lookup/types')).data.data,
  })

  const { data: values, isLoading: valuesLoading } = useQuery<LookupValue[]>({
    queryKey: ['lookup-values', selected?.code],
    queryFn: async () => (await api.get(`/lookup/types/${selected!.code}/values?activeOnly=false`)).data.data,
    enabled: !!selected,
  })

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Lookup Tipleri</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {types?.length ?? 0} tip — sipariş durumu, ödeme yöntemi gibi referans listeleri
          </p>
        </div>
        <Button size="sm" onClick={() => setNewType(true)}>+ Yeni Tip</Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {/* Tip listesi */}
        <div className="card overflow-hidden">
          {isLoading && <p className="p-4 text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</p>}
          {(types ?? []).map(t => (
            <button key={t.id} onClick={() => setSelected(t)}
              className={cn('w-full text-left px-4 py-3 text-sm transition-colors hover:bg-[var(--surface2)]',
                selected?.id === t.id && 'bg-[var(--surface2)]')}
              style={{ borderBottom: '1px solid var(--border)', color: 'var(--text)' }}>
              <span className="font-medium">{i18nAd(t.nameI18n)}</span>
              <span className="block text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
                <code className="font-mono">{t.code}</code>{t.isSystem ? ' · sistem' : ''}
              </span>
            </button>
          ))}
        </div>

        {/* Deger listesi */}
        <div className="md:col-span-2">
          {!selected && (
            <p className="text-sm p-4" style={{ color: 'var(--text-s)' }}>Değerlerini görmek için soldan bir tip seçin.</p>
          )}
          {selected && (
            <div className="card overflow-hidden">
              <div className="flex items-center justify-between px-4 py-3" style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
                <span className="text-sm font-semibold" style={{ color: 'var(--text)' }}>
                  {i18nAd(selected.nameI18n)} değerleri
                </span>
                <Button size="sm" variant="secondary" onClick={() => setEditingValue('new')}>+ Değer Ekle</Button>
              </div>
              {valuesLoading && <p className="p-4 text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</p>}
              {!valuesLoading && (values ?? []).length === 0 && (
                <p className="p-4 text-sm" style={{ color: 'var(--text-s)' }}>Bu tipin değeri yok.</p>
              )}
              {(values ?? []).map(v => (
                <button key={v.id} onClick={() => setEditingValue(v)}
                  className="w-full flex items-center gap-3 px-4 py-2.5 text-sm text-left transition-colors hover:bg-[var(--surface2)]"
                  style={{ borderBottom: '1px solid var(--border)', color: 'var(--text)' }}>
                  {v.color && <span className="w-3 h-3 rounded-full flex-shrink-0" style={{ background: v.color }} />}
                  <span className="flex-1">{i18nAd(v.nameI18n)}</span>
                  {v.isDefault && <Badge variant="info">Varsayılan</Badge>}
                  <Badge variant={v.isActive ? 'success' : 'neutral'}>{v.isActive ? 'Aktif' : 'Pasif'}</Badge>
                  <span className="text-xs" style={{ color: 'var(--text-s)' }}>Düzenle →</span>
                </button>
              ))}
            </div>
          )}
        </div>
      </div>

      {newType && <TipModal onClose={() => setNewType(false)} />}
      {selected && editingValue !== null && (
        <DegerModal typeCode={selected.code} value={editingValue} onClose={() => setEditingValue(null)} />
      )}
    </div>
  )
}

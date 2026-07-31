import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'

// Salt-okunur: kampanya TİP tanımları (definition.campaign_types) + parametre şablonları.
// Tipler kod ile seed edilir (yeni tip = motor handler'ı gerektirir); bu ekran yalnız görüntüler.
// Platforma kampanya ekleme ekranları (F1) ayrıca gelecek.

interface SchemaFieldOption { value: string; labelI18n: Record<string, string> }
interface SchemaFieldCondition { field: string; equals?: string; notEquals?: string }
interface CampaignSchemaField {
  key: string
  labelI18n: Record<string, string>
  type: string
  required: boolean
  unit?: string | null
  min?: number | null
  max?: number | null
  default?: unknown
  options?: SchemaFieldOption[] | null
  visibleWhen?: SchemaFieldCondition | null
  helpI18n?: Record<string, string> | null
}
interface CampaignType {
  id: string
  code: string
  nameI18n: Record<string, string>
  descriptionI18n?: Record<string, string>
  scope: string
  requiresProducts: boolean
  productPriceDisplay: boolean
  isStackable: boolean
  isActive: boolean
  sortOrder: number
  settingsSchema?: CampaignSchemaField[] | null
}

const tr = (m?: Record<string, string> | null) => m?.['tr'] ?? Object.values(m ?? {})[0] ?? ''

const SCOPE_LABEL: Record<string, string> = {
  cart: 'Sepet', product: 'Ürün', shipping: 'Kargo', member: 'Üye',
}
const FIELD_TYPE_LABEL: Record<string, string> = {
  percent: 'Yüzde', money: 'Tutar (₺)', integer: 'Tam sayı', number: 'Sayı',
  boolean: 'Evet/Hayır', select: 'Seçim',
}

function TypeDetailModal({ type, onClose }: { type: CampaignType; onClose: () => void }) {
  const fields = type.settingsSchema ?? []
  return (
    <Modal open onClose={onClose} size="lg" title={`Kampanya Tipi: ${type.code}`}>
      <div className="space-y-4">
        <div>
          <div className="text-base font-semibold" style={{ color: 'var(--text)' }}>{tr(type.nameI18n)}</div>
          {tr(type.descriptionI18n) && (
            <p className="text-sm mt-1" style={{ color: 'var(--text-m)' }}>{tr(type.descriptionI18n)}</p>
          )}
        </div>

        <div className="flex flex-wrap gap-2">
          <Badge variant="info">Kapsam: {SCOPE_LABEL[type.scope] ?? type.scope}</Badge>
          {type.requiresProducts && <Badge variant="neutral">Ürün seçimi (tümü/filtre/manuel)</Badge>}
          {type.productPriceDisplay && <Badge variant="success">Kartta kampanyalı fiyat</Badge>}
          {type.isStackable && <Badge variant="warning">Birleşebilir (stackable)</Badge>}
          <Badge variant={type.isActive ? 'success' : 'neutral'}>{type.isActive ? 'Aktif' : 'Pasif'}</Badge>
        </div>

        <div>
          <div className="text-xs font-semibold mb-2" style={{ color: 'var(--text-s)' }}>
            PARAMETRE ŞABLONU ({fields.length} alan) — kampanya oluşturulurken doldurulur
          </div>
          {fields.length === 0 ? (
            <p className="text-sm" style={{ color: 'var(--text-s)' }}>Bu tipin parametresi yok.</p>
          ) : (
            <div className="card overflow-hidden">
              <table className="w-full">
                <thead>
                  <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
                    {['ALAN', 'ANAHTAR', 'TİP', 'ZORUNLU', 'DEĞERLER / KOŞUL'].map(h => (
                      <th key={h} className="px-3 py-2 text-xs font-semibold text-left" style={{ color: 'var(--text-s)' }}>{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {fields.map(f => (
                    <tr key={f.key} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td className="px-3 py-2 text-sm" style={{ color: 'var(--text)' }}>
                        {tr(f.labelI18n)}{f.unit ? <span style={{ color: 'var(--text-s)' }}> ({f.unit})</span> : null}
                        {tr(f.helpI18n) && (
                          <div className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>{tr(f.helpI18n)}</div>
                        )}
                      </td>
                      <td className="px-3 py-2">
                        <code className="text-xs font-mono" style={{ color: 'var(--text-m)' }}>{f.key}</code>
                      </td>
                      <td className="px-3 py-2 text-xs" style={{ color: 'var(--text-m)' }}>
                        {FIELD_TYPE_LABEL[f.type] ?? f.type}
                      </td>
                      <td className="px-3 py-2 text-xs" style={{ color: 'var(--text-s)' }}>{f.required ? 'Evet' : '—'}</td>
                      <td className="px-3 py-2 text-xs" style={{ color: 'var(--text-s)' }}>
                        {f.options && f.options.length > 0 && (
                          <div>{f.options.map(o => tr(o.labelI18n)).join(' · ')}</div>
                        )}
                        {f.visibleWhen && (
                          <div style={{ color: 'var(--text-s)' }}>
                            görünür: <code className="font-mono">{f.visibleWhen.field}</code>
                            {f.visibleWhen.equals != null ? ` = ${f.visibleWhen.equals}` : ''}
                            {f.visibleWhen.notEquals != null ? ` ≠ ${f.visibleWhen.notEquals}` : ''}
                          </div>
                        )}
                        {(f.min != null || f.max != null) && (
                          <div>{f.min != null ? `min ${f.min}` : ''}{f.max != null ? ` max ${f.max}` : ''}</div>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        <p className="text-xs" style={{ color: 'var(--text-s)' }}>
          Tipler platformdan bağımsız tanımlardır ve kod ile yönetilir. Yeni tip eklemek motor
          (CampaignEngine) desteği gerektirir. Buradaki tipleri bir platforma uygulamak için
          kampanya oluşturma ekranı kullanılır.
        </p>
      </div>
    </Modal>
  )
}

export function CampaignTypesPage() {
  const [selected, setSelected] = useState<CampaignType | null>(null)
  const { data: types = [], isLoading } = useQuery<CampaignType[]>({
    queryKey: ['campaign-types', 'all'],
    queryFn: async () => (await api.get('/promotion/campaign-types?activeOnly=false')).data.data,
  })

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Kampanya Tipleri</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          Tanımlı kampanya tipleri ve parametre şablonları (salt-okunur). {types.length} tip
        </p>
      </div>

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['KOD', 'AD', 'KAPSAM', 'PARAMETRE', 'ÖZELLİK', 'DURUM', ''].map(h => (
                <th key={h} className={`px-4 py-3 text-xs font-semibold ${h === '' ? 'w-16' : 'text-left'}`}
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
            )}
            {!isLoading && types.length === 0 && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Kampanya tipi yok.</td></tr>
            )}
            {types.map(t => (
              <tr key={t.id} onClick={() => setSelected(t)}
                className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3"><code className="text-xs font-mono font-medium" style={{ color: 'var(--text)' }}>{t.code}</code></td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text)' }}>{tr(t.nameI18n)}</td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>{SCOPE_LABEL[t.scope] ?? t.scope}</td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>{t.settingsSchema?.length ?? 0} alan</td>
                <td className="px-4 py-3">
                  <div className="flex flex-wrap gap-1">
                    {t.requiresProducts && <Badge variant="neutral">Ürün</Badge>}
                    {t.productPriceDisplay && <Badge variant="success">Kart fiyatı</Badge>}
                    {t.isStackable && <Badge variant="warning">Stack</Badge>}
                  </div>
                </td>
                <td className="px-4 py-3"><Badge variant={t.isActive ? 'success' : 'neutral'}>{t.isActive ? 'Aktif' : 'Pasif'}</Badge></td>
                <td className="px-4 py-3 text-right"><span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {selected && <TypeDetailModal type={selected} onClose={() => setSelected(null)} />}
    </div>
  )
}

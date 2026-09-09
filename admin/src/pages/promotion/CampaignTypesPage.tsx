import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'

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

/** DataGrid satırı — /promotion/campaign-types/grid: ayar ŞEMASI yok, yalnız alan SAYISI (ağır jsonb). */
interface CampaignTypeRow {
  id: string
  code: string
  nameI18n: Record<string, string>
  descriptionI18n?: Record<string, string>
  scope: string
  handlerClass: string
  requiresProducts: boolean
  productPriceDisplay: boolean
  isStackable: boolean
  isActive: boolean
  sortOrder: number
  campaignCount: number
  settingsFieldCount: number
}

interface Sayfali<T> { items: T[]; totalCount: number; page: number; pageSize: number }

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
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (CampaignTypeGrid.Schema) + Excel + görünümler.
  // ★ Ayrı uç: /promotion/campaign-types TAM liste + ayar şeması döner (kampanya listesi/detayı bunu
  // bekler); bu ekran sayfalı /campaign-types/grid kullanır ve satırda yalnız alan SAYISI taşınır.
  // Satır tıklanınca açılan parametre şablonu modalı şemayı TALEP ANINDA tam listeden çeker.
  const [sp] = useSearchParams()
  const activeOnly = sp.get('activeOnly') === 'true'
  const grid = useGridState('campaign-types', { defaultPageSize: 30, defaultSort: 'sortOrder', defaultDir: 'asc' })
  const [selectedCode, setSelectedCode] = useState<string | null>(null)

  const { data, isLoading, isFetching, error: listError } = useQuery<Sayfali<CampaignTypeRow>>({
    queryKey: ['campaign-types-grid', activeOnly, ...grid.queryKey],
    queryFn: async () =>
      (await api.get(`/promotion/campaign-types/grid?${grid.toParams({ activeOnly: activeOnly ? 'true' : undefined })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  // Modal için tam tanım (ayar şemasıyla) — yalnız bir satır seçilince istenir, sonra önbellekte kalır.
  const { data: tamTipler = [] } = useQuery<CampaignType[]>({
    queryKey: ['campaign-types', 'all'],
    queryFn: async () => (await api.get('/promotion/campaign-types?activeOnly=false')).data.data,
    enabled: selectedCode !== null,
    staleTime: 5 * 60_000,
  })
  const selected = selectedCode ? tamTipler.find(t => t.code === selectedCode) ?? null : null

  const columns: GridColumn<CampaignTypeRow>[] = [
    { key: 'code', header: 'KOD', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 170,
      filter: { type: 'text', label: 'Kod', ops: ['startswith', 'contains', 'eq'] },
      filters: [{ field: 'handlerClass', label: 'Handler sınıfı', type: 'text' }],
      cell: t => <code className="text-xs font-mono font-medium" style={{ color: 'var(--text)' }}>{t.code}</code> },
    { key: 'name', header: 'AD', priority: 1, frozen: true, sortable: true, minWidth: 220,
      filter: { type: 'text', label: 'Ad' },
      filters: [{ field: 'description', label: 'Açıklama', type: 'text' }],
      cell: t => <span className="text-sm" style={{ color: 'var(--text)' }}>{tr(t.nameI18n)}</span> },
    { key: 'scope', header: 'KAPSAM', priority: 1, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Kapsam', options: Object.entries(SCOPE_LABEL).map(([value, label]) => ({ value, label })) },
      cell: t => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{SCOPE_LABEL[t.scope] ?? t.scope}</span> },
    { key: 'settingsFieldCount', header: 'PARAMETRE', priority: 2, align: 'right',
      cell: t => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{t.settingsFieldCount} alan</span> },
    { key: 'campaignCount', header: 'KAMPANYA', priority: 2, align: 'right', sortable: true,
      filter: { type: 'number', label: 'Kampanya sayısı' },
      filters: [{ field: 'kullanimda', label: 'Kullanımda (kampanyası var)', type: 'boolean' }],
      cell: t => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{t.campaignCount}</span> },
    { key: 'requiresProducts', header: 'ÖZELLİK', priority: 2, sortable: true,
      filter: { type: 'boolean', label: 'Ürün gerekir' },
      filters: [
        { field: 'productPriceDisplay', label: 'Kart fiyatını etkiler', type: 'boolean' },
        { field: 'isStackable', label: 'Birleşebilir (stack)', type: 'boolean' }],
      cell: t => <div className="flex flex-wrap gap-1">
        {t.requiresProducts && <Badge variant="neutral">Ürün</Badge>}
        {t.productPriceDisplay && <Badge variant="success">Kart fiyatı</Badge>}
        {t.isStackable && <Badge variant="warning">Stack</Badge>}
      </div> },
    { key: 'sortOrder', header: 'SIRA', priority: 3, align: 'center', sortable: true, filter: { type: 'number', label: 'Sıra' },
      cell: t => <span className="text-sm" style={{ color: 'var(--text-s)' }}>{t.sortOrder}</span> },
    { key: 'isActive', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'boolean', label: 'Aktif' },
      filters: [{ field: 'createdAt', label: 'Oluşturma', type: 'date' }],
      cell: t => <Badge variant={t.isActive ? 'success' : 'neutral'}>{t.isActive ? 'Aktif' : 'Pasif'}</Badge> },
    { key: 'detay', header: '', priority: 3, align: 'right', exportable: false,
      cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span> },
  ]

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Kampanya Tipleri</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          Tanımlı kampanya tipleri ve parametre şablonları (salt-okunur). {(data?.totalCount ?? 0).toLocaleString('tr-TR')} tip
          {grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}
        </p>
      </div>

      <DataGrid<CampaignTypeRow>
        gridId="campaign-types"
        views
        grid={grid}
        columns={columns}
        rows={data?.items ?? []}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        onRowClick={t => setSelectedCode(t.code)}
        empty="Kampanya tipi yok."
        search={{ placeholder: 'Tip kodu veya adıyla ara…' }}
        minWidth={1080}
        export={{ endpoint: '/promotion/campaign-types/export', named: () => ({ activeOnly: activeOnly ? 'true' : undefined }), fallbackFileName: 'kampanya-tipleri.xlsx' }}
        compact={{
          title: t => tr(t.nameI18n),
          subtitle: t => `${t.code} · ${SCOPE_LABEL[t.scope] ?? t.scope}`,
          right: t => `${t.settingsFieldCount} alan`,
          badge: t => <Badge variant={t.isActive ? 'success' : 'neutral'}>{t.isActive ? 'Aktif' : 'Pasif'}</Badge>,
        }}
      />

      {selectedCode && selected && <TypeDetailModal type={selected} onClose={() => setSelectedCode(null)} />}
    </div>
  )
}

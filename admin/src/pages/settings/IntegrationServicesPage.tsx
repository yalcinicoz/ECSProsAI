import { useState, useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, CheckCircle } from 'lucide-react'
import { cn, toSnakeCase } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { I18nField } from '@/components/ui/I18nField'
import { PageSpinner } from '@/components/ui/Spinner'
import { useLanguages } from '@/hooks/useLanguages'
import { useAuthStore } from '@/store/auth'
import { FL } from '@/lib/field-labels'
import { buildI18nValues } from '@/lib/i18n-helper'
import { SchemaEditor, getFieldLabel } from './PlatformTypesPage'
import type { SchemaField } from './PlatformTypesPage'

// ── Types ─────────────────────────────────────────────────────────────────────

export interface IntegrationServiceRow {
  id: string
  code: string
  nameI18n: Record<string, string>
  serviceType: string
  isAvailable: boolean
  logoUrl: string | null
  trackingUrlTemplate: string | null
  settingsSchema: SchemaField[] | null
  cargoCodeStrategy: string | null
  cargoCodeMinLength: number | null
  cargoCodeMaxLength: number | null
  cargoCodeCharset: string | null
}

const CARGO_STRATEGIES = [
  { value: '', label: '— (varsayılan: serbest)' },
  { value: 'free', label: 'Serbest — paket no + önek' },
  { value: 'pattern', label: 'Kurallı — uzunluk/karakter kontrolü' },
  { value: 'range', label: 'Tahsisli aralık (PTT tarzı)' },
  { value: 'external', label: 'Dış kod — taşıyıcı/pazaryeri verir' },
]

const SERVICE_TYPES = [
  { value: 'cargo', label: 'Kargo' },
  { value: 'email', label: 'E-Posta (SMTP)' },
  { value: 'visual_search', label: 'Görsel Arama' },
  { value: 'marketplace', label: 'Pazaryeri' },
  { value: 'invoice_integrator', label: 'e-Fatura Entegratörü' },
  { value: 'payment', label: 'Ödeme' },
  { value: 'social_login', label: 'Sosyal Giriş (OAuth)' },
  { value: 'sms', label: 'SMS' },
  { value: 'erp', label: 'ERP' },
  // Takip / reklam servisleri (İE-1, 2026-08-22 — docs/reklam-analytics-entegrasyon-is-akisi.md)
  { value: 'analytics', label: 'Analytics (GA4)' },
  { value: 'tag_manager', label: 'Tag Manager (GTM)' },
  { value: 'ads', label: 'Reklam (Google Ads)' },
  { value: 'merchant', label: 'Merchant Center (Feed)' },
  { value: 'search_console', label: 'Search Console' },
  { value: 'meta', label: 'Meta Pixel / CAPI' },
  { value: 'tiktok', label: 'TikTok Pixel / Events API' },
  { value: 'pinterest', label: 'Pinterest Tag / CAPI' },
  { value: 'microsoft_ads', label: 'Microsoft Ads (UET)' },
  { value: 'clarity', label: 'Microsoft Clarity' },
  { value: 'other', label: 'Diğer' },
]

const serviceTypeLabel = (t: string) => SERVICE_TYPES.find(s => s.value === t)?.label ?? t

type FormState = {
  nameI18n: Record<string, string>
  serviceType: string
  isAvailable: boolean
  logoUrl: string
  trackingUrlTemplate: string
  schema: SchemaField[]
  cargoCodeStrategy: string
  cargoCodeMinLength: string
  cargoCodeMaxLength: string
  cargoCodeCharset: string
}

const emptyForm = (): FormState => ({
  nameI18n: {}, serviceType: 'cargo', isAvailable: true,
  logoUrl: '', trackingUrlTemplate: '', schema: [],
  cargoCodeStrategy: '', cargoCodeMinLength: '', cargoCodeMaxLength: '', cargoCodeCharset: '',
})

function getName(s: Pick<IntegrationServiceRow, 'nameI18n' | 'code'>) {
  return s.nameI18n?.['tr'] ?? s.nameI18n?.[Object.keys(s.nameI18n ?? {})[0]] ?? s.code
}

// ── Main Component ────────────────────────────────────────────────────────────

export function IntegrationServicesPage() {
  const queryClient = useQueryClient()
  const { data: languages = [], isLoading: langsLoading } = useLanguages()
  // definition şeması: yalnız platform yönetimi (geliştirici firma) erişir —
  // sıradan firma kullanıcısına sayfa tamamen kapalı (sidebar'da da görünmez).
  const canManage = useAuthStore(s => s.hasPermission)('definition.manage')

  const [typeFilter, setTypeFilter] = useState<string>('')
  const [createOpen, setCreateOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<IntegrationServiceRow | null>(null)
  const [form, setForm] = useState<FormState>(emptyForm())
  const [savedOk, setSavedOk] = useState(false)

  const { data: services = [], isLoading } = useQuery<IntegrationServiceRow[]>({
    queryKey: ['integration-services'],
    queryFn: async () => {
      const { data } = await api.get('/core/integration-services')
      return data.data ?? []
    },
  })

  const filtered = typeFilter ? services.filter(s => s.serviceType === typeFilter) : services

  const sourceLang = languages.find(l => l.isDefault)?.code ?? 'tr'
  const i18nValues = useMemo(() => buildI18nValues(form.nameI18n, languages), [form.nameI18n, languages])
  const i18nFields = useMemo(() => [{ key: 'name', labels: FL.name, required: true }], [])
  const autoCode = toSnakeCase(form.nameI18n['tr'] ?? form.nameI18n[sourceLang] ?? '')

  const createMutation = useMutation({
    mutationFn: async () => {
      await api.post('/core/integration-services', {
        code: autoCode,
        nameI18n: form.nameI18n,
        serviceType: form.serviceType,
        isAvailable: form.isAvailable,
        logoUrl: form.logoUrl || null,
        trackingUrlTemplate: form.trackingUrlTemplate || null,
        settingsSchema: form.schema.length > 0 ? form.schema : null,
        cargoCodeStrategy: form.cargoCodeStrategy || null,
        cargoCodeMinLength: form.cargoCodeMinLength ? Number(form.cargoCodeMinLength) : null,
        cargoCodeMaxLength: form.cargoCodeMaxLength ? Number(form.cargoCodeMaxLength) : null,
        cargoCodeCharset: form.cargoCodeCharset || null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['integration-services'] })
      setCreateOpen(false)
    },
  })

  const updateMutation = useMutation({
    mutationFn: async () => {
      if (!editTarget) return
      await api.put(`/core/integration-services/${editTarget.id}`, {
        nameI18n: form.nameI18n,
        isAvailable: form.isAvailable,
        logoUrl: form.logoUrl || null,
        trackingUrlTemplate: form.trackingUrlTemplate || null,
        settingsSchema: form.schema.length > 0 ? form.schema : null,
        cargoCodeStrategy: form.cargoCodeStrategy || null,
        cargoCodeMinLength: form.cargoCodeMinLength ? Number(form.cargoCodeMinLength) : null,
        cargoCodeMaxLength: form.cargoCodeMaxLength ? Number(form.cargoCodeMaxLength) : null,
        cargoCodeCharset: form.cargoCodeCharset || null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['integration-services'] })
      setSavedOk(true)
      setTimeout(() => setSavedOk(false), 2500)
    },
  })

  function openCreate() {
    setForm(emptyForm())
    createMutation.reset()
    setCreateOpen(true)
  }

  function openEdit(s: IntegrationServiceRow) {
    setEditTarget(s)
    setSavedOk(false)
    updateMutation.reset()
    setForm({
      nameI18n: { ...s.nameI18n },
      serviceType: s.serviceType,
      isAvailable: s.isAvailable,
      logoUrl: s.logoUrl ?? '',
      trackingUrlTemplate: s.trackingUrlTemplate ?? '',
      schema: s.settingsSchema ? s.settingsSchema.map(f => ({ ...f, labelI18n: { ...f.labelI18n } })) : [],
      cargoCodeStrategy: s.cargoCodeStrategy ?? '',
      cargoCodeMinLength: s.cargoCodeMinLength != null ? String(s.cargoCodeMinLength) : '',
      cargoCodeMaxLength: s.cargoCodeMaxLength != null ? String(s.cargoCodeMaxLength) : '',
      cargoCodeCharset: s.cargoCodeCharset ?? '',
    })
  }

  function closeEdit() {
    setEditTarget(null)
    setSavedOk(false)
  }

  if (!canManage) {
    return (
      <div className="p-6">
        <div className="card p-8 text-center">
          <p className="text-sm font-medium" style={{ color: 'var(--text)' }}>
            Bu sayfa yalnız platform yönetimine açıktır.
          </p>
          <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
            Servis kataloğu (definition şeması) geliştirici firma tarafından yönetilir.
          </p>
        </div>
      </div>
    )
  }

  if (isLoading || langsLoading) return <PageSpinner />

  const formBody = (isEdit: boolean) => (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end gap-4">
        <div className="flex-1 min-w-48">
          <label className="flbl">Servis Tipi {isEdit && <span className="text-xs">(değiştirilemez)</span>}</label>
          <select className="sel" value={form.serviceType} disabled={isEdit}
            onChange={e => setForm(f => ({ ...f, serviceType: e.target.value }))}>
            {SERVICE_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
          </select>
        </div>
        <label className="flex items-center gap-2 cursor-pointer pb-2">
          <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
            checked={form.isAvailable}
            onChange={e => setForm(f => ({ ...f, isAvailable: e.target.checked }))} />
          <span className="text-sm" style={{ color: 'var(--text)' }}>Kullanılabilir</span>
        </label>
      </div>

      <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
        <I18nField sourceLang={sourceLang} languages={languages} fields={i18nFields}
          values={i18nValues}
          onChange={(lang, _key, val) => setForm(f => ({ ...f, nameI18n: { ...f.nameI18n, [lang]: val } }))} />
      </div>

      {!isEdit && (
        <div>
          <label className="flbl">Otomatik Kod</label>
          <div className="flex items-center gap-2 px-3 py-2 rounded-xl"
            style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
            <code className="text-sm font-mono" style={{ color: autoCode ? 'var(--brand)' : 'var(--text-s)' }}>
              {autoCode || '—'}
            </code>
          </div>
          <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>Türkçe addan otomatik üretilir. Kayıt sonrası değiştirilemez.</p>
        </div>
      )}

      <div className="grid grid-cols-2 gap-4">
        <div>
          <label className="flbl">Logo URL</label>
          <input className="inp" value={form.logoUrl} placeholder="https://…"
            onChange={e => setForm(f => ({ ...f, logoUrl: e.target.value }))} />
        </div>
        <div>
          <label className="flbl">Takip Linki Şablonu <span className="text-xs">(kargo)</span></label>
          <input className="inp" value={form.trackingUrlTemplate} placeholder="https://…?code={trackingNumber}"
            onChange={e => setForm(f => ({ ...f, trackingUrlTemplate: e.target.value }))} />
        </div>
      </div>

      {/* Dış div hep render edilir — koşullu düğüm sabit kardeşin önüne eklenmesin */}
      <div>
        {form.serviceType === 'cargo' && (
          <div className="rounded-xl p-4 space-y-3"
            style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
            <div>
              <label className="flbl">Kargo Kodu Stratejisi</label>
              <select className="sel" value={form.cargoCodeStrategy}
                onChange={e => setForm(f => ({ ...f, cargoCodeStrategy: e.target.value }))}>
                {CARGO_STRATEGIES.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
              </select>
              <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
                Tahsisli aralıkta kodlar firma entegrasyonuna tanımlı barkod aralığından atanır;
                aralık tükenince kod üretimi açık hata verir.
              </p>
            </div>
            <div className="grid grid-cols-3 gap-3">
              <div>
                <label className="flbl">En Az Uzunluk</label>
                <input className="inp" type="number" min={1} value={form.cargoCodeMinLength}
                  onChange={e => setForm(f => ({ ...f, cargoCodeMinLength: e.target.value }))} />
              </div>
              <div>
                <label className="flbl">En Çok Uzunluk</label>
                <input className="inp" type="number" min={1} value={form.cargoCodeMaxLength}
                  onChange={e => setForm(f => ({ ...f, cargoCodeMaxLength: e.target.value }))} />
              </div>
              <div>
                <label className="flbl">Karakter Kümesi</label>
                <select className="sel" value={form.cargoCodeCharset}
                  onChange={e => setForm(f => ({ ...f, cargoCodeCharset: e.target.value }))}>
                  <option value="">— serbest</option>
                  <option value="numeric">Yalnız rakam</option>
                  <option value="alnum">Harf + rakam</option>
                </select>
              </div>
            </div>
          </div>
        )}
      </div>

      <div>
        <label className="flbl mb-2 block">Entegrasyon Alan Şeması</label>
        <p className="text-xs mb-2" style={{ color: 'var(--text-s)' }}>
          Firma detayındaki entegrasyon formu bu alanlardan üretilir. <strong>Kimlik Bilgileri</strong> bölümündeki
          alanlar veritabanında şifreli saklanır.
        </p>
        <SchemaEditor
          schema={form.schema}
          onChange={schema => setForm(f => ({ ...f, schema }))}
          languages={languages}
        />
      </div>

      {(createMutation.isError || updateMutation.isError) && (
        <p className="text-sm" style={{ color: '#ef4444' }}>
          {((createMutation.error ?? updateMutation.error) as any)?.response?.data?.error ?? 'Hata oluştu.'}
        </p>
      )}
    </div>
  )

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Servis Kataloğu</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            Kargo, SMTP, görsel arama gibi dış servis tanımları ve entegrasyon form şemaları
          </p>
        </div>
        <div className="flex items-center gap-3">
          <select className="sel" style={{ width: 180 }} value={typeFilter}
            onChange={e => setTypeFilter(e.target.value)}>
            <option value="">Tüm tipler</option>
            {SERVICE_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
          </select>
          <Button size="sm" onClick={openCreate}><Plus size={14} /> Yeni Servis</Button>
        </div>
      </div>

      <div className="card overflow-hidden p-0">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['KOD', 'AD', 'TİP', 'ŞEMA ALANLARI', 'DURUM', ''].map(h => (
                <th key={h} className={cn('px-4 py-3 text-xs font-semibold tracking-wider',
                  h === '' ? 'w-24' : 'text-left')}
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 && (
              <tr><td colSpan={6} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Servis bulunamadı.
              </td></tr>
            )}
            {filtered.map(s => (
              <tr key={s.id} style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3">
                  <code className="text-xs px-2 py-0.5 rounded-md font-mono"
                    style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>
                    {s.code}
                  </code>
                </td>
                <td className="px-4 py-3">
                  <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{getName(s)}</span>
                </td>
                <td className="px-4 py-3">
                  <Badge variant="info">{serviceTypeLabel(s.serviceType)}</Badge>
                </td>
                <td className="px-4 py-3">
                  {s.settingsSchema && s.settingsSchema.length > 0 ? (
                    <div className="flex flex-wrap gap-1">
                      {s.settingsSchema.slice(0, 3).map(f => (
                        <span key={f.key} className="text-xs px-1.5 py-0.5 rounded"
                          style={{
                            background: f.section === 'credentials' ? '#fef3c7' : 'var(--surface2)',
                            color: f.section === 'credentials' ? '#92400e' : 'var(--text-s)',
                            border: '1px solid var(--border)',
                          }}>
                          {getFieldLabel(f)}
                        </span>
                      ))}
                      {s.settingsSchema.length > 3 && (
                        <span className="text-xs" style={{ color: 'var(--text-s)' }}>
                          +{s.settingsSchema.length - 3}
                        </span>
                      )}
                    </div>
                  ) : (
                    <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span>
                  )}
                </td>
                <td className="px-4 py-3">
                  <Badge variant={s.isAvailable ? 'success' : 'neutral'}>
                    {s.isAvailable ? 'Kullanılabilir' : 'Kapalı'}
                  </Badge>
                </td>
                <td className="px-4 py-3 text-right">
                  <button className="text-xs px-2 py-1 rounded-lg transition-colors"
                    style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
                    onClick={() => openEdit(s)}>
                    Düzenle
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Create Modal */}
      <Modal open={createOpen} onClose={() => setCreateOpen(false)} title="Yeni Servis Tanımı" size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button onClick={() => createMutation.mutate()} loading={createMutation.isPending}
              disabled={!autoCode || !form.nameI18n[sourceLang]}>
              Oluştur
            </Button>
          </>
        }>
        {formBody(false)}
      </Modal>

      {/* Edit Modal */}
      <Modal open={!!editTarget} onClose={closeEdit}
        title={`Servis Düzenle — ${editTarget ? getName(editTarget) : ''}`}
        size="lg"
        footer={
          <>
            <div className="flex items-center gap-2 flex-1">
              {savedOk && (
                <span className="flex items-center gap-1.5 text-xs px-2.5 py-1 rounded-lg font-medium"
                  style={{ background: '#f0fdf4', color: '#16a34a', border: '1px solid #bbf7d0' }}>
                  <CheckCircle size={12} /> Kaydedildi
                </span>
              )}
            </div>
            <Button variant="secondary" onClick={closeEdit}>Kapat</Button>
            <Button onClick={() => updateMutation.mutate()} loading={updateMutation.isPending}>
              Kaydet
            </Button>
          </>
        }>
        {formBody(true)}
      </Modal>
    </div>
  )
}

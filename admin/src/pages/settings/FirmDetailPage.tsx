import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ChevronRight, Info, Plus, X, Eye, EyeOff } from 'lucide-react'
import { useAuthStore } from '@/store/auth'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { useQuery as usePlatformTypesQuery } from '@tanstack/react-query'
import type { PlatformType } from './PlatformTypesPage'
import { ChannelForm } from './ChannelsPage'
import type { FirmPlatformWithFirm, Firm } from './ChannelsPage'
import { CapabilityBadges } from '@/components/channels/ChannelCapabilities'
import { getFieldHelp, getFieldLabel } from './platformTypeFields'
import type { SchemaField } from './PlatformTypesPage'
import { apiErrorMessage, apiErrorStatus } from '@/lib/api-error'

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
  isActive: boolean
  createdAt: string
  platforms: { id: string; code: string; nameI18n: Record<string, string>; isActive: boolean }[]
}

interface IntegrationService {
  id: string
  code: string
  nameI18n: Record<string, string>
  serviceType: string
  isAvailable: boolean
  settingsSchema: SchemaField[] | null
}

interface FirmIntegration {
  id: string
  firmId: string
  integrationServiceId: string
  serviceCode: string
  serviceNameI18n: Record<string, string>
  serviceType: string
  firmPlatformId: string | null
  firmPlatformNameI18n: Record<string, string> | null
  name: string | null
  credentials: Record<string, unknown> // maskeli gelir ("•••") — değer değiştirilmezse maske geri yollanır, backend saklı değeri korur
  settings: Record<string, unknown>
  isActive: boolean
  createdAt: string
  startDate: string | null
  endDate: string | null
  status: string
  terms: Record<string, unknown> | null
}

const CONTRACT_STATUSES = [
  { value: 'draft', label: 'Taslak' },
  { value: 'active', label: 'Aktif' },
  { value: 'expired', label: 'Süresi Doldu' },
  { value: 'cancelled', label: 'İptal Edildi' },
]

const CONTRACT_STATUS_BADGE: Record<string, 'success' | 'neutral' | 'warning' | 'danger'> = {
  draft: 'neutral',
  active: 'success',
  expired: 'warning',
  cancelled: 'danger',
}

function getI18nName(nameI18n: Record<string, string>) {
  return nameI18n['tr'] ?? nameI18n[Object.keys(nameI18n)[0]] ?? '—'
}

function formatDate(d: string | null) {
  if (!d) return '—'
  return new Date(d).toLocaleDateString('tr-TR')
}

function toDateInputValue(d: string | null) {
  if (!d) return ''
  return d.slice(0, 10)
}

// ── Key/Value Editor (Credentials / Settings / Terms) ────────────────────────

type KVRow = { key: string; value: string }

function recordToRows(rec: Record<string, unknown> | null | undefined): KVRow[] {
  return Object.entries(rec ?? {}).map(([key, value]) => ({ key, value: value == null ? '' : String(value) }))
}

function rowsToRecord(rows: KVRow[]): Record<string, string> {
  const out: Record<string, string> = {}
  for (const r of rows) if (r.key.trim()) out[r.key.trim()] = r.value
  return out
}

function KeyValueEditor({
  label, hint, rows, onChange,
}: {
  label: string
  hint?: string
  rows: KVRow[]
  onChange: (rows: KVRow[]) => void
}) {
  function updateRow(i: number, patch: Partial<KVRow>) {
    onChange(rows.map((r, idx) => (idx === i ? { ...r, ...patch } : r)))
  }
  function removeRow(i: number) {
    onChange(rows.filter((_, idx) => idx !== i))
  }
  function addRow() {
    onChange([...rows, { key: '', value: '' }])
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-1.5">
        <label className="flbl mb-0">{label}</label>
        <button type="button" onClick={addRow} className="text-xs font-medium" style={{ color: 'var(--brand)' }}>
          + Satır Ekle
        </button>
      </div>
      {hint && <p className="text-xs mb-2" style={{ color: 'var(--text-s)' }}>{hint}</p>}
      {rows.length === 0 ? (
        <p className="text-xs py-2 text-center rounded-xl" style={{ background: 'var(--surface2)', color: 'var(--text-s)' }}>
          Henüz alan eklenmedi.
        </p>
      ) : (
        <div className="space-y-2">
          {rows.map((r, i) => (
            <div key={i} className="flex items-center gap-2">
              <input className="inp" style={{ flex: '0 0 40%' }} placeholder="anahtar"
                value={r.key} onChange={e => updateRow(i, { key: e.target.value })} />
              <input className="inp" style={{ flex: 1 }} placeholder="değer"
                value={r.value} onChange={e => updateRow(i, { value: e.target.value })} />
              <button type="button" onClick={() => removeRow(i)} className="p-1.5 rounded-lg hover:opacity-70"
                style={{ color: 'var(--text-s)' }}>
                <X size={14} />
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

// ── Schema-Driven Fields (servis kataloğundaki SettingsSchema'dan üretilir) ───

/** Şemadaki helpI18n metni — info ikonuna tıklanınca açılan açıklama balonu.
 * Balon, ikon ekranın sağ yarısındaysa sola, solundaysa sağa doğru açılır
 * (sabit right-0 sol kenardan taşıyordu — docs/otp-bilgilendirme-format-sorunu.png).
 * DİKKAT: Bu bileşen bir <label> İÇİNE konmamalı — button etiketlenebilir eleman
 * olduğundan label, backdrop dahil her tıklamayı butona iletir ve balon yeniden
 * açılır (kapanmaz, ekran kilitlenir). Backdrop'taki preventDefault ek sigortadır. */
function FieldHelp({ text }: { text: string }) {
  const [open, setOpen] = useState(false)
  const [solaAcilir, setSolaAcilir] = useState(false)
  return (
    <span className="relative inline-flex align-middle">
      <button type="button" aria-label="Alan açıklaması" aria-expanded={open}
        className="inline-flex items-center hover:opacity-70"
        onClick={e => {
          e.preventDefault()
          setSolaAcilir(e.currentTarget.getBoundingClientRect().left > window.innerWidth / 2)
          setOpen(o => !o)
        }}>
        <Info size={14} style={{ color: 'var(--text-s)' }} />
      </button>
      {open && (
        <>
          <div className="fixed inset-0 z-40" onClick={e => { e.preventDefault(); setOpen(false) }} />
          <div role="tooltip"
            className={`absolute ${solaAcilir ? 'right-0' : 'left-0'} top-full mt-1.5 z-50 w-64 rounded-lg p-3 text-xs font-normal normal-case leading-relaxed shadow-lg whitespace-normal text-left`}
            style={{ background: 'var(--surface)', border: '1px solid var(--border)', color: 'var(--text-m)' }}>
            {text}
          </div>
        </>
      )}
    </span>
  )
}

function schemaFieldInput(
  f: SchemaField,
  value: string,
  onChange: (v: string) => void,
  reveal = false,
) {
  const help = getFieldHelp(f)
  if (f.type === 'boolean') {
    return (
      <div className="flex items-center gap-2 py-2">
        <label className="flex items-center gap-2 cursor-pointer">
          <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
            checked={value === 'true'}
            onChange={e => onChange(e.target.checked ? 'true' : 'false')} />
          <span className="text-sm" style={{ color: 'var(--text)' }}>{getFieldLabel(f)}</span>
        </label>
        {help && <FieldHelp text={help} />}
      </div>
    )
  }
  const inputType = f.type === 'password' && !reveal ? 'password' : f.type === 'number' ? 'number' : f.type === 'date' ? 'date' : 'text'
  return (
    <div>
      <div className="flbl">
        {getFieldLabel(f)} {f.required && <span style={{ color: '#ef4444' }}>*</span>}
        {help && <FieldHelp text={help} />}
      </div>
      <input className="inp" type={inputType} value={value} onChange={e => onChange(e.target.value)} />
    </div>
  )
}

function SchemaSectionFields({
  fields, values, onChange, reveal = false,
}: {
  fields: SchemaField[]
  values: Record<string, string>
  onChange: (key: string, v: string) => void
  reveal?: boolean
}) {
  return (
    <div className="grid grid-cols-2 gap-4">
      {fields.map(f => (
        <div key={f.key}>{schemaFieldInput(f, values[f.key] ?? '', v => onChange(f.key, v), reveal)}</div>
      ))}
    </div>
  )
}

/** Şema alan değerini API gövdesine çevirir — boolean/number tipine göre. */
function schemaValueToBody(f: SchemaField, raw: string): unknown {
  if (f.type === 'boolean') return raw === 'true'
  if (f.type === 'number') {
    const n = Number(raw)
    return Number.isFinite(n) ? n : raw
  }
  return raw
}

function initSchemaValues(schema: SchemaField[], target: FirmIntegration | null): Record<string, string> {
  const out: Record<string, string> = {}
  for (const f of schema) {
    const src = f.section === 'credentials' ? target?.credentials : target?.settings
    const v = src?.[f.key]
    if (v !== undefined && v !== null) out[f.key] = String(v)
  }
  return out
}

/** Şemada tanımlı olmayan mevcut anahtarlar — serbest editörde korunur. */
function extraRows(rec: Record<string, unknown> | null | undefined, schema: SchemaField[], section: SchemaField['section']): KVRow[] {
  const schemaKeys = new Set(schema.filter(f => f.section === section).map(f => f.key))
  return recordToRows(rec).filter(r => !schemaKeys.has(r.key))
}

// ── Integration Form ──────────────────────────────────────────────────────────

interface IntegrationFormProps {
  firmId: string
  platforms: FirmDetail['platforms']
  integrationServices: IntegrationService[]
  target: FirmIntegration | null
  onClose: () => void
  onSuccess: () => void
}

function IntegrationForm({ firmId, platforms, integrationServices, target, onClose, onSuccess }: IntegrationFormProps) {
  const queryClient = useQueryClient()
  const isEdit = !!target

  const [integrationServiceId, setIntegrationServiceId] = useState(target?.integrationServiceId ?? '')
  const [firmPlatformId, setFirmPlatformId] = useState(target?.firmPlatformId ?? '')
  const [name, setName] = useState(target?.name ?? '')
  const [isActive, setIsActive] = useState(target?.isActive ?? true)
  const [startDate, setStartDate] = useState(toDateInputValue(target?.startDate ?? null))
  const [endDate, setEndDate] = useState(toDateInputValue(target?.endDate ?? null))
  const [status, setStatus] = useState(target?.status ?? 'draft')

  const selectedService = integrationServices.find(s => s.id === integrationServiceId)
  const schema = selectedService?.settingsSchema ?? []
  const hasSchema = schema.length > 0

  const [schemaValues, setSchemaValues] = useState<Record<string, string>>(
    () => initSchemaValues(schema, target))
  // Şema dışı anahtarlar (ya da şemasız serviste tüm anahtarlar) serbest editörde
  const [credRows, setCredRows] = useState<KVRow[]>(() => extraRows(target?.credentials, schema, 'credentials'))
  const [settingsRows, setSettingsRows] = useState<KVRow[]>(() => extraRows(target?.settings, schema, 'settings'))
  const [termsRows, setTermsRows] = useState<KVRow[]>(() => recordToRows(target?.terms))

  // "Göster" (2026-08-29): integration.credentials.reveal yetkili kullanıcı saklı kimlik
  // bilgilerini açık metin görür; sunucu her çağrıyı audit_logs'a yazar. Açılan değerler
  // form alanlarına yazılır — kullanıcı değiştirmeden kaydederse aynı değer geri gider.
  const canReveal = useAuthStore(s => s.hasPermission)('integration.credentials.reveal')
  const [revealed, setRevealed] = useState(false)
  const [revealing, setRevealing] = useState(false)
  const [revealError, setRevealError] = useState<string | null>(null)
  async function revealCredentials() {
    if (!target) return
    setRevealing(true); setRevealError(null)
    try {
      const res = await api.get(`/core/firm-integrations/${target.id}/credentials/reveal`)
      const creds: Record<string, unknown> = res.data?.data?.credentials ?? {}
      const schemaKeys = new Set(credSchemaFields.map(f => f.key))
      setSchemaValues(sv => {
        const next = { ...sv }
        for (const f of credSchemaFields) {
          const v = creds[f.key]
          if (v !== undefined && v !== null) next[f.key] = String(v)
        }
        return next
      })
      setCredRows(recordToRows(creds).filter(r => !schemaKeys.has(r.key)))
      setRevealed(true)
    } catch (e: unknown) {
      setRevealError(apiErrorStatus(e) === 403
        ? 'Bu işlem için yetkiniz yok (integration.credentials.reveal).'
        : apiErrorMessage(e, 'Kimlik bilgileri alınamadı.'))
    } finally {
      setRevealing(false)
    }
  }

  function selectService(id: string) {
    setIntegrationServiceId(id)
    // yeni servisin şemasına göre alanlar sıfırlanır (create modunda)
    const svc = integrationServices.find(s => s.id === id)
    setSchemaValues(initSchemaValues(svc?.settingsSchema ?? [], target))
    setCredRows(extraRows(target?.credentials, svc?.settingsSchema ?? [], 'credentials'))
    setSettingsRows(extraRows(target?.settings, svc?.settingsSchema ?? [], 'settings'))
  }

  const credSchemaFields = schema.filter(f => f.section === 'credentials')
  const settingsSchemaFields = schema.filter(f => f.section === 'settings')

  const serviceOptions = integrationServices.map(s => ({
    value: s.id,
    label: `${getI18nName(s.nameI18n)} (${s.serviceType})`,
  }))

  const mutation = useMutation({
    mutationFn: async () => {
      // şema alanları + serbest satırlar bölümlerine göre birleşir; boş bırakılan şema
      // alanı gönderilmez (maskeli "•••" gönderilir → backend saklı değeri korur)
      const credentials: Record<string, unknown> = { ...rowsToRecord(credRows) }
      const settings: Record<string, unknown> = { ...rowsToRecord(settingsRows) }
      for (const f of credSchemaFields) {
        const raw = schemaValues[f.key]
        if (raw !== undefined && raw !== '') credentials[f.key] = schemaValueToBody(f, raw)
      }
      for (const f of settingsSchemaFields) {
        const raw = schemaValues[f.key]
        if (raw !== undefined && raw !== '') settings[f.key] = schemaValueToBody(f, raw)
      }
      // Zorunlu şema alanları (2026-08-22): boş bırakılan * alan kaydı engeller (sunucu da doğrular)
      const eksik = schema.filter(f => f.required && f.type !== 'boolean')
        .filter(f => { const raw = schemaValues[f.key]; return raw === undefined || String(raw).trim() === '' })
        .map(f => getFieldLabel(f))
      if (eksik.length) throw new Error('Zorunlu alan(lar) boş: ' + eksik.join(', '))
      const body = {
        integrationServiceId: isEdit ? undefined : integrationServiceId,
        firmPlatformId: firmPlatformId || null,
        name: name || null,
        credentials,
        settings,
        isActive,
        startDate: startDate || null,
        endDate: endDate || null,
        status,
        terms: rowsToRecord(termsRows),
      }
      if (isEdit) {
        await api.put(`/core/firm-integrations/${target!.id}`, body)
      } else {
        await api.post(`/core/firms/${firmId}/integrations`, body)
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['firm-integrations', firmId] })
      onSuccess()
    },
  })

  return (
    <div className="space-y-5">
      {!isEdit && (
        <div>
          <label className="flbl">Servis <span style={{ color: '#ef4444' }}>*</span></label>
          <SearchableSelect
            value={integrationServiceId || null}
            onChange={v => selectService(v ?? '')}
            options={serviceOptions}
            placeholder="— Servis seçin —"
            hasValue={!!integrationServiceId}
          />
        </div>
      )}

      <div>
        <label className="flbl">İsim</label>
        <input className="inp" value={name} onChange={e => setName(e.target.value)}
          placeholder="örn: Yurtiçi Kargo — 2026 Sözleşmesi" />
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div>
          <label className="flbl">Platform</label>
          <select className="sel" value={firmPlatformId} onChange={e => setFirmPlatformId(e.target.value)}>
            <option value="">Tüm platformlar (firma geneli)</option>
            {platforms.map(p => (
              <option key={p.id} value={p.id}>{getI18nName(p.nameI18n)}</option>
            ))}
          </select>
          <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
            Platforma özel kayıt, firma geneline tercih edilir.
          </p>
        </div>
        <div>
          <label className="flbl">Durum</label>
          <select className="sel" value={status} onChange={e => setStatus(e.target.value)}>
            {CONTRACT_STATUSES.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
          </select>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div>
          <label className="flbl">Başlangıç Tarihi</label>
          <input type="date" className="inp" value={startDate} onChange={e => setStartDate(e.target.value)} />
        </div>
        <div>
          <label className="flbl">Bitiş Tarihi</label>
          <input type="date" className="inp" value={endDate} onChange={e => setEndDate(e.target.value)} />
        </div>
      </div>

      <div className="p-4 rounded-xl space-y-4" style={{ background: '#fffbeb', border: '1px solid #fde68a' }}>
        {/* Göster/Gizle: düğümler HER ZAMAN mount, display ile gizlenir — koşullu mount/unmount
            tarayıcı çeviri uzantılarıyla removeChild/insertBefore çökmesine yol açıyor
            (bkz. hafıza: feedback_react_conditional_sibling_insertbefore, 2026-08-29 canlı hata). */}
        <div className="flex items-center justify-between gap-3"
          style={{ display: isEdit && canReveal ? undefined : 'none' }}>
          <span className="text-xs" style={{ color: 'var(--text-s)' }}>
            <span style={{ display: revealed ? undefined : 'none' }}>Kimlik bilgileri açık metin gösteriliyor — bu görüntüleme denetim kaydına yazıldı.</span>
            <span style={{ display: revealed ? 'none' : undefined }}>Saklı değerleri görmek için "Göster"e basın (denetim kaydına yazılır).</span>
          </span>
          <Button type="button" variant="secondary" size="sm" disabled={revealing}
            onClick={() => {
              if (revealed) {
                setRevealed(false)
                setSchemaValues(initSchemaValues(schema, target))
                setCredRows(extraRows(target?.credentials, schema, 'credentials'))
              } else {
                void revealCredentials()
              }
            }}>
            <EyeOff size={14} style={{ display: revealed ? undefined : 'none' }} />
            <Eye size={14} style={{ display: revealed ? 'none' : undefined }} />
            <span style={{ display: revealed ? undefined : 'none' }}>Gizle</span>
            <span style={{ display: !revealed && !revealing ? undefined : 'none' }}>Göster</span>
            <span style={{ display: !revealed && revealing ? undefined : 'none' }}>Alınıyor…</span>
          </Button>
        </div>
        <p className="text-xs" style={{ color: '#ef4444', display: revealError ? undefined : 'none' }}>{revealError ?? ''}</p>
        {credSchemaFields.length > 0 && (
          <div>
            <label className="flbl mb-1">Kimlik Bilgileri (API)</label>
            <p className="text-xs mb-3" style={{ color: 'var(--text-s)' }}>
              Şifreli saklanır; kayıttan sonra değerler maskeli (•••) görünür. Değiştirmek için
              maskenin üzerine yeni değeri yazın; maskeli bırakılan alan aynen korunur.
            </p>
            <SchemaSectionFields fields={credSchemaFields} values={schemaValues} reveal={revealed}
              onChange={(k, v) => setSchemaValues(s => ({ ...s, [k]: v }))} />
          </div>
        )}
        {(!hasSchema || credRows.length > 0) && (
          <KeyValueEditor
            label={hasSchema ? 'Şema Dışı Kimlik Bilgileri' : 'Kimlik Bilgileri (API)'}
            hint={hasSchema
              ? 'Bu serviste şemada tanımlı olmayan mevcut anahtarlar.'
              : 'Şifreli saklanır; kayıttan sonra değerler maskeli (•••) görünür. Değiştirmek için maskenin üzerine yeni değeri yazın; maskeli bırakılan alan aynen korunur.'}
            rows={credRows} onChange={setCredRows} />
        )}
      </div>

      <div className="p-4 rounded-xl space-y-4" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
        {settingsSchemaFields.length > 0 && (
          <div>
            <label className="flbl mb-3 block">Ayarlar</label>
            <SchemaSectionFields fields={settingsSchemaFields} values={schemaValues}
              onChange={(k, v) => setSchemaValues(s => ({ ...s, [k]: v }))} />
          </div>
        )}
        {(!hasSchema || settingsRows.length > 0) && (
          <KeyValueEditor
            label={hasSchema ? 'Şema Dışı Ayarlar' : 'Ayarlar'}
            rows={settingsRows} onChange={setSettingsRows} />
        )}
      </div>

      <div className="p-4 rounded-xl space-y-4" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
        <KeyValueEditor label="Sözleşme Şartları"
          hint="örn: komisyon %, desi fiyatı, mesaj birim ücreti — servis tipine göre değişir."
          rows={termsRows} onChange={setTermsRows} />
      </div>

      {isEdit && (
        <label className="flex items-center gap-2 cursor-pointer">
          <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
            checked={isActive} onChange={e => setIsActive(e.target.checked)} />
          <span className="text-sm" style={{ color: 'var(--text)' }}>Aktif</span>
        </label>
      )}

      {mutation.isError && (
        <p className="text-sm" style={{ color: '#ef4444' }}>
          {apiErrorMessage(
            mutation.error,
            mutation.error instanceof Error ? mutation.error.message : 'Hata oluştu. Lütfen tekrar deneyin.',
          )}
        </p>
      )}

      <div className="flex justify-end gap-2 pt-1">
        <Button variant="secondary" onClick={onClose} disabled={mutation.isPending}>İptal</Button>
        <Button
          onClick={() => mutation.mutate()}
          loading={mutation.isPending}
          disabled={!isEdit && !integrationServiceId}
        >
          {isEdit ? 'Kaydet' : 'Oluştur'}
        </Button>
      </div>
    </div>
  )
}

// ── Component ─────────────────────────────────────────────────────────────────

export function FirmDetailPage() {
  const { id } = useParams<{ id: string }>()
  const queryClient = useQueryClient()

  const [modalOpen, setModalOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<FirmPlatformWithFirm | null>(null)
  const [integrationModalOpen, setIntegrationModalOpen] = useState(false)
  const [integrationEditTarget, setIntegrationEditTarget] = useState<FirmIntegration | null>(null)

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

  const { data: firmIntegrations = [], isLoading: integrationsLoading } = useQuery<FirmIntegration[]>({
    queryKey: ['firm-integrations', id],
    queryFn: async () => {
      const { data } = await api.get(`/core/firms/${id}/integrations`)
      return data.data ?? []
    },
    enabled: !!id,
  })

  const { data: integrationServices = [] } = useQuery<IntegrationService[]>({
    queryKey: ['integration-services'],
    queryFn: async () => {
      const { data } = await api.get('/core/integration-services')
      return data.data ?? []
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

  function openIntegrationAdd() {
    setIntegrationEditTarget(null)
    setIntegrationModalOpen(true)
  }

  function openIntegrationEdit(fi: FirmIntegration) {
    setIntegrationEditTarget(fi)
    setIntegrationModalOpen(true)
  }

  function closeIntegrationModal() {
    setIntegrationModalOpen(false)
    setIntegrationEditTarget(null)
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
                        {fp.capabilities
                          ? <span className="ml-2"><CapabilityBadges caps={fp.capabilities} compact /></span>
                          : (pt?.isMarketplace ?? fp.platformTypeIsMarketplace) && (
                            <Badge variant="info" className="ml-2">Pazaryeri</Badge>
                          )}
                      </td>
                      <td className="px-4 py-3">
                        <span className="text-sm" style={{ color: 'var(--text-m)' }}>
                          {!fp.priceType ? 'Belirtilmemiş' :
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

      {/* Entegrasyonlar (kargo, ödeme kanalı, fatura entegratörü, SMS vb. sözleşmeler) */}
      <div className="mt-6">
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-base font-semibold" style={{ color: 'var(--text)' }}>
            Entegrasyonlar
            <span className="ml-2 text-sm font-normal" style={{ color: 'var(--text-s)' }}>
              ({firmIntegrations.length})
            </span>
          </h2>
          <Button size="sm" onClick={openIntegrationAdd}><Plus size={14} /> Entegrasyon Ekle</Button>
        </div>

        <div className="card overflow-hidden p-0">
          {integrationsLoading ? (
            <div className="px-4 py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</div>
          ) : (
            <table className="w-full">
              <thead>
                <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
                  {['SERVİS', 'İSİM', 'PLATFORM', 'DÖNEM', 'SÖZLEŞME DURUMU', 'AKTİF', ''].map(h => (
                    <th key={h}
                      className={`px-4 py-3 text-left text-xs font-semibold tracking-wider ${h === '' ? 'w-24' : ''}`}
                      style={{ color: 'var(--text-s)' }}>
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {firmIntegrations.length === 0 && (
                  <tr>
                    <td colSpan={7} className="px-4 py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                      Henüz entegrasyon eklenmemiş.
                    </td>
                  </tr>
                )}
                {firmIntegrations.map(fi => (
                  <tr key={fi.id} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td className="px-4 py-3">
                      <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>
                        {getI18nName(fi.serviceNameI18n)}
                      </span>
                      <Badge variant="info" className="ml-2">{fi.serviceType}</Badge>
                    </td>
                    <td className="px-4 py-3">
                      <span className="text-sm" style={{ color: 'var(--text-m)' }}>{fi.name || '—'}</span>
                    </td>
                    <td className="px-4 py-3">
                      <span className="text-sm" style={{ color: 'var(--text-m)' }}>
                        {fi.firmPlatformId ? getI18nName(fi.firmPlatformNameI18n ?? {}) : 'Tüm platformlar'}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <span className="text-sm" style={{ color: 'var(--text-m)' }}>
                        {fi.startDate || fi.endDate
                          ? `${formatDate(fi.startDate)} – ${formatDate(fi.endDate)}`
                          : '—'}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <Badge variant={CONTRACT_STATUS_BADGE[fi.status] ?? 'neutral'}>
                        {CONTRACT_STATUSES.find(s => s.value === fi.status)?.label ?? fi.status}
                      </Badge>
                    </td>
                    <td className="px-4 py-3">
                      <Badge variant={fi.isActive ? 'success' : 'neutral'}>
                        {fi.isActive ? 'Aktif' : 'Pasif'}
                      </Badge>
                    </td>
                    <td className="px-4 py-3 text-right">
                      <button
                        className="text-xs px-2 py-1 rounded-lg transition-colors"
                        style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
                        onClick={() => openIntegrationEdit(fi)}>
                        Düzenle
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {/* Modal — Add / Edit (Satış Kanalı) */}
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

      {/* Modal — Add / Edit (Entegrasyon) */}
      <Modal
        open={integrationModalOpen}
        onClose={closeIntegrationModal}
        title={integrationEditTarget
          ? `Entegrasyon Düzenle — ${integrationEditTarget.name ?? getI18nName(integrationEditTarget.serviceNameI18n)}`
          : 'Entegrasyon Ekle'}
        size="md"
        footer={null}
      >
        <IntegrationForm
          firmId={firm.id}
          platforms={firm.platforms}
          integrationServices={integrationServices}
          target={integrationEditTarget}
          onClose={closeIntegrationModal}
          onSuccess={closeIntegrationModal}
        />
      </Modal>
    </div>
  )
}

import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ChevronRight, Plus, X } from 'lucide-react'
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
}

interface FirmIntegration {
  id: string
  firmId: string
  integrationServiceId: string
  serviceCode: string
  serviceNameI18n: Record<string, string>
  serviceType: string
  name: string | null
  credentials: Record<string, unknown>
  settings: Record<string, unknown>
  isActive: boolean
  createdAt: string
  contractNumber: string | null
  startDate: string | null
  endDate: string | null
  status: string
  terms: Record<string, unknown> | null
  contactName: string | null
  contactPhone: string | null
  contactEmail: string | null
  documentUrl: string | null
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

// ── Integration Form ──────────────────────────────────────────────────────────

interface IntegrationFormProps {
  firmId: string
  integrationServices: IntegrationService[]
  target: FirmIntegration | null
  onClose: () => void
  onSuccess: () => void
}

function IntegrationForm({ firmId, integrationServices, target, onClose, onSuccess }: IntegrationFormProps) {
  const queryClient = useQueryClient()
  const isEdit = !!target

  const [integrationServiceId, setIntegrationServiceId] = useState(target?.integrationServiceId ?? '')
  const [name, setName] = useState(target?.name ?? '')
  const [isActive, setIsActive] = useState(target?.isActive ?? true)
  const [contractNumber, setContractNumber] = useState(target?.contractNumber ?? '')
  const [startDate, setStartDate] = useState(toDateInputValue(target?.startDate ?? null))
  const [endDate, setEndDate] = useState(toDateInputValue(target?.endDate ?? null))
  const [status, setStatus] = useState(target?.status ?? 'draft')
  const [contactName, setContactName] = useState(target?.contactName ?? '')
  const [contactPhone, setContactPhone] = useState(target?.contactPhone ?? '')
  const [contactEmail, setContactEmail] = useState(target?.contactEmail ?? '')
  const [documentUrl, setDocumentUrl] = useState(target?.documentUrl ?? '')
  const [credRows, setCredRows] = useState<KVRow[]>(() => recordToRows(target?.credentials))
  const [settingsRows, setSettingsRows] = useState<KVRow[]>(() => recordToRows(target?.settings))
  const [termsRows, setTermsRows] = useState<KVRow[]>(() => recordToRows(target?.terms))

  const serviceOptions = integrationServices.map(s => ({
    value: s.id,
    label: `${getI18nName(s.nameI18n)} (${s.serviceType})`,
  }))

  const mutation = useMutation({
    mutationFn: async () => {
      const body = {
        integrationServiceId: isEdit ? undefined : integrationServiceId,
        name: name || null,
        credentials: rowsToRecord(credRows),
        settings: rowsToRecord(settingsRows),
        isActive,
        contractNumber: contractNumber || null,
        startDate: startDate || null,
        endDate: endDate || null,
        status,
        terms: rowsToRecord(termsRows),
        contactName: contactName || null,
        contactPhone: contactPhone || null,
        contactEmail: contactEmail || null,
        documentUrl: documentUrl || null,
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
            onChange={v => setIntegrationServiceId(v ?? '')}
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
          <label className="flbl">Sözleşme No</label>
          <input className="inp" value={contractNumber} onChange={e => setContractNumber(e.target.value)} />
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

      <div className="grid grid-cols-3 gap-4">
        <div>
          <label className="flbl">Yetkili Kişi</label>
          <input className="inp" value={contactName} onChange={e => setContactName(e.target.value)} />
        </div>
        <div>
          <label className="flbl">Telefon</label>
          <input className="inp" value={contactPhone} onChange={e => setContactPhone(e.target.value)} />
        </div>
        <div>
          <label className="flbl">E-posta</label>
          <input className="inp" type="email" value={contactEmail} onChange={e => setContactEmail(e.target.value)} />
        </div>
      </div>

      <div>
        <label className="flbl">Sözleşme Belgesi (URL)</label>
        <input className="inp" value={documentUrl} onChange={e => setDocumentUrl(e.target.value)}
          placeholder="https://…" />
      </div>

      <div className="p-4 rounded-xl space-y-4" style={{ background: '#fffbeb', border: '1px solid #fde68a' }}>
        <KeyValueEditor label="Kimlik Bilgileri (API)" hint="Şifreli/gizli bağlantı bilgileri."
          rows={credRows} onChange={setCredRows} />
      </div>

      <div className="p-4 rounded-xl space-y-4" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
        <KeyValueEditor label="Ayarlar" rows={settingsRows} onChange={setSettingsRows} />
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
          {(mutation.error as any)?.response?.data?.error ?? 'Hata oluştu. Lütfen tekrar deneyin.'}
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
                        {(pt?.isMarketplace ?? fp.platformTypeIsMarketplace) && (
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
                  {['SERVİS', 'İSİM', 'SÖZLEŞME NO', 'DÖNEM', 'SÖZLEŞME DURUMU', 'AKTİF', ''].map(h => (
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
                      <span className="text-sm" style={{ color: 'var(--text-m)' }}>{fi.contractNumber || '—'}</span>
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
          integrationServices={integrationServices}
          target={integrationEditTarget}
          onClose={closeIntegrationModal}
          onSuccess={closeIntegrationModal}
        />
      </Modal>
    </div>
  )
}

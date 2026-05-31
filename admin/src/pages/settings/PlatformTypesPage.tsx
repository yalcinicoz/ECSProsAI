import { useState, useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2, CheckCircle } from 'lucide-react'
import { cn, toSnakeCase } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { I18nField } from '@/components/ui/I18nField'
import { PageSpinner } from '@/components/ui/Spinner'
import { useLanguages } from '@/hooks/useLanguages'
import { FL } from '@/lib/field-labels'
import { buildI18nValues } from '@/lib/i18n-helper'

// ── Types ─────────────────────────────────────────────────────────────────────

export interface SchemaField {
  key: string
  labelI18n: Record<string, string>
  type: 'text' | 'password' | 'number' | 'date' | 'boolean'
  section: 'credentials' | 'settings'
  required: boolean
}

export interface PlatformType {
  id: string
  code: string
  nameI18n: Record<string, string>
  isMarketplace: boolean
  isActive: boolean
  settingsSchema: SchemaField[] | null
}

type FormState = {
  code: string
  nameI18n: Record<string, string>
  isMarketplace: boolean
  isActive: boolean
  schema: SchemaField[]
}

const FIELD_TYPES = ['text', 'password', 'number', 'date', 'boolean'] as const
const SECTIONS = ['credentials', 'settings'] as const
const SECTION_LABELS: Record<string, string> = { credentials: 'Kimlik Bilgileri', settings: 'Ayarlar' }
const TYPE_LABELS: Record<string, string> = { text: 'Metin', password: 'Şifre', number: 'Sayı', date: 'Tarih', boolean: 'Evet/Hayır' }

const emptyField = (): SchemaField => ({ key: '', labelI18n: {}, type: 'text', section: 'credentials', required: false })
const emptyForm = (): FormState => ({ code: '', nameI18n: {}, isMarketplace: false, isActive: true, schema: [] })

function getName(p: Pick<PlatformType, 'nameI18n' | 'code'>) {
  return p.nameI18n?.['tr'] ?? p.nameI18n?.[Object.keys(p.nameI18n ?? {})[0]] ?? p.code
}

export function getFieldLabel(f: SchemaField, lang = 'tr'): string {
  if (!f.labelI18n) return f.key
  return f.labelI18n[lang] ?? f.labelI18n['tr'] ?? f.labelI18n[Object.keys(f.labelI18n)[0]] ?? f.key
}

// ── Schema Field Card ─────────────────────────────────────────────────────────

function SchemaFieldCard({
  field,
  idx,
  onChange,
  onRemove,
  languages,
}: {
  field: SchemaField
  idx: number
  onChange: (patch: Partial<SchemaField>) => void
  onRemove: () => void
  languages: { code: string; name: string; isDefault?: boolean }[]
}) {
  const [activeLang, setActiveLang] = useState(languages.find(l => l.isDefault)?.code ?? languages[0]?.code ?? 'tr')

  return (
    <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
      {/* Top row: key, type, section, required, delete */}
      <div className="grid grid-cols-12 gap-2 items-center px-3 py-2.5"
        style={{ background: 'var(--surface2)', borderBottom: '1px solid var(--border)' }}>
        <div className="col-span-1 text-xs font-semibold text-center" style={{ color: 'var(--text-s)' }}>
          #{idx + 1}
        </div>
        <div className="col-span-4">
          <input
            className="inp"
            style={{ padding: '4px 8px', fontSize: 12 }}
            placeholder="anahtar (örn: api_key)"
            value={field.key}
            onChange={e => onChange({ key: e.target.value.replace(/\s/g, '_').toLowerCase() })}
          />
        </div>
        <div className="col-span-2">
          <select className="sel" style={{ padding: '4px 6px', fontSize: 12 }}
            value={field.type}
            onChange={e => onChange({ type: e.target.value as SchemaField['type'] })}>
            {FIELD_TYPES.map(t => <option key={t} value={t}>{TYPE_LABELS[t]}</option>)}
          </select>
        </div>
        <div className="col-span-3">
          <select className="sel" style={{ padding: '4px 6px', fontSize: 12 }}
            value={field.section}
            onChange={e => onChange({ section: e.target.value as SchemaField['section'] })}>
            {SECTIONS.map(s => <option key={s} value={s}>{SECTION_LABELS[s]}</option>)}
          </select>
        </div>
        <div className="col-span-1 flex items-center justify-center">
          <label title="Zorunlu" className="cursor-pointer">
            <input type="checkbox" className="w-3.5 h-3.5 accent-[var(--brand)]"
              checked={field.required}
              onChange={e => onChange({ required: e.target.checked })} />
          </label>
        </div>
        <div className="col-span-1 flex items-center justify-center">
          <button onClick={onRemove} className="rounded p-0.5 transition-colors hover:opacity-60">
            <Trash2 size={13} style={{ color: '#ef4444' }} />
          </button>
        </div>
      </div>

      {/* Label i18n — tabbed */}
      <div className="px-3 py-2.5">
        <div className="flex items-center gap-0.5 mb-2">
          <span className="text-xs mr-2" style={{ color: 'var(--text-s)' }}>Etiket:</span>
          {languages.map(lang => (
            <button
              key={lang.code}
              onClick={() => setActiveLang(lang.code)}
              className={cn(
                'px-2.5 py-0.5 rounded-lg text-xs font-medium transition-all',
                activeLang === lang.code
                  ? 'text-[var(--brand)]'
                  : 'text-[var(--text-s)] hover:text-[var(--text-m)]'
              )}
              style={activeLang === lang.code
                ? { background: 'var(--brand-bg)', border: '1px solid var(--brand-b)' }
                : { border: '1px solid transparent' }}>
              {lang.code.toUpperCase()}
              {field.labelI18n?.[lang.code] && (
                <span className="ml-1 inline-block w-1.5 h-1.5 rounded-full bg-current opacity-60" />
              )}
            </button>
          ))}
        </div>
        <input
          className="inp"
          style={{ padding: '5px 10px', fontSize: 13 }}
          placeholder={`Etiket (${activeLang.toUpperCase()})`}
          value={field.labelI18n?.[activeLang] ?? ''}
          onChange={e => onChange({ labelI18n: { ...field.labelI18n, [activeLang]: e.target.value } })}
        />
      </div>
    </div>
  )
}

// ── Schema Editor ─────────────────────────────────────────────────────────────

function SchemaEditor({
  schema, onChange, languages,
}: {
  schema: SchemaField[]
  onChange: (s: SchemaField[]) => void
  languages: { code: string; name: string; isDefault?: boolean }[]
}) {
  function updateField(idx: number, patch: Partial<SchemaField>) {
    onChange(schema.map((f, i) => i === idx ? { ...f, ...patch } : f))
  }
  function removeField(idx: number) {
    onChange(schema.filter((_, i) => i !== idx))
  }
  function addField() {
    onChange([...schema, emptyField()])
  }

  return (
    <div className="space-y-3">
      {schema.length === 0 && (
        <p className="text-xs py-2 text-center" style={{ color: 'var(--text-s)' }}>
          Henüz alan tanımlanmadı.
        </p>
      )}

      {/* Column headers */}
      {schema.length > 0 && (
        <div className="grid grid-cols-12 gap-2 px-3">
          <div className="col-span-1" />
          <div className="col-span-4 text-xs font-semibold" style={{ color: 'var(--text-s)' }}>ANAHTAR</div>
          <div className="col-span-2 text-xs font-semibold" style={{ color: 'var(--text-s)' }}>TİP</div>
          <div className="col-span-3 text-xs font-semibold" style={{ color: 'var(--text-s)' }}>BÖLÜM</div>
          <div className="col-span-1 text-xs font-semibold text-center" style={{ color: 'var(--text-s)' }}>ZOR.</div>
          <div className="col-span-1" />
        </div>
      )}

      {schema.map((f, idx) => (
        <SchemaFieldCard
          key={idx}
          field={f}
          idx={idx}
          onChange={patch => updateField(idx, patch)}
          onRemove={() => removeField(idx)}
          languages={languages}
        />
      ))}

      <button
        onClick={addField}
        className="w-full flex items-center justify-center gap-1.5 py-2 rounded-xl text-xs font-medium transition-colors hover:bg-[var(--surface2)]"
        style={{ border: '1px dashed var(--border)', color: 'var(--brand)' }}>
        <Plus size={12} /> Alan Ekle
      </button>

      {schema.length > 0 && (
        <p className="text-xs" style={{ color: 'var(--text-s)' }}>
          <strong>Kimlik Bilgileri</strong>: API anahtarı, şifre — hassas veriler.{' '}
          <strong>Ayarlar</strong>: Sözleşme tarihi, komisyon vb.{' '}
          <strong>Zor.</strong>: Kanal oluşturulurken zorunlu alan.
        </p>
      )}
    </div>
  )
}

// ── Main Component ────────────────────────────────────────────────────────────

export function PlatformTypesPage() {
  const queryClient = useQueryClient()
  const { data: languages = [], isLoading: langsLoading } = useLanguages()

  const [activeOnly, setActiveOnly] = useState(false)
  const [createOpen, setCreateOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<PlatformType | null>(null)
  const [form, setForm] = useState<FormState>(emptyForm())
  const [savedOk, setSavedOk] = useState(false)

  const { data: platformTypes = [], isLoading } = useQuery<PlatformType[]>({
    queryKey: ['platform-types', activeOnly],
    queryFn: async () => {
      const { data } = await api.get(`/core/platform-types?activeOnly=${activeOnly}`)
      return data.data
    },
  })

  const sourceLang = languages.find(l => l.isDefault)?.code ?? 'tr'
  const i18nValues = useMemo(() => buildI18nValues(form.nameI18n, languages), [form.nameI18n, languages])
  const i18nFields = useMemo(() => [{ key: 'name', labels: FL.name, required: true }], [])
  const autoCode = toSnakeCase(form.nameI18n['tr'] ?? form.nameI18n[sourceLang] ?? '')

  const createMutation = useMutation({
    mutationFn: async () => {
      await api.post('/core/platform-types', {
        code: autoCode,
        nameI18n: form.nameI18n,
        isMarketplace: form.isMarketplace,
        settingsSchema: form.schema.length > 0 ? form.schema : null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['platform-types'] })
      setCreateOpen(false)
    },
  })

  const updateMutation = useMutation({
    mutationFn: async () => {
      if (!editTarget) return
      await api.put(`/core/platform-types/${editTarget.id}`, {
        nameI18n: form.nameI18n,
        isMarketplace: form.isMarketplace,
        isActive: form.isActive,
        settingsSchema: form.schema.length > 0 ? form.schema : null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['platform-types'] })
      setSavedOk(true)
      setTimeout(() => setSavedOk(false), 2500)
      // Modal KAPANMIYOR — kullanıcı manuel kapatır
    },
  })

  function openCreate() {
    setForm(emptyForm())
    createMutation.reset()
    setCreateOpen(true)
  }

  function openEdit(p: PlatformType, e: React.MouseEvent) {
    e.stopPropagation()
    setEditTarget(p)
    setSavedOk(false)
    updateMutation.reset()
    setForm({
      code: p.code,
      nameI18n: { ...p.nameI18n },
      isMarketplace: p.isMarketplace,
      isActive: p.isActive,
      schema: p.settingsSchema ? p.settingsSchema.map(f => ({ ...f, labelI18n: { ...f.labelI18n } })) : [],
    })
  }

  function closeEdit() {
    setEditTarget(null)
    setSavedOk(false)
  }

  if (isLoading || langsLoading) return <PageSpinner />

  const formBody = (isEdit: boolean) => (
    <div className="space-y-5">
      <div className="flex flex-wrap gap-4">
        <label className="flex items-center gap-2 cursor-pointer">
          <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
            checked={form.isMarketplace}
            onChange={e => setForm(f => ({ ...f, isMarketplace: e.target.checked }))} />
          <span className="text-sm" style={{ color: 'var(--text)' }}>Pazaryeri kanalı</span>
        </label>
        {isEdit && (
          <label className="flex items-center gap-2 cursor-pointer">
            <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
              checked={form.isActive}
              onChange={e => setForm(f => ({ ...f, isActive: e.target.checked }))} />
            <span className="text-sm" style={{ color: 'var(--text)' }}>Aktif</span>
          </label>
        )}
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

      <div>
        <label className="flbl mb-2 block">Kanal Alan Şeması</label>
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
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Platform Tipleri</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            Trendyol, Hepsiburada, Web Sitesi gibi kanal tiplerini ve alan şemalarını yönetin
          </p>
        </div>
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-1 rounded-xl p-1"
            style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
            {[false, true].map(v => (
              <button key={String(v)} onClick={() => setActiveOnly(v)}
                className={cn('px-3 py-1 rounded-lg text-sm font-medium transition-all',
                  activeOnly === v ? 'bg-white shadow-sm' : 'text-[var(--text-s)]')}
                style={activeOnly === v ? { color: 'var(--text)' } : {}}>
                {v ? 'Aktif' : 'Tümü'}
              </button>
            ))}
          </div>
          <Button size="sm" onClick={openCreate}><Plus size={14} /> Yeni Platform Tipi</Button>
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
            {platformTypes.length === 0 && (
              <tr><td colSpan={6} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Platform tipi bulunamadı.
              </td></tr>
            )}
            {platformTypes.map(p => (
              <tr key={p.id} style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3">
                  <code className="text-xs px-2 py-0.5 rounded-md font-mono"
                    style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>
                    {p.code}
                  </code>
                </td>
                <td className="px-4 py-3">
                  <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{getName(p)}</span>
                </td>
                <td className="px-4 py-3">
                  <Badge variant={p.isMarketplace ? 'info' : 'neutral'}>
                    {p.isMarketplace ? 'Pazaryeri' : 'Özel Kanal'}
                  </Badge>
                </td>
                <td className="px-4 py-3">
                  {p.settingsSchema && p.settingsSchema.length > 0 ? (
                    <div className="flex flex-wrap gap-1">
                      {p.settingsSchema.slice(0, 3).map(f => (
                        <span key={f.key} className="text-xs px-1.5 py-0.5 rounded"
                          style={{
                            background: f.section === 'credentials' ? '#fef3c7' : 'var(--surface2)',
                            color: f.section === 'credentials' ? '#92400e' : 'var(--text-s)',
                            border: '1px solid var(--border)',
                          }}>
                          {getFieldLabel(f)}
                        </span>
                      ))}
                      {p.settingsSchema.length > 3 && (
                        <span className="text-xs" style={{ color: 'var(--text-s)' }}>
                          +{p.settingsSchema.length - 3}
                        </span>
                      )}
                    </div>
                  ) : (
                    <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span>
                  )}
                </td>
                <td className="px-4 py-3">
                  <Badge variant={p.isActive ? 'success' : 'neutral'}>{p.isActive ? 'Aktif' : 'Pasif'}</Badge>
                </td>
                <td className="px-4 py-3 text-right">
                  <button className="text-xs px-2 py-1 rounded-lg transition-colors"
                    style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
                    onClick={e => openEdit(p, e)}>
                    Düzenle
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Create Modal */}
      <Modal open={createOpen} onClose={() => setCreateOpen(false)} title="Yeni Platform Tipi" size="lg"
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

      {/* Edit Modal — Kaydet kapanmaz, kullanıcı manuel kapatır */}
      <Modal open={!!editTarget} onClose={closeEdit}
        title={`Platform Tipi Düzenle — ${editTarget ? getName(editTarget) : ''}`}
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

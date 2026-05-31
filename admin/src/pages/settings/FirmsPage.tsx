import { useState, useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Plus, ChevronRight } from 'lucide-react'
import { cn } from '@/lib/utils'
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

export interface Firm {
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
}

type FirmForm = {
  code: string
  nameI18n: Record<string, string>
  taxOffice: string
  taxNumber: string
  address: string
  phone: string
  email: string
  isMain: boolean
  priceType: string
  priceMultiplier: string
  isActive: boolean
}

const PRICE_TYPES = [
  { value: 'manual',     label: 'Manuel' },
  { value: 'multiplier', label: 'Çarpan' },
]

const emptyForm = (): FirmForm => ({
  code: '', nameI18n: {}, taxOffice: '', taxNumber: '',
  address: '', phone: '', email: '',
  isMain: false, priceType: 'manual', priceMultiplier: '', isActive: true,
})

function getFirmName(f: Firm) {
  return f.nameI18n['tr'] ?? f.nameI18n[Object.keys(f.nameI18n)[0]] ?? f.code
}

// ── Component ─────────────────────────────────────────────────────────────────

export function FirmsPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { data: languages = [], isLoading: langsLoading } = useLanguages()

  const [createOpen, setCreateOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<Firm | null>(null)
  const [form, setForm] = useState<FirmForm>(emptyForm())

  const { data: firms = [], isLoading } = useQuery<Firm[]>({
    queryKey: ['firms'],
    queryFn: async () => {
      const { data } = await api.get('/core/firms?activeOnly=false')
      return data.data
    },
  })

  const sourceLang = languages.find(l => l.isDefault)?.code ?? 'tr'
  const i18nValues = useMemo(() => buildI18nValues(form.nameI18n, languages), [form.nameI18n, languages])
  const i18nFields = useMemo(() => [{ key: 'name', labels: FL.name, required: true }], [])

  const createMutation = useMutation({
    mutationFn: async () => {
      await api.post('/core/firms', {
        code: form.code.trim().toLowerCase(),
        nameI18n: form.nameI18n,
        taxOffice: form.taxOffice,
        taxNumber: form.taxNumber,
        address: form.address,
        phone: form.phone,
        email: form.email,
        isMain: form.isMain,
        priceType: form.priceType,
        priceMultiplier: form.priceMultiplier ? parseFloat(form.priceMultiplier) : null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['firms'] })
      setCreateOpen(false)
    },
  })

  const updateMutation = useMutation({
    mutationFn: async () => {
      if (!editTarget) return
      await api.put(`/core/firms/${editTarget.id}`, {
        nameI18n: form.nameI18n,
        taxOffice: form.taxOffice,
        taxNumber: form.taxNumber,
        address: form.address,
        phone: form.phone,
        email: form.email,
        isMain: form.isMain,
        priceType: form.priceType,
        priceMultiplier: form.priceMultiplier ? parseFloat(form.priceMultiplier) : null,
        isActive: form.isActive,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['firms'] })
      setEditTarget(null)
    },
  })

  function openCreate() {
    setForm(emptyForm())
    setCreateOpen(true)
  }

  function openEdit(f: Firm, e: React.MouseEvent) {
    e.stopPropagation()
    setEditTarget(f)
    setForm({
      code: f.code,
      nameI18n: { ...f.nameI18n },
      taxOffice: f.taxOffice,
      taxNumber: f.taxNumber,
      address: f.address,
      phone: f.phone,
      email: f.email,
      isMain: f.isMain,
      priceType: f.priceType,
      priceMultiplier: f.priceMultiplier?.toString() ?? '',
      isActive: f.isActive,
    })
  }

  if (isLoading || langsLoading) return <PageSpinner />

  const formBody = (isEdit: boolean) => (
    <div className="space-y-4">
      {!isEdit && (
        <div>
          <label className="flbl">Kod <span className="text-red-500">*</span></label>
          <input className="inp" value={form.code}
            onChange={e => setForm(f => ({ ...f, code: e.target.value.toLowerCase() }))}
            placeholder="Örn: main, firma-a" />
          <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>Küçük harf, boşluksuz. Sonradan değiştirilemez.</p>
        </div>
      )}
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="flbl">Vergi Dairesi</label>
          <input className="inp" value={form.taxOffice}
            onChange={e => setForm(f => ({ ...f, taxOffice: e.target.value }))}
            placeholder="Kadıköy VD" />
        </div>
        <div>
          <label className="flbl">Vergi No</label>
          <input className="inp" value={form.taxNumber}
            onChange={e => setForm(f => ({ ...f, taxNumber: e.target.value }))}
            placeholder="1234567890" />
        </div>
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="flbl">Telefon</label>
          <input className="inp" value={form.phone}
            onChange={e => setForm(f => ({ ...f, phone: e.target.value }))}
            placeholder="+90 212 000 0000" />
        </div>
        <div>
          <label className="flbl">E-posta</label>
          <input className="inp" type="email" value={form.email}
            onChange={e => setForm(f => ({ ...f, email: e.target.value }))}
            placeholder="info@firma.com" />
        </div>
      </div>
      <div>
        <label className="flbl">Adres</label>
        <textarea className="ta" rows={2} value={form.address}
          onChange={e => setForm(f => ({ ...f, address: e.target.value }))}
          placeholder="Tam adres" />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="flbl">Fiyatlama Tipi</label>
          <select className="inp" value={form.priceType}
            onChange={e => setForm(f => ({ ...f, priceType: e.target.value }))}>
            {PRICE_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
          </select>
        </div>
        {form.priceType === 'multiplier' && (
          <div>
            <label className="flbl">Çarpan</label>
            <input className="inp" type="number" step="0.01" value={form.priceMultiplier}
              onChange={e => setForm(f => ({ ...f, priceMultiplier: e.target.value }))}
              placeholder="Örn: 1.20" />
          </div>
        )}
      </div>
      <div className="flex items-center gap-4">
        <label className="flex items-center gap-2 cursor-pointer">
          <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
            checked={form.isMain}
            onChange={e => setForm(f => ({ ...f, isMain: e.target.checked }))} />
          <span className="text-sm" style={{ color: 'var(--text)' }}>Ana firma</span>
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
    </div>
  )

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Firmalar</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{firms.length} kayıt</p>
        </div>
        <Button size="sm" onClick={openCreate}><Plus size={14} /> Yeni Firma</Button>
      </div>

      <div className="card overflow-hidden p-0">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['KOD', 'AD', 'VERGİ NO', 'TELEFON', 'ANA', 'DURUM', ''].map(h => (
                <th key={h} className={cn('px-4 py-3 text-xs font-semibold tracking-wider', h === '' ? 'w-32' : 'text-left')}
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {firms.length === 0 && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Firma bulunamadı.
              </td></tr>
            )}
            {firms.map(f => (
              <tr key={f.id}
                onClick={() => navigate(`/settings/firms/${f.id}`)}
                className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3">
                  <code className="text-xs px-2 py-0.5 rounded-md font-mono"
                    style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>
                    {f.code}
                  </code>
                </td>
                <td className="px-4 py-3">
                  <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{getFirmName(f)}</span>
                </td>
                <td className="px-4 py-3">
                  <span className="text-sm" style={{ color: 'var(--text-m)' }}>{f.taxNumber || '—'}</span>
                </td>
                <td className="px-4 py-3">
                  <span className="text-sm" style={{ color: 'var(--text-m)' }}>{f.phone || '—'}</span>
                </td>
                <td className="px-4 py-3 text-center">
                  {f.isMain && <Badge variant="warning">Ana</Badge>}
                </td>
                <td className="px-4 py-3">
                  <Badge variant={f.isActive ? 'success' : 'neutral'}>{f.isActive ? 'Aktif' : 'Pasif'}</Badge>
                </td>
                <td className="px-4 py-3 text-right">
                  <div className="flex items-center justify-end gap-2">
                    <button
                      className="text-xs px-2 py-1 rounded-lg transition-colors"
                      style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
                      onClick={e => openEdit(f, e)}>
                      Düzenle
                    </button>
                    <ChevronRight size={14} style={{ color: 'var(--text-s)' }} />
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Create */}
      <Modal open={createOpen} onClose={() => setCreateOpen(false)} title="Yeni Firma" size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button onClick={() => createMutation.mutate()} loading={createMutation.isPending}
              disabled={!form.code || !form.nameI18n[sourceLang]}>
              Oluştur
            </Button>
          </>
        }>
        {formBody(false)}
      </Modal>

      {/* Edit */}
      <Modal open={!!editTarget} onClose={() => setEditTarget(null)} title="Firma Düzenle" size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setEditTarget(null)}>İptal</Button>
            <Button onClick={() => updateMutation.mutate()} loading={updateMutation.isPending}>Kaydet</Button>
          </>
        }>
        {formBody(true)}
      </Modal>
    </div>
  )
}

import { useState, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Pencil, Check } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { cn } from '@/lib/utils'

// ── Types ─────────────────────────────────────────────────────────────────────

interface CatalogSetting { key: string; value: string }

interface ImageSet {
  id: string
  code: string
  name: string
  isDefault: boolean
  fallbackSetId: string | null
  fallbackSetName: string | null
  sortPriority: number
  isActive: boolean
}

// ── Image Server Keys ─────────────────────────────────────────────────────────

const IMAGE_SERVER_FIELDS: { key: string; label: string; type: string; hint?: string; section?: string }[] = [
  { key: 'ImageServer.LocalSavePath', label: 'Resim Kayıt Dizini', type: 'text',     hint: 'örn: /opt/ECSProsAI/media/images/products/', section: 'Resim Sunucusu' },
  { key: 'ImageServer.PublicBaseUrl', label: 'Resim Sunucu URL',   type: 'text',     hint: 'örn: /media/images/products/' },
  { key: 'ImageServer.FtpHost',       label: 'FTP Sunucu Adresi',  type: 'text',     hint: 'örn: ftp.imageserver.com' },
  { key: 'ImageServer.FtpPort',       label: 'FTP Port',           type: 'number',   hint: '21' },
  { key: 'ImageServer.FtpUser',       label: 'FTP Kullanıcı Adı',  type: 'text' },
  { key: 'ImageServer.FtpPassword',   label: 'FTP Şifre',          type: 'password' },
  { key: 'ImageServer.FtpBasePath',   label: 'FTP Dosya Yolu',     type: 'text',     hint: 'örn: /images/products/' },
  { key: 'VideoServer.LocalSavePath', label: 'Video Kayıt Dizini', type: 'text',     hint: 'örn: /opt/ECSProsAI/media/videos/products/', section: 'Video Sunucusu' },
  { key: 'VideoServer.PublicBaseUrl', label: 'Video Sunucu URL',   type: 'text',     hint: 'örn: /media/videos/products/' },
  { key: 'VideoServer.FtpHost',       label: 'FTP Sunucu Adresi',  type: 'text',     hint: 'örn: ftp.imageserver.com' },
  { key: 'VideoServer.FtpPort',       label: 'FTP Port',           type: 'number',   hint: '21' },
  { key: 'VideoServer.FtpUser',       label: 'FTP Kullanıcı Adı',  type: 'text' },
  { key: 'VideoServer.FtpPassword',   label: 'FTP Şifre',          type: 'password' },
  { key: 'VideoServer.FtpBasePath',   label: 'FTP Dosya Yolu',     type: 'text',     hint: 'örn: /videos/products/' },
]

// ── CatalogSettingsPage ───────────────────────────────────────────────────────

export function CatalogSettingsPage() {
  const [tab, setTab] = useState<'image-server' | 'image-sets'>('image-server')

  return (
    <div className="flex-1 flex flex-col">
      {/* Header */}
      <div className="vh">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h1 className="text-lg font-bold">Katalog Ayarları</h1>
            <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
              Resim sunucusu ve resim seti yönetimi
            </p>
          </div>
        </div>

        {/* Tabs */}
        <div className="tab-scroll mt-4">
          {([
            ['image-server', 'Resim Sunucusu'],
            ['image-sets',   'Resim Setleri'],
          ] as const).map(([key, label]) => (
            <button
              key={key}
              onClick={() => setTab(key)}
              className={cn('stab', tab === key && 'active')}
            >
              {label}
            </button>
          ))}
        </div>
      </div>

      {/* Content */}
      <div className="vc">
        {tab === 'image-server' && <ImageServerTab />}
        {tab === 'image-sets'   && <ImageSetsTab />}
      </div>
    </div>
  )
}

// ── ImageServerTab ────────────────────────────────────────────────────────────

function ImageServerTab() {
  const qc = useQueryClient()
  const [draft, setDraft] = useState<Record<string, string>>({})
  const [saved, setSaved] = useState(false)

  const { data: settings = [], isLoading } = useQuery<CatalogSetting[]>({
    queryKey: ['catalog-settings'],
    queryFn: async () => {
      const { data } = await api.get('/catalog/settings')
      return data.data as CatalogSetting[]
    },
  })

  // Populate draft from loaded settings
  useEffect(() => {
    if (!settings.length) return
    const map: Record<string, string> = {}
    for (const f of IMAGE_SERVER_FIELDS) {
      map[f.key] = settings.find(s => s.key === f.key)?.value ?? ''
    }
    setDraft(map)
  }, [settings])

  const saveMutation = useMutation({
    mutationFn: async () => {
      for (const f of IMAGE_SERVER_FIELDS) {
        await api.put(`/catalog/settings/${f.key}`, { value: draft[f.key] ?? '' })
      }
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['catalog-settings'] })
      setSaved(true)
      setTimeout(() => setSaved(false), 2500)
    },
  })

  if (isLoading) return <PageSpinner />

  return (
    <div className="card" style={{ maxWidth: 560 }}>
      <p className="text-xs mb-5" style={{ color: 'var(--text-s)' }}>
        Resim yükleme için FTP sunucu bilgileri. Değişiklikler anında geçerli olur.
      </p>

      <div className="space-y-4">
        {IMAGE_SERVER_FIELDS.map((f, i) => (
          <div key={f.key}>
            {f.section && (
              <p className={cn('text-xs font-semibold uppercase tracking-wide mb-3', i > 0 && 'mt-6 pt-5 border-t border-[var(--border)]')} style={{ color: 'var(--text-s)' }}>
                {f.section}
              </p>
            )}
            <label className="flbl">{f.label}</label>
            <input
              className={cn('inp', draft[f.key] && 'ok')}
              type={f.type}
              placeholder={f.hint}
              value={draft[f.key] ?? ''}
              onChange={e => setDraft(d => ({ ...d, [f.key]: e.target.value }))}
            />
          </div>
        ))}
      </div>

      {saveMutation.isError && (
        <p className="text-sm mt-4" style={{ color: '#ef4444' }}>
          {(saveMutation.error as any)?.response?.data?.error ?? 'Kayıt sırasında hata oluştu.'}
        </p>
      )}

      <div className="flex items-center justify-end gap-3 mt-6 pt-5" style={{ borderTop: '1px solid var(--border)' }}>
        {saved && (
          <span className="flex items-center gap-1.5 text-sm" style={{ color: 'var(--brand)' }}>
            <Check size={14} /> Kaydedildi
          </span>
        )}
        <Button onClick={() => saveMutation.mutate()} loading={saveMutation.isPending}>
          Kaydet
        </Button>
      </div>
    </div>
  )
}

// ── ImageSetsTab ──────────────────────────────────────────────────────────────

interface ImageSetFormState {
  code: string
  name: string
  isDefault: boolean
  fallbackSetId: string
  sortPriority: number
  isActive: boolean
}

const emptyForm = (): ImageSetFormState => ({
  code: '', name: '', isDefault: false, fallbackSetId: '', sortPriority: 0, isActive: true,
})

function ImageSetsTab() {
  const qc = useQueryClient()
  const [modal, setModal] = useState<{ mode: 'create' | 'edit'; set?: ImageSet } | null>(null)
  const [form, setForm] = useState<ImageSetFormState>(emptyForm())

  const { data: sets = [], isLoading } = useQuery<ImageSet[]>({
    queryKey: ['image-sets', false],
    queryFn: async () => {
      const { data } = await api.get('/catalog/image-sets?activeOnly=false')
      return data.data as ImageSet[]
    },
  })

  const openCreate = () => { setForm(emptyForm()); setModal({ mode: 'create' }) }
  const openEdit = (s: ImageSet) => {
    setForm({
      code: s.code, name: s.name, isDefault: s.isDefault,
      fallbackSetId: s.fallbackSetId ?? '', sortPriority: s.sortPriority, isActive: s.isActive,
    })
    setModal({ mode: 'edit', set: s })
  }

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (modal?.mode === 'create') {
        await api.post('/catalog/image-sets', {
          code: form.code.trim(),
          name: form.name.trim(),
          isDefault: form.isDefault,
          fallbackSetId: form.fallbackSetId || null,
          sortPriority: form.sortPriority,
        })
      } else {
        await api.put(`/catalog/image-sets/${modal!.set!.id}`, {
          name: form.name.trim(),
          isDefault: form.isDefault,
          fallbackSetId: form.fallbackSetId || null,
          sortPriority: form.sortPriority,
          isActive: form.isActive,
        })
      }
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['image-sets'] })
      setModal(null)
    },
  })

  const canSave = form.name.trim().length > 0 && (modal?.mode === 'edit' || form.code.trim().length > 0)

  const otherSets = sets.filter(s => s.id !== modal?.set?.id)

  if (isLoading) return <PageSpinner />

  return (
    <>
      <div className="flex items-center justify-between mb-4">
        <p className="text-sm" style={{ color: 'var(--text-s)' }}>
          {sets.length} set tanımlı
        </p>
        <Button size="sm" onClick={openCreate}>
          <Plus size={14} className="mr-1.5" /> Yeni Set
        </Button>
      </div>

      <div className="card p-0 overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              <th className="text-left px-4 py-3 font-semibold text-xs" style={{ color: 'var(--text-s)' }}>KOD</th>
              <th className="text-left px-4 py-3 font-semibold text-xs" style={{ color: 'var(--text-s)' }}>AD</th>
              <th className="text-left px-4 py-3 font-semibold text-xs" style={{ color: 'var(--text-s)' }}>VARSAYILAN</th>
              <th className="text-left px-4 py-3 font-semibold text-xs" style={{ color: 'var(--text-s)' }}>FALLBACK</th>
              <th className="text-left px-4 py-3 font-semibold text-xs" style={{ color: 'var(--text-s)' }}>ÖNCELİK</th>
              <th className="text-left px-4 py-3 font-semibold text-xs" style={{ color: 'var(--text-s)' }}>DURUM</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {sets.length === 0 ? (
              <tr>
                <td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                  Henüz resim seti tanımlanmamış.
                </td>
              </tr>
            ) : (
              sets
                .sort((a, b) => a.sortPriority - b.sortPriority || a.name.localeCompare(b.name))
                .map(s => (
                  <tr
                    key={s.id}
                    className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                    style={{ borderBottom: '1px solid var(--border)' }}
                    onClick={() => openEdit(s)}
                  >
                    <td className="px-4 py-3 font-mono text-xs" style={{ color: 'var(--text-m)' }}>{s.code}</td>
                    <td className="px-4 py-3 font-medium">{s.name}</td>
                    <td className="px-4 py-3">
                      {s.isDefault && (
                        <Badge variant="success">Varsayılan</Badge>
                      )}
                    </td>
                    <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-m)' }}>
                      {s.fallbackSetName ?? <span style={{ color: 'var(--text-s)' }}>—</span>}
                    </td>
                    <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-m)' }}>{s.sortPriority}</td>
                    <td className="px-4 py-3">
                      <Badge variant={s.isActive ? 'success' : 'neutral'}>
                        {s.isActive ? 'Aktif' : 'Pasif'}
                      </Badge>
                    </td>
                    <td className="px-4 py-3 text-right">
                      <button
                        onClick={e => { e.stopPropagation(); openEdit(s) }}
                        className="p-1.5 rounded-lg hover:bg-[var(--brand-bg)] transition-colors"
                        style={{ color: 'var(--text-s)' }}
                      >
                        <Pencil size={13} />
                      </button>
                    </td>
                  </tr>
                ))
            )}
          </tbody>
        </table>
      </div>

      {/* Create / Edit Modal */}
      <Modal
        open={!!modal}
        onClose={() => setModal(null)}
        title={modal?.mode === 'create' ? 'Yeni Resim Seti' : 'Resim Setini Düzenle'}
      >
        <div className="space-y-4">
          {modal?.mode === 'create' && (
            <div>
              <label className="flbl">Kod <span className="text-amber-500 font-bold">*</span></label>
              <input
                className={cn('inp', form.code.trim() && 'ok')}
                placeholder="örn: standart"
                value={form.code}
                onChange={e => setForm(f => ({ ...f, code: e.target.value.toLowerCase().replace(/\s/g, '-') }))}
              />
              <p className="text-[11px] mt-1" style={{ color: 'var(--text-s)' }}>
                Oluşturulduktan sonra değiştirilemez. Dosya adlarında kullanılır.
              </p>
            </div>
          )}

          <div>
            <label className="flbl">Ad <span className="text-amber-500 font-bold">*</span></label>
            <input
              className={cn('inp', form.name.trim() && 'ok')}
              placeholder="örn: Standart Çekim"
              value={form.name}
              onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
            />
          </div>

          <div>
            <label className="flbl">Fallback Set</label>
            <select
              className="sel"
              value={form.fallbackSetId}
              onChange={e => setForm(f => ({ ...f, fallbackSetId: e.target.value }))}
            >
              <option value="">— Yok —</option>
              {otherSets.map(s => (
                <option key={s.id} value={s.id}>{s.name} ({s.code})</option>
              ))}
            </select>
            <p className="text-[11px] mt-1" style={{ color: 'var(--text-s)' }}>
              Bu sette resim yoksa kullanılacak yedek set.
            </p>
          </div>

          <div>
            <label className="flbl">Sıra Önceliği</label>
            <input
              className="inp"
              type="number"
              min={0}
              value={form.sortPriority}
              onChange={e => setForm(f => ({ ...f, sortPriority: parseInt(e.target.value) || 0 }))}
            />
          </div>

          <div className="flex items-center gap-4 pt-1">
            <label className="flex items-center gap-2 cursor-pointer select-none">
              <input
                type="checkbox"
                className="w-4 h-4 rounded accent-[var(--brand)]"
                checked={form.isDefault}
                onChange={e => setForm(f => ({ ...f, isDefault: e.target.checked }))}
              />
              <span className="text-sm font-medium">Varsayılan Set</span>
            </label>

            {modal?.mode === 'edit' && (
              <label className="flex items-center gap-2 cursor-pointer select-none">
                <input
                  type="checkbox"
                  className="w-4 h-4 rounded accent-[var(--brand)]"
                  checked={form.isActive}
                  onChange={e => setForm(f => ({ ...f, isActive: e.target.checked }))}
                />
                <span className="text-sm font-medium">Aktif</span>
              </label>
            )}
          </div>

          {saveMutation.isError && (
            <p className="text-sm" style={{ color: '#ef4444' }}>
              {(saveMutation.error as any)?.response?.data?.error ?? 'Hata oluştu.'}
            </p>
          )}

          <div className="flex justify-end gap-2 pt-2">
            <Button variant="secondary" onClick={() => setModal(null)}>İptal</Button>
            <Button
              onClick={() => saveMutation.mutate()}
              loading={saveMutation.isPending}
              disabled={!canSave}
            >
              {modal?.mode === 'create' ? 'Oluştur' : 'Kaydet'}
            </Button>
          </div>
        </div>
      </Modal>
    </>
  )
}

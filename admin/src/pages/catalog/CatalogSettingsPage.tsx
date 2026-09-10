import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Check, Trash2 } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText as gridErrText } from '@/components/ui/DataTable.utils'
import { cn } from '@/lib/utils'

// ── Types ─────────────────────────────────────────────────────────────────────

interface CatalogSetting { key: string; value: string }

function apiErrorMessage(error: unknown, fallback: string): string {
  return (error as { response?: { data?: { error?: string } } } | null)?.response?.data?.error ?? fallback
}

interface ImageSet {
  id: string
  code: string
  name: string
  isDefault: boolean
  fallbackSetId: string | null
  fallbackSetName: string | null
  sortPriority: number
  isActive: boolean
  /** Grid satırında gelir (tam listede yok): set silinebilir mi sorusunun yanıtı. */
  gorselSayisi?: number
  cdnBaseUrl?: string | null
}

// ── Image Server Keys ─────────────────────────────────────────────────────────

const IMAGE_SERVER_FIELDS: { key: string; label: string; type: string; hint?: string; section?: string }[] = [
  // CDN Ayarları
  { key: 'ImageServer.CdnBaseUrl',     label: 'CDN Temel URL',         type: 'text',   hint: 'örn: https://cdn.misharitalia.com/img', section: 'CDN Ayarları' },
  { key: 'ImageServer.CdnQuality',     label: 'CDN Kalite (%)',         type: 'number', hint: '85 (0-100 arası)' },
  { key: 'ImageServer.CdnThumbHeight', label: 'Thumbnail Yüksekliği',  type: 'number', hint: '240 (sepet, listeleme küçük resim)' },
  { key: 'ImageServer.CdnListHeight',  label: 'Liste/Detay Yüksekliği',type: 'number', hint: '640 (kategori listesi, ürün detayı)' },
  { key: 'ImageServer.CdnZoomHeight',  label: 'Zoom Yüksekliği',       type: 'number', hint: '1200 (ürün detayı zoom)' },
  // Toplu yükleme çift hedefi
  { key: 'ImageServer.UploadQuality',  label: 'Dönüştürme Kalitesi (%)', type: 'number', hint: '80 (1-100 arası)', section: 'Toplu Yükleme — SFTP' },
  { key: 'ImageServer.SftpHost',       label: 'SFTP Sunucu Adresi',    type: 'text',   hint: 'örn: images.example.com' },
  { key: 'ImageServer.SftpPort',       label: 'SFTP Port',             type: 'number', hint: '22' },
  { key: 'ImageServer.SftpUser',       label: 'SFTP Kullanıcı Adı',    type: 'text' },
  { key: 'ImageServer.SftpPassword',   label: 'SFTP Şifre',            type: 'password' },
  { key: 'ImageServer.SftpBasePath',   label: 'SFTP Dosya Yolu',       type: 'text',   hint: '/var/www/html/images' },
  { key: 'ImageServer.S3ServiceUrl',   label: 'S3 Servis URL',         type: 'text',   hint: 'https://s3.de.io.cloud.ovh.net/', section: 'Toplu Yükleme — OVH Object Storage' },
  { key: 'ImageServer.S3Bucket',       label: 'S3 Bucket',             type: 'text' },
  { key: 'ImageServer.S3AccessKey',    label: 'S3 Access Key',         type: 'password' },
  { key: 'ImageServer.S3SecretKey',    label: 'S3 Secret Key',         type: 'password' },
  // Yerel fallback (CDN kullanılmıyorsa)
  { key: 'ImageServer.LocalSavePath',  label: 'Yerel Kayıt Dizini',    type: 'text',   hint: 'örn: /opt/ECSProsAI/media/images/products/', section: 'Yerel Depolama' },
  { key: 'ImageServer.PublicBaseUrl',  label: 'Yerel Sunucu URL',      type: 'text',   hint: 'örn: /media/images/products/' },
  { key: 'VideoServer.LocalSavePath',  label: 'Video Kayıt Dizini',    type: 'text',   hint: 'örn: /opt/ECSProsAI/media/videos/products/', section: 'Video Sunucusu' },
  { key: 'VideoServer.PublicBaseUrl',  label: 'Video Sunucu URL',      type: 'text',   hint: 'örn: /media/videos/products/' },
]

// ── CatalogSettingsPage ───────────────────────────────────────────────────────

export function CatalogSettingsPage() {
  const [tab, setTab] = useState<'image-server' | 'image-sets' | 'mannequins'>('image-server')

  return (
    <div className="flex-1 flex flex-col">
      {/* Header */}
      <div className="vh">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h1 className="text-lg font-bold">Katalog Ayarları</h1>
            <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
              Resim sunucusu, resim seti ve manken kadrosu yönetimi
            </p>
          </div>
        </div>

        {/* Tabs */}
        <div className="tab-scroll mt-4">
          {([
            ['image-server', 'Resim Sunucusu'],
            ['image-sets',   'Resim Setleri'],
            ['mannequins',   'Mankenler'],
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
        {tab === 'mannequins'   && <MannequinsTab />}
      </div>
    </div>
  )
}

// ── ImageServerTab ────────────────────────────────────────────────────────────

function ImageServerTab() {
  const qc = useQueryClient()
  const [edits, setEdits] = useState<Record<string, string>>({})
  const [saved, setSaved] = useState(false)

  const { data: settings = [], isLoading } = useQuery<CatalogSetting[]>({
    queryKey: ['catalog-settings'],
    queryFn: async () => {
      const { data } = await api.get('/catalog/settings')
      return data.data as CatalogSetting[]
    },
  })

  const valueOf = (key: string) => edits[key] ?? settings.find(s => s.key === key)?.value ?? ''

  const saveMutation = useMutation({
    mutationFn: async () => {
      for (const f of IMAGE_SERVER_FIELDS) {
        await api.put(`/catalog/settings/${f.key}`, { value: valueOf(f.key) })
      }
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['catalog-settings'] })
      setEdits({})
      setSaved(true)
      setTimeout(() => setSaved(false), 2500)
    },
  })

  if (isLoading) return <PageSpinner />

  return (
    <div className="card" style={{ maxWidth: 560 }}>
      <p className="text-xs mb-5" style={{ color: 'var(--text-s)' }}>
        CDN ve uygulamanın otomatik toplu resim aktarım bilgileri. Kullanıcı FTP ile dosya göndermez.
        Vitrin Yönetimi de aynı güvenli SFTP/S3 bağlantısını kullanır; dosyalarını ürünlerin images dizinine
        değil ayrı storefront dizinine yazar. Secret alanları kaydedildikten sonra maskeli gösterilir;
        değişiklikler sonraki yüklemede geçerli olur.
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
              className={cn('inp', valueOf(f.key) && 'ok')}
              type={f.type}
              placeholder={f.hint}
              value={valueOf(f.key)}
              onChange={e => setEdits(d => ({ ...d, [f.key]: e.target.value }))}
            />
          </div>
        ))}
      </div>

      {saveMutation.isError && (
        <p className="text-sm mt-4" style={{ color: '#ef4444' }}>
          {apiErrorMessage(saveMutation.error, 'Kayıt sırasında hata oluştu.')}
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
  fallbackSetId: string
  sortPriority: number
  isActive: boolean
}

const emptyForm = (): ImageSetFormState => ({
  code: '', name: '', fallbackSetId: '', sortPriority: 0, isActive: true,
})

function ImageSetsTab() {
  const qc = useQueryClient()
  const [modal, setModal] = useState<{ mode: 'create' | 'edit'; set?: ImageSet } | null>(null)
  const [form, setForm] = useState<ImageSetFormState>(emptyForm())
  const [confirmDelete, setConfirmDelete] = useState(false)

  // DataGrid (2026-09-09, tur 12): sunucu filtre/sıralama/arama (ImageSetGrid.Schema) + Excel + görünümler.
  // ★ Ayrı uç: /catalog/image-sets TAM liste döner (ürün Resimler sekmesi, toplu yükleme ve AŞAĞIDAKİ
  // "yedek set" seçicisinin kaynağı) → tam liste burada da tutulur; SATIRLAR sayfalı grid ucundan gelir.
  const grid = useGridState('image-sets', { defaultPageSize: 50, defaultSort: 'sortPriority', defaultDir: 'asc' })
  const { data: tumSetler = [] } = useQuery<ImageSet[]>({
    queryKey: ['image-sets', false],
    queryFn: async () => {
      const { data } = await api.get('/catalog/image-sets?activeOnly=false')
      return data.data as ImageSet[]
    },
  })
  const { data, isLoading, isFetching, error: listError } = useQuery<{ items: ImageSet[]; totalCount: number }>({
    queryKey: ['image-sets-grid', ...grid.queryKey],
    queryFn: async () => (await api.get(`/catalog/image-sets/grid?${grid.toParams()}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })
  const sets = data?.items ?? []

  const openCreate = () => { setForm(emptyForm()); setModal({ mode: 'create' }) }
  const openEdit = (s: ImageSet) => {
    setForm({
      code: s.code, name: s.name,
      fallbackSetId: s.fallbackSetId ?? '', sortPriority: s.sortPriority, isActive: s.isActive,
    })
    setConfirmDelete(false)
    setModal({ mode: 'edit', set: s })
  }

  const deleteMutation = useMutation({
    mutationFn: async () => {
      await api.delete(`/catalog/image-sets/${modal!.set!.id}`)
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['image-sets'] })        // tam liste (seçiciler)
      qc.invalidateQueries({ queryKey: ['image-sets-grid'] })   // bu sekmenin sayfalı listesi
      setModal(null)
    },
  })

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (modal?.mode === 'create') {
        await api.post('/catalog/image-sets', {
          code: form.code.trim(),
          name: form.name.trim(),
          fallbackSetId: form.fallbackSetId || null,
          sortPriority: form.sortPriority,
        })
      } else {
        await api.put(`/catalog/image-sets/${modal!.set!.id}`, {
          name: form.name.trim(),
          fallbackSetId: form.fallbackSetId || null,
          sortPriority: form.sortPriority,
          isActive: form.isActive,
        })
      }
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['image-sets'] })
      qc.invalidateQueries({ queryKey: ['image-sets-grid'] })
      setModal(null)
    },
  })

  const canSave = form.name.trim().length > 0 && (modal?.mode === 'edit' || form.code.trim().length > 0)

  // Yedek set seçicisi TAM listeden: sayfalı satırlar seçenekleri kırpardı.
  const otherSets = tumSetler.filter(s => s.id !== modal?.set?.id)

  const columns: GridColumn<ImageSet>[] = [
    { key: 'code', header: 'KOD', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 160,
      filter: { type: 'text', label: 'Kod', ops: ['startswith', 'contains', 'eq'] },
      cell: s => <span className="flex items-center gap-2 font-mono text-xs" style={{ color: 'var(--text-m)' }}>
        {s.code}
        {s.isDefault && <Badge variant="success">Varsayılan</Badge>}
      </span> },
    { key: 'name', header: 'AD', priority: 1, sortable: true, minWidth: 200,
      filter: { type: 'text', label: 'Ad' },
      cell: s => <span className="font-medium text-sm" style={{ color: 'var(--text)' }}>{s.name}</span> },
    { key: 'fallback', header: 'YEDEK SET', priority: 2, sortable: true, minWidth: 160,
      filter: { type: 'text', label: 'Yedek set adı' },
      filters: [{ field: 'fallbackVar', label: 'Yedek seti var', type: 'boolean' }],
      cell: s => <span className="text-xs" style={{ color: 'var(--text-m)' }}>
        {s.fallbackSetName ?? <span style={{ color: 'var(--text-s)' }}>—</span>}</span> },
    { key: 'gorselSayisi', header: 'GÖRSEL', priority: 2, align: 'right', sortable: true,
      filter: { type: 'number', label: 'Görsel sayısı' },
      filters: [{ field: 'kullanildi', label: 'Görseli var (silinemez)', type: 'boolean' }],
      cell: s => <span className="text-xs tabular-nums" style={{ color: 'var(--text-m)' }}>
        {(s.gorselSayisi ?? 0).toLocaleString('tr-TR')}</span> },
    { key: 'sortPriority', header: 'ÖNCELİK', priority: 2, align: 'center', sortable: true,
      filter: { type: 'number', label: 'Öncelik' },
      cell: s => <span className="text-xs" style={{ color: 'var(--text-m)' }}>{s.sortPriority}</span> },
    { key: 'cdnBaseUrl', header: 'CDN ADRESİ', priority: 3, defaultVisible: false, minWidth: 200,
      filter: { type: 'text', label: 'CDN adresi' },
      cell: s => <span className="text-xs font-mono" style={{ color: 'var(--text-s)' }}>{s.cdnBaseUrl ?? '—'}</span> },
    { key: 'isActive', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'boolean', label: 'Aktif' },
      filters: [{ field: 'isDefault', label: 'Varsayılan set', type: 'boolean' }],
      cell: s => <Badge variant={s.isActive ? 'success' : 'neutral'}>{s.isActive ? 'Aktif' : 'Pasif'}</Badge> },
  ]

  return (
    <>
      <div className="flex items-center justify-between mb-4">
        <p className="text-sm" style={{ color: 'var(--text-s)' }}>
          {(data?.totalCount ?? 0).toLocaleString('tr-TR')} set{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}
        </p>
        <Button size="sm" onClick={openCreate}>
          <Plus size={14} className="mr-1.5" /> Yeni Set
        </Button>
      </div>

      <DataGrid<ImageSet>
        gridId="image-sets"
        views
        grid={grid}
        columns={columns}
        rows={sets}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? gridErrText(listError) : null}
        onRowClick={s => openEdit(s)}
        empty="Ölçütlere uyan resim seti yok."
        search={{ placeholder: 'Set kodu veya adı ara…' }}
        minWidth={980}
        pageSizes={[50, 100, 200]}
        export={{ endpoint: '/catalog/image-sets/export', fallbackFileName: 'resim-setleri.xlsx' }}
        compact={{
          title: s => s.name,
          subtitle: s => `${s.code}${s.fallbackSetName ? ` → ${s.fallbackSetName}` : ''}`,
          right: s => `${(s.gorselSayisi ?? 0).toLocaleString('tr-TR')} görsel`,
          badge: s => <Badge variant={s.isActive ? 'success' : 'neutral'}>{s.isActive ? 'Aktif' : 'Pasif'}</Badge>,
        }}
      />

      {/* Create / Edit Modal */}
      <Modal
        open={!!modal}
        onClose={() => setModal(null)}
        title={modal?.mode === 'create' ? 'Yeni Resim Seti' : 'Resim Setini Düzenle'}
      >
        {(() => {
          const isReadOnly = modal?.mode === 'edit' && !!modal.set?.isDefault
          return (
            <div className="space-y-4">
              {isReadOnly && (
                <div className="rounded-lg px-3 py-2 text-xs" style={{ background: 'var(--surface2)', color: 'var(--text-s)' }}>
                  Varsayılan resim seti düzenlenemez ve silinemez.
                </div>
              )}

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

              {isReadOnly ? (
                <>
                  <div>
                    <label className="flbl">Kod</label>
                    <input className="inp" value={form.code} readOnly />
                  </div>
                  <div>
                    <label className="flbl">Ad</label>
                    <input className="inp" value={form.name} readOnly />
                  </div>
                </>
              ) : (
                <div>
                  <label className="flbl">Ad <span className="text-amber-500 font-bold">*</span></label>
                  <input
                    className={cn('inp', form.name.trim() && 'ok')}
                    placeholder="örn: Standart Çekim"
                    value={form.name}
                    onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
                  />
                </div>
              )}

              {!isReadOnly && (
                <>
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

                  {modal?.mode === 'edit' && (
                    <div className="flex items-center gap-4 pt-1">
                      <label className="flex items-center gap-2 cursor-pointer select-none">
                        <input
                          type="checkbox"
                          className="w-4 h-4 rounded accent-[var(--brand)]"
                          checked={form.isActive}
                          onChange={e => setForm(f => ({ ...f, isActive: e.target.checked }))}
                        />
                        <span className="text-sm font-medium">Aktif</span>
                      </label>
                    </div>
                  )}
                </>
              )}

              {(saveMutation.isError || deleteMutation.isError) && (
                <p className="text-sm" style={{ color: '#ef4444' }}>
                  {apiErrorMessage(saveMutation.error, apiErrorMessage(deleteMutation.error, 'Hata oluştu.'))}
                </p>
              )}

              <div className="flex justify-between gap-2 pt-2">
                <div>
                  {modal?.mode === 'edit' && !isReadOnly && (
                    confirmDelete ? (
                      <div className="flex items-center gap-2">
                        <span className="text-xs" style={{ color: 'var(--text-s)' }}>Emin misiniz?</span>
                        <Button variant="danger" size="sm" onClick={() => deleteMutation.mutate()} loading={deleteMutation.isPending}>
                          Evet, Sil
                        </Button>
                        <Button variant="secondary" size="sm" onClick={() => setConfirmDelete(false)}>
                          Vazgeç
                        </Button>
                      </div>
                    ) : (
                      <button
                        onClick={() => setConfirmDelete(true)}
                        className="flex items-center gap-1.5 text-sm px-3 py-1.5 rounded-lg hover:bg-red-50 transition-colors"
                        style={{ color: '#ef4444' }}
                      >
                        <Trash2 size={14} /> Sil
                      </button>
                    )
                  )}
                </div>
                <div className="flex gap-2">
                  <Button variant="secondary" onClick={() => setModal(null)}>
                    {isReadOnly ? 'Kapat' : 'İptal'}
                  </Button>
                  {!isReadOnly && (
                    <Button onClick={() => saveMutation.mutate()} loading={saveMutation.isPending} disabled={!canSave}>
                      {modal?.mode === 'create' ? 'Oluştur' : 'Kaydet'}
                    </Button>
                  )}
                </div>
              </div>
            </div>
          )
        })()}
      </Modal>
    </>
  )
}


// ── MannequinsTab (FAZ 15.2a, 2026-09-10 — eski "Manken Listesi") ─────────────
// Manken kadrosu: ürün detayındaki "Manken" (json) özelliği bu listeden seçilir; seçilen ölçüler
// ürüne SNAPSHOT olarak yazılır (docs/manken-ozelligi-spec.md). Silme soft-delete; pasif manken
// seçim listesinde çıkmaz, eski ürün kayıtları etkilenmez.

export interface Mannequin {
  id: string; code: string | null; firstName: string; lastName: string | null; gender: string | null
  heightCm: number | null; weightKg: number | null; chestCm: number | null; waistCm: number | null; hipCm: number | null
  defaultWornSize: string | null; isActive: boolean; notes: string | null
}

/** Manken ölçülerinin tek satırlık özeti (ürün özelliğine snapshot olarak da bu yazılır). */
export function mankenOzet(m: Mannequin): string {
  const p: string[] = []
  if (m.heightCm) p.push(`${m.heightCm} cm`)
  if (m.weightKg) p.push(`${m.weightKg} kg`)
  if (m.chestCm || m.waistCm || m.hipCm) p.push(`${m.chestCm ?? '-'}/${m.waistCm ?? '-'}/${m.hipCm ?? '-'}`)
  if (m.defaultWornSize) p.push(`beden ${m.defaultWornSize}`)
  return p.join(', ')
}

export function mankenAd(m: Mannequin): string {
  return `${m.firstName}${m.lastName ? ' ' + m.lastName : ''}${m.code ? ` (${m.code})` : ''}`
}

const GENDER_OPTS = [{ value: '', label: '—' }, { value: 'female', label: 'Kadın' }, { value: 'male', label: 'Erkek' }, { value: 'child', label: 'Çocuk' }]
const GENDER_LABEL: Record<string, string> = { female: 'Kadın', male: 'Erkek', child: 'Çocuk' }

type MannequinForm = {
  code: string; firstName: string; lastName: string; gender: string
  heightCm: string; weightKg: string; chestCm: string; waistCm: string; hipCm: string
  defaultWornSize: string; isActive: boolean; notes: string
}
const bosMankenForm: MannequinForm = {
  code: '', firstName: '', lastName: '', gender: '', heightCm: '', weightKg: '', chestCm: '', waistCm: '', hipCm: '',
  defaultWornSize: '', isActive: true, notes: '',
}
const num = (v: string) => (v.trim() === '' ? null : Number(v))

function MannequinsTab() {
  const qc = useQueryClient()
  const [showPassive, setShowPassive] = useState(false)
  const [modal, setModal] = useState<'new' | Mannequin | null>(null)
  const [form, setForm] = useState<MannequinForm>(bosMankenForm)
  const [error, setError] = useState('')

  const { data: list = [], isLoading } = useQuery<Mannequin[]>({
    queryKey: ['mannequins', showPassive],
    queryFn: async () => (await api.get(`/catalog/mannequins?activeOnly=${!showPassive}`)).data.data,
  })

  const openNew = () => { setForm(bosMankenForm); setError(''); setModal('new') }
  const openEdit = (m: Mannequin) => {
    setForm({
      code: m.code ?? '', firstName: m.firstName, lastName: m.lastName ?? '', gender: m.gender ?? '',
      heightCm: m.heightCm?.toString() ?? '', weightKg: m.weightKg?.toString() ?? '', chestCm: m.chestCm?.toString() ?? '',
      waistCm: m.waistCm?.toString() ?? '', hipCm: m.hipCm?.toString() ?? '', defaultWornSize: m.defaultWornSize ?? '',
      isActive: m.isActive, notes: m.notes ?? '',
    })
    setError(''); setModal(m)
  }

  const save = useMutation({
    mutationFn: async () => {
      const body = {
        code: form.code.trim() || null, firstName: form.firstName.trim(), lastName: form.lastName.trim() || null,
        gender: form.gender || null, heightCm: num(form.heightCm), weightKg: num(form.weightKg), chestCm: num(form.chestCm),
        waistCm: num(form.waistCm), hipCm: num(form.hipCm), defaultWornSize: form.defaultWornSize.trim() || null,
        isActive: form.isActive, notes: form.notes.trim() || null,
      }
      if (modal === 'new') await api.post('/catalog/mannequins', body)
      else await api.put(`/catalog/mannequins/${(modal as Mannequin).id}`, body)
    },
    onSuccess: async () => { await qc.invalidateQueries({ queryKey: ['mannequins'] }); setModal(null) },
    onError: (e) => setError(apiErrorMessage(e, 'Kaydedilemedi.')),
  })
  const remove = useMutation({
    mutationFn: async (id: string) => { await api.delete(`/catalog/mannequins/${id}`) },
    onSuccess: async () => { await qc.invalidateQueries({ queryKey: ['mannequins'] }); setModal(null) },
    onError: (e) => setError(apiErrorMessage(e, 'Silinemedi.')),
  })

  if (isLoading) return <PageSpinner />

  const f = (k: keyof MannequinForm) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
    setForm(x => ({ ...x, [k]: e.target.value }))

  return (
    <div className="card p-0 overflow-hidden">
      <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-3 border-b" style={{ borderColor: 'var(--border)' }}>
        <div>
          <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>Manken Kadrosu ({list.length})</h2>
          <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
            Ürün detayı › Özellikler › <b>Manken</b> alanı bu listeden seçilir; ölçüler ürüne o anki hâliyle yazılır.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <label className="flex items-center gap-1.5 text-xs cursor-pointer" style={{ color: 'var(--text-m)' }}>
            <input type="checkbox" checked={showPassive} onChange={e => setShowPassive(e.target.checked)} /> Pasifleri göster
          </label>
          <Button size="sm" onClick={openNew}><Plus size={13} /> Yeni Manken</Button>
        </div>
      </div>
      {list.length === 0 ? (
        <p className="text-sm p-4" style={{ color: 'var(--text-s)' }}>Henüz manken tanımlanmadı.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border)' }}>
                {['MANKEN', 'CİNSİYET', 'BOY / KİLO', 'GÖĞÜS / BEL / BASEN', 'BEDEN', 'DURUM'].map(h => (
                  <th key={h} className="text-left px-4 py-2 text-xs font-semibold" style={{ color: 'var(--text-s)' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {list.map(m => (
                <tr key={m.id} className="cursor-pointer hover:bg-[var(--surface2)]" style={{ borderBottom: '1px solid var(--border)' }}
                  onClick={() => openEdit(m)}>
                  <td className="px-4 py-2" style={{ color: 'var(--text)' }}>
                    <div className="font-medium">{m.firstName} {m.lastName ?? ''}</div>
                    {m.code && <div className="text-xs" style={{ color: 'var(--text-s)' }}>{m.code}</div>}
                  </td>
                  <td className="px-4 py-2" style={{ color: 'var(--text-m)' }}>{m.gender ? (GENDER_LABEL[m.gender] ?? m.gender) : '—'}</td>
                  <td className="px-4 py-2" style={{ color: 'var(--text-m)' }}>{m.heightCm ?? '—'} cm / {m.weightKg ?? '—'} kg</td>
                  <td className="px-4 py-2" style={{ color: 'var(--text-m)' }}>{m.chestCm ?? '—'} / {m.waistCm ?? '—'} / {m.hipCm ?? '—'}</td>
                  <td className="px-4 py-2" style={{ color: 'var(--text-m)' }}>{m.defaultWornSize ?? '—'}</td>
                  <td className="px-4 py-2"><Badge variant={m.isActive ? 'success' : 'neutral'}>{m.isActive ? 'Aktif' : 'Pasif'}</Badge></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <Modal open={modal !== null} onClose={() => setModal(null)} title={modal === 'new' ? 'Yeni Manken' : 'Manken Düzenle'}>
        <div className="grid grid-cols-2 gap-3">
          <div><label className="flbl">Ad <span className="text-red-500">*</span></label><input className="inp" value={form.firstName} onChange={f('firstName')} /></div>
          <div><label className="flbl">Soyad</label><input className="inp" value={form.lastName} onChange={f('lastName')} /></div>
          <div><label className="flbl">Kod</label><input className="inp" value={form.code} onChange={f('code')} placeholder="örn. MNK-01" /></div>
          <div><label className="flbl">Cinsiyet</label>
            <select className="inp" value={form.gender} onChange={f('gender')}>{GENDER_OPTS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}</select></div>
          <div><label className="flbl">Boy (cm)</label><input className="inp" type="number" min="0" value={form.heightCm} onChange={f('heightCm')} /></div>
          <div><label className="flbl">Kilo (kg)</label><input className="inp" type="number" min="0" value={form.weightKg} onChange={f('weightKg')} /></div>
          <div><label className="flbl">Göğüs (cm)</label><input className="inp" type="number" min="0" value={form.chestCm} onChange={f('chestCm')} /></div>
          <div><label className="flbl">Bel (cm)</label><input className="inp" type="number" min="0" value={form.waistCm} onChange={f('waistCm')} /></div>
          <div><label className="flbl">Basen (cm)</label><input className="inp" type="number" min="0" value={form.hipCm} onChange={f('hipCm')} /></div>
          <div><label className="flbl">Giydiği Beden</label><input className="inp" value={form.defaultWornSize} onChange={f('defaultWornSize')} placeholder="örn. S, 38" /></div>
          <div className="col-span-2"><label className="flbl">Not</label><textarea className="ta" rows={2} value={form.notes} onChange={f('notes')} /></div>
          {modal !== 'new' && (
            <label className="col-span-2 flex items-center gap-2 text-sm cursor-pointer" style={{ color: 'var(--text)' }}>
              <input type="checkbox" checked={form.isActive} onChange={e => setForm(x => ({ ...x, isActive: e.target.checked }))} /> Aktif (seçim listesinde görünür)
            </label>
          )}
        </div>
        {error && <p className="text-sm mt-2 text-red-500">{error}</p>}
        <div className="flex justify-between gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          {modal !== 'new' ? (
            <Button size="sm" variant="danger" onClick={() => { if (confirm('Manken silinsin mi? Eski ürün kayıtlarındaki ölçü kopyaları korunur.')) remove.mutate((modal as Mannequin).id) }} loading={remove.isPending}>
              <Trash2 size={13} /> Sil
            </Button>
          ) : <span />}
          <div className="flex gap-2">
            <Button variant="secondary" onClick={() => setModal(null)}>Vazgeç</Button>
            <Button onClick={() => save.mutate()} loading={save.isPending} disabled={!form.firstName.trim()}><Check size={13} /> Kaydet</Button>
          </div>
        </div>
      </Modal>
    </div>
  )
}

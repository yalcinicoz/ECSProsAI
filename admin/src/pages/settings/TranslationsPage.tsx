import { useState, useMemo, useCallback } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useQuery, useMutation } from '@tanstack/react-query'
import { Plus, Save, Check, AlertCircle } from 'lucide-react'
import { cn } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText as gridErrText } from '@/components/ui/DataTable.utils'
import { useLanguages } from '@/hooks/useLanguages'

// ── Types ─────────────────────────────────────────────────────────────────────

/** Sunucudan gelen PİVOT satırı: anahtar + dil→değer (eksik dil sözlükte yok). */
interface TranslationRowDto {
  namespace: string
  key: string
  values: Record<string, string>
  doluDilSayisi: number
  eksikVar: boolean
  guncelleme: string | null
}

// Accumulated edits: "ns:key:lang" → new value
type DraftMap = Record<string, string>

// ── Constants ─────────────────────────────────────────────────────────────────

const NAMESPACES = [
  { value: 'common',     label: 'Genel' },
  { value: 'catalog',    label: 'Katalog' },
  { value: 'orders',     label: 'Siparişler' },
  { value: 'inventory',  label: 'Stok' },
  { value: 'crm',        label: 'Müşteriler' },
  { value: 'pos',        label: 'POS' },
  { value: 'finance',    label: 'Finans' },
  { value: 'fulfillment',label: 'Fulfillment' },
  { value: 'promotion',  label: 'Pazarlama' },
  { value: 'cms',        label: 'CMS' },
  { value: 'auth',       label: 'Kimlik' },
]

function draftKey(ns: string, key: string, lang: string) {
  return `${ns}:${key}:${lang}`
}

// ── Hücre (düzenlenebilir) ────────────────────────────────────────────────────

interface CellProps {
  dk: string
  orig: string
  lang: string
  drafts: DraftMap
  onDraftChange: (dk: string, value: string) => void
}

/**
 * Pivot hücresi — DataGrid'e geçerken satır bileşeni yerine HÜCRE bileşeni oldu; taslak (draft)
 * biriktirme ve "kaydedilmedi" göstergesi aynı kaldı: değişiklikler tek "Kaydet" ile toplu gider.
 */
function TranslationCell({ dk, orig, lang, drafts, onDraftChange }: CellProps) {
  const draft = drafts[dk]
  const cur   = draft !== undefined ? draft : orig
  const dirty = draft !== undefined && draft !== orig

  return (
    <div className="relative">
      <input
        type="text"
        value={cur}
        onChange={(e) => onDraftChange(dk, e.target.value)}
        placeholder={`[${lang}]`}
        className={cn(
          'w-full text-sm px-3 py-1.5 rounded-lg outline-none transition-all',
          dirty ? 'ring-1 ring-[var(--brand)]' : 'focus:ring-1 focus:ring-[var(--border)]',
        )}
        style={{
          background: dirty ? 'var(--brand-bg)' : 'var(--surface)',
          border: '1px solid var(--border)',
          color: 'var(--text)',
        }}
      />
      {dirty && (
        <span className="absolute right-2 top-1/2 -translate-y-1/2 w-1.5 h-1.5 rounded-full"
          style={{ background: 'var(--brand)' }} />
      )}
    </div>
  )
}

// ── Main Component ────────────────────────────────────────────────────────────

export function TranslationsPage() {
  const { data: languages = [], isLoading: langsLoading } = useLanguages()
  const langs = languages.map((l) => l.code)

  // DataGrid (2026-09-09, tur 12): ANAHTAR bazlı sunucu sayfalama + arama/süzgeç/sıralama
  // (UiTranslationGrid.Schema) + Excel. Pivot ve toplu kaydetme akışı korundu.
  const [sp] = useSearchParams()
  const activeNs = sp.get('namespace') ?? 'common'
  const dilFiltre = sp.get('dil') ?? ''
  const eksikOlanlar = sp.get('eksikOlanlar') === 'true'
  const grid = useGridState('ui-translations', { defaultPageSize: 50, defaultSort: 'key', defaultDir: 'asc' })
  const [drafts, setDrafts]       = useState<DraftMap>({})
  const [saveStatus, setSaveStatus] = useState<'idle' | 'ok' | 'err'>('idle')

  // Add-key modal
  const [addOpen, setAddOpen]   = useState(false)
  const [newKey, setNewKey]     = useState('')
  const [newValues, setNewValues] = useState<Record<string, string>>({})

  // ── Fetch translations for active namespace ──────────────────────────────────

  const named = () => ({
    namespace: activeNs,
    dil: dilFiltre || undefined,
    eksikOlanlar: eksikOlanlar ? 'true' : undefined,
  })

  const { data, isLoading, isFetching, error: listError, refetch } = useQuery<{ items: TranslationRowDto[]; totalCount: number }>({
    queryKey: ['ui-translations-grid', activeNs, dilFiltre, eksikOlanlar, ...grid.queryKey],
    queryFn: async () => (await api.get(`/core/ui-translations/grid?${grid.toParams(named())}`)).data.data,
    placeholderData: prev => prev,
    staleTime: 0,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })
  const rows = data?.items ?? []

  // Dirty count
  const dirtyCount = useMemo(() => Object.keys(drafts).length, [drafts])

  // ── Handlers ─────────────────────────────────────────────────────────────────

  const handleDraftChange = useCallback((dk: string, value: string) => {
    setDrafts((d) => ({ ...d, [dk]: value }))
    setSaveStatus('idle')
  }, [])

  // Grup değişince: taslaklar düşer VE grid'in sayfa/arama/süzgeç durumu sıfırlanır — aksi hâlde
  // önceki grubun filtresi yeni grupta "sonuç yok" gösteriyordu (çoklu grid dersi, tur 6).
  function switchNs(ns: string) {
    setDrafts({})
    setSaveStatus('idle')
    grid.mutate(n => {
      for (const k of [...n.keys()]) if (k.startsWith('f.') || k.startsWith('fq.')) n.delete(k)
      for (const k of ['page', 'search', 'dil', 'eksikOlanlar']) n.delete(k)
      n.set('namespace', ns)
    })
  }

  // ── Save mutation ────────────────────────────────────────────────────────────

  const saveMutation = useMutation({
    mutationFn: async () => {
      const items = Object.entries(drafts).map(([dk, value]) => {
        const [ns, key, lang] = dk.split(':')
        return { namespace: ns, key, lang, value }
      })
      await api.put('/core/ui-translations/batch', { items })
    },
    onSuccess: async () => {
      await refetch()
      setDrafts({})
      setSaveStatus('ok')
      setTimeout(() => setSaveStatus('idle'), 2500)
    },
    onError: () => setSaveStatus('err'),
  })

  // ── Add-key mutation ─────────────────────────────────────────────────────────

  const addMutation = useMutation({
    mutationFn: async () => {
      const items = langs.map((lang) => ({
        namespace: activeNs,
        key: newKey.trim().toLowerCase().replace(/\s+/g, '_'),
        lang,
        value: newValues[lang] ?? '',
      }))
      await api.put('/core/ui-translations/batch', { items })
    },
    onSuccess: async () => {
      await refetch()
      setAddOpen(false)
      setNewKey('')
      setNewValues({})
    },
  })

  // ── Render ────────────────────────────────────────────────────────────────────

  const nsLabel = NAMESPACES.find((n) => n.value === activeNs)?.label ?? activeNs

  const columns: GridColumn<TranslationRowDto>[] = [
    { key: 'key', header: 'ANAHTAR', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 240,
      // Değer süzgeci de burada: pivotta dil kolonları veritabanında AYNI alandır (Value), bu yüzden
      // "hangi dilde" araç çubuğundaki dil seçicisidir.
      filter: { type: 'text', label: 'Anahtar', ops: ['startswith', 'contains', 'eq'] },
      filters: [
        { field: 'value', label: 'Değer içinde ara', type: 'text' },
        { field: 'bos', label: 'Boş değer', type: 'boolean' },
      ],
      cell: r => <code className="text-xs px-2 py-0.5 rounded-md font-mono"
        style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>
        {r.key}
      </code> },
    ...langs.map<GridColumn<TranslationRowDto>>(lang => ({
      key: `lang_${lang}`,
      header: lang.toUpperCase(),
      priority: 1,
      minWidth: 220,
      exportable: false,   // Excel DÜZ biçimde (anahtar × dil) sunucudan gelir
      stopRowClick: true,
      cell: r => <TranslationCell dk={draftKey(r.namespace, r.key, lang)} orig={r.values[lang] ?? ''}
        lang={lang} drafts={drafts} onDraftChange={handleDraftChange} />,
    })),
    { key: 'doluDilSayisi', header: 'DOLULUK', priority: 2, align: 'center',
      filter: { type: 'boolean', label: 'Eksik çeviri var', field: 'bos' },
      cell: r => r.eksikVar
        ? <Badge variant="warning">{r.doluDilSayisi}/{langs.length}</Badge>
        : <Badge variant="success">tam</Badge> },
    { key: 'guncelleme', header: 'GÜNCELLEME', priority: 3, sortable: true, defaultVisible: false,
      filter: { type: 'date', label: 'Güncelleme' },
      cell: r => <span className="text-xs whitespace-nowrap" style={{ color: 'var(--text-s)' }}>
        {r.guncelleme ? new Date(r.guncelleme).toLocaleDateString('tr-TR') : '—'}</span> },
  ]

  if (langsLoading) return <PageSpinner />

  return (
    <div className="p-6 flex flex-col gap-5 h-full">

      {/* ── Page header ── */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Arayüz Çevirileri</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            Panelde görünen statik metinleri dil bazında yönetin
          </p>
        </div>

        <div className="flex items-center gap-2">
          {dirtyCount > 0 && (
            <span className="text-xs px-2 py-1 rounded-lg font-medium" style={{ background: 'var(--brand-bg)', color: 'var(--brand)' }}>
              {dirtyCount} değişiklik
            </span>
          )}
          <Button
            variant="secondary"
            onClick={() => { setNewKey(''); setNewValues({}); setAddOpen(true) }}
          >
            <Plus size={14} /> Anahtar Ekle
          </Button>
          <Button
            onClick={() => saveMutation.mutate()}
            loading={saveMutation.isPending}
            disabled={dirtyCount === 0}
          >
            {saveStatus === 'ok' ? (
              <><Check size={14} /> Kaydedildi</>
            ) : (
              <><Save size={14} /> Kaydet</>
            )}
          </Button>
        </div>
      </div>

      {saveStatus === 'err' && (
        <div
          className="flex items-center gap-2 px-4 py-3 rounded-xl text-sm"
          style={{ background: '#fef2f2', border: '1px solid #fecaca', color: '#dc2626' }}
        >
          <AlertCircle size={14} />
          Kaydetme başarısız. Lütfen tekrar deneyin.
        </div>
      )}

      <div className="flex gap-4 flex-1 min-h-0">

        {/* ── Namespace sidebar ── */}
        <div
          className="w-44 flex-shrink-0 rounded-2xl overflow-hidden"
          style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}
        >
          <div
            className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider"
            style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}
          >
            Grup
          </div>
          <nav className="p-1.5 space-y-0.5">
            {NAMESPACES.map((ns) => (
              <button
                key={ns.value}
                onClick={() => switchNs(ns.value)}
                className={cn(
                  'w-full text-left px-3 py-2 rounded-xl text-sm transition-colors',
                  activeNs === ns.value
                    ? 'font-semibold'
                    : 'hover:bg-[var(--surface2)]',
                )}
                style={
                  activeNs === ns.value
                    ? { background: 'var(--brand-bg)', color: 'var(--brand)' }
                    : { color: 'var(--text-m)' }
                }
              >
                {ns.label}
              </button>
            ))}
          </nav>
        </div>

        {/* ── Tablo alanı (DataGrid) ── */}
        <div className="flex-1 flex flex-col gap-3 min-w-0">
          <DataGrid<TranslationRowDto>
            gridId="ui-translations"
            views
            grid={grid}
            columns={columns}
            rows={rows}
            totalCount={data?.totalCount ?? 0}
            loading={isLoading}
            fetching={isFetching}
            error={listError ? gridErrText(listError) : null}
            rowKey={r => `${r.namespace}:${r.key}`}
            empty={`${nsLabel} grubunda ölçütlere uyan anahtar yok.`}
            search={{ placeholder: 'Anahtar veya değer ara…' }}
            minWidth={320 + langs.length * 220}
            pageSizes={[50, 100, 200]}
            filterLeading={
              <>
                <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={dilFiltre} aria-label="Dil"
                  onChange={e => grid.mutate(n => {
                    if (e.target.value) n.set('dil', e.target.value); else n.delete('dil')
                    n.delete('page')
                  })}>
                  <option value="">Tüm dillerde ara</option>
                  {langs.map(l => <option key={l} value={l}>
                    {languages.find(x => x.code === l)?.name ?? l} ({l.toUpperCase()})
                  </option>)}
                </select>
                <label className="flex items-center gap-1.5 text-sm whitespace-nowrap" style={{ color: 'var(--text)' }}>
                  <input type="checkbox" checked={eksikOlanlar}
                    onChange={e => grid.mutate(n => {
                      if (e.target.checked) n.set('eksikOlanlar', 'true'); else n.delete('eksikOlanlar')
                      n.delete('page')
                    })} />
                  Yalnız eksik çevirisi olanlar
                </label>
              </>
            }
            export={{ endpoint: '/core/ui-translations/export', named, fallbackFileName: 'arayuz-cevirileri.xlsx' }}
            compact={{
              title: r => r.key,
              subtitle: r => langs.map(l => `${l.toUpperCase()}: ${r.values[l] ?? '—'}`).join(' · '),
              right: r => `${r.doluDilSayisi}/${langs.length}`,
              badge: r => r.eksikVar ? <Badge variant="warning">eksik</Badge> : <Badge variant="success">tam</Badge>,
            }}
          />
          <p className="text-xs" style={{ color: 'var(--text-s)' }}>
            Hücreleri düzenleyip üstteki <b>Kaydet</b> ile toplu gönderin. Sayfa değiştirmek
            kaydedilmemiş değişiklikleri korur; grup değiştirmek taslakları düşürür.
          </p>
        </div>
      </div>

      {/* ── Add Key Modal ── */}
      <Modal
        open={addOpen}
        onClose={() => setAddOpen(false)}
        title={`Yeni Anahtar — ${nsLabel}`}
        size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setAddOpen(false)}>İptal</Button>
            <Button
              onClick={() => addMutation.mutate()}
              loading={addMutation.isPending}
              disabled={!newKey.trim()}
            >
              Ekle
            </Button>
          </>
        }
      >
        <div className="space-y-5">
          <div>
            <label className="flbl">Anahtar *</label>
            <input
              className={cn('inp', newKey && 'ok')}
              value={newKey}
              onChange={(e) => setNewKey(e.target.value)}
              placeholder="örnek_metin_anahtari"
              autoFocus
            />
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              Küçük harf ve alt çizgi. Kayıt sonrası değiştirilemez.
            </p>
          </div>

          {langs.map((lang) => (
            <div key={lang}>
              <label className="flbl">
                <Badge variant="info" className="mr-2">{lang.toUpperCase()}</Badge>
                {languages.find((l) => l.code === lang)?.name ?? lang}
              </label>
              <input
                className="inp mt-1"
                value={newValues[lang] ?? ''}
                onChange={(e) =>
                  setNewValues((v) => ({ ...v, [lang]: e.target.value }))
                }
                placeholder={`[${lang}] çeviri`}
              />
            </div>
          ))}

          {addMutation.isError && (
            <p className="text-sm" style={{ color: 'var(--danger, #ef4444)' }}>
              Hata oluştu. Tekrar deneyin.
            </p>
          )}
        </div>
      </Modal>
    </div>
  )
}

import { useState, useMemo, useCallback } from 'react'
import { useQuery, useMutation } from '@tanstack/react-query'
import { Plus, Save, Search, Check, AlertCircle } from 'lucide-react'
import { cn } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { useLanguages } from '@/hooks/useLanguages'

// ── Types ─────────────────────────────────────────────────────────────────────

interface TranslationDto {
  id: string
  namespace: string
  key: string
  lang: string
  value: string
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

// ── Row Component ─────────────────────────────────────────────────────────────

interface RowProps {
  rowKey: string
  namespace: string
  langs: string[]
  valuesByLang: Record<string, string>   // lang → value (existing)
  drafts: DraftMap
  onDraftChange: (dk: string, value: string) => void
}

function TranslationRow({ rowKey, namespace, langs, valuesByLang, drafts, onDraftChange }: RowProps) {
  return (
    <tr
      className="hover:bg-[var(--surface2)] transition-colors"
      style={{ borderBottom: '1px solid var(--border)' }}
    >
      <td className="px-4 py-2.5">
        <code
          className="text-xs px-2 py-0.5 rounded-md font-mono"
          style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}
        >
          {rowKey}
        </code>
      </td>
      {langs.map((lang) => {
        const dk    = draftKey(namespace, rowKey, lang)
        const draft = drafts[dk]
        const orig  = valuesByLang[lang] ?? ''
        const cur   = draft !== undefined ? draft : orig
        const dirty = draft !== undefined && draft !== orig

        return (
          <td key={lang} className="px-3 py-2">
            <div className="relative">
              <input
                type="text"
                value={cur}
                onChange={(e) => onDraftChange(dk, e.target.value)}
                placeholder={`[${lang}]`}
                className={cn(
                  'w-full text-sm px-3 py-1.5 rounded-lg outline-none transition-all',
                  dirty
                    ? 'ring-1 ring-[var(--brand)]'
                    : 'focus:ring-1 focus:ring-[var(--border)]',
                )}
                style={{
                  background: dirty ? 'var(--brand-bg)' : 'var(--surface)',
                  border: '1px solid var(--border)',
                  color: 'var(--text)',
                }}
              />
              {dirty && (
                <span
                  className="absolute right-2 top-1/2 -translate-y-1/2 w-1.5 h-1.5 rounded-full"
                  style={{ background: 'var(--brand)' }}
                />
              )}
            </div>
          </td>
        )
      })}
    </tr>
  )
}

// ── Main Component ────────────────────────────────────────────────────────────

export function TranslationsPage() {
  const { data: languages = [], isLoading: langsLoading } = useLanguages()
  const langs = languages.map((l) => l.code)

  const [activeNs, setActiveNs]   = useState('common')
  const [search, setSearch]       = useState('')
  const [drafts, setDrafts]       = useState<DraftMap>({})
  const [saveStatus, setSaveStatus] = useState<'idle' | 'ok' | 'err'>('idle')

  // Add-key modal
  const [addOpen, setAddOpen]   = useState(false)
  const [newKey, setNewKey]     = useState('')
  const [newValues, setNewValues] = useState<Record<string, string>>({})

  // ── Fetch translations for active namespace ──────────────────────────────────

  const { data: translations = [], isLoading, refetch } = useQuery<TranslationDto[]>({
    queryKey: ['ui-translations', activeNs],
    queryFn: async () => {
      const { data } = await api.get(`/core/ui-translations?namespace=${activeNs}`)
      return data.data
    },
    staleTime: 0,
  })

  // ── Build table data ─────────────────────────────────────────────────────────

  // Group by key → { lang: value }
  const rowData = useMemo(() => {
    const map: Record<string, Record<string, string>> = {}
    for (const t of translations) {
      if (!map[t.key]) map[t.key] = {}
      map[t.key][t.lang] = t.value
    }
    return map
  }, [translations])

  // Filtered keys
  const filteredKeys = useMemo(() => {
    const keys = Object.keys(rowData)
    if (!search.trim()) return keys
    const q = search.toLowerCase()
    return keys.filter((k) => {
      if (k.toLowerCase().includes(q)) return true
      return Object.values(rowData[k]).some((v) => v.toLowerCase().includes(q))
    })
  }, [rowData, search])

  // Dirty count
  const dirtyCount = useMemo(() => Object.keys(drafts).length, [drafts])

  // ── Handlers ─────────────────────────────────────────────────────────────────

  const handleDraftChange = useCallback((dk: string, value: string) => {
    setDrafts((d) => ({ ...d, [dk]: value }))
    setSaveStatus('idle')
  }, [])

  function switchNs(ns: string) {
    setActiveNs(ns)
    setDrafts({})
    setSearch('')
    setSaveStatus('idle')
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

        {/* ── Table area ── */}
        <div className="flex-1 flex flex-col gap-3 min-w-0">

          {/* Search + stats */}
          <div className="flex items-center gap-3">
            <div className="relative flex-1 max-w-xs">
              <Search
                size={13}
                className="absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none"
                style={{ color: 'var(--text-s)' }}
              />
              <input
                type="text"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Anahtar veya değer ara…"
                className="inp pl-8 w-full"
                style={{ fontSize: '13px' }}
              />
            </div>
            <span className="text-sm" style={{ color: 'var(--text-s)' }}>
              <span className="font-medium" style={{ color: 'var(--text)' }}>{filteredKeys.length}</span>
              {' '}anahtar · {langs.length} dil
            </span>
          </div>

          {/* Table */}
          <div
            className="flex-1 rounded-2xl overflow-auto"
            style={{ border: '1px solid var(--border)' }}
          >
            {isLoading ? (
              <div className="flex items-center justify-center h-48">
                <PageSpinner />
              </div>
            ) : (
              <table className="w-full">
                <thead style={{ position: 'sticky', top: 0, zIndex: 1 }}>
                  <tr style={{ background: 'var(--surface2)', borderBottom: '2px solid var(--border)' }}>
                    <th
                      className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider w-56"
                      style={{ color: 'var(--text-s)' }}
                    >
                      Anahtar
                    </th>
                    {langs.map((lang) => (
                      <th
                        key={lang}
                        className="text-left px-3 py-3 text-xs font-semibold uppercase tracking-wider"
                        style={{ color: 'var(--text-s)', minWidth: '200px' }}
                      >
                        <div className="flex items-center gap-2">
                          <Badge variant="info">{lang.toUpperCase()}</Badge>
                          <span>{languages.find((l) => l.code === lang)?.name ?? lang}</span>
                        </div>
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {filteredKeys.length === 0 && (
                    <tr>
                      <td
                        colSpan={langs.length + 1}
                        className="text-center py-16 text-sm"
                        style={{ color: 'var(--text-s)' }}
                      >
                        {search
                          ? `"${search}" için sonuç bulunamadı`
                          : (
                            <div className="flex flex-col items-center gap-3">
                              <span>{nsLabel} grubunda henüz çeviri yok</span>
                              <Button size="sm" onClick={() => { setNewKey(''); setNewValues({}); setAddOpen(true) }}>
                                <Plus size={12} /> İlk anahtarı ekle
                              </Button>
                            </div>
                          )
                        }
                      </td>
                    </tr>
                  )}
                  {filteredKeys.map((key) => (
                    <TranslationRow
                      key={key}
                      rowKey={key}
                      namespace={activeNs}
                      langs={langs}
                      valuesByLang={rowData[key]}
                      drafts={drafts}
                      onDraftChange={handleDraftChange}
                    />
                  ))}
                </tbody>
              </table>
            )}
          </div>
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

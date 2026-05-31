import { useState, useCallback, useMemo, useEffect, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Plus, Trash2, ChevronRight, ChevronDown, Save, GripVertical } from 'lucide-react'
import { cn } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { I18nField } from '@/components/ui/I18nField'
import { PageSpinner } from '@/components/ui/Spinner'
import { useLanguages, type LanguageExt } from '@/hooks/useLanguages'
import { FL } from '@/lib/field-labels'
import { buildI18nValues } from '@/lib/i18n-helper'

// ── Types ─────────────────────────────────────────────────────────────────────

interface MenuDetail {
  id: string
  firmPlatformId: string
  code: string
  nameI18n: Record<string, string>
  menuType: string
  isActive: boolean
  sortOrder: number
  nodes: NavNodeDto[]
}

interface NavNodeDto {
  id: string
  parentNavNodeId: string | null
  nameOverrideI18n: Record<string, string> | null
  nodeType: string
  categoryId: string | null
  customUrl: string | null
  icon: string | null
  imageUrl: string | null
  openInNewTab: boolean
  isActive: boolean
  sortOrder: number
  children: NavNodeDto[]
}

// Draft node (local state, before saving)
interface DraftItem {
  _key: string
  parentKey: string | null
  nameOverrideI18n: Record<string, string>
  nodeType: string
  categoryId: string
  customUrl: string
  isActive: boolean
  sortOrder: number
  openInNewTab: boolean
  expanded: boolean
}

const NODE_TYPE_OPTIONS = [
  { value: 'category', label: 'Kategori' },
  { value: 'link',     label: 'Bağlantı' },
  { value: 'label',    label: 'Başlık' },
]

let _keyCounter = 0
function nextKey() { return `dk_${++_keyCounter}` }

function dtoToFlat(nodes: NavNodeDto[], parentKey: string | null = null): DraftItem[] {
  const result: DraftItem[] = []
  for (const n of nodes) {
    const key = nextKey()
    result.push({
      _key:             key,
      parentKey,
      nameOverrideI18n: { ...(n.nameOverrideI18n ?? {}) },
      nodeType:         n.nodeType,
      categoryId:       n.categoryId ?? '',
      customUrl:        n.customUrl ?? '',
      isActive:         n.isActive,
      sortOrder:        n.sortOrder,
      openInNewTab:     n.openInNewTab,
      expanded:         true,
    })
    result.push(...dtoToFlat(n.children, key))
  }
  return result
}

function flatToInput(items: DraftItem[], parentKey: string | null): object[] {
  return items
    .filter((i) => i.parentKey === parentKey)
    .sort((a, b) => a.sortOrder - b.sortOrder)
    .map((i) => ({
      nameOverrideI18n: Object.keys(i.nameOverrideI18n).length > 0 ? i.nameOverrideI18n : null,
      nodeType:         i.nodeType,
      categoryId:       i.categoryId || null,
      customUrl:        i.customUrl || null,
      slug:             null,
      imageUrl:         null,
      badgeLabel:       null,
      icon:             null,
      openInNewTab:     i.openInNewTab,
      isActive:         i.isActive,
      sortOrder:        i.sortOrder,
      children:         flatToInput(items, i._key),
    }))
}

function getName(i18n: Record<string, string> | null | undefined, fallback = ''): string {
  if (!i18n) return fallback
  return i18n['tr'] ?? i18n['en'] ?? i18n[Object.keys(i18n)[0]] ?? fallback
}

// ── Item edit panel ────────────────────────────────────────────────────────────

interface ItemEditPanelProps {
  item: DraftItem
  depth: number
  languages: LanguageExt[]
  onUpdate: (key: string, patch: Partial<DraftItem>) => void
}

function ItemEditPanel({ item, depth, languages, onUpdate }: ItemEditPanelProps) {
  const catQuery = useQuery({
    queryKey: ['nav-target-categories'],
    queryFn: async () => { const { data } = await api.get('/catalog/categories?activeOnly=false'); return data.data as { id: string; code: string; nameI18n: Record<string, string> }[] },
    enabled: item.nodeType === 'category',
    staleTime: 60_000,
  })

  const categoryOptions = (catQuery.data ?? []).map((c) => ({
    value: c.id,
    label: c.nameI18n['tr'] ?? c.nameI18n['en'] ?? c.code,
  }))

  const sourceLang = languages.find((l) => l.isDefault)?.code ?? languages[0]?.code ?? 'tr'
  const nameFields = useMemo(() => [{ key: 'name', labels: FL.categoryName, required: false }], [])

  return (
    <div
      className="mx-4 mb-2 p-4 rounded-xl space-y-3"
      style={{ marginLeft: `${depth * 20 + 16}px`, background: 'var(--surface2)', border: '1px solid var(--border)' }}
    >
      {/* Ad override (opsiyonel — boşsa Category.nameI18n kullanılır) */}
      {languages.length > 0 && (
        <div>
          <label className="flbl mb-2">Ad (Override)</label>
          <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
            <I18nField
              sourceLang={sourceLang}
              languages={languages}
              fields={nameFields}
              values={buildI18nValues(item.nameOverrideI18n, languages)}
              onChange={(lang, _key, value) =>
                onUpdate(item._key, { nameOverrideI18n: { ...item.nameOverrideI18n, [lang]: value } })
              }
            />
          </div>
        </div>
      )}

      <div>
        <label className="flbl">Düğüm Tipi</label>
        <SearchableSelect
          value={item.nodeType}
          onChange={(v) => v && onUpdate(item._key, { nodeType: v, categoryId: '', customUrl: '' })}
          options={NODE_TYPE_OPTIONS}
          hasValue
        />
      </div>

      {/* Kategori seçici */}
      {item.nodeType === 'category' && (
        <div>
          <label className="flbl">Kategori</label>
          {catQuery.isLoading ? (
            <div className="inp flex items-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</div>
          ) : (
            <SearchableSelect
              value={item.categoryId || null}
              onChange={(v) => onUpdate(item._key, { categoryId: v ?? '' })}
              options={categoryOptions}
              placeholder="Kategori seçin…"
              hasValue={!!item.categoryId}
            />
          )}
        </div>
      )}

      {/* URL */}
      {item.nodeType === 'link' && (
        <div>
          <label className="flbl">URL</label>
          <input
            className="inp"
            value={item.customUrl}
            onChange={(e) => onUpdate(item._key, { customUrl: e.target.value })}
            placeholder="/outlet veya https://..."
          />
        </div>
      )}

      <div className="flex items-center gap-6">
        <label className="flex items-center gap-2 cursor-pointer text-sm" style={{ color: 'var(--text-m)' }}>
          <input type="checkbox" checked={item.isActive}
            onChange={(e) => onUpdate(item._key, { isActive: e.target.checked })} className="w-4 h-4 rounded" />
          Aktif
        </label>
        <label className="flex items-center gap-2 cursor-pointer text-sm" style={{ color: 'var(--text-m)' }}>
          <input type="checkbox" checked={item.openInNewTab}
            onChange={(e) => onUpdate(item._key, { openInNewTab: e.target.checked })} className="w-4 h-4 rounded" />
          Yeni sekmede aç
        </label>
      </div>
    </div>
  )
}

// ── Main Component ────────────────────────────────────────────────────────────

export function MenuDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { data: languages = [] } = useLanguages()

  const [items, setItems] = useState<DraftItem[]>([])
  const [editKey, setEditKey] = useState<string | null>(null)
  const [savedOk, setSavedOk] = useState(false)
  const initedMenuId = useRef<string | null>(null)

  // ── Fetch ─────────────────────────────────────────────────────────────────

  const { data: menu, isLoading } = useQuery<MenuDetail>({
    queryKey: ['menu', id],
    queryFn: async () => {
      const { data } = await api.get(`/navigation/menus/${id}`)
      return data.data
    },
    enabled: !!id,
  })

  useEffect(() => {
    if (menu && initedMenuId.current !== menu.id) {
      initedMenuId.current = menu.id
      setItems(dtoToFlat(menu.nodes))
    }
  }, [menu])

  // ── Mutations ─────────────────────────────────────────────────────────────

  const saveMutation = useMutation({
    mutationFn: async () => {
      const tree = flatToInput(items, null)
      await api.put(`/navigation/menus/${id}/nodes`, { nodes: tree })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['menus'] })
      setSavedOk(true)
      setTimeout(() => setSavedOk(false), 3000)
    },
  })

  // ── Item operations ───────────────────────────────────────────────────────

  const addItem = useCallback((parentKey: string | null) => {
    const siblings = items.filter((i) => i.parentKey === parentKey)
    const maxOrder = siblings.length > 0 ? Math.max(...siblings.map((s) => s.sortOrder)) + 1 : 0
    const newItem: DraftItem = {
      _key: nextKey(),
      parentKey,
      nameOverrideI18n: languages.reduce((acc, l) => ({ ...acc, [l.code]: '' }), {}),
      nodeType: 'link',
      categoryId: '',
      customUrl: '',
      isActive: true,
      sortOrder: maxOrder,
      openInNewTab: false,
      expanded: true,
    }
    setItems((prev) => {
      // Insert after last sibling or at end
      const idx = [...prev].reverse().findIndex((i) => i.parentKey === parentKey)
      if (idx === -1) return [...prev, newItem]
      const insertAt = prev.length - idx
      return [...prev.slice(0, insertAt), newItem, ...prev.slice(insertAt)]
    })
    setEditKey(newItem._key)
  }, [items, languages])

  const removeItem = useCallback((key: string) => {
    // Remove item and all descendants
    const toRemove = new Set<string>()
    const queue = [key]
    while (queue.length) {
      const k = queue.pop()!
      toRemove.add(k)
      items.filter((i) => i.parentKey === k).forEach((i) => queue.push(i._key))
    }
    setItems((prev) => prev.filter((i) => !toRemove.has(i._key)))
    if (editKey && toRemove.has(editKey)) setEditKey(null)
  }, [items, editKey])

  const updateItem = useCallback((key: string, patch: Partial<DraftItem>) => {
    setItems((prev) => prev.map((i) => i._key === key ? { ...i, ...patch } : i))
  }, [])

  const moveItem = useCallback((key: string, dir: -1 | 1) => {
    setItems((prev) => {
      const item = prev.find((i) => i._key === key)!
      const siblings = prev.filter((i) => i.parentKey === item.parentKey).sort((a, b) => a.sortOrder - b.sortOrder)
      const idx = siblings.findIndex((i) => i._key === key)
      const swapIdx = idx + dir
      if (swapIdx < 0 || swapIdx >= siblings.length) return prev
      const swap = siblings[swapIdx]
      return prev.map((i) => {
        if (i._key === key) return { ...i, sortOrder: swap.sortOrder }
        if (i._key === swap._key) return { ...i, sortOrder: item.sortOrder }
        return i
      })
    })
  }, [])

  // ── Render helpers ────────────────────────────────────────────────────────

  function renderTree(parentKey: string | null, depth: number): React.ReactNode {
    const children = items
      .filter((i) => i.parentKey === parentKey)
      .sort((a, b) => a.sortOrder - b.sortOrder)

    return children.map((item) => {
      const grandChildren = items.filter((i) => i.parentKey === item._key)
      const isEditing = editKey === item._key

      return (
        <div key={item._key}>
          {/* Item row */}
          <div
            className={cn(
              'flex items-center gap-2 px-3 py-2 rounded-xl cursor-pointer transition-colors',
              isEditing ? 'bg-[var(--brand-bg)]' : 'hover:bg-[var(--surface2)]',
            )}
            style={{ marginLeft: `${depth * 20}px` }}
            onClick={() => setEditKey(isEditing ? null : item._key)}
          >
            <GripVertical size={12} style={{ color: 'var(--text-s)', flexShrink: 0 }} />

            {/* Expand toggle */}
            <button
              onClick={(e) => { e.stopPropagation(); updateItem(item._key, { expanded: !item.expanded }) }}
              className="w-4 h-4 flex items-center justify-center flex-shrink-0"
              style={{ color: 'var(--text-s)' }}
            >
              {grandChildren.length > 0
                ? item.expanded ? <ChevronDown size={11} /> : <ChevronRight size={11} />
                : <span className="w-2 h-2 rounded-full inline-block" style={{ background: 'var(--border)' }} />
              }
            </button>

            {/* Name + type */}
            <div className="flex-1 min-w-0">
              <span className="text-sm font-medium truncate" style={{ color: isEditing ? 'var(--brand)' : 'var(--text)' }}>
                {getName(item.nameOverrideI18n) || <span style={{ color: 'var(--text-s)', fontStyle: 'italic' }}>İsimsiz düğüm</span>}
              </span>
              <span className="ml-2 text-xs" style={{ color: 'var(--text-s)' }}>
                {NODE_TYPE_OPTIONS.find((o) => o.value === item.nodeType)?.label}
              </span>
            </div>

            {!item.isActive && <Badge variant="neutral">Pasif</Badge>}

            {/* Move up/down */}
            <button
              onClick={(e) => { e.stopPropagation(); moveItem(item._key, -1) }}
              className="w-6 h-6 flex items-center justify-center rounded hover:bg-white/10 flex-shrink-0"
              style={{ color: 'var(--text-s)' }}
              title="Yukarı"
            >▲</button>
            <button
              onClick={(e) => { e.stopPropagation(); moveItem(item._key, 1) }}
              className="w-6 h-6 flex items-center justify-center rounded hover:bg-white/10 flex-shrink-0"
              style={{ color: 'var(--text-s)' }}
              title="Aşağı"
            >▼</button>

            {/* Add child */}
            <button
              onClick={(e) => { e.stopPropagation(); addItem(item._key) }}
              className="w-6 h-6 flex items-center justify-center rounded hover:bg-[var(--brand-bg)] flex-shrink-0"
              style={{ color: 'var(--brand)' }}
              title="Alt öğe ekle"
            >
              <Plus size={11} />
            </button>

            {/* Delete */}
            <button
              onClick={(e) => { e.stopPropagation(); removeItem(item._key) }}
              className="w-6 h-6 flex items-center justify-center rounded hover:bg-red-50 flex-shrink-0"
              style={{ color: '#ef4444' }}
              title="Sil"
            >
              <Trash2 size={11} />
            </button>
          </div>

          {/* Inline edit panel */}
          {isEditing && (
            <ItemEditPanel
              item={item}
              depth={depth}
              languages={languages}
              onUpdate={updateItem}
            />
          )}

          {/* Children */}
          {item.expanded && renderTree(item._key, depth + 1)}
        </div>
      )
    })
  }

  // ── Loading ───────────────────────────────────────────────────────────────

  if (isLoading || !menu) return <PageSpinner />

  return (
    <div className="p-6 pb-24">
      {/* Header — sticky */}
      <div
        className="flex items-center gap-3 mb-6 sticky top-0 z-20 py-3 -mx-6 px-6"
        style={{ background: 'var(--surface)', borderBottom: '1px solid var(--border)' }}
      >
        <button
          onClick={() => navigate('/navigation/menus')}
          className="w-8 h-8 flex items-center justify-center rounded-xl transition-colors"
          style={{ background: 'var(--surface2)', color: 'var(--text-m)' }}
        >
          <ArrowLeft size={15} />
        </button>
        <div className="flex-1 min-w-0">
          <h1 className="text-xl font-bold truncate" style={{ color: 'var(--text)' }}>
            {getName(menu.nameI18n, menu.code)}
          </h1>
          <div className="flex items-center gap-2 mt-0.5">
            <code className="text-xs" style={{ color: 'var(--text-s)' }}>{menu.code}</code>
            <Badge variant={menu.isActive ? 'success' : 'neutral'}>{menu.isActive ? 'Aktif' : 'Pasif'}</Badge>
          </div>
        </div>
      </div>

      {/* Store API hint */}
      <div
        className="flex items-center gap-2 px-4 py-2.5 rounded-xl mb-6 text-sm"
        style={{ background: 'var(--brand-bg)', color: 'var(--brand)' }}
      >
        <span className="font-medium">Store API:</span>
        <code className="text-xs">GET /api/store/cms/menus/{menu.code}?firmPlatformId=…</code>
      </div>

      {/* Tree */}
      <div className="card p-4 space-y-0.5">
        <div className="flex items-center justify-between mb-3">
          <span className="text-sm font-semibold" style={{ color: 'var(--text)' }}>
            Menü Öğeleri ({items.length})
          </span>
          <Button size="sm" onClick={() => addItem(null)}>
            <Plus size={13} /> Kök Öğe Ekle
          </Button>
        </div>

        {items.length === 0 && (
          <div
            className="py-10 text-center text-sm rounded-xl"
            style={{ color: 'var(--text-s)', border: '2px dashed var(--border)' }}
          >
            Henüz öğe yok — "Kök Öğe Ekle" ile başlayın
          </div>
        )}

        {renderTree(null, 0)}
      </div>

      {/* Sticky footer — kaydet */}
      <div
        className="fixed bottom-0 left-0 right-0 z-30 flex items-center justify-between px-6 py-3"
        style={{ background: 'var(--surface)', borderTop: '1px solid var(--border)' }}
      >
        <div className="text-sm">
          {saveMutation.isError && <span style={{ color: '#ef4444' }}>Kayıt hatası. Tekrar deneyin.</span>}
          {savedOk && <span style={{ color: '#16a34a' }}>Menü başarıyla kaydedildi.</span>}
        </div>
        <Button onClick={() => saveMutation.mutate()} loading={saveMutation.isPending}>
          <Save size={14} /> Değişiklikleri Kaydet
        </Button>
      </div>
    </div>
  )
}

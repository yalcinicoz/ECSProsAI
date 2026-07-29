import { useState, useEffect, useRef, useMemo } from 'react'
import { useQuery, useQueries, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Plus, Pencil, Trash2, ChevronDown, ChevronRight, GripVertical,
  ArrowUp, ArrowDown, Link2, Tag, Save, Search,
} from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { PageSpinner } from '@/components/ui/Spinner'

/**
 * Menü Yerleşimi — sitenin üst menüsünü (nav_menus code='header' + nav_nodes) düzenler.
 * Solda sürükle-bırak menü ağacı, sağda kategori havuzu. Aynı kategori menüde birden çok
 * yerde görünebilir (çapraz yerleşim). Kaydet = tüm ağaç değiştirilir (PUT nodes).
 */

interface Firm { id: string; nameI18n: Record<string, string> }
interface Channel {
  id: string; nameI18n: Record<string, string>; code: string; firmId: string; firmName: string
}
interface CategoryItem {
  id: string; parentId: string | null; nameI18n: Record<string, string>
  slug: string; status: string
}
interface MenuSummary { id: string; code: string; menuType: string; isActive: boolean }
interface NavNodeDto {
  id: string
  channelCategoryId: string | null
  nameOverrideI18n: Record<string, string> | null
  nodeType: string
  slug: string | null
  customUrl: string | null
  imageUrl: string | null
  badgeLabel: string | null
  icon: string | null
  openInNewTab: boolean
  isActive: boolean
  sortOrder: number
  children: NavNodeDto[]
}

interface EditNode {
  key: string
  nodeType: 'category' | 'link' | 'label'
  channelCategoryId: string | null
  nameOverride: string
  slug: string | null
  customUrl: string
  imageUrl: string
  badgeLabel: string
  isActive: boolean
  children: EditNode[]
}

type DropPos = 'before' | 'after' | 'child'

function getName(i18n: Record<string, string> | null | undefined): string {
  if (!i18n) return ''
  return i18n['tr'] ?? i18n[Object.keys(i18n)[0]] ?? ''
}

let keySeq = 0
const newKey = () => `n${++keySeq}`

function fromDto(n: NavNodeDto): EditNode {
  return {
    key: newKey(),
    nodeType: (n.nodeType as EditNode['nodeType']) ?? 'category',
    channelCategoryId: n.channelCategoryId,
    nameOverride: getName(n.nameOverrideI18n),
    slug: n.slug,
    customUrl: n.customUrl ?? '',
    imageUrl: n.imageUrl ?? '',
    badgeLabel: n.badgeLabel ?? '',
    isActive: n.isActive,
    children: (n.children ?? []).map(fromDto),
  }
}

function toInput(n: EditNode, sortOrder: number): Record<string, unknown> {
  return {
    nameOverrideI18n: n.nameOverride.trim() ? { tr: n.nameOverride.trim() } : null,
    nodeType: n.nodeType,
    channelCategoryId: n.nodeType === 'category' ? n.channelCategoryId : null,
    slug: n.slug,
    customUrl: n.nodeType === 'link' ? (n.customUrl || null) : null,
    imageUrl: n.imageUrl || null,
    badgeLabel: n.badgeLabel || null,
    icon: null,
    openInNewTab: false,
    isActive: n.isActive,
    sortOrder,
    children: n.children.map((c, i) => toInput(c, i)),
  }
}

// Ağaç yardımcıları — hepsi yeni kopya döner (state immutability)
function removeNode(tree: EditNode[], key: string): [EditNode[], EditNode | null] {
  let removed: EditNode | null = null
  const walk = (nodes: EditNode[]): EditNode[] =>
    nodes.filter(n => {
      if (n.key === key) { removed = n; return false }
      return true
    }).map(n => ({ ...n, children: walk(n.children) }))
  return [walk(tree), removed]
}

function insertNode(tree: EditNode[], targetKey: string, pos: DropPos, node: EditNode): EditNode[] {
  const walk = (nodes: EditNode[]): EditNode[] => {
    const out: EditNode[] = []
    for (const n of nodes) {
      if (n.key === targetKey) {
        if (pos === 'before') { out.push(node, { ...n, children: walk(n.children) }); continue }
        if (pos === 'after') { out.push({ ...n, children: walk(n.children) }, node); continue }
        out.push({ ...n, children: [...walk(n.children), node] }); continue
      }
      out.push({ ...n, children: walk(n.children) })
    }
    return out
  }
  return walk(tree)
}

function isDescendant(node: EditNode, key: string): boolean {
  return node.children.some(c => c.key === key || isDescendant(c, key))
}

function findNode(tree: EditNode[], key: string): EditNode | null {
  for (const n of tree) {
    if (n.key === key) return n
    const f = findNode(n.children, key)
    if (f) return f
  }
  return null
}

function moveSibling(tree: EditNode[], key: string, dir: -1 | 1): EditNode[] {
  const walk = (nodes: EditNode[]): EditNode[] => {
    const idx = nodes.findIndex(n => n.key === key)
    if (idx >= 0) {
      const j = idx + dir
      if (j < 0 || j >= nodes.length) return nodes
      const copy = [...nodes]
      ;[copy[idx], copy[j]] = [copy[j], copy[idx]]
      return copy
    }
    return nodes.map(n => ({ ...n, children: walk(n.children) }))
  }
  return walk(tree)
}

function countNodes(tree: EditNode[]): number {
  return tree.reduce((s, n) => s + 1 + countNodes(n.children), 0)
}

function usedCategoryIds(tree: EditNode[], acc = new Map<string, number>()): Map<string, number> {
  for (const n of tree) {
    if (n.channelCategoryId) acc.set(n.channelCategoryId, (acc.get(n.channelCategoryId) ?? 0) + 1)
    usedCategoryIds(n.children, acc)
  }
  return acc
}

export function MenuPlacementPage() {
  const queryClient = useQueryClient()

  const [selectedChannelId, setSelectedChannelId] = useState<string>(
    () => sessionStorage.getItem('menuPlacement.channelId')
      ?? sessionStorage.getItem('channelCategories.channelId') ?? ''
  )
  useEffect(() => {
    if (selectedChannelId) sessionStorage.setItem('menuPlacement.channelId', selectedChannelId)
  }, [selectedChannelId])

  // ── Kanal listesi (firmalar × platformlar) ──
  const { data: firms = [], isLoading: firmsLoading } = useQuery<Firm[]>({
    queryKey: ['firms'],
    queryFn: async () => (await api.get('/core/firms')).data.data ?? [],
  })
  const platformQueries = useQueries({
    queries: firms.map(firm => ({
      queryKey: ['firm-platforms', firm.id],
      queryFn: async (): Promise<Channel[]> => {
        const { data } = await api.get(`/core/firms/${firm.id}/platforms`)
        const firmName = getName(firm.nameI18n)
        return (data.data ?? []).map((ch: Channel) => ({ ...ch, firmId: firm.id, firmName }))
      },
      enabled: firms.length > 0,
    })),
  })
  const channels: Channel[] = platformQueries.flatMap(q => q.data ?? [])
  const chLoading = firmsLoading || platformQueries.some(q => q.isLoading)

  // ── Header menüsü + düğümler + kategoriler ──
  const { data: menus = [], isLoading: menusLoading } = useQuery<MenuSummary[]>({
    queryKey: ['nav-menus', selectedChannelId],
    queryFn: async () =>
      (await api.get(`/navigation/menus?firmPlatformId=${selectedChannelId}`)).data.data ?? [],
    enabled: !!selectedChannelId,
  })
  const headerMenu = menus.find(m => m.code === 'header')

  const { data: menuDetail, isLoading: detailLoading } = useQuery<{ nodes: NavNodeDto[] }>({
    queryKey: ['nav-menu-detail', headerMenu?.id],
    queryFn: async () => (await api.get(`/navigation/menus/${headerMenu!.id}`)).data.data,
    enabled: !!headerMenu,
  })

  const { data: categories = [] } = useQuery<CategoryItem[]>({
    queryKey: ['channel-categories', selectedChannelId],
    queryFn: async () =>
      (await api.get(`/navigation/channel-categories?firmPlatformId=${selectedChannelId}`)).data.data ?? [],
    enabled: !!selectedChannelId,
  })
  const catById = useMemo(() => new Map(categories.map(c => [c.id, c])), [categories])

  // ── Editör durumu ──
  const [tree, setTree] = useState<EditNode[]>([])
  const [dirty, setDirty] = useState(false)
  const loadedMenuId = useRef<string | null>(null)
  useEffect(() => {
    if (!menuDetail) return
    if (loadedMenuId.current === headerMenu?.id && dirty) return // kaydedilmemiş değişiklikleri ezme
    setTree((menuDetail.nodes ?? []).map(fromDto))
    setDirty(false)
    loadedMenuId.current = headerMenu?.id ?? null
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [menuDetail, headerMenu?.id])

  const apply = (next: EditNode[]) => { setTree(next); setDirty(true) }

  const [collapsed, setCollapsed] = useState<Set<string>>(new Set())
  const toggleCollapse = (key: string) =>
    setCollapsed(prev => {
      const s = new Set(prev)
      if (s.has(key)) s.delete(key); else s.add(key)
      return s
    })

  // ── Sürükle-bırak ──
  const [dragKey, setDragKey] = useState<string | null>(null)   // ağaç içi taşıma
  const [dragCatId, setDragCatId] = useState<string | null>(null) // havuzdan ekleme
  const [dropHint, setDropHint] = useState<{ key: string; pos: DropPos } | null>(null)

  const makeCategoryNode = (catId: string): EditNode => ({
    key: newKey(), nodeType: 'category', channelCategoryId: catId,
    nameOverride: '', slug: catById.get(catId)?.slug ?? null,
    customUrl: '', imageUrl: '', badgeLabel: '', isActive: true, children: [],
  })

  const performDrop = (targetKey: string, pos: DropPos) => {
    if (dragCatId) {
      apply(insertNode(tree, targetKey, pos, makeCategoryNode(dragCatId)))
    } else if (dragKey && dragKey !== targetKey) {
      const dragged = findNode(tree, dragKey)
      if (!dragged) return
      if (dragged.key === targetKey || isDescendant(dragged, targetKey)) return // kendi altına taşınamaz
      const [without, removed] = removeNode(tree, dragKey)
      if (removed) apply(insertNode(without, targetKey, pos, removed))
    }
    setDragKey(null); setDragCatId(null); setDropHint(null)
  }

  // ── Düğüm ekleme / düzenleme modalı ──
  const emptyForm: EditNode = {
    key: '', nodeType: 'category', channelCategoryId: null, nameOverride: '',
    slug: null, customUrl: '', imageUrl: '', badgeLabel: '', isActive: true, children: [],
  }
  const [editOpen, setEditOpen] = useState(false)
  const [editKey, setEditKey] = useState<string | null>(null)     // null = yeni düğüm
  const [addParentKey, setAddParentKey] = useState<string | null>(null) // yeni düğümün ebeveyni (null = kök)
  const [form, setForm] = useState<EditNode>(emptyForm)

  const openAdd = (parentKey: string | null) => {
    setEditKey(null); setAddParentKey(parentKey); setForm({ ...emptyForm, key: newKey() }); setEditOpen(true)
  }
  const openEdit = (node: EditNode) => {
    setEditKey(node.key); setForm({ ...node }); setEditOpen(true)
  }
  const submitForm = () => {
    if (form.nodeType === 'category' && !form.channelCategoryId) return
    const node: EditNode = {
      ...form,
      slug: form.nodeType === 'category'
        ? (catById.get(form.channelCategoryId!)?.slug ?? form.slug) : form.slug,
    }
    if (editKey) {
      const walk = (nodes: EditNode[]): EditNode[] =>
        nodes.map(n => n.key === editKey ? { ...node, children: n.children } : { ...n, children: walk(n.children) })
      apply(walk(tree))
    } else if (addParentKey) {
      const walk = (nodes: EditNode[]): EditNode[] =>
        nodes.map(n => n.key === addParentKey
          ? { ...n, children: [...n.children, node] }
          : { ...n, children: walk(n.children) })
      apply(walk(tree))
      setCollapsed(prev => { const s = new Set(prev); s.delete(addParentKey); return s })
    } else {
      apply([...tree, node])
    }
    setEditOpen(false)
  }

  // ── Kaydet / menü oluştur ──
  const saveMutation = useMutation({
    mutationFn: async () => {
      await api.put(`/navigation/menus/${headerMenu!.id}/nodes`, {
        nodes: tree.map((n, i) => toInput(n, i)),
      })
    },
    onSuccess: () => {
      setDirty(false)
      queryClient.invalidateQueries({ queryKey: ['nav-menu-detail', headerMenu?.id] })
    },
  })

  const createMenuMutation = useMutation({
    mutationFn: async () => {
      await api.post('/navigation/menus', {
        firmPlatformId: selectedChannelId, code: 'header',
        nameI18n: { tr: 'Ana Menü' }, menuType: 'header',
      })
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['nav-menus', selectedChannelId] }),
  })

  // ── Kategori havuzu ──
  const [poolSearch, setPoolSearch] = useState('')
  const used = useMemo(() => usedCategoryIds(tree), [tree])
  const pool = useMemo(() => {
    const q = poolSearch.trim().toLowerCase()
    return categories
      .filter(c => !q || getName(c.nameI18n).toLowerCase().includes(q) || c.slug.includes(q))
      .sort((a, b) => getName(a.nameI18n).localeCompare(getName(b.nameI18n), 'tr'))
  }, [categories, poolSearch])

  // ── Satır bileşeni ──
  const renderNode = (node: EditNode, depth: number, siblingCount: number, index: number) => {
    const cat = node.channelCategoryId ? catById.get(node.channelCategoryId) : null
    const label = node.nameOverride || (cat ? getName(cat.nameI18n) : node.slug ?? '—')
    const slug = node.nodeType === 'link' ? node.customUrl : (cat?.slug ?? node.slug)
    const hint = dropHint?.key === node.key ? dropHint.pos : null
    const kayipKategori = node.nodeType === 'category' && node.channelCategoryId && !cat

    return (
      <div key={node.key}>
        <div
          draggable
          onDragStart={(e) => { e.stopPropagation(); setDragKey(node.key); e.dataTransfer.effectAllowed = 'move' }}
          onDragEnd={() => { setDragKey(null); setDropHint(null) }}
          onDragOver={(e) => {
            if (!dragKey && !dragCatId) return
            if (dragKey === node.key) return
            e.preventDefault(); e.stopPropagation()
            const rect = e.currentTarget.getBoundingClientRect()
            const y = (e.clientY - rect.top) / rect.height
            const pos: DropPos = y < 0.25 ? 'before' : y > 0.75 ? 'after' : 'child'
            if (dropHint?.key !== node.key || dropHint.pos !== pos) setDropHint({ key: node.key, pos })
          }}
          onDrop={(e) => { e.preventDefault(); e.stopPropagation(); performDrop(node.key, dropHint?.pos ?? 'child') }}
          className="group flex items-center gap-1.5 py-1.5 pr-2 rounded-lg transition-colors"
          style={{
            paddingLeft: 8 + depth * 22,
            opacity: node.isActive ? 1 : 0.45,
            background: hint === 'child' ? 'var(--brand-bg, rgba(59,130,246,.12))' : undefined,
            boxShadow: hint === 'before' ? 'inset 0 2px 0 var(--brand)'
              : hint === 'after' ? 'inset 0 -2px 0 var(--brand)' : undefined,
            cursor: 'grab',
          }}
        >
          <GripVertical size={13} className="shrink-0" style={{ color: 'var(--text-s)' }} />
          {node.children.length > 0 ? (
            <button onClick={() => toggleCollapse(node.key)} className="shrink-0 p-0.5"
              style={{ color: 'var(--text-s)' }}>
              {collapsed.has(node.key) ? <ChevronRight size={13} /> : <ChevronDown size={13} />}
            </button>
          ) : <span className="w-[18px] shrink-0" />}
          {node.imageUrl && (
            <img src={node.imageUrl} alt="" className="w-5 h-6 rounded object-cover shrink-0"
              style={{ border: '1px solid var(--border)' }} />
          )}
          {node.nodeType === 'link' && <Link2 size={12} className="shrink-0" style={{ color: 'var(--text-s)' }} />}
          {node.nodeType === 'label' && <Tag size={12} className="shrink-0" style={{ color: 'var(--text-s)' }} />}
          <span className="text-sm truncate" style={{ color: kayipKategori ? '#dc2626' : 'var(--text)' }}>
            {kayipKategori ? 'Silinmiş kategori' : label}
          </span>
          {node.badgeLabel && (
            <span className="text-[10px] px-1.5 rounded-full shrink-0"
              style={{ background: 'var(--brand-bg, rgba(59,130,246,.12))', color: 'var(--brand)' }}>
              {node.badgeLabel}
            </span>
          )}
          {slug && (
            <code className="text-[11px] truncate shrink-0 max-w-[160px]" style={{ color: 'var(--text-s)' }}>
              /{slug}
            </code>
          )}
          <span className="flex-1" />
          <span className="hidden group-hover:flex items-center gap-0.5 shrink-0">
            <button title="Yukarı" onClick={() => apply(moveSibling(tree, node.key, -1))}
              disabled={index === 0} className="p-1 rounded disabled:opacity-30"
              style={{ color: 'var(--text-m)' }}><ArrowUp size={13} /></button>
            <button title="Aşağı" onClick={() => apply(moveSibling(tree, node.key, 1))}
              disabled={index === siblingCount - 1} className="p-1 rounded disabled:opacity-30"
              style={{ color: 'var(--text-m)' }}><ArrowDown size={13} /></button>
            <button title="Alt öğe ekle" onClick={() => openAdd(node.key)} className="p-1 rounded"
              style={{ color: 'var(--text-m)' }}><Plus size={13} /></button>
            <button title="Düzenle" onClick={() => openEdit(node)} className="p-1 rounded"
              style={{ color: 'var(--text-m)' }}><Pencil size={13} /></button>
            <button title="Sil" onClick={() => {
              if (node.children.length > 0 && !confirm(`"${label}" ve ${countNodes(node.children)} alt öğesi menüden çıkarılacak. Emin misiniz?`)) return
              apply(removeNode(tree, node.key)[0])
            }} className="p-1 rounded" style={{ color: '#dc2626' }}><Trash2 size={13} /></button>
          </span>
        </div>
        {!collapsed.has(node.key) &&
          node.children.map((c, i) => renderNode(c, depth + 1, node.children.length, i))}
      </div>
    )
  }

  if (chLoading) return <PageSpinner />

  const channelOptions = channels.map(c => ({
    value: c.id, label: `${getName(c.nameI18n) || c.code} (${c.firmName})`,
  }))

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Menü Yerleşimi</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            Sitenin üst menüsü — sürükleyerek taşıyın; aynı kategori birden çok bölümde yer alabilir.
            Kaydedilen değişiklik sitede en geç 5 dakika içinde görünür.
          </p>
        </div>
        <div className="flex items-center gap-2">
          {dirty && (
            <span className="text-xs px-2 py-1 rounded-full"
              style={{ background: '#fef9c3', color: '#854d0e' }}>Kaydedilmemiş değişiklik</span>
          )}
          <Button onClick={() => saveMutation.mutate()} loading={saveMutation.isPending}
            disabled={!headerMenu || !dirty}>
            <Save size={14} /> Kaydet
          </Button>
        </div>
      </div>

      <div className="card mb-6">
        <label className="flbl mb-2">Satış Kanalı</label>
        <SearchableSelect
          value={selectedChannelId}
          onChange={(v) => { if (v) { setSelectedChannelId(v); setDirty(false); loadedMenuId.current = null } }}
          options={channelOptions}
          placeholder="Kanal seçin…"
          hasValue={!!selectedChannelId}
        />
      </div>

      {saveMutation.isError && (
        <div className="px-4 py-3 rounded-xl mb-4 text-sm"
          style={{ background: '#fee2e2', color: '#991b1b', border: '1px solid #fecaca' }}>
          Kaydedilemedi: {(saveMutation.error as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'bilinmeyen hata'}
        </div>
      )}

      {selectedChannelId && !menusLoading && !headerMenu && (
        <div className="card text-center py-10">
          <p className="text-sm mb-4" style={{ color: 'var(--text-m)' }}>
            Bu kanalda henüz üst menü tanımı yok. Menü oluşturulana kadar site, kategori ağacını
            olduğu gibi gösterir.
          </p>
          <Button onClick={() => createMenuMutation.mutate()} loading={createMenuMutation.isPending}>
            <Plus size={14} /> Üst Menü Oluştur
          </Button>
        </div>
      )}

      {selectedChannelId && headerMenu && (
        <div className="flex gap-6 items-start">
          {/* Menü ağacı */}
          <div className="card flex-1 min-w-0 p-3">
            <div className="flex items-center justify-between px-2 pb-2 mb-1"
              style={{ borderBottom: '1px solid var(--border)' }}>
              <span className="text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>
                Menü Ağacı — {countNodes(tree)} öğe
              </span>
              <Button variant="secondary" size="sm" onClick={() => openAdd(null)}>
                <Plus size={13} /> Kök Öğe Ekle
              </Button>
            </div>
            {detailLoading && <div className="py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</div>}
            {!detailLoading && tree.length === 0 && (
              <div className="py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Menü boş — sağdaki havuzdan kategori sürükleyin veya "Kök Öğe Ekle" ile başlayın.
              </div>
            )}
            <div
              onDragOver={(e) => {
                if (dragKey || dragCatId) {
                  e.preventDefault()
                  // satır üzerinde değil boş alandayız — satır ipucunu temizle
                  if (dropHint) setDropHint(null)
                }
              }}
              onDrop={(e) => {
                // boş alana bırakma = kök sona ekle
                e.preventDefault()
                if (dropHint) return
                if (dragCatId) apply([...tree, makeCategoryNode(dragCatId)])
                else if (dragKey) {
                  const [without, removed] = removeNode(tree, dragKey)
                  if (removed) apply([...without, removed])
                }
                setDragKey(null); setDragCatId(null)
              }}
            >
              {tree.map((n, i) => renderNode(n, 0, tree.length, i))}
            </div>
          </div>

          {/* Kategori havuzu */}
          <div className="card w-80 shrink-0 p-3">
            <div className="px-1 pb-2 mb-2" style={{ borderBottom: '1px solid var(--border)' }}>
              <span className="text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>
                Kategori Havuzu
              </span>
              <div className="relative mt-2">
                <Search size={13} className="absolute left-2.5 top-1/2 -translate-y-1/2"
                  style={{ color: 'var(--text-s)' }} />
                <input
                  value={poolSearch}
                  onChange={(e) => setPoolSearch(e.target.value)}
                  placeholder="Kategori ara…"
                  className="w-full pl-8 pr-3 py-1.5 rounded-lg text-sm"
                  style={{ background: 'var(--surface2)', border: '1px solid var(--border)', color: 'var(--text)' }}
                />
              </div>
            </div>
            <div className="max-h-[60vh] overflow-y-auto space-y-0.5">
              {pool.map(cat => {
                const usage = used.get(cat.id) ?? 0
                return (
                  <div
                    key={cat.id}
                    draggable
                    onDragStart={() => setDragCatId(cat.id)}
                    onDragEnd={() => { setDragCatId(null); setDropHint(null) }}
                    className="group flex items-center gap-2 px-2 py-1.5 rounded-lg"
                    style={{ cursor: 'grab' }}
                  >
                    <GripVertical size={12} className="shrink-0" style={{ color: 'var(--text-s)' }} />
                    <div className="min-w-0 flex-1">
                      <div className="text-sm truncate" style={{ color: 'var(--text)' }}>{getName(cat.nameI18n)}</div>
                      <code className="text-[11px]" style={{ color: 'var(--text-s)' }}>/{cat.slug}</code>
                    </div>
                    {usage > 0 && (
                      <span className="text-[10px] px-1.5 rounded-full shrink-0"
                        style={{ background: 'var(--surface2)', color: 'var(--text-s)' }}>
                        menüde ×{usage}
                      </span>
                    )}
                    <button
                      title="Menü sonuna ekle"
                      onClick={() => apply([...tree, makeCategoryNode(cat.id)])}
                      className="hidden group-hover:block p-1 rounded shrink-0"
                      style={{ color: 'var(--brand)' }}
                    ><Plus size={13} /></button>
                  </div>
                )
              })}
              {pool.length === 0 && (
                <div className="py-6 text-center text-sm" style={{ color: 'var(--text-s)' }}>Kategori bulunamadı</div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Düğüm ekle/düzenle */}
      <Modal
        open={editOpen}
        onClose={() => setEditOpen(false)}
        title={editKey ? 'Menü Öğesini Düzenle' : 'Menü Öğesi Ekle'}
        footer={
          <>
            <Button variant="secondary" onClick={() => setEditOpen(false)}>İptal</Button>
            <Button onClick={submitForm}
              disabled={form.nodeType === 'category' ? !form.channelCategoryId : !form.nameOverride.trim()}>
              {editKey ? 'Uygula' : 'Ekle'}
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <div>
            <label className="flbl">Öğe Tipi</label>
            <div className="flex gap-2 mt-1">
              {([['category', 'Kategori'], ['link', 'Link'], ['label', 'Başlık']] as const).map(([v, l]) => (
                <button key={v} onClick={() => setForm(f => ({ ...f, nodeType: v }))}
                  className="px-3 py-1.5 rounded-lg text-sm"
                  style={form.nodeType === v
                    ? { background: 'var(--brand)', color: '#fff' }
                    : { background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>
                  {l}
                </button>
              ))}
            </div>
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              Kategori: ürün listesine gider · Link: serbest adres · Başlık: tıklanmayan grup başlığı
            </p>
          </div>

          {form.nodeType === 'category' && (
            <div>
              <label className="flbl">Kategori</label>
              <SearchableSelect
                value={form.channelCategoryId}
                onChange={(v) => setForm(f => ({ ...f, channelCategoryId: v }))}
                options={categories.map(c => ({ value: c.id, label: `${getName(c.nameI18n)} (/${c.slug})` }))}
                placeholder="Kategori seçin…"
                hasValue={!!form.channelCategoryId}
              />
            </div>
          )}

          {form.nodeType === 'link' && (
            <div>
              <label className="flbl">Adres (URL)</label>
              <input value={form.customUrl}
                onChange={(e) => setForm(f => ({ ...f, customUrl: e.target.value }))}
                className="w-full px-3 py-2 rounded-xl text-sm"
                style={{ background: 'var(--surface2)', border: '1px solid var(--border)', color: 'var(--text)' }}
                placeholder="/kampanya veya https://…" />
            </div>
          )}

          <div>
            <label className="flbl">
              Menü Etiketi {form.nodeType === 'category' && <span style={{ color: 'var(--text-s)' }}>(boş = kategori adı)</span>}
            </label>
            <input value={form.nameOverride}
              onChange={(e) => setForm(f => ({ ...f, nameOverride: e.target.value }))}
              className="w-full px-3 py-2 rounded-xl text-sm"
              style={{ background: 'var(--surface2)', border: '1px solid var(--border)', color: 'var(--text)' }}
              placeholder="Örn: KADIN" />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="flbl">Menü Görseli (URL)</label>
              <input value={form.imageUrl}
                onChange={(e) => setForm(f => ({ ...f, imageUrl: e.target.value }))}
                className="w-full px-3 py-2 rounded-xl text-sm"
                style={{ background: 'var(--surface2)', border: '1px solid var(--border)', color: 'var(--text)' }}
                placeholder="/media/menu/…" />
            </div>
            <div>
              <label className="flbl">Rozet</label>
              <input value={form.badgeLabel}
                onChange={(e) => setForm(f => ({ ...f, badgeLabel: e.target.value }))}
                className="w-full px-3 py-2 rounded-xl text-sm"
                style={{ background: 'var(--surface2)', border: '1px solid var(--border)', color: 'var(--text)' }}
                placeholder="YENİ" />
            </div>
          </div>

          <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
            <input type="checkbox" checked={form.isActive}
              onChange={(e) => setForm(f => ({ ...f, isActive: e.target.checked }))} />
            Menüde göster (pasif öğe sitede görünmez, yerleşimde saklanır)
          </label>
        </div>
      </Modal>
    </div>
  )
}

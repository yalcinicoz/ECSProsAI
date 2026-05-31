import { useState, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, ChevronRight, ChevronDown, Folder, FolderOpen } from 'lucide-react'
import { cn } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { IntegerInput } from '@/components/ui/IntegerInput'
import { I18nField } from '@/components/ui/I18nField'
import { PageSpinner } from '@/components/ui/Spinner'
import { useLanguages } from '@/hooks/useLanguages'
import { FL } from '@/lib/field-labels'
import { buildI18nValues } from '@/lib/i18n-helper'

// ── Types ─────────────────────────────────────────────────────────────────────

interface CategoryDto {
  id: string
  code: string
  nameI18n: Record<string, string>
  parentId: string | null
  isActive: boolean
  sortOrder: number
}

interface FormState {
  code: string
  nameI18n: Record<string, string>
  parentId: string | null
  fillType: string
  sortOrder: number
}

interface EditFormState {
  nameI18n: Record<string, string>
  parentId: string | null
  fillType: string
  isActive: boolean
  sortOrder: number
}

// ── Constants ─────────────────────────────────────────────────────────────────

const FILL_TYPE_OPTIONS = [
  { value: 'manual', label: 'Manuel' },
  { value: 'filter', label: 'Filtre' },
  { value: 'mixed',  label: 'Karma' },
]


function getName(item: Pick<CategoryDto, 'nameI18n' | 'code'>): string {
  return item.nameI18n['tr'] ?? item.nameI18n[Object.keys(item.nameI18n)[0]] ?? item.code
}

// ── Component ─────────────────────────────────────────────────────────────────

export function CategoriesPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { data: languages = [], isLoading: langsLoading } = useLanguages()

  // Expansion state — Set of expanded category IDs
  const [expanded, setExpanded] = useState<Set<string>>(new Set())
  // Children cache: parentId → CategoryDto[]
  const [childrenMap, setChildrenMap] = useState<Map<string, CategoryDto[]>>(new Map())

  // Create modal
  const [createOpen, setCreateOpen]     = useState(false)
  const [createParentId, setCreateParentId] = useState<string | null>(null)
  const [form, setForm] = useState<FormState>({
    code: '', nameI18n: {}, parentId: null, fillType: 'manual', sortOrder: 0,
  })

  // Edit modal
  const [editTarget, setEditTarget] = useState<CategoryDto | null>(null)
  const [editForm, setEditForm] = useState<EditFormState>({
    nameI18n: {}, parentId: null, fillType: 'manual', isActive: true, sortOrder: 0,
  })

  // ── Root categories ──────────────────────────────────────────────────────────

  const { data: roots = [], isLoading } = useQuery<CategoryDto[]>({
    queryKey: ['categories-root'],
    queryFn: async () => {
      const { data } = await api.get('/catalog/categories?activeOnly=false')
      return data.data
    },
  })

  // All root categories as options for parent selection
  const rootOptions = useMemo(
    () => roots.map((r) => ({ value: r.id, label: getName(r) })),
    [roots],
  )

  // ── Expand / collapse ────────────────────────────────────────────────────────

  async function toggleExpand(cat: CategoryDto) {
    const next = new Set(expanded)
    if (next.has(cat.id)) {
      next.delete(cat.id)
    } else {
      next.add(cat.id)
      if (!childrenMap.has(cat.id)) {
        try {
          const { data } = await api.get(`/catalog/categories?parentId=${cat.id}&activeOnly=false`)
          setChildrenMap((m) => new Map(m).set(cat.id, data.data))
        } catch {
          setChildrenMap((m) => new Map(m).set(cat.id, []))
        }
      }
    }
    setExpanded(next)
  }

  function invalidateChildren(parentId: string | null) {
    if (parentId) {
      setChildrenMap((m) => {
        const next = new Map(m)
        next.delete(parentId)
        return next
      })
      // Re-fetch if still expanded
      if (expanded.has(parentId)) {
        api.get(`/catalog/categories?parentId=${parentId}&activeOnly=false`).then(({ data }) => {
          setChildrenMap((m) => new Map(m).set(parentId, data.data))
        })
      }
    } else {
      queryClient.invalidateQueries({ queryKey: ['categories-root'] })
    }
  }

  // ── Create mutation ──────────────────────────────────────────────────────────

  const createMutation = useMutation({
    mutationFn: async () => {
      const { data } = await api.post('/catalog/categories', {
        code:     form.code.trim().toLowerCase().replace(/\s+/g, '_'),
        nameI18n: form.nameI18n,
        parentId: form.parentId || null,
        fillType: form.fillType,
        sortOrder: form.sortOrder,
      })
      return data.data?.id as string
    },
    onSuccess: () => {
      invalidateChildren(form.parentId)
      setCreateOpen(false)
    },
  })

  function openCreate(parentId: string | null = null) {
    setCreateParentId(parentId)
    setForm({ code: '', nameI18n: {}, parentId, fillType: 'manual', sortOrder: 0 })
    setCreateOpen(true)
  }

  // ── Edit mutation ────────────────────────────────────────────────────────────

  const editMutation = useMutation({
    mutationFn: async () => {
      if (!editTarget) return
      await api.put(`/catalog/categories/${editTarget.id}`, {
        nameI18n: editForm.nameI18n,
        parentId: editForm.parentId || null,
        fillType: editForm.fillType,
        isActive: editForm.isActive,
        sortOrder: editForm.sortOrder,
      })
    },
    onSuccess: () => {
      // Invalidate original parent and new parent
      invalidateChildren(editTarget?.parentId ?? null)
      if (editForm.parentId !== editTarget?.parentId) {
        invalidateChildren(editForm.parentId)
      }
      setEditTarget(null)
    },
  })

  // ── I18n helpers ─────────────────────────────────────────────────────────────

  const sourceLang = languages.find((l) => l.isDefault)?.code ?? languages[0]?.code ?? 'tr'
  const nameFields = useMemo(() => [{ key: 'name', labels: FL.categoryName, required: true }], [])

  function buildLocalI18nValues(nameI18n: Record<string, string>) {
    return buildI18nValues(nameI18n, languages)
  }

  // ── Row renderer ─────────────────────────────────────────────────────────────

  function CategoryRow({ cat, depth }: { cat: CategoryDto; depth: number }) {
    const isOpen   = expanded.has(cat.id)
    const children = childrenMap.get(cat.id) ?? []

    return (
      <>
        <tr
          className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
          style={{ borderBottom: '1px solid var(--border)' }}
          onClick={() => navigate(`/catalog/categories/${cat.id}`)}
        >
          {/* Expand + Icon */}
          <td className="px-4 py-3">
            <div className="flex items-center gap-1" style={{ paddingLeft: `${depth * 20}px` }}>
              <button
                className="w-5 h-5 flex items-center justify-center rounded flex-shrink-0"
                style={{ color: 'var(--text-s)' }}
                onClick={(e) => { e.stopPropagation(); toggleExpand(cat) }}
                title={isOpen ? 'Kapat' : 'Genişlet'}
              >
                {isOpen ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
              </button>
              <div
                className="w-8 h-8 rounded-xl flex-shrink-0 flex items-center justify-center"
                style={{ background: 'var(--brand-bg)', color: 'var(--brand)' }}
              >
                {isOpen
                  ? <FolderOpen size={13} />
                  : <Folder size={13} />
                }
              </div>
              <div className="ml-1">
                <div className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{getName(cat)}</div>
                <code className="text-xs" style={{ color: 'var(--text-s)' }}>{cat.code}</code>
              </div>
            </div>
          </td>

          {/* Fill Type — DTO'da dönmüyor, placeholder */}
          <td className="px-4 py-3">
            <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span>
          </td>

          {/* Sıra */}
          <td className="px-4 py-3 text-center">
            <span className="text-sm" style={{ color: 'var(--text-s)' }}>{cat.sortOrder}</span>
          </td>

          {/* Durum */}
          <td className="px-4 py-3 text-center">
            <Badge variant={cat.isActive ? 'success' : 'neutral'}>
              {cat.isActive ? 'Aktif' : 'Pasif'}
            </Badge>
          </td>

          {/* Alt kategori ekle */}
          <td className="px-4 py-3 text-right">
            <button
              className="text-xs px-2 py-1 rounded-lg transition-colors"
              style={{ color: 'var(--brand)', background: 'var(--brand-bg)' }}
              onClick={(e) => { e.stopPropagation(); openCreate(cat.id) }}
              title="Alt kategori ekle"
            >
              <Plus size={11} />
            </button>
          </td>
        </tr>

        {/* Children */}
        {isOpen && children.map((child) => (
          <CategoryRow key={child.id} cat={child} depth={depth + 1} />
        ))}
      </>
    )
  }

  // ── Render ────────────────────────────────────────────────────────────────────

  if (isLoading || langsLoading) return <PageSpinner />

  return (
    <div className="p-6">
      {/* Page header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Kategoriler</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{roots.length} kök kategori</p>
        </div>
        <Button onClick={() => openCreate(null)}>
          <Plus size={14} /> Yeni Kategori
        </Button>
      </div>

      {/* Table */}
      <div className="card overflow-hidden p-0">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Kategori</th>
              <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Dolum Tipi</th>
              <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Sıra</th>
              <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Durum</th>
              <th className="w-12 px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {roots.length === 0 && (
              <tr>
                <td colSpan={5} className="text-center py-12 text-sm" style={{ color: 'var(--text-s)' }}>
                  Henüz kategori eklenmemiş
                </td>
              </tr>
            )}
            {roots.map((cat) => (
              <CategoryRow key={cat.id} cat={cat} depth={0} />
            ))}
          </tbody>
        </table>
      </div>

      {/* ── Create Modal ──────────────────────────────────────────────────────── */}
      <Modal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        title={createParentId ? 'Alt Kategori Ekle' : 'Yeni Kategori'}
        size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button
              onClick={() => createMutation.mutate()}
              loading={createMutation.isPending}
              disabled={!form.code.trim()}
            >
              Kaydet
            </Button>
          </>
        }
      >
        <div className="space-y-5">
          {/* Kod */}
          <div>
            <label className="flbl">Kod *</label>
            <input
              className={cn('inp', form.code && 'ok')}
              value={form.code}
              onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))}
              placeholder="erkek_giyim"
              autoFocus
            />
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              Küçük harf ve alt çizgi. Kayıt sonrası değiştirilemez.
            </p>
          </div>

          {/* Üst Kategori */}
          {!createParentId && (
            <div>
              <label className="flbl">Üst Kategori</label>
              <SearchableSelect
                value={form.parentId ?? ''}
                onChange={(v) => setForm((f) => ({ ...f, parentId: v || null }))}
                options={rootOptions}
                placeholder="Kök kategori"
                hasValue={!!form.parentId}
              />
            </div>
          )}

          {/* Dolum Tipi */}
          <div>
            <label className="flbl">Dolum Tipi</label>
            <SearchableSelect
              value={form.fillType}
              onChange={(v) => v && setForm((f) => ({ ...f, fillType: v }))}
              options={FILL_TYPE_OPTIONS}
              hasValue={!!form.fillType}
            />
          </div>

          {/* Sıra */}
          <div>
            <label className="flbl">Sıra</label>
            <IntegerInput
              value={form.sortOrder}
              onChange={(v) => setForm((f) => ({ ...f, sortOrder: v ?? 0 }))}
            />
          </div>

          {/* Ad — I18n */}
          {languages.length > 0 && (
            <div>
              <label className="flbl mb-2">Ad</label>
              <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
                <I18nField
                  sourceLang={sourceLang}
                  languages={languages}
                  fields={nameFields}
                  values={buildLocalI18nValues(form.nameI18n)}
                  onChange={(lang, _key, value) =>
                    setForm((f) => ({ ...f, nameI18n: { ...f.nameI18n, [lang]: value } }))
                  }
                />
              </div>
            </div>
          )}

          {createMutation.isError && (
            <p className="text-sm" style={{ color: 'var(--danger, #ef4444)' }}>Hata oluştu. Tekrar deneyin.</p>
          )}
        </div>
      </Modal>

      {/* ── Edit Modal ────────────────────────────────────────────────────────── */}
      <Modal
        open={!!editTarget}
        onClose={() => setEditTarget(null)}
        title={editTarget ? `Kategori: ${getName(editTarget)}` : ''}
        size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setEditTarget(null)}>İptal</Button>
            <Button
              onClick={() => editMutation.mutate()}
              loading={editMutation.isPending}
            >
              Kaydet
            </Button>
          </>
        }
      >
        {editTarget && (
          <div className="space-y-5">
            {/* Kod (read-only) */}
            <div>
              <label className="flbl">Kod</label>
              <code
                className="block text-sm px-3 py-2 rounded-xl"
                style={{
                  background: 'var(--surface2)',
                  border: '1px solid var(--border)',
                  color: 'var(--text-m)',
                }}
              >
                {editTarget.code}
              </code>
            </div>

            {/* Üst Kategori */}
            <div>
              <label className="flbl">Üst Kategori</label>
              <SearchableSelect
                value={editForm.parentId ?? ''}
                onChange={(v) => setEditForm((f) => ({ ...f, parentId: v || null }))}
                options={rootOptions.filter((o) => o.value !== editTarget.id)}
                placeholder="Kök kategori"
                hasValue={!!editForm.parentId}
              />
            </div>

            {/* Dolum Tipi */}
            <div>
              <label className="flbl">Dolum Tipi</label>
              <SearchableSelect
                value={editForm.fillType}
                onChange={(v) => v && setEditForm((f) => ({ ...f, fillType: v }))}
                options={FILL_TYPE_OPTIONS}
                hasValue={!!editForm.fillType}
              />
            </div>

            {/* Sıra */}
            <div>
              <label className="flbl">Sıra</label>
              <IntegerInput
                value={editForm.sortOrder}
                onChange={(v) => setEditForm((f) => ({ ...f, sortOrder: v ?? 0 }))}
              />
            </div>

            {/* Durum */}
            <div className="flex items-center gap-3">
              <label className="flbl mb-0">Durum</label>
              <button
                onClick={() => setEditForm((f) => ({ ...f, isActive: !f.isActive }))}
                className={cn(
                  'relative w-10 h-5 rounded-full transition-colors',
                  editForm.isActive ? 'bg-[var(--brand)]' : 'bg-[var(--border)]',
                )}
              >
                <span
                  className={cn(
                    'absolute top-0.5 w-4 h-4 rounded-full bg-white transition-transform shadow-sm',
                    editForm.isActive ? 'translate-x-5' : 'translate-x-0.5',
                  )}
                />
              </button>
              <span className="text-sm" style={{ color: editForm.isActive ? 'var(--brand)' : 'var(--text-s)' }}>
                {editForm.isActive ? 'Aktif' : 'Pasif'}
              </span>
            </div>

            {/* Ad — I18n */}
            {languages.length > 0 && (
              <div>
                <label className="flbl mb-2">Ad</label>
                <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
                  <I18nField
                    sourceLang={sourceLang}
                    languages={languages}
                    fields={nameFields}
                    values={buildLocalI18nValues(editForm.nameI18n)}
                    onChange={(lang, _key, value) =>
                      setEditForm((f) => ({ ...f, nameI18n: { ...f.nameI18n, [lang]: value } }))
                    }
                  />
                </div>
              </div>
            )}

            {editMutation.isError && (
              <p className="text-sm" style={{ color: 'var(--danger, #ef4444)' }}>Hata oluştu. Tekrar deneyin.</p>
            )}
          </div>
        )}
      </Modal>
    </div>
  )
}

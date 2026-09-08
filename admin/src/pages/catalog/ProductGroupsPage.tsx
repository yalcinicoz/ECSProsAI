import { useState, useMemo, useRef } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Plus, ChevronRight, FolderOpen, Search, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Input } from '@/components/ui/Input'
import { PageSpinner } from '@/components/ui/Spinner'
import { PermissionGuard, ReadOnlyBadge } from '@/components/ui/PermissionGuard'

import { CreateProductGroupModal } from './CreateProductGroupModal'

const PLATFORM_PERM = 'catalog.platform.manage'

// ── Types ────────────────────────────────────────────────────────────────────

export interface ProductGroupAttribute {
  id: string
  attributeTypeId: string
  attributeTypeCode: string
  attributeTypeNameI18n: Record<string, string>
  isVariant: boolean
  isRequired: boolean
  isPrimaryAxis: boolean
  sortOrder: number
  defaultAttributeValueId?: string | null
}

export interface ProductGroupAxisSubAttribute {
  id: string
  axisAttributeTypeId: string
  axisAttributeTypeCode: string
  axisAttributeTypeNameI18n: Record<string, string>
  subAttributeTypeId: string
  subAttributeTypeCode: string
  subAttributeTypeNameI18n: Record<string, string>
  isRequired: boolean
  sortOrder: number
}

export interface ProductGroup {
  id: string
  code: string
  nameI18n: Record<string, string>
  isActive: boolean
  sortOrder: number
  hasProducts: boolean
  attributes: ProductGroupAttribute[]
  axisSubAttributes?: ProductGroupAxisSubAttribute[]
}

function getName(pg: ProductGroup): string {
  return pg.nameI18n['tr'] ?? pg.nameI18n[Object.keys(pg.nameI18n)[0]] ?? '—'
}

function normalizeSearch(value: string): string {
  return value.toLocaleLowerCase('tr').normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '').replace(/ı/g, 'i').replace(/\s+/g, ' ').trim()
}

// ── Component ─────────────────────────────────────────────────────────────────

export function ProductGroupsPage() {
  const navigate = useNavigate()

  const [activeOnly, setActiveOnly] = useState(false)
  const [search, setSearch] = useState('')
  const searchRef = useRef<HTMLInputElement>(null)
  const [createOpen, setCreateOpen] = useState(false)

  const { data: groups = [], isLoading } = useQuery<ProductGroup[]>({
    queryKey: ['product-groups', activeOnly],
    queryFn: async () => {
      const { data } = await api.get(`/catalog/product-groups?activeOnly=${activeOnly}`)
      return data.data
    },
  })

  const sorted = useMemo(
    () => [...groups].sort((a, b) => a.sortOrder - b.sortOrder || getName(a).localeCompare(getName(b), 'tr')),
    [groups],
  )
  const filtered = useMemo(() => {
    const query = normalizeSearch(search)
    return query
      ? sorted.filter((g) => [g.code, ...Object.values(g.nameI18n)].some((value) => normalizeSearch(value).includes(query)))
      : sorted
  }, [sorted, search])

  function clearSearch() {
    setSearch('')
    searchRef.current?.focus()
  }

  if (isLoading) return <PageSpinner />

  return (
    <div className="p-6">
      {/* Page header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Ürün Grupları</h1>
            <PermissionGuard permission={PLATFORM_PERM} fallback={<ReadOnlyBadge />} />
          </div>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{groups.length} kayıt</p>
        </div>

        <div className="flex items-center gap-3">
          <div
            className="flex items-center gap-1 rounded-xl p-1"
            style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}
          >
            <button
              onClick={() => setActiveOnly(false)}
              className={cn(
                'px-3 py-1 rounded-lg text-sm font-medium transition-all',
                !activeOnly ? 'bg-white shadow-sm' : 'text-[var(--text-s)]',
              )}
              style={!activeOnly ? { color: 'var(--text)' } : {}}
            >
              Tümü
            </button>
            <button
              onClick={() => setActiveOnly(true)}
              className={cn(
                'px-3 py-1 rounded-lg text-sm font-medium transition-all',
                activeOnly ? 'bg-white shadow-sm' : 'text-[var(--text-s)]',
              )}
              style={activeOnly ? { color: 'var(--text)' } : {}}
            >
              Aktif
            </button>
          </div>

          <PermissionGuard permission={PLATFORM_PERM}>
            <Button onClick={() => setCreateOpen(true)}>
              <Plus size={14} /> Yeni Grup
            </Button>
          </PermissionGuard>
        </div>
      </div>

      {/* Table */}
      <div className="card overflow-hidden p-0">
        <div
          className="flex flex-col gap-2 px-4 py-3 sm:flex-row sm:items-center sm:justify-between"
          style={{ borderBottom: '1px solid var(--border)' }}
        >
          <div className="relative w-full sm:max-w-sm">
            <Search size={16} aria-hidden="true" className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2" style={{ color: 'var(--text-s)' }} />
            <Input
              ref={searchRef}
              role="searchbox"
              aria-label="Ürün grubu ara"
              placeholder="Grup adı veya koduyla ara…"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              onKeyDown={(event) => { if (event.key === 'Escape') clearSearch() }}
              style={{ paddingLeft: 36, paddingRight: 40 }}
            />
            {search && (
              <button
                type="button"
                aria-label="Aramayı temizle"
                onClick={clearSearch}
                className="absolute right-1 top-1/2 -translate-y-1/2 rounded-md p-2 hover:bg-[var(--surface2)] focus-visible:outline-2 focus-visible:outline-[var(--brand)]"
                style={{ color: 'var(--text-s)' }}
              >
                <X size={14} aria-hidden="true" />
              </button>
            )}
          </div>
          <span role="status" className="text-xs shrink-0" style={{ color: 'var(--text-s)' }}>
            {filtered.length} / {groups.length} grup
          </span>
        </div>
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Ad</th>
              <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Kod</th>
              <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Özellik</th>
              <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Varyant</th>
              <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Sıra</th>
              <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Durum</th>
              <th className="w-8 px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 && (
              <tr>
                <td colSpan={7} className="text-center py-12 text-sm" style={{ color: 'var(--text-s)' }}>
                  {search.trim() ? 'Aramanızla eşleşen ürün grubu bulunamadı' : 'Ürün grubu bulunamadı'}
                </td>
              </tr>
            )}
            {filtered.map((g) => {
              const variantCount = g.attributes.filter((a) => a.isVariant).length
              return (
                <tr
                  key={g.id}
                  onClick={() => navigate(`/catalog/product-groups/${g.id}`)}
                  className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                  style={{ borderBottom: '1px solid var(--border)' }}
                >
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <FolderOpen size={14} style={{ color: 'var(--brand)', flexShrink: 0 }} />
                      <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{getName(g)}</span>
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <code
                      className="text-xs px-2 py-0.5 rounded-md font-mono"
                      style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}
                    >
                      {g.code}
                    </code>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span className="text-sm" style={{ color: 'var(--text-m)' }}>{g.attributes.length}</span>
                  </td>
                  <td className="px-4 py-3 text-center">
                    {variantCount > 0 ? (
                      <Badge variant="default">{variantCount} eksen</Badge>
                    ) : (
                      <span className="text-sm" style={{ color: 'var(--text-s)' }}>—</span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span className="text-sm" style={{ color: 'var(--text-s)' }}>{g.sortOrder}</span>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <Badge variant={g.isActive ? 'success' : 'neutral'}>
                      {g.isActive ? 'Aktif' : 'Pasif'}
                    </Badge>
                  </td>
                  <td className="px-4 py-3">
                    <ChevronRight size={14} style={{ color: 'var(--text-s)' }} />
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {createOpen && (
        <CreateProductGroupModal
          onClose={() => setCreateOpen(false)}
          onCreated={(group) => {
            setCreateOpen(false)
            navigate(`/catalog/product-groups/${group.id}`)
          }}
        />
      )}
    </div>
  )
}

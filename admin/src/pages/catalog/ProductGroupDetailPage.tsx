import { useState, useMemo, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { Plus, ChevronRight, ArrowLeft, Tag, Layers, X, Star, Lock, Pencil, Trash2 } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { IntegerInput } from '@/components/ui/IntegerInput'
import { I18nField } from '@/components/ui/I18nField'
import { PageSpinner } from '@/components/ui/Spinner'
import { PermissionGuard, ReadOnlyBadge } from '@/components/ui/PermissionGuard'
import { useAuthStore } from '@/store/auth'

const PLATFORM_PERM = 'catalog.platform.manage'
import { useLanguages } from '@/hooks/useLanguages'
import { FL } from '@/lib/field-labels'
import { buildI18nValues } from '@/lib/i18n-helper'
import type { ProductGroup, ProductGroupAttribute } from './ProductGroupsPage'

interface AttributeType {
  id: string
  code: string
  nameI18n: Record<string, string>
  dataType: string
  isActive: boolean
  sortOrder: number
}

interface AxisSubAttribute {
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

interface ProductGroupWithAxis extends ProductGroup {
  axisSubAttributes: AxisSubAttribute[]
}

function getAttrTypeName(a: Pick<ProductGroupAttribute, 'attributeTypeCode' | 'attributeTypeNameI18n'>): string {
  return a.attributeTypeNameI18n['tr'] ?? a.attributeTypeNameI18n[Object.keys(a.attributeTypeNameI18n)[0]] ?? a.attributeTypeCode
}

function getI18nName(nameI18n: Record<string, string>, code: string): string {
  return nameI18n['tr'] ?? nameI18n[Object.keys(nameI18n)[0]] ?? code
}

interface ConfirmDialogProps {
  open: boolean
  title: string
  message: string
  onConfirm: () => void
  onCancel: () => void
  loading?: boolean
}

function ConfirmDialog({ open, title, message, onConfirm, onCancel, loading }: ConfirmDialogProps) {
  return (
    <Modal open={open} onClose={onCancel} title={title} size="sm"
      footer={
        <>
          <Button variant="secondary" onClick={onCancel} disabled={loading}>İptal</Button>
          <Button variant="danger" onClick={onConfirm} loading={loading}>Sil</Button>
        </>
      }>
      <p className="text-sm" style={{ color: 'var(--text)' }}>{message}</p>
    </Modal>
  )
}

export function ProductGroupDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { data: languages = [], isLoading: langsLoading } = useLanguages()

  const hasPermission = useAuthStore(s => s.hasPermission)
  const canEdit = hasPermission(PLATFORM_PERM)

  // Editable nameI18n state
  const [editNameI18n, setEditNameI18n] = useState<Record<string, string>>({})
  const [nameDirty, setNameDirty] = useState(false)
  const [nameSaved, setNameSaved] = useState(false)

  // Add attribute modal
  const [addAttrOpen, setAddAttrOpen] = useState(false)
  const [attrForm, setAttrForm] = useState<{
    attributeTypeId: string | null; isVariant: boolean; isRequired: boolean; sortOrder: number
  }>({ attributeTypeId: null, isVariant: false, isRequired: false, sortOrder: 0 })

  // Edit attribute modal
  const [editAttrOpen, setEditAttrOpen] = useState(false)
  const [editAttrTarget, setEditAttrTarget] = useState<ProductGroupAttribute | null>(null)
  const [editAttrForm, setEditAttrForm] = useState<{
    isVariant: boolean; isRequired: boolean; sortOrder: number
  }>({ isVariant: false, isRequired: false, sortOrder: 0 })

  // Delete group confirm
  const [deleteGroupOpen, setDeleteGroupOpen] = useState(false)

  // Delete attribute confirm
  const [deleteAttrTarget, setDeleteAttrTarget] = useState<ProductGroupAttribute | null>(null)

  // Add sub-attribute modal
  const [addSubAttrOpen, setAddSubAttrOpen] = useState(false)
  const [subAttrForm, setSubAttrForm] = useState<{
    axisAttributeTypeId: string | null; subAttributeTypeId: string | null; isRequired: boolean; sortOrder: number
  }>({ axisAttributeTypeId: null, subAttributeTypeId: null, isRequired: false, sortOrder: 0 })

  // Edit sub-attribute modal
  const [editSubAttrOpen, setEditSubAttrOpen] = useState(false)
  const [editSubAttrTarget, setEditSubAttrTarget] = useState<AxisSubAttribute | null>(null)
  const [editSubAttrForm, setEditSubAttrForm] = useState<{ isRequired: boolean; sortOrder: number }>({ isRequired: false, sortOrder: 0 })

  // Delete sub-attribute confirm
  const [deleteSubAttrTarget, setDeleteSubAttrTarget] = useState<AxisSubAttribute | null>(null)

  const { data: groups = [], isLoading: groupsLoading } = useQuery<ProductGroupWithAxis[]>({
    queryKey: ['product-groups', false],
    queryFn: async () => { const { data } = await api.get('/catalog/product-groups?activeOnly=false'); return data.data },
  })

  const { data: attrTypes = [], isLoading: attrTypesLoading } = useQuery<AttributeType[]>({
    queryKey: ['attribute-types', false],
    queryFn: async () => { const { data } = await api.get('/catalog/attribute-types?activeOnly=false'); return data.data },
  })

  const group = groups.find((g) => g.id === id) as ProductGroupWithAxis | undefined

  const addAttrMutation = useMutation({
    mutationFn: async () => {
      await api.post(`/catalog/product-groups/${id}/attributes`, {
        attributeTypeId: attrForm.attributeTypeId, isVariant: attrForm.isVariant,
        isRequired: attrForm.isRequired, sortOrder: attrForm.sortOrder,
      })
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['product-groups'] }); setAddAttrOpen(false) },
  })

  const editAttrMutation = useMutation({
    mutationFn: async () => {
      if (!editAttrTarget) return
      await api.put(`/catalog/product-groups/${id}/attributes/${editAttrTarget.id}`, {
        isVariant: editAttrForm.isVariant, isRequired: editAttrForm.isRequired, sortOrder: editAttrForm.sortOrder,
      })
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['product-groups'] }); setEditAttrOpen(false) },
  })

  const deleteAttrMutation = useMutation({
    mutationFn: async (attrId: string) => {
      await api.delete(`/catalog/product-groups/${id}/attributes/${attrId}`)
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['product-groups'] }); setDeleteAttrTarget(null) },
  })

  const addSubAttrMutation = useMutation({
    mutationFn: async () => {
      await api.post(`/catalog/product-groups/${id}/axis-sub-attributes`, {
        axisAttributeTypeId: subAttrForm.axisAttributeTypeId,
        subAttributeTypeId: subAttrForm.subAttributeTypeId,
        isRequired: subAttrForm.isRequired, sortOrder: subAttrForm.sortOrder,
      })
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['product-groups'] }); setAddSubAttrOpen(false) },
  })

  const editSubAttrMutation = useMutation({
    mutationFn: async () => {
      if (!editSubAttrTarget) return
      await api.put(`/catalog/product-groups/${id}/axis-sub-attributes/${editSubAttrTarget.id}`, {
        isRequired: editSubAttrForm.isRequired, sortOrder: editSubAttrForm.sortOrder,
      })
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['product-groups'] }); setEditSubAttrOpen(false) },
  })

  const removeSubAttrMutation = useMutation({
    mutationFn: async (subAttrId: string) => {
      await api.delete(`/catalog/product-groups/${id}/axis-sub-attributes/${subAttrId}`)
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['product-groups'] }); setDeleteSubAttrTarget(null) },
  })

  const setPrimaryAxisMutation = useMutation({
    mutationFn: async (attrId: string) => {
      await api.patch(`/catalog/product-groups/${id}/attributes/${attrId}/set-primary-axis`)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['product-groups'] }),
  })

  const deleteGroupMutation = useMutation({
    mutationFn: async () => { await api.delete(`/catalog/product-groups/${id}`) },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product-groups'] })
      navigate('/catalog/product-groups')
    },
  })

  const updateNameMutation = useMutation({
    mutationFn: async () => {
      if (!group) return
      await api.put(`/catalog/product-groups/${id}`, {
        nameI18n: editNameI18n,
        sortOrder: group.sortOrder,
        isActive: group.isActive,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product-groups'] })
      setNameDirty(false)
      setNameSaved(true)
      setTimeout(() => setNameSaved(false), 2500)
    },
  })

  useEffect(() => {
    if (group) {
      setEditNameI18n(group.nameI18n)
      setNameDirty(false)
    }
  }, [group?.id])

  const sourceLang = languages.find((l) => l.isDefault)?.code ?? languages[0]?.code ?? 'tr'

  const nameI18nValues = useMemo(
    () => buildI18nValues(editNameI18n, languages),
    [editNameI18n, languages],
  )

  const i18nFields = useMemo(() => [{ key: 'name', labels: FL.name, required: true }], [])

  const alreadyAddedIds = new Set(group?.attributes.map((a) => a.attributeTypeId) ?? [])
  const attrTypeOptions = attrTypes.filter((at) => !alreadyAddedIds.has(at.id)).map((at) => ({ value: at.id, label: getI18nName(at.nameI18n, at.code) }))
  const variantAxes = useMemo(() => (group?.attributes ?? []).filter((a) => a.isVariant), [group])
  const axisOptions = variantAxes.map((a) => ({ value: a.attributeTypeId, label: getAttrTypeName(a) }))

  const alreadySubAttrIds = useMemo(() => {
    if (!subAttrForm.axisAttributeTypeId || !group) return new Set<string>()
    return new Set((group.axisSubAttributes ?? []).filter((s) => s.axisAttributeTypeId === subAttrForm.axisAttributeTypeId).map((s) => s.subAttributeTypeId))
  }, [subAttrForm.axisAttributeTypeId, group])

  const subAttrTypeOptions = useMemo(() => {
    if (!subAttrForm.axisAttributeTypeId) return []
    return attrTypes.filter((at) => at.id !== subAttrForm.axisAttributeTypeId && !alreadySubAttrIds.has(at.id)).map((at) => ({ value: at.id, label: getI18nName(at.nameI18n, at.code) }))
  }, [attrTypes, subAttrForm.axisAttributeTypeId, alreadySubAttrIds])

  const subAttrsByAxis = useMemo(() => {
    const map = new Map<string, AxisSubAttribute[]>()
    for (const s of group?.axisSubAttributes ?? []) { const arr = map.get(s.axisAttributeTypeId) ?? []; arr.push(s); map.set(s.axisAttributeTypeId, arr) }
    return map
  }, [group?.axisSubAttributes])

  function openAddAttr() {
    setAttrForm({ attributeTypeId: null, isVariant: false, isRequired: false, sortOrder: (group?.attributes.length ?? 0) * 10 })
    setAddAttrOpen(true)
  }

  function openEditAttr(attr: ProductGroupAttribute) {
    setEditAttrTarget(attr)
    setEditAttrForm({ isVariant: attr.isVariant, isRequired: attr.isRequired, sortOrder: attr.sortOrder })
    editAttrMutation.reset()
    setEditAttrOpen(true)
  }

  function openAddSubAttr(axisId?: string) {
    setSubAttrForm({ axisAttributeTypeId: axisId ?? null, subAttributeTypeId: null, isRequired: false, sortOrder: 0 })
    addSubAttrMutation.reset()
    setAddSubAttrOpen(true)
  }

  function openEditSubAttr(s: AxisSubAttribute) {
    setEditSubAttrTarget(s)
    setEditSubAttrForm({ isRequired: s.isRequired, sortOrder: s.sortOrder })
    editSubAttrMutation.reset()
    setEditSubAttrOpen(true)
  }

  const isLoading = groupsLoading || langsLoading || attrTypesLoading
  if (isLoading) return <PageSpinner />

  if (!group) {
    return (
      <div className="p-6">
        <div className="flex flex-col items-center justify-center py-24 gap-4">
          <p className="text-sm" style={{ color: 'var(--text-s)' }}>Ürün grubu bulunamadı.</p>
          <Button variant="secondary" onClick={() => navigate('/catalog/product-groups')}><ArrowLeft size={14} /> Geri Dön</Button>
        </div>
      </div>
    )
  }

  const variantAttrs = group.attributes.filter((a) => a.isVariant)

  return (
    <div className="p-6 space-y-6">
      <nav className="flex items-center gap-1 text-sm" style={{ color: 'var(--text-s)' }}>
        <Link to="/catalog/product-groups" className="hover:text-[var(--text)] transition-colors">Ürün Grupları</Link>
        <ChevronRight size={12} />
        <span style={{ color: 'var(--text)' }}>{group.nameI18n['tr'] ?? '—'}</span>
      </nav>

      <div className="flex items-center justify-between">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>{group.nameI18n['tr'] ?? '—'}</h1>
            <PermissionGuard permission={PLATFORM_PERM} fallback={<ReadOnlyBadge />} />
          </div>
          <div className="flex items-center gap-2 mt-1">
            <code className="text-xs px-2 py-0.5 rounded-md font-mono" style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>{group.code}</code>
            <Badge variant={group.isActive ? 'success' : 'neutral'}>{group.isActive ? 'Aktif' : 'Pasif'}</Badge>
          </div>
        </div>
        <PermissionGuard permission={PLATFORM_PERM}>
          {!group.hasProducts && (
            <Button variant="danger" onClick={() => setDeleteGroupOpen(true)}>
              <Trash2 size={14} /> Grubu Sil
            </Button>
          )}
        </PermissionGuard>
      </div>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <div className="card p-4"><div className="text-xs font-semibold mb-1" style={{ color: 'var(--text-s)' }}>Toplam Özellik</div><div className="text-lg font-bold" style={{ color: 'var(--text)' }}>{group.attributes.length}</div></div>
        <div className="card p-4"><div className="text-xs font-semibold mb-1" style={{ color: 'var(--text-s)' }}>Varyant Ekseni</div><div className="text-lg font-bold" style={{ color: 'var(--brand)' }}>{variantAttrs.length}</div></div>
        <div className="card p-4"><div className="text-xs font-semibold mb-1" style={{ color: 'var(--text-s)' }}>Sıra</div><div className="text-sm font-medium" style={{ color: 'var(--text)' }}>{group.sortOrder}</div></div>
        <div className="card p-4"><div className="text-xs font-semibold mb-1" style={{ color: 'var(--text-s)' }}>Durum</div><Badge variant={group.isActive ? 'success' : 'neutral'}>{group.isActive ? 'Aktif' : 'Pasif'}</Badge></div>
      </div>

      {languages.length > 0 && (
        <div className="card p-0 overflow-hidden">
          <div className="flex items-center justify-between px-5 py-3 border-b" style={{ borderColor: 'var(--border)' }}>
            <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>Ad (Çeviriler)</h2>
            {canEdit && (
              <div className="flex items-center gap-2">
                {nameSaved && <span className="text-xs" style={{ color: 'var(--brand)' }}>Kaydedildi</span>}
                <Button size="sm" onClick={() => updateNameMutation.mutate()} loading={updateNameMutation.isPending} disabled={!nameDirty}>
                  Kaydet
                </Button>
              </div>
            )}
          </div>
          <I18nField
            sourceLang={sourceLang}
            languages={languages}
            fields={i18nFields}
            values={nameI18nValues}
            readOnly={!canEdit}
            onChange={(lang, _key, val) => {
              setEditNameI18n(prev => ({ ...prev, [lang]: val }))
              setNameDirty(true)
              setNameSaved(false)
            }}
          />
        </div>
      )}

      {/* Özellikler */}
      <div className="card p-0 overflow-hidden">
        <div className="flex items-center justify-between px-5 py-3 border-b" style={{ borderColor: 'var(--border)' }}>
          <div className="flex items-center gap-3">
            <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>Özellikler <span className="ml-2 font-normal" style={{ color: 'var(--text-s)' }}>({group.attributes.length})</span></h2>
            {group.hasProducts && (
              <span className="flex items-center gap-1 text-xs px-2 py-0.5 rounded-lg"
                style={{ background: 'var(--surface2)', color: 'var(--text-s)', border: '1px solid var(--border)' }}>
                <Lock size={10} /> Ürün mevcut — ana eksen kilitli
              </span>
            )}
          </div>
          <PermissionGuard permission={PLATFORM_PERM}>
            <Button size="sm" onClick={openAddAttr} disabled={attrTypeOptions.length === 0}><Plus size={13} /> Özellik Ekle</Button>
          </PermissionGuard>
        </div>
        {group.attributes.length === 0 ? (
          <div className="py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Henüz özellik eklenmemiş</div>
        ) : (
          <table className="w-full">
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
                <th className="text-left px-4 py-2.5 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Özellik Tipi</th>
                <th className="text-center px-4 py-2.5 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Varyant Ekseni</th>
                <th className="text-center px-4 py-2.5 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Ana Eksen</th>
                <th className="text-center px-4 py-2.5 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Zorunlu</th>
                <th className="text-center px-4 py-2.5 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Sıra</th>
                <PermissionGuard permission={PLATFORM_PERM}>
                  <th className="px-4 py-2.5" />
                </PermissionGuard>
              </tr>
            </thead>
            <tbody>
              {group.attributes.slice().sort((a, b) => a.sortOrder - b.sortOrder).map((attr) => (
                <tr key={attr.id} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      {attr.isVariant ? <Layers size={14} style={{ color: 'var(--brand)', flexShrink: 0 }} /> : <Tag size={14} style={{ color: 'var(--text-s)', flexShrink: 0 }} />}
                      <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{getAttrTypeName(attr)}</span>
                      <code className="text-xs px-1.5 py-0.5 rounded font-mono" style={{ background: 'var(--surface2)', color: 'var(--text-s)', border: '1px solid var(--border)' }}>{attr.attributeTypeCode}</code>
                    </div>
                  </td>
                  <td className="px-4 py-3 text-center">{attr.isVariant ? <Badge variant="default">Evet</Badge> : <span className="text-sm" style={{ color: 'var(--text-s)' }}>—</span>}</td>
                  <td className="px-4 py-3 text-center">
                    {attr.isVariant ? (
                      group.hasProducts ? (
                        <span
                          title="Bu gruba ait ürünler mevcut — ana eksen değiştirilemez"
                          className="inline-flex items-center justify-center w-7 h-7 rounded-lg"
                        >
                          {attr.isPrimaryAxis
                            ? <Star size={15} fill="#f59e0b" style={{ color: '#f59e0b' }} />
                            : <Lock size={13} style={{ color: 'var(--text-s)' }} />}
                        </span>
                      ) : (
                        <PermissionGuard permission={PLATFORM_PERM} fallback={
                          <span className="inline-flex items-center justify-center w-7 h-7">
                            <Star size={15} fill={attr.isPrimaryAxis ? '#f59e0b' : 'none'}
                              style={{ color: attr.isPrimaryAxis ? '#f59e0b' : 'var(--text-s)' }} />
                          </span>
                        }>
                          <button
                            title={attr.isPrimaryAxis ? 'Ana eksen' : 'Ana eksen yap'}
                            onClick={() => !attr.isPrimaryAxis && setPrimaryAxisMutation.mutate(attr.id)}
                            disabled={setPrimaryAxisMutation.isPending}
                            className="inline-flex items-center justify-center w-7 h-7 rounded-lg transition-colors hover:bg-[var(--surface2)]"
                          >
                            <Star size={15} fill={attr.isPrimaryAxis ? '#f59e0b' : 'none'}
                              style={{ color: attr.isPrimaryAxis ? '#f59e0b' : 'var(--text-s)' }} />
                          </button>
                        </PermissionGuard>
                      )
                    ) : (
                      <span className="text-sm" style={{ color: 'var(--text-s)' }}>—</span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-center">{attr.isRequired ? <Badge variant="warning">Zorunlu</Badge> : <span className="text-sm" style={{ color: 'var(--text-s)' }}>—</span>}</td>
                  <td className="px-4 py-3 text-center"><span className="text-sm" style={{ color: 'var(--text-s)' }}>{attr.sortOrder}</span></td>
                  <PermissionGuard permission={PLATFORM_PERM}>
                    <td className="px-3 py-3">
                      <div className="flex items-center justify-end gap-1">
                        <button
                          title="Düzenle"
                          onClick={() => openEditAttr(attr)}
                          className="inline-flex items-center justify-center w-7 h-7 rounded-lg transition-colors hover:bg-[var(--surface2)]"
                        >
                          <Pencil size={13} style={{ color: 'var(--text-s)' }} />
                        </button>
                        <button
                          title="Sil"
                          onClick={() => setDeleteAttrTarget(attr)}
                          className="inline-flex items-center justify-center w-7 h-7 rounded-lg transition-colors hover:bg-[var(--surface2)]"
                        >
                          <Trash2 size={13} style={{ color: '#ef4444' }} />
                        </button>
                      </div>
                    </td>
                  </PermissionGuard>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Varyant Ekseni Alt Özellikleri */}
      {variantAttrs.length > 0 && (
        <div className="card p-0 overflow-hidden">
          <div className="flex items-center justify-between px-5 py-3 border-b" style={{ borderColor: 'var(--border)' }}>
            <div>
              <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>Varyant Ekseni Alt Özellikleri</h2>
              <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>Her varyant değerinin kendine özgü ölçülebilir özellikleri (örn. Beden 38 → Paça Boyu: 74 cm)</p>
            </div>
            <PermissionGuard permission={PLATFORM_PERM}>
              <Button size="sm" onClick={() => openAddSubAttr()}><Plus size={13} /> Alt Özellik Ekle</Button>
            </PermissionGuard>
          </div>
          <div>
            {variantAttrs.map((axis, idx) => {
              const subAttrs = (subAttrsByAxis.get(axis.attributeTypeId) ?? []).sort((a, b) => a.sortOrder - b.sortOrder)
              return (
                <div key={axis.attributeTypeId} className="px-5 py-4"
                  style={{ borderBottom: idx < variantAttrs.length - 1 ? '1px solid var(--border)' : undefined }}>
                  <div className="flex items-center justify-between mb-3">
                    <div className="flex items-center gap-2">
                      <Layers size={14} style={{ color: 'var(--brand)' }} />
                      <span className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{getAttrTypeName(axis)}</span>
                      <span className="text-xs" style={{ color: 'var(--text-s)' }}>ekseni</span>
                    </div>
                    <PermissionGuard permission={PLATFORM_PERM}>
                      <button className="text-xs px-2 py-1 rounded-lg transition-colors"
                        style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
                        onClick={() => openAddSubAttr(axis.attributeTypeId)}>
                        + Alt Özellik
                      </button>
                    </PermissionGuard>
                  </div>
                  {subAttrs.length === 0 ? (
                    <p className="text-xs pl-5" style={{ color: 'var(--text-s)' }}>Bu eksen için henüz alt özellik tanımlanmamış</p>
                  ) : (
                    <div className="pl-5 flex flex-wrap gap-2">
                      {subAttrs.map((s) => (
                        <div key={s.id} className="flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs"
                          style={{ background: 'var(--surface2)', border: '1px solid var(--border)', color: 'var(--text)' }}>
                          <Tag size={11} style={{ color: 'var(--text-s)' }} />
                          <span>{getI18nName(s.subAttributeTypeNameI18n, s.subAttributeTypeCode)}</span>
                          {s.isRequired && <span className="text-amber-500 font-bold leading-none ml-0.5">*</span>}
                          <span className="text-xs ml-0.5" style={{ color: 'var(--text-s)' }}>#{s.sortOrder}</span>
                          <PermissionGuard permission={PLATFORM_PERM}>
                            <button
                              title="Düzenle"
                              onClick={() => openEditSubAttr(s)}
                              className="ml-0.5 rounded p-0.5 transition-colors hover:opacity-60"
                            >
                              <Pencil size={11} style={{ color: 'var(--text-s)' }} />
                            </button>
                            <button
                              title="Sil"
                              onClick={() => setDeleteSubAttrTarget(s)}
                              className="rounded p-0.5 transition-colors hover:opacity-60"
                            >
                              <X size={11} style={{ color: '#ef4444' }} />
                            </button>
                          </PermissionGuard>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        </div>
      )}

      {/* Özellik Ekle Modal */}
      <Modal open={addAttrOpen} onClose={() => setAddAttrOpen(false)} title="Özellik Ekle" size="md"
        footer={<><Button variant="secondary" onClick={() => setAddAttrOpen(false)}>İptal</Button><Button onClick={() => addAttrMutation.mutate()} loading={addAttrMutation.isPending} disabled={!attrForm.attributeTypeId}>Ekle</Button></>}>
        <div className="space-y-5">
          <div><label className="flbl">Özellik Tipi *</label><SearchableSelect value={attrForm.attributeTypeId} onChange={(v) => setAttrForm((f) => ({ ...f, attributeTypeId: v }))} options={attrTypeOptions} placeholder="— Seçin —" hasValue={!!attrForm.attributeTypeId} /></div>
          <div className="flex gap-4">
            <label className="flex items-center gap-2 cursor-pointer select-none"><input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={attrForm.isVariant} onChange={(e) => setAttrForm((f) => ({ ...f, isVariant: e.target.checked }))} /><span className="text-sm" style={{ color: 'var(--text)' }}>Varyant Ekseni</span></label>
            <label className="flex items-center gap-2 cursor-pointer select-none"><input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={attrForm.isRequired} onChange={(e) => setAttrForm((f) => ({ ...f, isRequired: e.target.checked }))} /><span className="text-sm" style={{ color: 'var(--text)' }}>Zorunlu</span></label>
          </div>
          <div><label className="flbl">Sıra</label><IntegerInput value={attrForm.sortOrder} onChange={(v) => setAttrForm((f) => ({ ...f, sortOrder: v ?? 0 }))} /></div>
          {addAttrMutation.isError && <p className="text-sm" style={{ color: '#ef4444' }}>Hata oluştu. Lütfen tekrar deneyin.</p>}
        </div>
      </Modal>

      {/* Özellik Düzenle Modal */}
      <Modal open={editAttrOpen} onClose={() => setEditAttrOpen(false)} title={`Özellik Düzenle — ${editAttrTarget ? getAttrTypeName(editAttrTarget) : ''}`} size="md"
        footer={<><Button variant="secondary" onClick={() => setEditAttrOpen(false)}>İptal</Button><Button onClick={() => editAttrMutation.mutate()} loading={editAttrMutation.isPending}>Kaydet</Button></>}>
        <div className="space-y-5">
          <div className="flex gap-4">
            <label className="flex items-center gap-2 cursor-pointer select-none"><input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={editAttrForm.isVariant} onChange={(e) => setEditAttrForm((f) => ({ ...f, isVariant: e.target.checked }))} /><span className="text-sm" style={{ color: 'var(--text)' }}>Varyant Ekseni</span></label>
            <label className="flex items-center gap-2 cursor-pointer select-none"><input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={editAttrForm.isRequired} onChange={(e) => setEditAttrForm((f) => ({ ...f, isRequired: e.target.checked }))} /><span className="text-sm" style={{ color: 'var(--text)' }}>Zorunlu</span></label>
          </div>
          <div><label className="flbl">Sıra</label><IntegerInput value={editAttrForm.sortOrder} onChange={(v) => setEditAttrForm((f) => ({ ...f, sortOrder: v ?? 0 }))} /></div>
          {editAttrMutation.isError && <p className="text-sm" style={{ color: '#ef4444' }}>Hata oluştu. Lütfen tekrar deneyin.</p>}
        </div>
      </Modal>

      {/* Alt Özellik Ekle Modal */}
      <Modal open={addSubAttrOpen} onClose={() => setAddSubAttrOpen(false)} title="Eksen Alt Özelliği Ekle" size="md"
        footer={<><Button variant="secondary" onClick={() => setAddSubAttrOpen(false)}>İptal</Button><Button onClick={() => addSubAttrMutation.mutate()} loading={addSubAttrMutation.isPending} disabled={!subAttrForm.axisAttributeTypeId || !subAttrForm.subAttributeTypeId}>Ekle</Button></>}>
        <div className="space-y-5">
          <div><label className="flbl">Varyant Ekseni *</label><SearchableSelect value={subAttrForm.axisAttributeTypeId} onChange={(v) => setSubAttrForm((f) => ({ ...f, axisAttributeTypeId: v, subAttributeTypeId: null }))} options={axisOptions} placeholder="— Eksen seçin —" hasValue={!!subAttrForm.axisAttributeTypeId} /></div>
          <div>
            <label className="flbl">Alt Özellik Tipi *</label>
            <SearchableSelect value={subAttrForm.subAttributeTypeId} onChange={(v) => setSubAttrForm((f) => ({ ...f, subAttributeTypeId: v }))} options={subAttrTypeOptions} placeholder={subAttrForm.axisAttributeTypeId ? '— Seçin —' : '— Önce eksen seçin —'} hasValue={!!subAttrForm.subAttributeTypeId} />
            {subAttrForm.axisAttributeTypeId && subAttrTypeOptions.length === 0 && <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>Bu eksen için eklenebilecek özellik kalmadı.</p>}
          </div>
          <label className="flex items-center gap-2 cursor-pointer select-none"><input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={subAttrForm.isRequired} onChange={(e) => setSubAttrForm((f) => ({ ...f, isRequired: e.target.checked }))} /><span className="text-sm" style={{ color: 'var(--text)' }}>Zorunlu</span></label>
          <div><label className="flbl">Sıra</label><IntegerInput value={subAttrForm.sortOrder} onChange={(v) => setSubAttrForm((f) => ({ ...f, sortOrder: v ?? 0 }))} /></div>
          {addSubAttrMutation.isError && <p className="text-sm" style={{ color: '#ef4444' }}>{(addSubAttrMutation.error as any)?.response?.data?.error ?? 'Hata oluştu. Lütfen tekrar deneyin.'}</p>}
        </div>
      </Modal>

      {/* Alt Özellik Düzenle Modal */}
      <Modal open={editSubAttrOpen} onClose={() => setEditSubAttrOpen(false)}
        title={`Alt Özellik Düzenle — ${editSubAttrTarget ? getI18nName(editSubAttrTarget.subAttributeTypeNameI18n, editSubAttrTarget.subAttributeTypeCode) : ''}`}
        size="sm"
        footer={<><Button variant="secondary" onClick={() => setEditSubAttrOpen(false)}>İptal</Button><Button onClick={() => editSubAttrMutation.mutate()} loading={editSubAttrMutation.isPending}>Kaydet</Button></>}>
        <div className="space-y-5">
          <label className="flex items-center gap-2 cursor-pointer select-none"><input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={editSubAttrForm.isRequired} onChange={(e) => setEditSubAttrForm((f) => ({ ...f, isRequired: e.target.checked }))} /><span className="text-sm" style={{ color: 'var(--text)' }}>Zorunlu</span></label>
          <div><label className="flbl">Sıra</label><IntegerInput value={editSubAttrForm.sortOrder} onChange={(v) => setEditSubAttrForm((f) => ({ ...f, sortOrder: v ?? 0 }))} /></div>
          {editSubAttrMutation.isError && <p className="text-sm" style={{ color: '#ef4444' }}>Hata oluştu. Lütfen tekrar deneyin.</p>}
        </div>
      </Modal>

      {/* Özellik Sil Onay */}
      <ConfirmDialog
        open={!!deleteAttrTarget}
        title="Özelliği Sil"
        message={`"${deleteAttrTarget ? getAttrTypeName(deleteAttrTarget) : ''}" özelliği bu gruptan kaldırılacak. Emin misiniz?`}
        onConfirm={() => deleteAttrTarget && deleteAttrMutation.mutate(deleteAttrTarget.id)}
        onCancel={() => setDeleteAttrTarget(null)}
        loading={deleteAttrMutation.isPending}
      />

      {/* Alt Özellik Sil Onay */}
      <ConfirmDialog
        open={!!deleteSubAttrTarget}
        title="Alt Özelliği Sil"
        message={`"${deleteSubAttrTarget ? getI18nName(deleteSubAttrTarget.subAttributeTypeNameI18n, deleteSubAttrTarget.subAttributeTypeCode) : ''}" alt özelliği kaldırılacak. Emin misiniz?`}
        onConfirm={() => deleteSubAttrTarget && removeSubAttrMutation.mutate(deleteSubAttrTarget.id)}
        onCancel={() => setDeleteSubAttrTarget(null)}
        loading={removeSubAttrMutation.isPending}
      />

      {/* Grup Sil Onay */}
      <ConfirmDialog
        open={deleteGroupOpen}
        title="Ürün Grubunu Sil"
        message={`"${group.nameI18n['tr'] ?? group.code}" grubu kalıcı olarak silinecek. Bu işlem geri alınamaz. Devam etmek istiyor musunuz?`}
        onConfirm={() => deleteGroupMutation.mutate()}
        onCancel={() => setDeleteGroupOpen(false)}
        loading={deleteGroupMutation.isPending}
      />
    </div>
  )
}

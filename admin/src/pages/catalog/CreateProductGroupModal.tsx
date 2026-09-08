import { useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Copy } from 'lucide-react'
import { toSnakeCase } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { IntegerInput } from '@/components/ui/IntegerInput'
import { I18nField } from '@/components/ui/I18nField'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { useLanguages } from '@/hooks/useLanguages'
import { FL } from '@/lib/field-labels'
import { buildI18nValues } from '@/lib/i18n-helper'
import type { ProductGroup, ProductGroupAttribute } from './ProductGroupsPage'

export interface CreatedProductGroup {
  id: string
  nameI18n: Record<string, string>
}

function getName(pg: ProductGroup): string {
  return pg.nameI18n['tr'] ?? Object.values(pg.nameI18n)[0] ?? '—'
}

// Mounted only while open: every opening starts a fresh form, in either entry point.
export function CreateProductGroupModal({ initialName = '', notice, onClose, onCreated }: {
  initialName?: string
  notice?: string
  onClose: () => void
  onCreated: (group: CreatedProductGroup) => void
}) {
  const queryClient = useQueryClient()
  const { data: languages = [], isLoading: langsLoading } = useLanguages()
  const [form, setForm] = useState<{
    nameI18n: Record<string, string>
    sortOrder: number
    copyFromGroupId: string | null
  }>({ nameI18n: initialName ? { tr: initialName } : {}, sortOrder: 0, copyFromGroupId: null })

  // Kopya kaynağı listesi: aktif/pasif tüm gruplar (liste filtresi "Aktif" olsa da pasif gruptan kopyalanabilsin)
  const { data: allGroups = [] } = useQuery<ProductGroup[]>({
    queryKey: ['product-groups', false],
    queryFn: async () => {
      const { data } = await api.get('/catalog/product-groups?activeOnly=false')
      return data.data
    },
  })

  const mutation = useMutation({
    mutationFn: async (): Promise<CreatedProductGroup> => {
      const { data } = await api.post('/catalog/product-groups', {
        nameI18n: form.nameI18n,
        sortOrder: form.sortOrder,
        copyAttributesFromGroupId: form.copyFromGroupId,
      })
      return { id: data.data.id as string, nameI18n: { ...form.nameI18n } }
    },
    onSuccess: (group) => {
      queryClient.invalidateQueries({ queryKey: ['product-groups'] })
      queryClient.invalidateQueries({ queryKey: ['mapping-overview'] })
      onCreated(group)
    },
  })

  const sourceLang = languages.find((l) => l.isDefault)?.code ?? languages[0]?.code ?? 'tr'
  const previewCode = toSnakeCase(form.nameI18n['tr'] ?? form.nameI18n[sourceLang] ?? '')

  const i18nValues = useMemo(
    () => buildI18nValues(form.nameI18n, languages),
    [languages, form.nameI18n],
  )

  const i18nFields = useMemo(
    () => [{ key: 'name', labels: FL.name, required: true }],
    [],
  )

  const copySourceOptions = useMemo(
    () =>
      [...allGroups]
        .sort((a, b) => getName(a).localeCompare(getName(b), 'tr'))
        .map((g) => ({ value: g.id, label: g.isActive ? getName(g) : `${getName(g)} (pasif)` })),
    [allGroups],
  )
  const copySource = useMemo(
    () => allGroups.find((g) => g.id === form.copyFromGroupId) ?? null,
    [allGroups, form.copyFromGroupId],
  )
  const attrLabel = (a: ProductGroupAttribute) =>
    a.attributeTypeNameI18n['tr'] ?? Object.values(a.attributeTypeNameI18n)[0] ?? a.attributeTypeCode

  return (
      <Modal
        open
        onClose={() => { if (!mutation.isPending) onClose() }}
        title="Yeni Ürün Grubu"
        size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={onClose} disabled={mutation.isPending}>İptal</Button>
            <Button
              onClick={() => mutation.mutate()}
              loading={mutation.isPending}
              disabled={!previewCode || langsLoading}
            >
              Kaydet
            </Button>
          </>
        }
      >
        <div className="space-y-5">
          {notice && <p className="text-sm" style={{ color: 'var(--text-m)' }}>{notice}</p>}
          {langsLoading && <p className="text-sm">Diller yükleniyor…</p>}
          <div>
            <label className="flbl">Sıra</label>
            <IntegerInput
              value={form.sortOrder}
              onChange={(v) => setForm((f) => ({ ...f, sortOrder: v ?? 0 }))}
            />
          </div>

          {languages.length > 0 && (
            <div>
              <label className="flbl mb-2">Ad</label>
              <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
                <I18nField
                  sourceLang={sourceLang}
                  languages={languages}
                  fields={i18nFields}
                  values={i18nValues}
                  onChange={(lang, _key, val) =>
                    setForm((f) => ({ ...f, nameI18n: { ...f.nameI18n, [lang]: val } }))
                  }
                />
              </div>
            </div>
          )}

          <div>
            <label className="flbl mb-2">Özellikleri Kopyala <span style={{ color: 'var(--text-s)' }}>(isteğe bağlı)</span></label>
            <SearchableSelect
              value={form.copyFromGroupId}
              onChange={(v) => setForm((f) => ({ ...f, copyFromGroupId: v }))}
              options={copySourceOptions}
              placeholder="Kaynak ürün grubu seçin…"
              clearable
              portal
            />
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              Seçilen grubun özellik şablonu (varyant ekseni, ana eksen, zorunluluk, sıra, varsayılan değer ve eksen alt
              özellikleri) yeni gruba kopyalanır. Kayıt sonrası detay sayfasından düzenlenebilir.
            </p>
            {copySource && (
              <div
                className="mt-2 rounded-xl px-3 py-2"
                style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}
                data-testid="copy-preview"
              >
                <div className="flex items-center gap-2 text-xs font-medium" style={{ color: 'var(--text)' }}>
                  <Copy size={12} style={{ color: 'var(--brand)' }} />
                  {copySource.attributes.length} özellik
                  {' · '}{copySource.attributes.filter((a) => a.isVariant).length} varyant ekseni
                  {' · '}{(copySource.axisSubAttributes ?? []).length} eksen alt özelliği kopyalanacak
                </div>
                {copySource.attributes.length > 0 ? (
                  <div className="flex flex-wrap gap-1 mt-2">
                    {[...copySource.attributes]
                      .sort((a, b) => a.sortOrder - b.sortOrder)
                      .map((a) => (
                        <span
                          key={a.id}
                          className="text-xs px-2 py-0.5 rounded-md"
                          style={{
                            background: a.isVariant ? 'var(--brand-bg)' : 'var(--surface)',
                            border: `1px solid ${a.isVariant ? 'var(--brand-b)' : 'var(--border)'}`,
                            color: 'var(--text-m)',
                          }}
                          title={a.isVariant ? (a.isPrimaryAxis ? 'Varyant ekseni (ana)' : 'Varyant ekseni') : a.isRequired ? 'Zorunlu' : undefined}
                        >
                          {a.isPrimaryAxis ? '★ ' : ''}{attrLabel(a)}{a.isRequired && !a.isVariant ? ' *' : ''}
                        </span>
                      ))}
                  </div>
                ) : (
                  <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>Kaynak grubun özelliği yok; boş şablonla oluşturulur.</p>
                )}
              </div>
            )}
          </div>

          <div>
            <label className="flbl">Otomatik Kod</label>
            <div
              className="flex items-center gap-2 px-3 py-2 rounded-xl"
              style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}
            >
              <code className="text-sm font-mono" style={{ color: previewCode ? 'var(--brand)' : 'var(--text-s)' }}>
                {previewCode || '—'}
              </code>
            </div>
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              Türkçe addan otomatik üretilir. Kayıt sonrası değiştirilemez.
            </p>
          </div>

          {mutation.isError && (
            <p className="text-sm" style={{ color: '#ef4444' }}>
              {(mutation.error as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Grup oluşturulamadı. Lütfen tekrar deneyin.'}
            </p>
          )}
        </div>
      </Modal>
  )
}

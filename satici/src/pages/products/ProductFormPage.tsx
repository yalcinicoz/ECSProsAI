import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { pickName } from '@/lib/i18n'
import { Button } from '@/components/ui/Button'

/*
 * Yeni ürün / revizyon formu (2026-08-11) — partner API POST /products sözleşmesinin BİREBİR
 * panel karşılığı: grup seçilir, eksen/özellikler grubun ŞEMASINDAN kurulur, varyantlar satır
 * satır girilir, görseller URL ile eklenir. Gönderim aynı Kapı-1 doğrulamasından ve onay
 * akışından geçer — panel ile API arasında kural farkı yoktur.
 * Düzenleme modu (/products/:code/edit): kod + grup kilitli; ad ve varyantlar canlı üründen
 * ön-doldurulur (eksen değerleri ve özellikler şemadan yeniden seçilir — onaylı revizyon
 * canlı ürünü GÜNCELLER).
 */

interface GroupOpt {
  code: string
  name: Record<string, string>
}
interface SchemaAttr {
  code: string
  name: Record<string, string>
  required: boolean
  primaryAxis: boolean
  allowedValues: { value: string; name: Record<string, string>; hexCode: string | null }[]
}
interface GroupSchema {
  code: string
  name: Record<string, string>
  variantAxes: SchemaAttr[]
  attributes: SchemaAttr[]
}
interface VariantRow {
  sku: string
  barcode: string
  stock: string
  price: string
  axisValues: Record<string, string>
}
interface DetailDto {
  supplierProductCode: string
  product: {
    name: Record<string, string>
    groupCode: string
    variants: { sku: string; barcode: string | null; basePrice: number; axes: { typeName: Record<string, string>; valueName: Record<string, string> }[] }[]
  } | null
}

const bosVaryant = (): VariantRow => ({ sku: '', barcode: '', stock: '', price: '', axisValues: {} })

export function ProductFormPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { code: editCode } = useParams()
  const editMode = !!editCode

  const [groupCode, setGroupCode] = useState('')
  const [supplierProductCode, setSupplierProductCode] = useState('')
  const [nameTr, setNameTr] = useState('')
  const [descTr, setDescTr] = useState('')
  const [attrValues, setAttrValues] = useState<Record<string, string>>({})
  const [variants, setVariants] = useState<VariantRow[]>([bosVaryant()])
  const [images, setImages] = useState<string[]>([''])
  const [errors, setErrors] = useState<{ field: string; message: string }[]>([])

  const { data: groups = [] } = useQuery<GroupOpt[]>({
    queryKey: ['supplier-groups'],
    queryFn: async () => (await api.get('/supplier/catalog/groups')).data.data ?? [],
  })
  const { data: schema } = useQuery<GroupSchema>({
    queryKey: ['supplier-group-schema', groupCode],
    queryFn: async () => (await api.get(`/supplier/catalog/groups/${encodeURIComponent(groupCode)}`)).data.data,
    enabled: !!groupCode,
  })

  // Düzenleme: canlı üründen ön-doldur (eksen değer adları şema havuzuyla ada göre eşlenir)
  const { data: detail } = useQuery<DetailDto>({
    queryKey: ['supplier-product-detail', editCode],
    queryFn: async () => (await api.get(`/supplier/products/${encodeURIComponent(editCode!)}`)).data.data,
    enabled: editMode,
  })
  useEffect(() => {
    if (!detail?.product) return
    setSupplierProductCode(detail.supplierProductCode)
    setGroupCode(detail.product.groupCode)
    setNameTr(pickName(detail.product.name))
  }, [detail])
  useEffect(() => {
    if (!detail?.product || !schema) return
    setVariants(detail.product.variants.map(v => {
      const axisValues: Record<string, string> = {}
      for (const eksen of schema.variantAxes) {
        const eksenAdi = pickName(eksen.name)
        const vAxis = v.axes.find(a => pickName(a.typeName) === eksenAdi)
        if (!vAxis) continue
        const havuz = eksen.allowedValues.find(x => pickName(x.name) === pickName(vAxis.valueName))
        if (havuz) axisValues[eksen.code] = havuz.value
      }
      return { sku: v.sku, barcode: v.barcode ?? '', stock: '', price: String(v.basePrice), axisValues }
    }))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [detail, schema])

  const submit = useMutation({
    mutationFn: async () => {
      const body = {
        supplierProductCode: supplierProductCode.trim() || null,
        group: groupCode,
        name: nameTr.trim() ? { tr: nameTr.trim() } : null,
        description: descTr.trim() ? { tr: descTr.trim() } : null,
        attributes: Object.fromEntries(Object.entries(attrValues).filter(([, v]) => v !== '')),
        variants: variants
          .filter(v => v.sku.trim() || v.price.trim())
          .map(v => ({
            axisValues: v.axisValues,
            sku: v.sku.trim() || null,
            barcode: v.barcode.trim() || null,
            stock: v.stock.trim() === '' ? null : parseInt(v.stock),
            price: v.price.trim() === '' ? null : { amount: parseFloat(v.price), currency: 'TRY' },
          })),
        images: images.filter(u => u.trim()).map(u => ({ url: u.trim(), main: false })),
      }
      const { data } = await api.post('/supplier/products', body)
      return data
    },
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['supplier-products'] })
      navigate(`/products/${encodeURIComponent(data.data.supplierProductCode)}`)
    },
    onError: (e: unknown) => {
      const yanit = (e as { response?: { data?: { errors?: { field: string; message: string }[]; error?: string } } })?.response?.data
      setErrors(yanit?.errors ?? (yanit?.error ? [{ field: '-', message: yanit.error }] : [{ field: '-', message: 'Gönderilemedi.' }]))
    },
  })

  const setVaryant = (i: number, patch: Partial<VariantRow>) =>
    setVariants(prev => prev.map((v, j) => (j === i ? { ...v, ...patch } : v)))

  return (
    <div className="max-w-4xl">
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-lg font-bold">{editMode ? 'Ürünü Düzenle (revizyon)' : 'Yeni Ürün'}</h1>
        <Button variant="secondary" onClick={() => navigate(-1)}>Geri</Button>
      </div>
      <p className="text-xs mb-4 opacity-70">
        Gönderim onaya düşer; onaylanınca {editMode ? 'canlı ürün güncellenir' : 'ürün satışa hazır olur'}.
        Fiyat ve stok onay beklemeden ürün detay sayfasından da güncellenebilir.
      </p>

      <div className="card p-5 mb-4 grid gap-4 sm:grid-cols-2">
        <div>
          <label className="block text-xs mb-1">Ürün Grubu *</label>
          <select className="inp w-full" value={groupCode} disabled={editMode}
            onChange={e => { setGroupCode(e.target.value); setAttrValues({}); setVariants([bosVaryant()]) }}>
            <option value="">Seçin…</option>
            {groups.map(g => <option key={g.code} value={g.code}>{pickName(g.name)}</option>)}
          </select>
        </div>
        <div>
          <label className="block text-xs mb-1">Ürün Kodunuz {editMode ? '' : '(boşsa üretilir)'}</label>
          <input className="inp w-full" value={supplierProductCode} disabled={editMode}
            onChange={e => setSupplierProductCode(e.target.value)} placeholder="STOK-001" />
        </div>
        <div className="sm:col-span-2">
          <label className="block text-xs mb-1">Ürün Adı *</label>
          <input className="inp w-full" value={nameTr} onChange={e => setNameTr(e.target.value)} />
        </div>
        <div className="sm:col-span-2">
          <label className="block text-xs mb-1">Açıklama</label>
          <textarea className="inp w-full min-h-24" value={descTr} onChange={e => setDescTr(e.target.value)} />
        </div>
      </div>

      {schema && schema.attributes.length > 0 && (
        <div className="card p-5 mb-4">
          <div className="text-xs font-semibold uppercase tracking-wider mb-3 opacity-70">Ürün Özellikleri</div>
          <div className="grid gap-3 sm:grid-cols-2">
            {schema.attributes.map(a => (
              <div key={a.code}>
                <label className="block text-xs mb-1">{pickName(a.name)}{a.required && ' *'}</label>
                <select className="inp w-full" value={attrValues[a.code] ?? ''}
                  onChange={e => setAttrValues(prev => ({ ...prev, [a.code]: e.target.value }))}>
                  <option value="">Seçin…</option>
                  {a.allowedValues.map(v => <option key={v.value} value={v.value}>{pickName(v.name)}</option>)}
                </select>
              </div>
            ))}
          </div>
        </div>
      )}

      {schema && (
        <div className="card p-5 mb-4">
          <div className="flex items-center justify-between mb-3">
            <div className="text-xs font-semibold uppercase tracking-wider opacity-70">
              Varyantlar {schema.variantAxes.length > 0 && `(eksenler: ${schema.variantAxes.map(a => pickName(a.name)).join(', ')})`}
            </div>
            <Button size="sm" variant="secondary" onClick={() => setVariants(p => [...p, bosVaryant()])}>Satır Ekle</Button>
          </div>
          <div className="tbl-wrap overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs opacity-70">
                  {schema.variantAxes.map(a => <th key={a.code} className="py-1.5 pr-3">{pickName(a.name)}</th>)}
                  <th className="py-1.5 pr-3">SKU</th>
                  <th className="py-1.5 pr-3">Barkod</th>
                  <th className="py-1.5 pr-3">Stok</th>
                  <th className="py-1.5 pr-3">Fiyat (TL)</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {variants.map((v, i) => (
                  <tr key={i}>
                    {schema.variantAxes.map(a => (
                      <td key={a.code} className="py-1 pr-3">
                        <select className="inp !w-36" value={v.axisValues[a.code] ?? ''}
                          onChange={e => setVaryant(i, { axisValues: { ...v.axisValues, [a.code]: e.target.value } })}>
                          <option value="">—</option>
                          {a.allowedValues.map(x => <option key={x.value} value={x.value}>{pickName(x.name)}</option>)}
                        </select>
                      </td>
                    ))}
                    <td className="py-1 pr-3"><input className="inp !w-36" value={v.sku} onChange={e => setVaryant(i, { sku: e.target.value })} /></td>
                    <td className="py-1 pr-3"><input className="inp !w-36" value={v.barcode} onChange={e => setVaryant(i, { barcode: e.target.value })} /></td>
                    <td className="py-1 pr-3"><input className="inp !w-24 text-right" type="number" min="0" value={v.stock} onChange={e => setVaryant(i, { stock: e.target.value })} /></td>
                    <td className="py-1 pr-3"><input className="inp !w-28 text-right" type="number" step="0.01" min="0" value={v.price} onChange={e => setVaryant(i, { price: e.target.value })} /></td>
                    <td className="py-1">
                      {variants.length > 1 && (
                        <button className="text-xs text-red-600" onClick={() => setVariants(p => p.filter((_, j) => j !== i))}>Sil</button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <div className="card p-5 mb-4">
        <div className="flex items-center justify-between mb-3">
          <div className="text-xs font-semibold uppercase tracking-wider opacity-70">Görseller (URL)</div>
          <Button size="sm" variant="secondary" onClick={() => setImages(p => [...p, ''])}>Görsel Ekle</Button>
        </div>
        {images.map((u, i) => (
          <div key={i} className="flex items-center gap-2 mb-2">
            <input className="inp flex-1" placeholder="https://…" value={u}
              onChange={e => setImages(p => p.map((x, j) => (j === i ? e.target.value : x)))} />
            {images.length > 1 && (
              <button className="text-xs text-red-600" onClick={() => setImages(p => p.filter((_, j) => j !== i))}>Sil</button>
            )}
          </div>
        ))}
      </div>

      {errors.length > 0 && (
        <div className="card p-4 mb-4 border-red-300">
          <div className="text-sm font-semibold text-red-600 mb-1">Gönderim doğrulamadan geçemedi:</div>
          <ul className="text-xs text-red-600 list-disc pl-4">
            {errors.map((e, i) => <li key={i}>{e.field !== '-' && <code>{e.field}: </code>}{e.message}</li>)}
          </ul>
        </div>
      )}

      <div className="flex items-center gap-3">
        <Button onClick={() => { setErrors([]); submit.mutate() }} disabled={!groupCode || submit.isPending}>
          {submit.isPending ? 'Gönderiliyor…' : 'Onaya Gönder'}
        </Button>
      </div>
    </div>
  )
}

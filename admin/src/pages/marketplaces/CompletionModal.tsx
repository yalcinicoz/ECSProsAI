import { useState } from 'react'
import { useQuery, useMutation } from '@tanstack/react-query'
import { Check } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { MpCategoryPicker, type MpCategory } from './MappingPage'

// ── Types (API DTO karşılıkları) ─────────────────────────────────────────────

interface CompletionAttr {
  externalId: string
  name: string
  allowCustom: boolean
  valueMode: string
  reasonCode: string
  values: { externalId: string | null; code: string | null; value: string }[]
  currentValueExternalId: string | null
  currentValueText: string | null
}

interface CompletionView {
  productId: string
  productCode: string
  productName: string | null
  groupName: string
  status: string
  reasonLabels: string[]
  resolvedCategoryExternalId: string | null
  resolvedCategoryPath: string | null
  needsCategory: boolean
  mappingKind: string
  poolCandidates: { externalId: string; name: string; path: string }[]
  suggestions: { externalId: string; name: string; path: string; score: number }[]
  missingAttributes: CompletionAttr[]
}

/**
 * Tamamlama ekranı (F3 §3): eksik üründe kategori ataması (istisna olarak yazılır, genel
 * eşlemeye dokunmaz) + zorunlu özellik doldurma (ürün-özel pazaryeri değeri — kendi kataloğa
 * yazılmaz). productIds birden fazlaysa TOPLU mod: form ilk ürüne göre kurulur, girilenler
 * seçili ürünlerin tümüne uygulanır.
 */
interface CompletionModalProps {
  open: boolean
  onClose: () => void
  marketplace: string
  productIds: string[]
  onSaved: () => void
}

export function CompletionModal(props: CompletionModalProps) {
  const resetKey = props.open ? `${props.marketplace}:${props.productIds.join(',')}` : 'closed'
  return <CompletionModalContent key={resetKey} {...props} />
}

function CompletionModalContent({
  open, onClose, marketplace, productIds, onSaved,
}: CompletionModalProps) {
  const bulk = productIds.length > 1
  const [category, setCategory] = useState<(MpCategory & { source: string }) | null>(null)
  const [values, setValues] = useState<Record<string, { externalId: string | null; text: string | null }>>({})
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null)

  const { data: view, isLoading, refetch } = useQuery<CompletionView>({
    queryKey: ['mp-completion', marketplace, productIds[0]],
    queryFn: async () =>
      (await api.get(`/marketplaces/mapping/completion?marketplace=${marketplace}&productId=${productIds[0]}`)).data.data,
    enabled: open && productIds.length > 0,
  })

  const save = useMutation({
    mutationFn: async () => {
      const valueItems = Object.entries(values)
        .map(([attrId, v]) => {
          const attr = view?.missingAttributes.find((a) => a.externalId === attrId)
          return {
            mpAttributeExternalId: attrId,
            mpAttributeName: attr?.name ?? attrId,
            valueExternalId: v.externalId,
            valueText: v.text,
          }
        })
        .filter((v) => v.valueExternalId || v.valueText)
      const body = {
        marketplace,
        productIds,
        category: category
          ? { externalId: category.externalId, name: category.name, path: category.path, source: category.source }
          : null,
        mpCategoryExternalId: view?.resolvedCategoryExternalId ?? category?.externalId ?? null,
        values: valueItems.length > 0 ? valueItems : null,
      }
      return (await api.put('/marketplaces/mapping/completion', body)).data.data
    },
    onSuccess: (result) => {
      setMsg({
        ok: true,
        text: `Kaydedildi — ${result.ready} ürün hazır, ${result.missing} üründe hâlâ eksik var.`,
      })
      setCategory(null)
      setValues({})
      refetch()
      onSaved()
    },
    onError: (err: unknown) => {
      const e = err as { response?: { data?: { error?: string } } }
      setMsg({ ok: false, text: e.response?.data?.error ?? 'Kaydedilemedi.' })
    },
  })

  const title = bulk
    ? `Toplu Tamamla — ${productIds.length} ürün`
    : view ? `Tamamla: ${view.productName ?? view.productCode}` : 'Tamamla'

  const nothingToSave = !category && Object.values(values).every((v) => !v.externalId && !v.text)

  return (
    <Modal open={open} onClose={onClose} title={title} size="lg" footer={null}>
      {isLoading || !view ? (
        <p className="text-sm py-8 text-center" style={{ color: 'var(--text-s)' }}>Yükleniyor…</p>
      ) : (
        <div>
          <div className="flex items-center gap-2 flex-wrap mb-3 text-xs" style={{ color: 'var(--text-s)' }}>
            <span>{view.productCode} · Grup: <b style={{ color: 'var(--text-m)' }}>{view.groupName}</b></span>
            {view.resolvedCategoryPath ? (
              <span>· Çözülen kategori: <b style={{ color: 'var(--text-m)' }}>{view.resolvedCategoryPath}</b></span>
            ) : (
              <Badge variant="warning">Kategori çözülemedi</Badge>
            )}
          </div>

          <div className="min-h-[20px] mb-3">
            {view.reasonLabels.length > 0 ? (
              <div className="flex items-center gap-1.5 flex-wrap">
                {view.reasonLabels.map((l, i) => (
                  <span key={i} className="badge bg-amber-50 text-amber-700">{l}</span>
                ))}
              </div>
            ) : (
              <Badge variant="success">Bu ürün hazır ✓</Badge>
            )}
          </div>

          {bulk ? (
            <p className="text-xs px-3 py-2 rounded-lg mb-3"
              style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>
              Toplu mod: form ilk seçili ürüne göre oluşturuldu; girilenler {productIds.length} ürünün
              tümüne uygulanır (aynı gruptaki ürünler için tasarlandı).
            </p>
          ) : null}

          {/* 1) Kategori adımı */}
          {view.needsCategory || bulk ? (
            <div className="mb-4">
              <p className="text-xs font-semibold mb-1.5" style={{ color: 'var(--text-s)' }}>
                1) KATEGORİ {view.needsCategory ? '' : '(opsiyonel — mevcut çözümü istisnayla değiştirir)'}
              </p>
              <div className="mb-2">
                {view.mappingKind === 'pool' && view.poolCandidates.length > 0 ? (
                  <div className="flex flex-col gap-1 mb-2">
                    {view.poolCandidates.map((p) => (
                      <label key={p.externalId} className="flex items-center gap-2 text-sm cursor-pointer" style={{ color: 'var(--text)' }}>
                        <input
                          type="radio"
                          checked={category?.externalId === p.externalId}
                          onChange={() => setCategory({ ...p, source: 'pool_assignment' })}
                        />
                        <span title={p.path}>{p.path}</span>
                      </label>
                    ))}
                  </div>
                ) : null}
                {view.suggestions.length > 0 ? (
                  <div className="flex items-center gap-1.5 flex-wrap mb-2">
                    {view.suggestions.map((s) => (
                      <button
                        key={s.externalId}
                        onClick={() => setCategory({ externalId: s.externalId, name: s.name, path: s.path, source: 'manual' })}
                        title={s.path}
                        className="text-[11px] font-medium px-2 py-0.5 rounded-full hover:opacity-75"
                        style={{ background: 'var(--brand-bg)', color: 'var(--brand)', border: '1px solid var(--brand-b)' }}
                      >
                        {s.name} %{s.score}
                      </button>
                    ))}
                  </div>
                ) : null}
                <MpCategoryPicker
                  marketplace={marketplace}
                  value={category}
                  onChange={(c) => setCategory(c ? { ...c, source: 'manual' } : null)}
                  placeholder="Veya farklı bir kategori ara…"
                />
              </div>
            </div>
          ) : null}

          {/* 2) Eksik zorunlu özellikler */}
          {view.missingAttributes.length > 0 ? (
            <div className="mb-4">
              <p className="text-xs font-semibold mb-1.5" style={{ color: 'var(--text-s)' }}>
                {view.needsCategory || bulk ? '2)' : ''} EKSİK ZORUNLU ÖZELLİKLER ({view.resolvedCategoryPath})
              </p>
              <div className="flex flex-col gap-2">
                {view.missingAttributes.map((a) => {
                  const cur = values[a.externalId]
                  const isList = a.values.length > 0
                  return (
                    <div key={a.externalId} className="flex items-center gap-2">
                      <span className="text-sm w-44 truncate shrink-0" title={a.name} style={{ color: 'var(--text)' }}>
                        {a.name}
                        {a.reasonCode === 'value_unmapped' ? (
                          <span className="block text-[10px]" style={{ color: '#b45309' }}>değer eşlemesiz — buradan ürün-özel seçilebilir</span>
                        ) : null}
                      </span>
                      {isList ? (
                        <select
                          className="inp flex-1"
                          value={cur?.externalId ?? a.currentValueExternalId ?? ''}
                          onChange={(e) => {
                            const v = a.values.find((x) => x.externalId === e.target.value)
                            setValues((vals) => ({
                              ...vals,
                              [a.externalId]: { externalId: e.target.value || null, text: v?.value ?? null },
                            }))
                          }}
                        >
                          <option value="">— seç —</option>
                          {a.values.map((v) => (
                            <option key={v.externalId ?? v.value} value={v.externalId ?? ''}>{v.value}</option>
                          ))}
                        </select>
                      ) : (
                        <input
                          className="inp flex-1"
                          placeholder="Serbest metin…"
                          defaultValue={cur?.text ?? a.currentValueText ?? ''}
                          onBlur={(e) =>
                            setValues((vals) => ({
                              ...vals,
                              [a.externalId]: { externalId: null, text: e.target.value.trim() || null },
                            }))
                          }
                        />
                      )}
                    </div>
                  )
                })}
              </div>
              <p className="text-[11px] mt-2" style={{ color: 'var(--text-s)' }}>
                Buraya girilenler kendi kataloğunuza YAZILMAZ — yalnız bu ürünlerin pazaryeri kaydında tutulur.
              </p>
            </div>
          ) : view.resolvedCategoryExternalId ? null : (
            <p className="text-xs mb-4" style={{ color: 'var(--text-s)' }}>
              Özellik denetimi kategori çözüldükten sonra yapılır — önce kategori atayıp kaydedin.
            </p>
          )}

          <div className="flex items-center gap-2 justify-end">
            <span className="text-xs" style={{ color: msg ? (msg.ok ? 'var(--brand)' : '#ef4444') : 'var(--text-s)' }}>
              {msg?.text ?? ''}
            </span>
            <Button size="sm" variant="ghost" onClick={onClose}>Kapat</Button>
            <Button size="sm" onClick={() => save.mutate()} disabled={save.isPending || nothingToSave}>
              <Check size={13} /> Kaydet ve Yeniden Denetle
            </Button>
          </div>
        </div>
      )}
    </Modal>
  )
}

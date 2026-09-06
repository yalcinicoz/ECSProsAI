import { useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Check, ChevronDown, ChevronUp, Database, Plus, Search, Trash2, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { PageSpinner } from '@/components/ui/Spinner'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { StoreLogo } from './MarketplacesPage'
import { pickTr } from './marketplaceOverview'

// ── Types (API DTO karşılıkları) ─────────────────────────────────────────────

interface MappingCondition { attributeTypeCode: string; valueId: string; valueLabel: string }
// EM1: kural = 1..n koşul (VE) → hedef; attributeTypeCode/valueId eski tek-koşul alanları (geriye uyum)
interface MappingRule {
  order: number
  attributeTypeCode?: string | null
  valueId?: string | null
  valueLabel?: string | null
  targetExternalId: string
  targetName: string
  targetPath: string
  conditions?: MappingCondition[] | null
}
const ruleConditions = (r: MappingRule): MappingCondition[] =>
  r.conditions && r.conditions.length > 0
    ? r.conditions
    : r.attributeTypeCode && r.valueId
      ? [{ attributeTypeCode: r.attributeTypeCode, valueId: r.valueId, valueLabel: r.valueLabel ?? '' }]
      : [{ attributeTypeCode: '', valueId: '', valueLabel: '' }]
interface PoolTarget { externalId: string; name: string; path: string }
interface CategoryMapping {
  id: string
  mappingKind: 'direct' | 'rules' | 'pool'
  targetExternalId: string | null
  targetName: string | null
  targetPath: string | null
  rules: MappingRule[]
  pool: PoolTarget[]
  status: string
  statusNote: string | null
}
interface GroupRow {
  productGroupId: string
  code: string
  name: string
  productCount: number
  mapping: CategoryMapping | null
}
interface Overview { groups: GroupRow[]; mappedCount: number; unmappedCount: number; reviewCount: number }
export interface MpCategory { externalId: string; name: string; path: string }
interface Suggestion extends MpCategory { score: number }
interface OwnAttrType { id: string; code?: string; nameI18n: Record<string, string>; values?: { id: string; nameI18n: Record<string, string> }[] }
interface MpAttributeRow {
  externalId: string
  name: string
  isRequired: boolean
  allowCustom: boolean
  isVariantAxis: boolean
  valueMode: string
  valueCount: number
  mappingId: string | null
  strategy: string | null
  attributeTypeId: string | null
  fixedValue: string | null
  status: string | null
  statusNote: string | null
  ownValueCount: number
  mappedValueCount: number
}
interface MappedTarget { externalId: string; name: string; path: string; viaGroups: string[] }
interface ValueRow {
  attributeValueId: string
  label: string
  targetExternalId: string | null
  targetValue: string | null
  status: string
  suggestedExternalId: string | null
  suggestedValue: string | null
  suggestedScore: number
}
interface MpValue { externalId: string | null; code: string | null; value: string }
interface ReviewRow {
  mappingId: string
  mappingType: string
  marketplace: string
  status: string
  title: string
  note: string | null
  mpCategoryExternalId: string | null
  productGroupId: string | null
}

const MP_NAME: Record<string, string> = {
  trendyol: 'Trendyol', hepsiburada: 'Hepsiburada', n11: 'n11',
  amazon: 'Amazon', ciceksepeti: 'Çiçeksepeti', pazarama: 'Pazarama',
}

function errText(err: unknown, fallback: string) {
  const e = err as { response?: { data?: { error?: string } } }
  return e.response?.data?.error ?? fallback
}

// ── Pazaryeri kategori seçici (aramalı) ──────────────────────────────────────

export function MpCategoryPicker({
  marketplace, value, onChange, placeholder = 'Kategori ara…',
}: {
  marketplace: string
  value: MpCategory | null
  onChange: (c: MpCategory | null) => void
  placeholder?: string
}) {
  const [open, setOpen] = useState(false)
  const [q, setQ] = useState('')

  const { data: results = [] } = useQuery<MpCategory[]>({
    queryKey: ['mp-cat-search', marketplace, q],
    queryFn: async () =>
      (await api.get(`/marketplaces/mapping/mp-categories?marketplace=${marketplace}&q=${encodeURIComponent(q)}`)).data.data ?? [],
    enabled: open && q.trim().length >= 2,
    staleTime: 60 * 1000,
  })

  return (
    <div className="relative">
      <div className="min-h-[34px]">
        {value ? (
          <div
            className="flex items-center gap-2 px-2.5 py-1.5 rounded-lg text-sm"
            style={{ border: '1px solid var(--border)', background: 'var(--surface2)', color: 'var(--text)' }}
          >
            <span className="truncate" title={value.path}>{value.path}</span>
            <button className="ml-auto shrink-0 hover:opacity-70" onClick={() => onChange(null)} title="Temizle">
              <X size={13} />
            </button>
          </div>
        ) : (
          <div className="relative">
            <Search size={13} className="absolute left-2.5 top-1/2 -translate-y-1/2" style={{ color: 'var(--text-s)' }} />
            <input
              className="inp w-full"
              style={{ paddingLeft: 28 }}
              placeholder={placeholder}
              value={q}
              onChange={(e) => { setQ(e.target.value); setOpen(true) }}
              onFocus={() => setOpen(true)}
              onBlur={() => setTimeout(() => setOpen(false), 150)}
            />
          </div>
        )}
      </div>
      <div>
        {open && !value && q.trim().length >= 2 ? (
          <div
            className="absolute left-0 right-0 top-full mt-1 z-20 rounded-lg shadow-lg max-h-56 overflow-y-auto"
            style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}
          >
            {results.length === 0 ? (
              <p className="text-xs px-3 py-2" style={{ color: 'var(--text-s)' }}>Sonuç yok.</p>
            ) : (
              results.map((c) => (
                <button
                  key={c.externalId}
                  className="block w-full text-left text-xs px-3 py-2 hover:opacity-70"
                  style={{ color: 'var(--text)' }}
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => { onChange(c); setQ(''); setOpen(false) }}
                >
                  {c.path}
                </button>
              ))
            )}
          </div>
        ) : null}
      </div>
    </div>
  )
}

// ── Kategori eşleme editörü ──────────────────────────────────────────────────

function CategoryEditor({
  marketplace, group, ownTypes, onSaved,
}: {
  marketplace: string
  group: GroupRow
  ownTypes: OwnAttrType[]
  onSaved: () => void
}) {
  const m = group.mapping
  const [kind, setKind] = useState<'direct' | 'rules' | 'pool'>(m?.mappingKind ?? 'direct')
  const [target, setTarget] = useState<MpCategory | null>(
    m?.targetExternalId ? { externalId: m.targetExternalId, name: m.targetName ?? '', path: m.targetPath ?? '' } : null,
  )
  const [rules, setRules] = useState<MappingRule[]>((m?.rules ?? []).map((r) => ({ ...r, conditions: ruleConditions(r) })))
  const [pool, setPool] = useState<PoolTarget[]>(m?.pool ?? [])
  const [poolAdd, setPoolAdd] = useState<MpCategory | null>(null)
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null)

  const { data: suggestions = [] } = useQuery<Suggestion[]>({
    queryKey: ['mp-cat-suggest', marketplace, group.productGroupId],
    queryFn: async () =>
      (await api.get(`/marketplaces/mapping/suggest-categories?marketplace=${marketplace}&productGroupId=${group.productGroupId}`)).data.data ?? [],
    staleTime: 5 * 60 * 1000,
  })

  const save = useMutation({
    mutationFn: async () => {
      const body = {
        marketplace,
        productGroupId: group.productGroupId,
        mappingKind: kind,
        targetExternalId: target?.externalId ?? null,
        targetName: target?.name ?? null,
        targetPath: target?.path ?? null,
        rules: kind === 'rules' ? rules.map((r, i) => {
          const conds = (r.conditions ?? []).filter((c) => c.attributeTypeCode && c.valueId)
          return { order: i, conditions: conds, attributeTypeCode: conds[0]?.attributeTypeCode ?? null, valueId: conds[0]?.valueId ?? null, valueLabel: conds[0]?.valueLabel ?? null,
            targetExternalId: r.targetExternalId, targetName: r.targetName, targetPath: r.targetPath }
        }) : null,
        pool: kind === 'pool' ? pool : null,
      }
      await api.put('/marketplaces/mapping/category', body)
    },
    onSuccess: () => { setMsg({ ok: true, text: 'Eşleme kaydedildi.' }); onSaved() },
    onError: (err) => setMsg({ ok: false, text: errText(err, 'Kaydedilemedi.') }),
  })

  const remove = useMutation({
    mutationFn: async () => api.delete(`/marketplaces/mapping/category/${m!.id}`),
    onSuccess: () => { setMsg({ ok: true, text: 'Eşleme kaldırıldı.' }); onSaved() },
    onError: (err) => setMsg({ ok: false, text: errText(err, 'Kaldırılamadı.') }),
  })

  function typeValues(code: string) {
    const t = ownTypes.find((x) => x.code === code)
    return t?.values ?? []
  }
  function updateRule(i: number, patch: Partial<MappingRule>) {
    setRules((rs) => rs.map((r, idx) => (idx === i ? { ...r, ...patch } : r)))
  }
  function updateCondition(i: number, ci: number, patch: Partial<MappingCondition>) {
    setRules((rs) => rs.map((r, idx) => idx !== i ? r : {
      ...r, conditions: (r.conditions ?? []).map((c, cidx) => (cidx === ci ? { ...c, ...patch } : c)),
    }))
  }
  function addCondition(i: number) {
    setRules((rs) => rs.map((r, idx) => idx !== i ? r : { ...r, conditions: [...(r.conditions ?? []), { attributeTypeCode: '', valueId: '', valueLabel: '' }] }))
  }
  function removeCondition(i: number, ci: number) {
    setRules((rs) => rs.map((r, idx) => idx !== i ? r : { ...r, conditions: (r.conditions ?? []).filter((_, cidx) => cidx !== ci) }))
  }
  function moveRule(i: number, dir: -1 | 1) {
    setRules((rs) => {
      const next = [...rs]
      const j = i + dir
      if (j < 0 || j >= next.length) return rs
      ;[next[i], next[j]] = [next[j], next[i]]
      return next
    })
  }

  const suggestionChips = (onPick: (s: Suggestion) => void) => (
    <div className="flex items-center gap-1.5 flex-wrap mt-2 min-h-[22px]">
      {suggestions.map((s) => (
        <button
          key={s.externalId}
          onClick={() => onPick(s)}
          title={s.path}
          className="text-[11px] font-medium px-2 py-0.5 rounded-full hover:opacity-75"
          style={{ background: 'var(--brand-bg)', color: 'var(--brand)', border: '1px solid var(--brand-b)' }}
        >
          {s.name} %{s.score}
        </button>
      ))}
    </div>
  )

  return (
    <div className="card p-4">
      <div className="flex items-center gap-2 mb-1">
        <h3 className="text-sm font-bold" style={{ color: 'var(--text)' }}>{group.name}</h3>
        <span className="text-xs" style={{ color: 'var(--text-s)' }}>
          {group.code} · {group.productCount.toLocaleString('tr-TR')} ürün
        </span>
      </div>

      <div className="min-h-[24px] mb-2">
        {m && m.status !== 'active' ? (
          <div
            className="text-xs px-3 py-2 rounded-lg"
            style={m.status === 'broken'
              ? { background: '#fef2f2', color: '#b91c1c', border: '1px solid #fecaca' }
              : { background: '#fffbeb', color: '#b45309', border: '1px solid #fde68a' }}
          >
            {m.status === 'broken' ? '⛔ ' : '⚠ '}{m.statusNote ?? 'Eşleme gözden geçirilmeli.'}
            <span className="ml-1">Düzeltip kaydedin — kayıt eşlemeyi onaylanmış sayar.</span>
          </div>
        ) : null}
      </div>

      <div className="flex items-center gap-4 mb-3">
        {([['direct', 'Birebir'], ['rules', 'Koşullu'], ['pool', 'Havuz']] as const).map(([k, label]) => (
          <label key={k} className="flex items-center gap-1.5 text-sm cursor-pointer" style={{ color: 'var(--text)' }}>
            <input type="radio" checked={kind === k} onChange={() => setKind(k)} /> {label}
          </label>
        ))}
      </div>

      {kind === 'direct' ? (
        <div>
          <p className="text-xs font-semibold mb-1" style={{ color: 'var(--text-s)' }}>HEDEF KATEGORİ</p>
          <MpCategoryPicker marketplace={marketplace} value={target} onChange={setTarget} />
          {suggestionChips((s) => setTarget({ externalId: s.externalId, name: s.name, path: s.path }))}
        </div>
      ) : kind === 'rules' ? (
        <div>
          <p className="text-xs font-semibold mb-1" style={{ color: 'var(--text-s)' }}>
            KURALLAR — ilk eşleşen kazanır; bir kuralın TÜM koşulları sağlanmalı (ör. cinsiyet = Kadın VE yaş grubu = Çocuk → Kız Çocuk Kot Ceket)
          </p>
          {rules.map((r, i) => (
            <div key={i} className="rounded-lg p-2.5 mb-2" style={{ border: '1px solid var(--border)' }}>
              <div className="flex items-start gap-2 mb-2 flex-wrap">
                <div className="flex flex-col gap-1.5">
                  {(r.conditions ?? []).map((c, ci) => (
                    <div key={ci} className="flex items-center gap-2">
                      {ci > 0 && <span className="text-[10px] font-semibold w-6" style={{ color: 'var(--brand)' }}>VE</span>}
                      {ci === 0 && <span className="w-6" />}
                      <select
                        className="inp"
                        style={{ width: 160 }}
                        value={c.attributeTypeCode}
                        onChange={(e) => updateCondition(i, ci, { attributeTypeCode: e.target.value, valueId: '', valueLabel: '' })}
                      >
                        <option value="">Özellik seç…</option>
                        {ownTypes.map((t) => (
                          <option key={t.id} value={t.code ?? t.id}>{pickTr(t.nameI18n, t.code ?? '')}</option>
                        ))}
                      </select>
                      <span className="text-xs" style={{ color: 'var(--text-s)' }}>=</span>
                      <select
                        className="inp"
                        style={{ width: 160 }}
                        value={c.valueId}
                        onChange={(e) => {
                          const v = typeValues(c.attributeTypeCode).find((x) => x.id === e.target.value)
                          updateCondition(i, ci, { valueId: e.target.value, valueLabel: v ? pickTr(v.nameI18n) : '' })
                        }}
                      >
                        <option value="">Değer seç…</option>
                        {typeValues(c.attributeTypeCode).map((v) => (
                          <option key={v.id} value={v.id}>{pickTr(v.nameI18n)}</option>
                        ))}
                      </select>
                      {(r.conditions ?? []).length > 1 && (
                        <button onClick={() => removeCondition(i, ci)} className="p-1 hover:opacity-70" title="Koşulu kaldır"><X size={13} /></button>
                      )}
                    </div>
                  ))}
                  <button onClick={() => addCondition(i)} className="text-[11px] self-start ml-8 underline" style={{ color: 'var(--brand)' }}>+ koşul ekle (VE)</button>
                </div>
                <div className="ml-auto flex items-center gap-1">
                  <button onClick={() => moveRule(i, -1)} className="p-1 hover:opacity-70" title="Yukarı"><ChevronUp size={14} /></button>
                  <button onClick={() => moveRule(i, 1)} className="p-1 hover:opacity-70" title="Aşağı"><ChevronDown size={14} /></button>
                  <button onClick={() => setRules((rs) => rs.filter((_, idx) => idx !== i))} className="p-1 hover:opacity-70" title="Kuralı sil" style={{ color: '#ef4444' }}>
                    <Trash2 size={14} />
                  </button>
                </div>
              </div>
              <MpCategoryPicker
                marketplace={marketplace}
                value={r.targetExternalId ? { externalId: r.targetExternalId, name: r.targetName, path: r.targetPath } : null}
                onChange={(c) => updateRule(i, {
                  targetExternalId: c?.externalId ?? '', targetName: c?.name ?? '', targetPath: c?.path ?? '',
                })}
                placeholder="Bu kuralın hedef kategorisi…"
              />
            </div>
          ))}
          <Button size="sm" variant="ghost" onClick={() =>
            setRules((rs) => [...rs, { order: rs.length, conditions: [{ attributeTypeCode: '', valueId: '', valueLabel: '' }], targetExternalId: '', targetName: '', targetPath: '' }])}>
            <Plus size={13} /> Kural Ekle
          </Button>
          <div className="mt-3">
            <p className="text-xs font-semibold mb-1" style={{ color: 'var(--text-s)' }}>
              VARSAYILAN HEDEF (hiçbir kural tutmazsa — boş bırakılırsa ürün "eksik" listesine düşer)
            </p>
            <MpCategoryPicker marketplace={marketplace} value={target} onChange={setTarget} />
            {suggestionChips((s) => setTarget({ externalId: s.externalId, name: s.name, path: s.path }))}
          </div>
        </div>
      ) : (
        <div>
          <p className="text-xs font-semibold mb-1" style={{ color: 'var(--text-s)' }}>
            ADAY KATEGORİLER — ürün bazında atama Ürünler ekranındaki tamamlama adımında yapılır
          </p>
          {pool.map((p) => (
            <div key={p.externalId} className="flex items-center gap-2 text-sm px-2.5 py-1.5 rounded-lg mb-1.5"
              style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>
              <span className="truncate" title={p.path}>{p.path}</span>
              <button className="ml-auto shrink-0 hover:opacity-70" style={{ color: '#ef4444' }}
                onClick={() => setPool((ps) => ps.filter((x) => x.externalId !== p.externalId))}>
                <X size={13} />
              </button>
            </div>
          ))}
          <MpCategoryPicker
            marketplace={marketplace}
            value={poolAdd}
            onChange={(c) => {
              if (c && !pool.some((p) => p.externalId === c.externalId))
                setPool((ps) => [...ps, { externalId: c.externalId, name: c.name, path: c.path }])
              setPoolAdd(null)
            }}
            placeholder="Aday kategori ekle…"
          />
          {suggestionChips((s) => {
            if (!pool.some((p) => p.externalId === s.externalId))
              setPool((ps) => [...ps, { externalId: s.externalId, name: s.name, path: s.path }])
          })}
        </div>
      )}

      <div className="flex items-center gap-2 mt-4">
        {m ? (
          <Button size="sm" variant="danger" onClick={() => remove.mutate()} disabled={remove.isPending}>
            Eşlemeyi Kaldır
          </Button>
        ) : null}
        <div className="ml-auto flex items-center gap-2">
          <span className="text-xs" style={{ color: msg ? (msg.ok ? 'var(--brand)' : '#ef4444') : 'var(--text-s)' }}>
            {msg?.text ?? ''}
          </span>
          <Button size="sm" onClick={() => save.mutate()} disabled={save.isPending}>
            <Check size={13} /> Kaydet
          </Button>
        </div>
      </div>
    </div>
  )
}

// ── Kategori sekmesi ─────────────────────────────────────────────────────────

function statusIcon(g: GroupRow) {
  if (!g.mapping) return <span style={{ color: 'var(--text-s)' }}>—</span>
  if (g.mapping.status === 'broken') return <span style={{ color: '#ef4444' }}>⛔</span>
  if (g.mapping.status === 'needs_review') return <span style={{ color: '#f59e0b' }}>⚠</span>
  return <span style={{ color: 'var(--brand)' }}>✓</span>
}

function CategoryTab({
  marketplace, overview, ownTypes, selectedGroupId, onSelect, onSaved,
}: {
  marketplace: string
  overview: Overview
  ownTypes: OwnAttrType[]
  selectedGroupId: string | null
  onSelect: (id: string) => void
  onSaved: () => void
}) {
  const [search, setSearch] = useState('')
  const [onlyUnmapped, setOnlyUnmapped] = useState(false)
  const [bulkMode, setBulkMode] = useState(false) // RF4: toplu eşleme modu (grup → pazaryeri kategorisi)

  const list = useMemo(() => {
    let l = overview.groups
    if (onlyUnmapped) l = l.filter((g) => !g.mapping || g.mapping.status !== 'active')
    if (search.trim()) {
      const t = search.trim().toLocaleLowerCase('tr-TR')
      l = l.filter((g) => g.name.toLocaleLowerCase('tr-TR').includes(t) || g.code.toLowerCase().includes(t))
    }
    return l
  }, [overview.groups, search, onlyUnmapped])

  const selected = overview.groups.find((g) => g.productGroupId === selectedGroupId) ?? null

  return (
    <div className="grid grid-cols-1 lg:grid-cols-[340px_1fr] gap-4 items-start">
      <div className="card p-0 overflow-hidden">
        <div className="p-3" style={{ borderBottom: '1px solid var(--border)' }}>
          <input className="inp w-full mb-2" placeholder="Grup ara…" value={search} onChange={(e) => setSearch(e.target.value)} />
          <label className="flex items-center gap-1.5 text-xs cursor-pointer" style={{ color: 'var(--text-m)' }}>
            <input type="checkbox" checked={onlyUnmapped} onChange={(e) => setOnlyUnmapped(e.target.checked)} />
            Yalnız eşsiz / gözden geçirilecekler
          </label>
          {/* RF4: ilerleme + toplu eşleme modu — eşlenmemiş grup kalmayana kadar öneriyle hızlı eşleme */}
          <div className="mt-2 text-[11px]" style={{ color: 'var(--text-s)' }}>
            Eşli {overview.mappedCount}/{overview.groups.length} grup
            <span className="mx-1">·</span>
            <button type="button" className="underline" style={{ color: 'var(--brand)' }}
              onClick={() => setBulkMode((v) => !v)}>
              {bulkMode ? 'Tekil düzenlemeye dön' : `Toplu öneriyle eşle (${overview.unmappedCount + overview.reviewCount})`}
            </button>
          </div>
        </div>
        <div className="max-h-[560px] overflow-y-auto">
          {list.map((g) => (
            <button
              key={g.productGroupId}
              onClick={() => onSelect(g.productGroupId)}
              className={cn('flex items-center gap-2 w-full text-left px-3 py-2 text-sm hover:opacity-80')}
              style={{
                color: 'var(--text)',
                background: g.productGroupId === selectedGroupId ? 'var(--brand-bg)' : undefined,
                borderLeft: g.productGroupId === selectedGroupId ? '3px solid var(--brand)' : '3px solid transparent',
              }}
            >
              {statusIcon(g)}
              <span className="truncate">{g.name}</span>
              <span className="ml-auto shrink-0 text-[11px] tabular-nums" style={{ color: 'var(--text-s)' }}>
                {g.productCount.toLocaleString('tr-TR')}
              </span>
            </button>
          ))}
          <div className="min-h-[1px]">
            {list.length === 0 ? (
              <p className="text-xs px-3 py-3" style={{ color: 'var(--text-s)' }}>Filtreye uyan grup yok.</p>
            ) : null}
          </div>
        </div>
      </div>

      <div>
        {bulkMode ? (
          <BulkSuggestPanel marketplace={marketplace} onSaved={onSaved} />
        ) : selected ? (
          <CategoryEditor
            key={`${marketplace}-${selected.productGroupId}-${selected.mapping?.id ?? 'new'}`}
            marketplace={marketplace}
            group={selected}
            ownTypes={ownTypes}
            onSaved={onSaved}
          />
        ) : (
          <div className="card py-16 text-center">
            <p className="text-sm" style={{ color: 'var(--text-m)' }}>Soldan bir ürün grubu seçin.</p>
          </div>
        )}
      </div>
    </div>
  )
}

// ── RF4: toplu eşleme paneli (2026-09-01) — ürün grubu → pazaryeri kategorisi ──
// Aktif eşlemesi olmayan TÜM gruplar tek tabloda; her satırda ilk 3 öneri hap olarak,
// en yüksek skorlu öneri ÖN SEÇİLİ gelir. "Atla" satırı kampanyadan çıkarır. Kaydet,
// seçilenleri tek istekte (bulk-category) birebir eşler — kısmi hata işi durdurmaz.

interface SuggestRow {
  productGroupId: string; code: string; name: string; productCount: number
  suggestions: { externalId: string; name: string; path: string; score: number }[]
}

function BulkSuggestPanel({ marketplace, onSaved }: { marketplace: string; onSaved: () => void }) {
  const [secimler, setSecimler] = useState<Record<string, string>>({}) // groupId → externalId | '' (atla)
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null)

  const { data: rows = [], isLoading, refetch } = useQuery<SuggestRow[]>({
    queryKey: ['mapping-suggest-all', marketplace],
    queryFn: async () =>
      (await api.get(`/marketplaces/mapping/suggest-all?marketplace=${marketplace}`)).data.data ?? [],
  })

  // Kullanıcı açıkça "atla" seçmediyse ilk öneri ön seçilidir.
  const secimOf = (row: SuggestRow) => secimler[row.productGroupId] ?? row.suggestions[0]?.externalId ?? ''
  const seciliAdet = rows.filter((row) => secimOf(row)).length

  const kaydet = useMutation({
    mutationFn: async () => {
      const items = rows
        .filter((row) => secimOf(row))
        .map((row) => ({ productGroupId: row.productGroupId, targetExternalId: secimOf(row) }))
      return (await api.post('/marketplaces/mapping/bulk-category', { marketplace, items })).data.data
    },
    onSuccess: (d) => {
      setMsg({ ok: d.failed === 0, text: `${d.saved} grup eşlendi${d.failed ? `, ${d.failed} hata: ${d.errors?.[0] ?? ''}` : '.'}` })
      setSecimler({})
      refetch()
      onSaved()
    },
    onError: () => setMsg({ ok: false, text: 'Toplu eşleme kaydedilemedi.' }),
  })

  if (isLoading) return <div className="card py-16 text-center text-sm" style={{ color: 'var(--text-m)' }}>Öneriler hesaplanıyor…</div>
  if (rows.length === 0)
    return <div className="card py-16 text-center text-sm" style={{ color: 'var(--text-m)' }}>🎉 Eşsiz grup kalmadı — tüm gruplar eşli.</div>

  return (
    <div className="card p-0 overflow-hidden">
      <div className="flex flex-wrap items-center gap-2 px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
        <strong className="text-sm" style={{ color: 'var(--text)' }}>Toplu eşleme — {rows.length} eşlenmemiş ürün grubu</strong>
        <span className="text-xs" style={{ color: 'var(--text-s)' }}>Öneriye tıklayarak değiştirin; "atla" satırı bu turda dışarıda bırakır.</span>
        <div className="ml-auto flex items-center gap-2">
          {msg && <span className="text-xs" style={{ color: msg.ok ? 'var(--brand)' : '#ef4444' }}>{msg.text}</span>}
          <Button size="sm" disabled={seciliAdet === 0 || kaydet.isPending} onClick={() => kaydet.mutate()}>
            Seçilen {seciliAdet} grubu eşle
          </Button>
        </div>
      </div>
      <div className="max-h-[620px] overflow-y-auto">
        {rows.map((r) => (
          <div key={r.productGroupId} className="px-4 py-2.5" style={{ borderBottom: '1px solid var(--border)' }}>
            <div className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
              <strong>{r.name}</strong>
              <span className="text-[11px] tabular-nums" style={{ color: 'var(--text-s)' }}>{r.productCount.toLocaleString('tr-TR')} ürün</span>
            </div>
            <div className="mt-1.5 flex flex-wrap gap-1.5">
              {r.suggestions.length === 0 && (
                <span className="text-xs" style={{ color: '#d97706' }}>Öneri bulunamadı — tekil düzenlemeden elle eşleyin.</span>
              )}
              {r.suggestions.map((s) => (
                <button key={s.externalId} type="button" title={s.path}
                  onClick={() => setSecimler((m) => ({ ...m, [r.productGroupId]: s.externalId }))}
                  className="px-2 py-1 rounded-lg text-xs"
                  style={{
                    border: '1px solid var(--border)',
                    background: secimOf(r) === s.externalId ? 'var(--brand)' : 'var(--surface)',
                    color: secimOf(r) === s.externalId ? '#fff' : 'var(--text-m)',
                  }}>
                  {s.path} <span className="opacity-70">%{s.score}</span>
                </button>
              ))}
              {r.suggestions.length > 0 && (
                <button type="button"
                  onClick={() => setSecimler((m) => ({ ...m, [r.productGroupId]: '' }))}
                  className="px-2 py-1 rounded-lg text-xs"
                  style={{
                    border: '1px dashed var(--border)',
                    background: !secimOf(r) ? 'var(--surface2)' : 'var(--surface)',
                    color: 'var(--text-s)',
                  }}>
                  atla
                </button>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}

// ── Değer eşleme paneli ──────────────────────────────────────────────────────

function ValuePanel({
  marketplace, mpCategoryId, attr, onSaved,
}: {
  marketplace: string
  mpCategoryId: string
  attr: MpAttributeRow
  onSaved: () => void
}) {
  const [drafts, setDrafts] = useState<Record<string, string>>({}) // attributeValueId → mp externalId|'' (silme)
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null)

  const { data, isLoading, refetch } = useQuery<{ rows: ValueRow[]; mpValues: MpValue[] }>({
    queryKey: ['mp-values', marketplace, mpCategoryId, attr.externalId],
    queryFn: async () => {
      const { data } = await api.get(
        `/marketplaces/mapping/values?marketplace=${marketplace}&mpCategoryId=${mpCategoryId}&mpAttributeId=${attr.externalId}`)
      return { rows: data.data.rows ?? [], mpValues: data.data.mpValues ?? [] }
    },
  })

  const save = useMutation({
    mutationFn: async () => {
      const items = Object.entries(drafts).map(([attributeValueId, extId]) => {
        const mp = data?.mpValues.find((v) => v.externalId === extId)
        return {
          attributeValueId,
          targetExternalId: extId || null,
          targetCode: mp?.code ?? null,
          targetValue: mp?.value ?? null,
        }
      })
      const { data: resp } = await api.put('/marketplaces/mapping/values', {
        marketplace, mpCategoryExternalId: mpCategoryId, mpAttributeExternalId: attr.externalId, items,
      })
      return resp.data
    },
    onSuccess: (d) => {
      setMsg({ ok: true, text: `${d.changed} değer kaydedildi.` })
      setDrafts({})
      refetch()
      onSaved()
    },
    onError: (err) => setMsg({ ok: false, text: errText(err, 'Kaydedilemedi.') }),
  })

  if (isLoading || !data) return <div className="py-4 text-center text-xs" style={{ color: 'var(--text-s)' }}>Yükleniyor…</div>

  const rows = data.rows
  const currentOf = (r: ValueRow) => drafts[r.attributeValueId] ?? r.targetExternalId ?? ''
  const mappedCount = rows.filter((r) => currentOf(r) !== '').length
  const suggestible = rows.filter((r) => !currentOf(r) && r.suggestedExternalId && r.suggestedScore >= 90)

  function applySuggestions() {
    setDrafts((d) => {
      const next = { ...d }
      for (const r of suggestible) next[r.attributeValueId] = r.suggestedExternalId!
      return next
    })
  }

  return (
    <div className="px-4 pb-3 pt-1" style={{ background: 'var(--surface2)' }}>
      <div className="flex items-center gap-2 mb-2">
        <span className="text-xs font-semibold" style={{ color: 'var(--text-m)' }}>
          {mappedCount}/{rows.length} eşlendi
        </span>
        <Button size="sm" variant="ghost" onClick={applySuggestions} disabled={suggestible.length === 0}>
          Önerileri Uygula ({suggestible.length})
        </Button>
        <span className="text-[11px]" style={{ color: 'var(--text-s)' }}>%90+ benzerlik doldurulur; Kaydet'e kadar yazılmaz</span>
        <div className="ml-auto flex items-center gap-2">
          <span className="text-xs" style={{ color: msg ? (msg.ok ? 'var(--brand)' : '#ef4444') : 'var(--text-s)' }}>{msg?.text ?? ''}</span>
          <Button size="sm" onClick={() => save.mutate()} disabled={save.isPending || Object.keys(drafts).length === 0}>
            <Check size={13} /> Kaydet
          </Button>
        </div>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-x-6 gap-y-1.5">
        {rows.map((r) => (
          <div key={r.attributeValueId} className="flex items-center gap-2">
            <span className="text-sm w-32 truncate shrink-0" title={r.label} style={{ color: 'var(--text)' }}>{r.label}</span>
            <select
              className="inp flex-1"
              value={currentOf(r)}
              onChange={(e) => setDrafts((d) => ({ ...d, [r.attributeValueId]: e.target.value }))}
            >
              <option value="">— eşleme yok —</option>
              {data.mpValues.map((v) => (
                <option key={v.externalId ?? v.value} value={v.externalId ?? ''}>{v.value}</option>
              ))}
            </select>
            <span className="w-24 shrink-0 text-[11px]" style={{ color: r.status === 'broken' ? '#ef4444' : 'var(--text-s)' }}>
              {r.status === 'broken' ? 'değer kalktı' :
                !currentOf(r) && r.suggestedValue ? `öneri: ${r.suggestedValue} %${r.suggestedScore}` :
                currentOf(r) ? '✓' : ''}
            </span>
          </div>
        ))}
      </div>
    </div>
  )
}

// ── Özellik sekmesi ──────────────────────────────────────────────────────────

function AttributesTab({
  marketplace, ownTypes, initialTarget,
}: {
  marketplace: string
  ownTypes: OwnAttrType[]
  initialTarget: string | null
}) {
  const queryClient = useQueryClient()
  const [targetId, setTargetId] = useState<string | null>(initialTarget)
  const [expanded, setExpanded] = useState<string | null>(null)
  const [rowMsg, setRowMsg] = useState<Record<string, string>>({})

  const erp = isErpTarget(marketplace)
  const { data: mappedTargets = [] } = useQuery<MappedTarget[]>({
    queryKey: ['mapped-targets', marketplace],
    queryFn: async () => (await api.get(`/marketplaces/mapping/mapped-targets?marketplace=${marketplace}`)).data.data ?? [],
    enabled: !erp,
  })
  // EM3: ERP özellikleri kategoriye bağlı değildir — tek sanal hedef "*"
  const targets: MappedTarget[] = erp ? [{ externalId: '*', name: 'Tüm ERP özellikleri ve varyant eksenleri', path: '', viaGroups: [] }] : mappedTargets

  const effectiveTarget = targetId ?? targets[0]?.externalId ?? null

  const { data: view, refetch } = useQuery<{ attributes: MpAttributeRow[] }>({
    queryKey: ['mp-attributes', marketplace, effectiveTarget],
    queryFn: async () =>
      (await api.get(`/marketplaces/mapping/attributes?marketplace=${marketplace}&mpCategoryId=${effectiveTarget}`)).data.data,
    enabled: !!effectiveTarget,
  })

  const saveAttr = useMutation({
    mutationFn: async (p: { row: MpAttributeRow; strategy: string; attributeTypeId: string | null; fixedValue: string | null }) => {
      await api.put('/marketplaces/mapping/attribute', {
        marketplace,
        mpCategoryExternalId: effectiveTarget,
        mpAttributeExternalId: p.row.externalId,
        mpAttributeName: p.row.name,
        strategy: p.strategy,
        attributeTypeId: p.attributeTypeId,
        fixedValue: p.fixedValue,
      })
      return p.row.externalId
    },
    onSuccess: (extId) => {
      setRowMsg((m) => ({ ...m, [extId]: '✓ kaydedildi' }))
      refetch()
      queryClient.invalidateQueries({ queryKey: ['mapping-overview'] })
    },
    onError: (err, p) => setRowMsg((m) => ({ ...m, [p.row.externalId]: errText(err, 'Hata') })),
  })

  if (targets.length === 0)
    return (
      <div className="card py-16 text-center">
        <p className="text-sm" style={{ color: 'var(--text-m)' }}>
          Önce Kategoriler sekmesinden en az bir grup eşleyin — özellikler hedef kategoriye göre listelenir.
        </p>
      </div>
    )

  return (
    <div>
      <div className="flex items-center gap-2 mb-3 flex-wrap">
        <span className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>PAZARYERİ KATEGORİSİ</span>
        <select className="inp" style={{ minWidth: 320 }} value={effectiveTarget ?? ''} onChange={(e) => { setTargetId(e.target.value); setExpanded(null) }}>
          {targets.map((t) => (
            <option key={t.externalId} value={t.externalId}>
              {t.path} — ({t.viaGroups.join(', ')})
            </option>
          ))}
        </select>
      </div>

      <div className="card p-0 overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr style={{ background: 'var(--surface2)' }}>
              {['Pazaryeri Özelliği', 'Tip', 'Bizim Karşılık', 'Değerler', ''].map((h) => (
                <th key={h} className="px-3 py-2 text-left text-xs font-semibold" style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {(view?.attributes ?? []).map((a) => {
              const strategy = a.strategy ?? 'map_values'
              return [
                <tr
                  key={a.externalId}
                  className="cursor-pointer hover:opacity-90"
                  style={{ borderTop: '1px solid var(--border)' }}
                  onClick={() => setExpanded(expanded === a.externalId ? null : a.externalId)}
                >
                  <td className="px-3 py-2">
                    <div className="flex items-center gap-1.5 flex-wrap">
                      <span className="font-medium" style={{ color: 'var(--text)' }}>{a.name}</span>
                      {a.isRequired ? <Badge variant="danger">Zorunlu</Badge> : null}
                      {a.isVariantAxis ? <Badge variant="info">Varyant ekseni</Badge> : null}
                      {a.status && a.status !== 'active' ? <Badge variant="warning">Gözden geçir</Badge> : null}
                    </div>
                    <div className="min-h-[14px]">
                      {a.statusNote ? <p className="text-[11px] mt-0.5" style={{ color: '#b45309' }}>{a.statusNote}</p> : null}
                    </div>
                  </td>
                  <td className="px-3 py-2 text-xs" style={{ color: 'var(--text-m)' }}>
                    {a.valueCount > 0 ? `Liste (${a.valueCount})` : 'Serbest'}
                    {a.allowCustom && a.valueCount > 0 ? ' + serbest' : ''}
                  </td>
                  <td className="px-3 py-2" onClick={(e) => e.stopPropagation()}>
                    <div className="flex items-center gap-1.5 flex-wrap">
                      <select
                        className="inp"
                        style={{ width: 130 }}
                        value={strategy}
                        onChange={(e) => saveAttr.mutate({
                          row: a, strategy: e.target.value,
                          attributeTypeId: e.target.value === 'fixed_value' ? null : a.attributeTypeId,
                          fixedValue: e.target.value === 'fixed_value' ? (a.fixedValue ?? '-') : null,
                        })}
                      >
                        <option value="map_values">Değer eşle</option>
                        <option value="pass_literal" disabled={!a.allowCustom}>Serbest geçir</option>
                        {erp && <option value="ignore">Yok say (katalog özelliği yapılmaz)</option>}
                        <option value="fixed_value">Sabit değer</option>
                      </select>
                      {strategy === 'ignore' ? (
                        <span className="text-xs" style={{ color: 'var(--text-s)' }}>ERP'den gelen bu alan katalog özelliğine yazılmaz.</span>
                      ) : strategy === 'fixed_value' ? (
                        <input
                          className="inp"
                          style={{ width: 140 }}
                          defaultValue={a.fixedValue ?? ''}
                          placeholder="Sabit değer…"
                          onBlur={(e) => {
                            if (e.target.value !== (a.fixedValue ?? ''))
                              saveAttr.mutate({ row: a, strategy: 'fixed_value', attributeTypeId: null, fixedValue: e.target.value })
                          }}
                        />
                      ) : (
                        <select
                          className="inp"
                          style={{ width: 150 }}
                          value={a.attributeTypeId ?? ''}
                          onChange={(e) => saveAttr.mutate({
                            row: a, strategy, attributeTypeId: e.target.value || null, fixedValue: null,
                          })}
                        >
                          <option value="">— seç —</option>
                          {ownTypes.map((t) => (
                            <option key={t.id} value={t.id}>{pickTr(t.nameI18n, t.code ?? '')}</option>
                          ))}
                        </select>
                      )}
                    </div>
                    <div className="min-h-[14px]">
                      <span className="text-[11px]" style={{ color: rowMsg[a.externalId]?.startsWith('✓') ? 'var(--brand)' : '#ef4444' }}>
                        {rowMsg[a.externalId] ?? ''}
                      </span>
                    </div>
                  </td>
                  <td className="px-3 py-2 text-xs tabular-nums" style={{ color: 'var(--text-m)' }}>
                    {strategy === 'map_values' && a.attributeTypeId
                      ? <span style={{ color: a.mappedValueCount >= a.ownValueCount && a.ownValueCount > 0 ? 'var(--brand)' : '#b45309' }}>
                          {a.mappedValueCount}/{a.ownValueCount}
                        </span>
                      : '—'}
                  </td>
                  <td className="px-3 py-2 text-right" style={{ color: 'var(--text-s)' }}>
                    {expanded === a.externalId ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
                  </td>
                </tr>,
                expanded === a.externalId ? (
                  <tr key={`${a.externalId}-panel`}>
                    <td colSpan={5} className="p-0">
                      {strategy === 'map_values' && a.attributeTypeId ? (
                        <ValuePanel marketplace={marketplace} mpCategoryId={effectiveTarget!} attr={a} onSaved={() => refetch()} />
                      ) : (
                        <p className="text-xs px-4 py-3" style={{ background: 'var(--surface2)', color: 'var(--text-s)' }}>
                          Değer eşlemek için stratejiyi "Değer eşle" yapıp bizim özellik tipini seçin.
                        </p>
                      )}
                    </td>
                  </tr>
                ) : null,
              ]
            })}
          </tbody>
        </table>
      </div>
    </div>
  )
}

// ── Gözden geçir sekmesi ─────────────────────────────────────────────────────

function ReviewTab({ marketplace, onGoCategory }: { marketplace: string; onGoCategory: (groupId: string) => void }) {
  const queryClient = useQueryClient()
  const { data: rows = [], isLoading, refetch } = useQuery<ReviewRow[]>({
    queryKey: ['mapping-review', marketplace],
    queryFn: async () => (await api.get(`/marketplaces/mapping/review?marketplace=${marketplace}`)).data.data ?? [],
  })

  const ack = useMutation({
    mutationFn: async (r: ReviewRow) =>
      api.post(`/marketplaces/mapping/review/${r.mappingId}/acknowledge`, { mappingType: r.mappingType }),
    onSuccess: () => { refetch(); queryClient.invalidateQueries({ queryKey: ['mapping-overview'] }) },
  })

  if (isLoading) return <PageSpinner />
  if (rows.length === 0)
    return (
      <div className="card py-16 text-center">
        <p className="text-sm" style={{ color: 'var(--text-m)' }}>Gözden geçirilecek eşleme yok — her şey sağlıklı. 🎉</p>
      </div>
    )

  const TYPE_LABEL: Record<string, string> = { category: 'Kategori', attribute: 'Özellik', value: 'Değer' }

  return (
    <div className="card p-0 overflow-hidden">
      <table className="w-full text-sm">
        <tbody>
          {rows.map((r) => (
            <tr key={`${r.mappingType}-${r.mappingId}`} style={{ borderTop: '1px solid var(--border)' }}>
              <td className="px-3 py-2.5 w-24">
                <Badge variant={r.status === 'broken' ? 'danger' : 'warning'}>
                  {r.status === 'broken' ? 'Kırıldı' : 'Gözden geçir'}
                </Badge>
              </td>
              <td className="px-3 py-2.5 w-20 text-xs" style={{ color: 'var(--text-s)' }}>{TYPE_LABEL[r.mappingType] ?? r.mappingType}</td>
              <td className="px-3 py-2.5">
                <p className="font-medium" style={{ color: 'var(--text)' }}>{r.title}</p>
                <div className="min-h-[14px]">
                  {r.note ? <p className="text-xs mt-0.5" style={{ color: 'var(--text-m)' }}>{r.note}</p> : null}
                </div>
              </td>
              <td className="px-3 py-2.5 text-right whitespace-nowrap">
                {r.mappingType === 'category' && r.productGroupId ? (
                  <Button size="sm" variant="ghost" onClick={() => onGoCategory(r.productGroupId!)}>Eşlemeye Git</Button>
                ) : null}
                <Button size="sm" variant="ghost" onClick={() => ack.mutate(r)} disabled={ack.isPending}>
                  <Check size={13} /> Onayla
                </Button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

// ── Ana Sayfa ────────────────────────────────────────────────────────────────

// ── EM0: ERP sözlüğü (docs/erp-esleme-plani.md §2.1) ─────────────────────────
interface ErpItem { id: string; targetSystem: string; kind: string; code: string; name: string; parentCode: string | null; isActive: boolean; source: string; lastSeenAt: string; isMapped: boolean; mappingId?: string | null; mappedProductGroupId?: string | null; mappedProductGroupName?: string | null; mappedTargetId?: string | null; mappedTargetLabel?: string | null }
const ERP_KINDS: Record<string, string> = {
  product_group: 'Ürün grubu', variant_axis: 'Varyant ekseni', attribute_type: 'Özellik tipi', attribute_value: 'Özellik değeri', supplier: 'Tedarikçi', color: 'Renk',
}
const isErpTarget = (t: string) => t.startsWith('erp:')

// EM3 (2026-09-06 kullanıcı isteği): sözlükteki ERP grubu → bizim grup, popup'ta aramalı seçiciyle birebir eşleme
function ErpGroupMapModal({ target, item, overview, ownTypes, onClose, onSaved }: {
  target: string; item: ErpItem; overview: Overview | undefined; ownTypes: OwnAttrType[]; onClose: () => void; onSaved: () => void
}) {
  const [groupId, setGroupId] = useState<string | null>(null)
  const [error, setError] = useState('')
  // Gruplar: üst sayfanın özeti henüz yüklenmediyse popup kendisi çeker (aynı sorgu anahtarı → önbellek paylaşılır)
  const { data: ownOverview } = useQuery<Overview>({
    queryKey: ['mapping-overview', target],
    queryFn: async () => (await api.get(`/marketplaces/mapping/overview?marketplace=${target}`)).data.data,
    enabled: !overview,
  })
  const groups = (overview ?? ownOverview)?.groups ?? []
  const trCmp = (a: string, b: string) => a.localeCompare(b, 'tr-TR')
  const groupOptions = useMemo(() => [...groups]
    .sort((a, b) => trCmp(a.name, b.name))
    .map((g) => ({ value: g.productGroupId, label: `${g.name} · ${g.productCount} ürün${g.mapping ? ' · eşli' : ''}` })), [groups])
  const typeOptions = useMemo(() => [...ownTypes]
    .map((t) => ({ value: t.code ?? t.id, label: pickTr(t.nameI18n, t.code ?? '') }))
    .sort((a, b) => trCmp(a.label, b.label)), [ownTypes])
  const valueOptions = (code: string) => (ownTypes.find((x) => x.code === code)?.values ?? [])
    .map((v) => ({ value: v.id, label: pickTr(v.nameI18n) }))
    .sort((a, b) => trCmp(a.label, b.label))
  const secili = groups.find((g) => g.productGroupId === groupId)
  const mevcut = secili?.mapping
  // Kip: bu ERP grubu, seçilen bizim grubun eşlemesine hangi rolle girer?
  const [kind, setKind] = useState<'direct' | 'rules' | 'pool'>('direct')
  const [conds, setConds] = useState<MappingCondition[]>([{ attributeTypeCode: '', valueId: '', valueLabel: '' }])
  const hedef = { targetExternalId: item.code, targetName: item.name, targetPath: `${item.name} [${item.code}]` }
  const gecerliKosullar = conds.filter((c) => c.attributeTypeCode && c.valueId)
  const save = useMutation({
    mutationFn: async () => {
      if (kind === 'direct') {
        await api.put('/marketplaces/mapping/category', { marketplace: target, productGroupId: groupId, mappingKind: 'direct', ...hedef, rules: null, pool: null })
      } else if (kind === 'rules') {
        // Mevcut kurallar korunur; bu ERP grubunu hedef alan yeni kural sona eklenir. Varsayılan hedef: mevcut kurallı
        // eşlemenin varsayılanı, o yoksa mevcut birebir hedef; hiçbiri yoksa boş (hiç kural tutmazsa "eşleşmedi").
        const eski = mevcut?.mappingKind === 'rules' ? (mevcut.rules ?? []) : []
        const varsayilan = mevcut && mevcut.mappingKind !== 'pool' && mevcut.targetExternalId
          ? { targetExternalId: mevcut.targetExternalId, targetName: mevcut.targetName ?? null, targetPath: mevcut.targetPath ?? null }
          : { targetExternalId: null, targetName: null, targetPath: null }
        const yeni = { order: eski.length, conditions: gecerliKosullar, attributeTypeCode: gecerliKosullar[0].attributeTypeCode, valueId: gecerliKosullar[0].valueId, valueLabel: gecerliKosullar[0].valueLabel, ...hedef }
        await api.put('/marketplaces/mapping/category', {
          marketplace: target, productGroupId: groupId, mappingKind: 'rules', ...varsayilan,
          rules: [...eski.map((r, i) => ({ ...r, order: i })), yeni], pool: null,
        })
      } else {
        const eski = mevcut?.mappingKind === 'pool' ? (mevcut.pool ?? []) : []
        const pool = [...eski.filter((p) => p.externalId !== item.code), { externalId: item.code, name: item.name, path: hedef.targetPath }]
        await api.put('/marketplaces/mapping/category', { marketplace: target, productGroupId: groupId, mappingKind: 'pool', targetExternalId: null, targetName: null, targetPath: null, rules: null, pool })
      }
    },
    onSuccess: () => { onSaved(); onClose() },
    onError: (err) => setError(errText(err, 'Eşlenemedi.')),
  })
  const kaydedilebilir = !!groupId && (kind !== 'rules' || gecerliKosullar.length > 0)
  return (
    <Modal open onClose={onClose} title="ERP grubunu eşle"
      footer={<>
        <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
        <Button onClick={() => save.mutate()} loading={save.isPending} disabled={!kaydedilebilir}>Eşle</Button>
      </>} size="lg">
      <div className="space-y-3">
        <div className="rounded-lg p-3 text-sm" style={{ background: 'var(--surface2)', color: 'var(--text)' }}>
          <span className="font-mono font-semibold">{item.code}</span> · {item.name}
          {item.parentCode && <span className="text-xs ml-2" style={{ color: 'var(--text-s)' }}>üst: {item.parentCode}</span>}
        </div>
        <div>
          <label className="flbl">Bizim ürün grubu *</label>
          {/* portal: modal gövdesi overflow'lu → açılır liste body'de sabit konumla açılır, kırpılmaz */}
          <SearchableSelect value={groupId} onChange={setGroupId} options={groupOptions} placeholder={groups.length === 0 ? 'Gruplar yükleniyor…' : 'Grup ara ve seç…'} hasValue={!!groupId} portal />
        </div>
        {secili && (
          <div className="rounded-lg p-3 space-y-2" style={{ border: '1px solid var(--border)' }}>
            <div className="flex items-center gap-3 flex-wrap">
              <span className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>KİP</span>
              {([['direct', 'Birebir'], ['rules', 'Kurallı'], ['pool', 'Havuz']] as const).map(([k, l]) => (
                <label key={k} className="flex items-center gap-1.5 text-sm cursor-pointer" style={{ color: 'var(--text)' }}>
                  <input type="radio" name="erp-map-kind" checked={kind === k} onChange={() => setKind(k)} /> {l}
                </label>
              ))}
            </div>
            {mevcut && (
              <p className="text-xs" style={{ color: '#b45309' }}>
                Mevcut eşleme: {mevcut.mappingKind === 'direct' ? `birebir → ${mevcut.targetName ?? mevcut.targetExternalId}` : mevcut.mappingKind === 'rules' ? `kurallı (${(mevcut.rules ?? []).length} kural)` : `havuz (${(mevcut.pool ?? []).length} aday)`}.
                {kind === 'direct' ? ' Birebir seçilirse bu ERP koduyla DEĞİŞTİRİLİR.' : kind === 'rules' ? ' Kurallı: mevcut kurallar korunur, bu ERP grubu yeni kural olarak eklenir.' : ' Havuz: bu ERP grubu adaylara eklenir.'}
              </p>
            )}
            {kind === 'direct' && (
              <p className="text-xs" style={{ color: 'var(--text-s)' }}>{secili.name} grubundaki tüm ürünler ERP'de <b>{item.name}</b> grubuna gider.</p>
            )}
            {kind === 'rules' && (
              <div className="space-y-1.5">
                <p className="text-xs" style={{ color: 'var(--text-s)' }}>Şu koşullar sağlanınca (VE) ürün ERP'de <b>{item.name}</b> grubuna gider:</p>
                {conds.map((c, ci) => (
                  <div key={ci} className="flex items-center gap-2">
                    <span className="text-[10px] font-semibold w-6" style={{ color: 'var(--brand)' }}>{ci > 0 ? 'VE' : ''}</span>
                    <div style={{ width: 190 }}>
                      <SearchableSelect value={c.attributeTypeCode || null} options={typeOptions} placeholder="Özellik ara…" portal hasValue={!!c.attributeTypeCode}
                        onChange={(v) => setConds((cs) => cs.map((x, i) => i === ci ? { attributeTypeCode: v ?? '', valueId: '', valueLabel: '' } : x))} />
                    </div>
                    <span className="text-xs" style={{ color: 'var(--text-s)' }}>=</span>
                    <div style={{ width: 190 }}>
                      <SearchableSelect value={c.valueId || null} options={valueOptions(c.attributeTypeCode)} placeholder="Değer ara…" portal hasValue={!!c.valueId} disabled={!c.attributeTypeCode}
                        onChange={(v) => { const opt = valueOptions(c.attributeTypeCode).find((o) => o.value === v); setConds((cs) => cs.map((x, i) => i === ci ? { ...x, valueId: v ?? '', valueLabel: opt?.label ?? '' } : x)) }} />
                    </div>
                    {conds.length > 1 && <button type="button" onClick={() => setConds((cs) => cs.filter((_, i) => i !== ci))} className="p-1 hover:opacity-70"><X size={13} /></button>}
                  </div>
                ))}
                <button type="button" onClick={() => setConds((cs) => [...cs, { attributeTypeCode: '', valueId: '', valueLabel: '' }])} className="text-[11px] ml-8 underline" style={{ color: 'var(--brand)' }}>+ koşul ekle (VE)</button>
              </div>
            )}
            {kind === 'pool' && (
              <p className="text-xs" style={{ color: 'var(--text-s)' }}>Bu ERP grubu {secili.name} grubunun aday havuzuna eklenir; ürün başına seçim tamamlama ekranında yapılır. Havuzda en az iki aday olmalı.</p>
            )}
          </div>
        )}
        {error && <p className="text-sm text-red-500">{error}</p>}
      </div>
    </Modal>
  )
}

function ErpDictionaryPanel({ target, overview, ownTypes, onSaved }: { target: string; overview: Overview | undefined; ownTypes: OwnAttrType[]; onSaved: () => void }) {
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(true)
  const [mapItem, setMapItem] = useState<ErpItem | null>(null)
  const [kind, setKind] = useState('product_group')
  const [q, setQ] = useState('')
  const [form, setForm] = useState({ code: '', name: '', parentCode: '' })
  const [error, setError] = useState('')
  const { data: items = [] } = useQuery<ErpItem[]>({
    queryKey: ['erp-items', target, kind, q],
    queryFn: async () => (await api.get(`/marketplaces/mapping/erp-items?target=${encodeURIComponent(target)}&kind=${kind}&q=${encodeURIComponent(q)}&limit=500`)).data.data ?? [],
  })
  const invalidate = () => { queryClient.invalidateQueries({ queryKey: ['erp-items'] }); queryClient.invalidateQueries({ queryKey: ['mapping-targets'] }) }
  const upsert = useMutation({
    mutationFn: async () => { await api.post('/marketplaces/mapping/erp-items', { target, kind, code: form.code, name: form.name, parentCode: form.parentCode || null }) },
    onSuccess: () => { invalidate(); setForm({ code: '', name: '', parentCode: '' }); setError('') },
    onError: (e: unknown) => setError((e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Kaydedilemedi.'),
  })
  const remove = useMutation({
    mutationFn: async (id: string) => { await api.delete(`/marketplaces/mapping/erp-items/${id}`) },
    onSuccess: () => { invalidate(); setError('') },
    onError: (e: unknown) => setError((e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Silinemedi.'),
  })
  const removeMapping = useMutation({
    mutationFn: async (mappingId: string) => { await api.delete(`/marketplaces/mapping/category/${mappingId}`) },
    onSuccess: () => { invalidate(); onSaved(); setError('') },
    onError: (e: unknown) => setError((e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Eşleme silinemedi.'),
  })
  const needsParent = kind === 'attribute_value'
  return (
    <div className="card overflow-hidden p-0 mb-4">
      <button className="w-full flex items-center justify-between px-4 py-3" onClick={() => setOpen((o) => !o)}>
        <div className="text-left">
          <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>ERP Sözlüğü</h2>
          <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
            ERP tarafındaki grup, özellik, değer ve tedarikçi kodları. Ürün grubu eşlemesi satırdaki "Eşle" ile yapılır; eşleme ve sözlük kaydı silinebilir.
          </p>
        </div>
        {open ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
      </button>
      {open && (
        <div className="px-4 pb-4 space-y-3" style={{ borderTop: '1px solid var(--border)' }}>
          <div className="flex flex-wrap items-end gap-2 pt-3">
            <div>
              <label className="flbl">Tür</label>
              <select className="inp" value={kind} onChange={(e) => setKind(e.target.value)}>
                {Object.entries(ERP_KINDS).map(([k, l]) => <option key={k} value={k}>{l}</option>)}
              </select>
            </div>
            <div><label className="flbl">Kod</label><input className="inp font-mono" style={{ width: 140 }} value={form.code} onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))} placeholder="ERP kodu" /></div>
            <div><label className="flbl">Ad</label><input className="inp" style={{ width: 220 }} value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} placeholder="ERP'deki ad" /></div>
            {(needsParent || kind === 'product_group') && (
              <div><label className="flbl">{needsParent ? 'Özellik tipi kodu *' : 'Üst grup kodu'}</label><input className="inp font-mono" style={{ width: 140 }} value={form.parentCode} onChange={(e) => setForm((f) => ({ ...f, parentCode: e.target.value }))} /></div>
            )}
            <Button size="sm" onClick={() => upsert.mutate()} loading={upsert.isPending} disabled={!form.code.trim() || !form.name.trim() || (needsParent && !form.parentCode.trim())}><Plus size={14} /> Ekle / Güncelle</Button>
            <div className="ml-auto"><input className="inp" style={{ width: 200 }} value={q} onChange={(e) => setQ(e.target.value)} placeholder="Sözlükte ara" /></div>
          </div>
          {error && <p className="text-sm text-red-500">{error}</p>}
          <div className="max-h-64 overflow-y-auto rounded-lg" style={{ border: '1px solid var(--border)' }}>
            <table className="w-full">
              <thead>
                <tr style={{ background: 'var(--surface2)' }}>
                  {['KOD', 'AD', 'ÜST', 'KAYNAK', 'BİZİM GRUP', 'DURUM', ''].map((h) => <th key={h} className={cn('px-3 py-2 text-xs font-semibold', h === '' ? 'text-right' : 'text-left')} style={{ color: 'var(--text-s)' }}>{h}</th>)}
                </tr>
              </thead>
              <tbody>
                {items.map((i) => (
                  <tr key={i.id} style={{ borderTop: '1px solid var(--border)' }}>
                    <td className="px-3 py-1.5 font-mono text-xs" style={{ color: 'var(--text)' }}>{i.code}</td>
                    <td className="px-3 py-1.5 text-sm" style={{ color: 'var(--text)' }}>{i.name}</td>
                    <td className="px-3 py-1.5 font-mono text-xs" style={{ color: 'var(--text-s)' }}>{i.parentCode ?? '—'}</td>
                    <td className="px-3 py-1.5 text-xs" style={{ color: 'var(--text-s)' }}>{i.source === 'sync' ? 'ERP' : 'elle'}</td>
                    <td className="px-3 py-1.5 text-sm" style={{ color: 'var(--text)' }}>{i.kind === 'product_group' ? (i.mappedProductGroupName ?? <span style={{ color: 'var(--text-s)' }}>—</span>) : (i.mappedTargetLabel ?? '')}</td>
                    <td className="px-3 py-1.5">
                      {i.kind === 'product_group' || i.kind === 'supplier'
                        ? (i.isMapped ? <Badge variant="success">Eşli</Badge> : <Badge variant="danger">Eşlenmemiş</Badge>)
                        : <Badge variant="info">Sözlükte</Badge>}
                    </td>
                    <td className="px-3 py-1.5 text-right whitespace-nowrap">
                      {i.kind === 'product_group' && !i.mappingId && (
                        <Button size="sm" className="mr-2" onClick={() => setMapItem(i)}>Eşle</Button>
                      )}
                      {i.kind === 'product_group' && i.mappingId && (
                        <Button size="sm" variant="secondary" className="mr-2" loading={removeMapping.isPending && removeMapping.variables === i.mappingId}
                          onClick={() => { if (window.confirm(`${i.code} ↔ ${i.mappedProductGroupName} eşlemesi silinsin mi?`)) removeMapping.mutate(i.mappingId!) }}>Eşlemeyi Sil</Button>
                      )}
                      <button className="text-xs underline" style={{ color: i.mappingId ? 'var(--text-s)' : '#dc2626' }}
                        title={i.mappingId ? 'Önce eşlemeyi silin' : 'Sözlük kaydını sil'} disabled={!!i.mappingId}
                        onClick={() => { if (window.confirm(`${i.code} sözlük kaydı silinsin mi?`)) remove.mutate(i.id) }}>sil</button>
                    </td>
                  </tr>
                ))}
                {items.length === 0 && <tr><td colSpan={7} className="px-3 py-4 text-center text-xs" style={{ color: 'var(--text-s)' }}>Bu türde sözlük kaydı yok — yukarıdan ekleyin ya da ERP senkronunu bekleyin.</td></tr>}
              </tbody>
            </table>
          </div>
        </div>
      )}
      {mapItem && (
        <ErpGroupMapModal target={target} item={mapItem} overview={overview} ownTypes={ownTypes}
          onClose={() => setMapItem(null)}
          onSaved={() => { invalidate(); onSaved() }} />
      )}
    </div>
  )
}

// ── EM3: ERP tedarikçi eşlemesi (sözlük satırı → cari) ───────────────────────
function ErpSuppliersTab({ target }: { target: string }) {
  const queryClient = useQueryClient()
  const [error, setError] = useState('')
  const { data: items = [] } = useQuery<ErpItem[] & { mappedTargetId?: string | null; mappedTargetLabel?: string | null }[]>({
    queryKey: ['erp-items', target, 'supplier', ''],
    queryFn: async () => (await api.get(`/marketplaces/mapping/erp-items?target=${encodeURIComponent(target)}&kind=supplier&limit=1000`)).data.data ?? [],
  })
  const { data: accounts = [] } = useQuery<{ id: string; code: string; title: string }[]>({
    queryKey: ['supplier-accounts-for-erp'],
    queryFn: async () => (await api.get('/accounts?accountType=supplier&pageSize=500')).data.data?.items ?? [],
    staleTime: 60_000,
  })
  const save = useMutation({
    mutationFn: async (p: { id: string; targetId: string | null; label: string | null }) => {
      await api.put(`/marketplaces/mapping/erp-items/${p.id}/target`, { targetKind: p.targetId ? 'account' : null, targetId: p.targetId, label: p.label })
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['erp-items'] }); setError('') },
    onError: (err) => setError(errText(err, 'Kaydedilemedi.')),
  })
  const rows = items as (ErpItem & { mappedTargetId?: string | null; mappedTargetLabel?: string | null })[]
  return (
    <div className="card overflow-hidden p-0">
      <div className="px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
        <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>Tedarikçiler</h2>
        <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
          ERP tedarikçi kodu → bizim tedarikçi carisi. Eşlenmemiş tedarikçili ürünlerde kart tedarikçisi boş kalır (ürün yine yazılır).
        </p>
      </div>
      <table className="w-full">
        <thead>
          <tr style={{ background: 'var(--surface2)' }}>
            {['ERP KODU', 'ERP ADI', 'BİZİM CARİ', 'DURUM'].map((h) => <th key={h} className="px-3 py-2 text-left text-xs font-semibold" style={{ color: 'var(--text-s)' }}>{h}</th>)}
          </tr>
        </thead>
        <tbody>
          {rows.map((i) => (
            <tr key={i.id} style={{ borderTop: '1px solid var(--border)' }}>
              <td className="px-3 py-2 font-mono text-xs" style={{ color: 'var(--text)' }}>{i.code}</td>
              <td className="px-3 py-2 text-sm" style={{ color: 'var(--text)' }}>{i.name}</td>
              <td className="px-3 py-2">
                <select className="inp" style={{ minWidth: 260 }} value={i.mappedTargetId ?? ''}
                  onChange={(e) => { const a = accounts.find((x) => x.id === e.target.value); save.mutate({ id: i.id, targetId: e.target.value || null, label: a ? `${a.code} · ${a.title}` : null }) }}>
                  <option value="">— eşlenmemiş —</option>
                  {accounts.map((a) => <option key={a.id} value={a.id}>{a.code} · {a.title}</option>)}
                </select>
              </td>
              <td className="px-3 py-2">{i.mappedTargetId ? <Badge variant="success">Eşli</Badge> : <Badge variant="danger">Eşlenmemiş</Badge>}</td>
            </tr>
          ))}
          {rows.length === 0 && <tr><td colSpan={4} className="px-3 py-6 text-center text-xs" style={{ color: 'var(--text-s)' }}>Sözlükte tedarikçi kaydı yok — ERP Sözlüğü panelinden "Tedarikçi" türüyle ekleyin ya da senkronu bekleyin.</td></tr>}
        </tbody>
      </table>
      {error && <p className="px-4 py-2 text-sm text-red-500">{error}</p>}
    </div>
  )
}

export function MappingPage() {
  const queryClient = useQueryClient()
  const [searchParams, setSearchParams] = useSearchParams()
  const marketplace = searchParams.get('mp') ?? 'trendyol'
  const tab = searchParams.get('tab') ?? 'kategoriler'
  const [selectedGroupId, setSelectedGroupId] = useState<string | null>(null)

  // EM0: hedefler = pazaryerleri (referans özeti) + ERP servisleri ("erp:<kod>")
  const { data: targetsData } = useQuery<{ marketplaces: { marketplace: string; categoryCount: number }[]; erp: { key: string; serviceCode: string; name: string; hasContract: boolean; groupCount: number }[] }>({
    queryKey: ['mapping-targets'],
    queryFn: async () => (await api.get('/marketplaces/mapping/targets')).data.data,
    staleTime: 60 * 1000,
  })
  const isErp = isErpTarget(marketplace)


  const { data: overview, isLoading } = useQuery<Overview>({
    queryKey: ['mapping-overview', marketplace],
    queryFn: async () => (await api.get(`/marketplaces/mapping/overview?marketplace=${marketplace}`)).data.data,
  })

  const { data: ownTypes = [] } = useQuery<OwnAttrType[]>({
    queryKey: ['attribute-types-mapping'],
    queryFn: async () => (await api.get('/catalog/attribute-types')).data.data ?? [],
    staleTime: 5 * 60 * 1000,
  })

  function setParam(key: string, value: string) {
    setSearchParams((p) => { const n = new URLSearchParams(p); n.set(key, value); return n }, { replace: true })
  }

  const refSummary = targetsData?.marketplaces ?? []
  const marketplaces = refSummary.length > 0 ? refSummary : [{ marketplace: 'trendyol', categoryCount: 0 }]
  const erpTargets = targetsData?.erp ?? []

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Eşleştirme — Pazaryeri &amp; ERP</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          Ürün gruplarınızı, özelliklerinizi ve değerlerinizi pazaryeri ya da ERP karşılıklarıyla eşleyin. Eşleme grup + özellik koşuluyla kurulur (kurallı kip).
        </p>
      </div>

      {/* Pazaryeri çipleri */}
      <div className="flex items-center gap-1.5 flex-wrap mb-4">
        {marketplaces.map((m) => {
          const enabled = m.categoryCount > 0
          return (
            <button
              key={m.marketplace}
              onClick={() => enabled && setParam('mp', m.marketplace)}
              disabled={!enabled}
              title={enabled ? undefined : 'Önce referans verisini indirin (Pazaryerleri → Referans Verisi)'}
              className={cn('flex items-center gap-1.5 px-2.5 py-1.5 rounded-xl text-sm font-medium transition-all',
                !enabled && 'opacity-40 cursor-not-allowed')}
              style={
                marketplace === m.marketplace
                  ? { background: 'var(--surface)', color: 'var(--text)', border: '1px solid var(--brand)' }
                  : { background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }
              }
            >
              <StoreLogo code={m.marketplace} size={18} />
              {MP_NAME[m.marketplace] ?? m.marketplace}
            </button>
          )
        })}
        {erpTargets.map((t) => (
          <button
            key={t.key}
            onClick={() => setParam('mp', t.key)}
            title={t.hasContract ? `${t.groupCount} ERP grubu sözlükte` : 'Bu ERP için sözleşme yok — sözlük yine de elle doldurulabilir'}
            className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-xl text-sm font-medium transition-all"
            style={
              marketplace === t.key
                ? { background: 'var(--surface)', color: 'var(--text)', border: '1px solid var(--brand)' }
                : { background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }
            }
          >
            <Database size={16} />
            ERP: {t.name}
            {!t.hasContract && <span className="text-[10px]" style={{ color: '#f59e0b' }}>sözleşmesiz</span>}
          </button>
        ))}
        <div className="ml-auto flex items-center gap-3 text-xs" style={{ color: 'var(--text-m)' }}>
          {overview ? (
            <>
              <span><b style={{ color: 'var(--brand)' }}>{overview.mappedCount}</b> eşli</span>
              <span><b style={{ color: overview.unmappedCount > 0 ? '#f59e0b' : 'var(--text)' }}>{overview.unmappedCount}</b> eşsiz</span>
              <span><b style={{ color: overview.reviewCount > 0 ? '#ef4444' : 'var(--text)' }}>{overview.reviewCount}</b> gözden geçirilecek</span>
            </>
          ) : null}
        </div>
      </div>

      {/* Sekmeler */}
      <div className="flex items-center gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {[
          ...(isErp ? [] : [['kategoriler', 'Kategori Eşleme']]),
          ['ozellikler', 'Özellik & Değer'],
          ['gozden', `Gözden Geçir${overview && overview.reviewCount > 0 ? ` (${overview.reviewCount})` : ''}`],
          ...(isErp ? [['tedarikciler', 'Tedarikçiler']] : []),
        ].map(([key, label]) => (
          <button key={key} className={cn('stab', tab === key && 'active')} onClick={() => setParam('tab', key)}>
            {label}
          </button>
        ))}
      </div>

      {isErp && <ErpDictionaryPanel target={marketplace} overview={overview} ownTypes={ownTypes} onSaved={() => queryClient.invalidateQueries({ queryKey: ['mapping-overview', marketplace] })} />}

      {isLoading || !overview ? (
        <PageSpinner />
      ) : tab === 'kategoriler' && !isErp ? (
        <CategoryTab
          marketplace={marketplace}
          overview={overview}
          ownTypes={ownTypes}
          selectedGroupId={selectedGroupId}
          onSelect={setSelectedGroupId}
          onSaved={() => queryClient.invalidateQueries({ queryKey: ['mapping-overview', marketplace] })}
        />
      ) : tab === 'ozellikler' ? (
        <AttributesTab marketplace={marketplace} ownTypes={ownTypes} initialTarget={null} />
      ) : tab === 'tedarikciler' && isErp ? (
        <ErpSuppliersTab target={marketplace} />
      ) : (tab === 'kategoriler' || tab === 'eslenmemis') && isErp ? (
        <AttributesTab marketplace={marketplace} ownTypes={ownTypes} initialTarget={null} />
      ) : (
        <ReviewTab
          marketplace={marketplace}
          onGoCategory={(groupId) => { setSelectedGroupId(groupId); setParam('tab', 'kategoriler') }}
        />
      )}
    </div>
  )
}

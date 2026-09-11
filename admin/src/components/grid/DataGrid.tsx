import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { useSearchParams } from 'react-router-dom'
import { ChevronRight, LayoutList, Table2 } from 'lucide-react'
import { ArrowDown, ArrowUp, ChevronsUpDown } from 'lucide-react'
import { cn } from '@/lib/utils'
import { ColumnsMenu } from './ColumnsMenu'
import { FilterBar } from './FilterBar'
import { ExportButton, type GridExportConfig } from './ExportButton'
import { ViewsMenu } from './ViewsMenu'
import { HeaderFilterButton } from './HeaderFilter'
import { useGridViews } from './useGridViews'
import type { GridFilterField } from './filterUtils'
import { GridPagination } from './GridPagination'
import { useGridScrollRegistry } from './gridScrollContext'
import { useBreakpoint } from './useBreakpoint'
import { useStickyChrome } from './stickyChrome'
import type { GridStateApi } from './useGridState'
import type { GridBreakpoint, GridColumn, GridCompactConfig, GridFrozenConfig, GridSelection } from './types'

// DataGrid çekirdeği (plan §2.1, §2.4-2.7): tanım-güdümlü kolonlar, priority/kullanıcı tercihi, frozen bütçesi (%35 hedef / %40 sınır),
// ghost yatay scrollbar + kenar gölgeleri, sıralama başlıkları, satır tıklama → detay (klavye dahil), Kolonlar menüsü, sayfalama.
// Sticky chrome (2026-09-08): kolon başlıkları üstte, sayfalama altta sabit kopya (stickyChrome.ts) — ghost scrollbar ile aynı görünürlük kuralı.
// Mevcut tasarım dili: card, --border/--surface token'ları, DataTable başlık/hücre sınıfları.

export interface DataGridProps<T> {
  gridId: string
  columns: GridColumn<T>[]
  rows: T[]
  totalCount: number
  grid: GridStateApi
  /** ilk yükleme (satır yok) */
  loading?: boolean
  /** arka planda yenileme (eski satırlar kalır, ince ilerleme çubuğu) */
  fetching?: boolean
  /** sunucu hatası (örn. 400 geçersiz filtre) — tablo yerine mesaj; filtreler çipte kalır, kaldırılabilir */
  error?: string | null
  onRowClick?: (row: T) => void
  rowKey?: (row: T) => string
  empty?: ReactNode
  /** araç çubuğu: sol (arama/hızlı filtreler), sağ (export vb.) — Kolonlar menüsü her zaman sağda */
  toolbarLeft?: ReactNode
  toolbarRight?: ReactNode
  /** araç çubuğunun altında tam genişlik alan (filtre çipleri, gelişmiş panel) */
  toolbarBelow?: ReactNode
  frozen?: Partial<GridFrozenConfig>
  pageSizes?: number[]
  /** global arama kutusu (false → yok) */
  search?: { placeholder?: string } | false
  /** kolona bağlı olmayan ek filtre alanları (örn. 'paid' boolean) */
  extraFilters?: GridFilterField[]
  /** Açılır/kapanır gelişmiş filtre paneli (FilterBar.advanced); alanlar çip ve mobil listesine de girer */
  advancedFilters?: boolean | { fields: GridFilterField[]; note?: ReactNode }
  /** filtre satırının en solunda (örn. küçük seçici) */
  filterLeading?: ReactNode
  /** Excel export (plan §2.8): verilirse "Excel'e aktar ▾" düğmesi Kolonlar'ın solunda */
  export?: GridExportConfig
  /** Kaydedilmiş görünümler (kişisel, sunucu tercihleri) — "Görünüm ▾" seçici */
  views?: boolean
  /** Mobil kompakt görünüm tanımı — verilirse mobilde Kompakt/Tablo geçişi */
  compact?: GridCompactConfig<T>
  /** Satır seçimi (onay kutusu kolonu + seçim çubuğu) */
  selection?: GridSelection
  /**
   * Genişleyen satır (2026-09-09, tur 12): satır tıklandığında ALTINA tüm kolonları kaplayan bir
   * panel açılır (örn. pazaryeri özelliğinin değer eşleme paneli). Hangi satırın açık olduğu
   * ÇAĞIRANDA tutulur — panel kaydettiğinde açık kalması ve dış olaylarla kapanması gerekiyor.
   * `onRowClick` ile birlikte kullanılırsa ikisi de çalışır: tıklama hem satır olayını hem paneli tetikler.
   */
  expandedRow?: { key: string | null; render: (row: T) => ReactNode; onToggle: (key: string | null) => void }
  /** tablo min genişliği (px) — yatay kaydırmanın her zaman erişilebilir olması için (varsayılan: görünür kolon sayısı × 140) */
  minWidth?: number
  className?: string
}

const DEFAULT_FROZEN: GridFrozenConfig = { desktop: 2, tablet: 1, mobile: 0 }
const FROZEN_TARGET = 0.35   // toplam sabit genişlik / viewport hedefi [E1]
const FROZEN_HARD = 0.40     // tek kolon bile bunu aşarsa sabitleme kapanır [E1]

function defaultVisible<T>(c: GridColumn<T>, bp: GridBreakpoint) {
  if (c.defaultVisible === false) return false
  const p = c.priority ?? 1
  return bp === 'mobile' ? p === 1 : bp === 'tablet' ? p <= 2 : true
}

export function DataGrid<T>({
  gridId, columns, rows, totalCount, grid, loading, fetching, error, onRowClick, rowKey, empty,
  toolbarLeft, toolbarRight, toolbarBelow, frozen, pageSizes, minWidth, className, search, extraFilters, advancedFilters, filterLeading, export: exportCfg,
  views: viewsEnabled, compact, selection, expandedRow,
}: DataGridProps<T>) {
  const bp = useBreakpoint()
  const [sp] = useSearchParams()
  const viewsApi = useGridViews(gridId, grid, sp, { enabled: !!viewsEnabled })
  // varsayılan görünüm: temiz girişte bir kez (E14: filtreler çipte görünür kalır)
  useEffect(() => { if (viewsEnabled && viewsApi.shouldApplyDefault) viewsApi.applyDefaultOnce() }, [viewsEnabled, viewsApi])
  const { state, prefs, setPrefs, resetPrefs } = grid
  const fzD = frozen?.desktop ?? DEFAULT_FROZEN.desktop, fzT = frozen?.tablet ?? DEFAULT_FROZEN.tablet, fzM = frozen?.mobile ?? DEFAULT_FROZEN.mobile
  const frozenCfg = useMemo<GridFrozenConfig>(() => ({ desktop: fzD, tablet: fzT, mobile: fzM }), [fzD, fzT, fzM])

  // ── kolon sırası + görünürlük (priority varsayılanı; kullanıcı tercihi ezilmez [E5]) ──
  // sabit kolon kümesi: kullanıcı seçimi (frozenKeys) yoksa kritik kolonlar; eski 'off' tercihi → boş küme
  const pinnedKeys = useMemo(() => new Set(prefs.frozenKeys ?? (prefs.frozen === 'off' ? [] : columns.filter(c => c.frozen).map(c => c.key))),
    [prefs.frozenKeys, prefs.frozen, columns])
  const userPinned = prefs.frozenKeys !== undefined
  const ordered = useMemo(() => {
    const byKey = new Map(columns.map(c => [c.key, c]))
    const out: GridColumn<T>[] = []
    for (const k of prefs.order ?? []) { const c = byKey.get(k); if (c) { out.push(c); byKey.delete(k) } }
    for (const c of columns) if (byKey.has(c.key)) out.push(c)
    // sabit kolonlar sola alınır (kendi aralarındaki sıra korunur)
    return [...out.filter(c => pinnedKeys.has(c.key)), ...out.filter(c => !pinnedKeys.has(c.key))]
  }, [columns, prefs.order, pinnedKeys])

  const visibleKeys = useMemo(() => {
    const s = new Set<string>()
    for (const c of ordered) {
      if (c.lockVisible) { s.add(c.key); continue }
      if (prefs.manualHidden?.includes(c.key)) continue
      if (prefs.manualVisible?.includes(c.key)) { s.add(c.key); continue }
      if (defaultVisible(c, bp)) s.add(c.key)
    }
    return s
  }, [ordered, prefs.manualHidden, prefs.manualVisible, bp])

  const visible = useMemo(() => ordered.filter(c => visibleKeys.has(c.key)), [ordered, visibleKeys])

  // ── frozen: breakpoint adedi + bütçe kuralı; kullanıcı "off" diyebilir ──
  const scrollRef = useRef<HTMLDivElement>(null)
  const tableRef = useRef<HTMLTableElement>(null)
  const [colWidths, setColWidths] = useState<Record<string, number>>({})
  const [containerW, setContainerW] = useState(0)

  useLayoutEffect(() => {
    const el = scrollRef.current; const tbl = tableRef.current
    if (!el || !tbl) return
    const measure = () => {
      setContainerW(el.clientWidth)
      const ths = tbl.querySelectorAll<HTMLTableCellElement>('thead th[data-key]')
      const w: Record<string, number> = {}
      ths.forEach(th => { w[th.dataset.key!] = th.getBoundingClientRect().width })
      setColWidths(prev => {
        const keys = Object.keys(w)
        if (keys.length === Object.keys(prev).length && keys.every(k => Math.abs((prev[k] ?? -1) - w[k]) < 0.5)) return prev
        return w
      })
    }
    measure()
    const ro = new ResizeObserver(measure)
    ro.observe(el); ro.observe(tbl)
    return () => ro.disconnect()
  }, [visible, rows])

  const frozenLefts = useMemo(() => {
    const map = new Map<string, number>()
    const allowed = frozenCfg[bp]
    if (allowed <= 0 || !containerW) return map
    // varsayılan (kritik) küme: breakpoint adedi + %35 hedef; kullanıcı seçimi: adet sınırı yok, yalnız %40 mutlak sınır (sığmayan serbest kalır)
    const pinned = visible.filter(c => pinnedKeys.has(c.key))
    const candidates = userPinned ? pinned : pinned.slice(0, allowed)
    const limit = userPinned ? FROZEN_HARD : FROZEN_TARGET
    let total = 0
    for (const c of candidates) {
      const w = colWidths[c.key] ?? 0
      if (!w) break
      if (map.size === 0 && w > containerW * FROZEN_HARD) break           // tek kolon bile sınırı aşıyor → sabitleme yok
      if (total + w > containerW * limit) break                          // bütçeyi aşan aday serbest bırakılır
      map.set(c.key, total); total += w
    }
    return map
  }, [visible, colWidths, containerW, bp, pinnedKeys, userPinned, frozenCfg])
  const frozenWidth = useMemo(() => Array.from(frozenLefts.entries()).reduce((acc, [k]) => acc + (colWidths[k] ?? 0), 0), [frozenLefts, colWidths])
  const lastFrozenKey = useMemo(() => { let last: string | null = null; for (const c of visible) if (frozenLefts.has(c.key)) last = c.key; return last }, [visible, frozenLefts])

  // ── kaydırma durumu (kenar gölgeleri [E9]) + ghost scrollbar kaydı [E2/E8] ──
  const [scrollPos, setScrollPos] = useState<'none' | 'start' | 'middle' | 'end'>('none')
  const registry = useGridScrollRegistry()
  const scrollId = `grid-scroll-${gridId}`

  const updateScrollPos = useCallback(() => {
    const el = scrollRef.current; if (!el) return
    const max = el.scrollWidth - el.clientWidth
    setScrollPos(max <= 1 ? 'none' : el.scrollLeft <= 1 ? 'start' : el.scrollLeft >= max - 1 ? 'end' : 'middle')
  }, [])

  useEffect(() => {
    const el = scrollRef.current; if (!el) return
    const raf = requestAnimationFrame(updateScrollPos)
    const onScroll = () => { updateScrollPos(); registry?.report(scrollId, { lastInteraction: Date.now() }) }
    el.addEventListener('scroll', onScroll, { passive: true })
    const ro = new ResizeObserver(updateScrollPos); ro.observe(el)
    return () => { cancelAnimationFrame(raf); el.removeEventListener('scroll', onScroll); ro.disconnect() }
  }, [updateScrollPos, registry, scrollId, rows, visible])

  useEffect(() => {
    const el = scrollRef.current
    if (!el || !registry || bp === 'mobile') return   // dokunmatikte doğal kaydırma; ghost gösterilmez
    const unregister = registry.register({ id: scrollId, el, visibility: 0, bottomVisible: true, lastInteraction: 0 })
    const bodyObs = new IntersectionObserver(([e]) => registry.report(scrollId, { visibility: e.isIntersecting ? e.intersectionRatio : 0 }),
      { threshold: [0, 0.1, 0.25, 0.5, 0.75, 1] })
    bodyObs.observe(el)
    const sentinel = document.createElement('div'); sentinel.style.height = '1px'; sentinel.setAttribute('data-grid-bottom', '')
    el.parentElement?.insertBefore(sentinel, el.nextSibling)
    const bottomObs = new IntersectionObserver(([e]) => registry.report(scrollId, { bottomVisible: e.isIntersecting }))
    bottomObs.observe(sentinel)
    return () => { bodyObs.disconnect(); bottomObs.disconnect(); sentinel.remove(); unregister() }
  }, [registry, scrollId, bp])

  // ── satır tıklama → detay (klavye dahil) ──
  const key = useMemo(() => rowKey ?? ((r: T) => (r as { id?: string }).id ?? ''), [rowKey])
  const rowClick = useCallback((r: T, e: React.MouseEvent | React.KeyboardEvent) => {
    if (!onRowClick) return
    const target = e.target as HTMLElement
    if (target.closest('[data-stop-row-click], a, button, input, select, textarea, label')) return
    onRowClick(r)
  }, [onRowClick])

  // filtre alanları: kolon tanımındaki filter'lar (+ ek alanlar); alan adı filter.field ?? kolon anahtarı
  // kolon başına başlık filtresi alanları (filter + filters); FilterBar çipler/mobil için tümünü görür, çubukta yalnız quick + kolonsuz ek alanlar
  const columnFields = useMemo(() => {
    const m = new Map<string, GridFilterField[]>()
    for (const c of columns) {
      const list: GridFilterField[] = []
      if (c.filter) list.push({ ...c.filter, key: c.filter.field ?? c.key, label: c.filter.label ?? c.header })
      for (const f of c.filters ?? []) list.push({ ...f, key: f.field, label: f.label })
      if (list.length) m.set(c.key, list)
    }
    return m
  }, [columns])
  const filterFields = useMemo<GridFilterField[]>(() => {
    const list = [...Array.from(columnFields.values()).flat(), ...(extraFilters ?? [])]
    const seen = new Set(list.map(f => f.key))
    for (const f of (typeof advancedFilters === 'object' ? advancedFilters.fields : [])) if (!seen.has(f.key)) { list.push(f); seen.add(f.key) }
    return list
  }, [columnFields, extraFilters, advancedFilters])
  const headerFieldKeys = useMemo(() => new Set(Array.from(columnFields.values()).flat().map(f => f.key)), [columnFields])
  const hasFilterBar = search !== false && (search !== undefined || filterFields.length > 0) || filterFields.length > 0

  // export kolon anahtarları: exportable !== false olan kolonlar (tanım sırasıyla); görünür küme kullanıcının o anki seçimi
  const allExportKeys = useMemo(() => columns.filter(c => c.exportable !== false).map(c => c.key), [columns])
  const visibleExportKeys = useMemo(() => ordered.filter(c => c.exportable !== false && visibleKeys.has(c.key)).map(c => c.key), [ordered, visibleKeys])

  // ── satır seçimi ──
  const rowIds = useMemo(() => rows.map(r => key(r)), [rows, key])
  const allSelected = !!selection && rowIds.length > 0 && rowIds.every(id => selection.selected.has(id))
  const toggleAll = () => { if (!selection) return; const s = new Set(selection.selected); if (allSelected) rowIds.forEach(id => s.delete(id)); else rowIds.forEach(id => s.add(id)); selection.onChange(s) }
  const toggleOne = (id: string) => { if (!selection) return; const s = new Set(selection.selected); if (s.has(id)) s.delete(id); else s.add(id); selection.onChange(s) }

  // ── mobil kompakt görünüm ──
  const compactMode = !!compact && bp === 'mobile' && (prefs.mobileView ?? 'compact') === 'compact'
  const [expanded, setExpanded] = useState<string | null>(null)

  const colCount = visible.length + (selection ? 1 : 0)
  const tableMinWidth = minWidth ?? Math.max(480, colCount * 140)
  const filtered = state.filters.length > 0 || !!state.search
  const frozenSupported = frozenCfg[bp] > 0   // herhangi bir kolon sabitlenebilir (mobilde kapalı)

  // ── sticky chrome: ghost başlık (üstte) + ghost sayfalama (altta) ──
  const cardRef = useRef<HTMLDivElement>(null)
  const theadRef = useRef<HTMLTableSectionElement>(null)
  const pagRef = useRef<HTMLDivElement>(null)
  const ghostHeadRef = useRef<HTMLDivElement>(null)
  const chromeRefs = useMemo(() => ({ card: cardRef, scroll: scrollRef, thead: theadRef, pagination: pagRef }), [])
  const chrome = useStickyChrome(chromeRefs, { header: !compactMode, pagination: true, version: rows })
  // ghost başlık gerçek kaydırmayı izler (tek yön); üzerinde tekerlek yatayı gerçek kaba iletilir
  useEffect(() => {
    const g = ghostHeadRef.current, real = scrollRef.current
    if (!g || !real || !chrome.header) return
    const sync = () => { g.scrollLeft = real.scrollLeft }
    sync()
    real.addEventListener('scroll', sync, { passive: true })
    return () => real.removeEventListener('scroll', sync)
  }, [chrome.header])

  const headerRow = (ghost: boolean) => (
    <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
      {selection && (
        <th scope="col" className="px-3 py-3 w-8"><input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={allSelected} onChange={toggleAll} aria-label="Sayfadaki tümünü seç" /></th>
      )}
      {visible.map(c => {
        const left = frozenLefts.get(c.key)
        const sorted = state.sort === c.key
        const isFrozen = left !== undefined
        return (
          <th key={c.key} data-key={ghost ? undefined : c.key} data-ghost-key={ghost ? c.key : undefined} scope="col"
            aria-sort={sorted ? (state.dir === 'desc' ? 'descending' : 'ascending') : undefined}
            className={cn('px-4 py-3 text-xs font-semibold text-left whitespace-nowrap select-none', c.className,
              c.align === 'right' && 'text-right', c.align === 'center' && 'text-center',
              isFrozen && 'grid-frozen', isFrozen && c.key === lastFrozenKey && 'grid-frozen-last', c.frozenRight && 'grid-frozen-right',
              c.sortable && 'cursor-pointer hover:text-[var(--text)]')}
            style={{ color: sorted ? 'var(--text)' : 'var(--text-s)', width: c.width, minWidth: c.minWidth, left }}
            onClick={c.sortable ? () => grid.toggleSort(c.key) : undefined}>
            <span className="inline-flex items-center gap-1">
              {c.header}
              {c.sortable && (sorted
                ? (state.dir === 'desc' ? <ArrowDown size={12} /> : <ArrowUp size={12} />)
                : <ChevronsUpDown size={12} className="opacity-40" />)}
              {columnFields.has(c.key) && <HeaderFilterButton fields={columnFields.get(c.key)!} grid={grid} header={c.header || c.key} />}
            </span>
          </th>
        )
      })}
    </tr>
  )
  const pagination = (
    <GridPagination page={state.page} pageSize={state.pageSize} totalCount={totalCount} filtered={filtered}
      onPage={grid.setPage} onPageSize={grid.setPageSize} pageSizes={pageSizes} />
  )

  return (
    <div className={cn('grid-root', className)} data-grid-id={gridId}>
      {(toolbarLeft || toolbarRight || columns.length > 0) && (
        <div className="flex flex-wrap items-start gap-2 mb-3">
          <div className="flex flex-wrap items-center gap-2 flex-1 min-w-0">
            {viewsEnabled && <ViewsMenu views={viewsApi} />}
            {toolbarLeft}
            {hasFilterBar && <FilterBar grid={grid} fields={filterFields} search={search} bp={bp} leading={filterLeading} headerFieldKeys={headerFieldKeys}
              advanced={typeof advancedFilters === 'object' ? { fields: advancedFilters.fields, storageKey: `grid:${gridId}:adv`, note: advancedFilters.note } : advancedFilters} />}
          </div>
          <div className="flex items-center gap-2 ml-auto">
            {toolbarRight}
            {compact && bp === 'mobile' && (
              <button type="button" aria-label={compactMode ? 'Tablo görünümüne geç' : 'Kompakt görünüme geç'} title={compactMode ? 'Tablo' : 'Kompakt'}
                onClick={() => setPrefs({ mobileView: compactMode ? 'table' : 'compact' })}
                className="inline-flex items-center px-2.5 py-1.5 rounded-lg text-sm hover:bg-[var(--surface2)]" style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>
                {compactMode ? <Table2 size={15} /> : <LayoutList size={15} />}
              </button>
            )}
            {exportCfg && <ExportButton grid={grid} config={exportCfg} visibleExportKeys={visibleExportKeys} allExportKeys={allExportKeys} />}
            <ColumnsMenu columns={ordered} visibleKeys={visibleKeys} prefs={prefs} setPrefs={setPrefs} resetPrefs={resetPrefs} frozenSupported={frozenSupported} pinnedKeys={pinnedKeys} effectiveFrozen={frozenLefts} />
          </div>
        </div>
      )}
      {toolbarBelow && <div className="mb-3">{toolbarBelow}</div>}
      {selection && selection.selected.size > 0 && (
        <div className="flex flex-wrap items-center gap-2 mb-3 px-3 py-2 rounded-xl text-sm" style={{ background: 'var(--surface2)', border: '1px solid var(--border)', color: 'var(--text)' }} role="status">
          <span className="font-medium">{selection.selected.size} seçili</span>
          <button type="button" className="text-xs underline" style={{ color: 'var(--text-s)' }} onClick={() => selection.onChange(new Set())}>seçimi temizle</button>
          <div className="flex items-center gap-2 ml-auto">{selection.actions?.(selection.selected)}</div>
        </div>
      )}

      <div ref={cardRef} className="card overflow-hidden relative">
        {fetching && !loading && <div className="grid-progress" aria-hidden />}
        {compactMode ? (
          <div className="grid-compact" role="list">
            {loading && <div className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</div>}
            {!loading && error && <div className="px-4 py-6 text-center text-sm" role="alert" style={{ color: '#dc2626' }}>{error}</div>}
            {!loading && !error && rows.length === 0 && <div className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>{empty ?? (filtered ? 'Filtreye uyan kayıt bulunamadı.' : 'Kayıt bulunamadı.')}</div>}
            {!loading && !error && rows.map(r => {
              const id = key(r); const isOpen = expanded === id
              return (
                <div key={id} role="listitem" className="grid-compact-row" style={{ borderBottom: '1px solid var(--border)' }}>
                  <button type="button" className="w-full flex items-center gap-3 px-3 py-2.5 text-left" aria-expanded={isOpen} onClick={() => setExpanded(isOpen ? null : id)}>
                    {selection && <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={selection.selected.has(id)} onClick={e => e.stopPropagation()} onChange={() => toggleOne(id)} aria-label="Satırı seç" />}
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2"><span className="text-sm font-medium truncate" style={{ color: 'var(--text)' }}>{compact!.title(r)}</span>{compact!.badge?.(r)}</div>
                      {compact!.subtitle && <div className="text-xs truncate" style={{ color: 'var(--text-s)' }}>{compact!.subtitle(r)}</div>}
                    </div>
                    {compact!.right && <div className="text-sm font-medium whitespace-nowrap" style={{ color: 'var(--text)' }}>{compact!.right(r)}</div>}
                    <ChevronRight size={16} className={cn('transition-transform', isOpen && 'rotate-90')} style={{ color: 'var(--text-s)' }} />
                  </button>
                  {isOpen && (
                    <div className="px-3 pb-3 space-y-1">
                      {visible.filter(c => c.header).map(c => (
                        <div key={c.key} className="flex items-start justify-between gap-3 text-sm">
                          <span className="text-xs uppercase" style={{ color: 'var(--text-s)' }}>{c.header}</span>
                          <span className="text-right" style={{ color: 'var(--text)' }}>{c.cell(r)}</span>
                        </div>
                      ))}
                      {onRowClick && (
                        <button type="button" onClick={() => onRowClick(r)} className="mt-2 w-full px-3 py-2 rounded-lg text-sm font-medium" style={{ background: 'var(--brand)', color: '#fff' }}>Detay →</button>
                      )}
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        ) : (
        <div className="grid-scroll-wrap" data-scroll={scrollPos} style={{ ['--grid-frozen-w' as string]: `${frozenWidth}px` }}>
          <div ref={scrollRef} id={scrollId} className="grid-scroll thin-scroll" tabIndex={-1}>
            <table ref={tableRef} className="w-full" style={{ minWidth: tableMinWidth }}>
              <thead ref={theadRef}>
                {headerRow(false)}
              </thead>
              <tbody className={cn(fetching && !loading && 'opacity-60 transition-opacity')}>
                {loading && (
                  <tr><td colSpan={colCount} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
                )}
                {!loading && error && (
                  <tr><td colSpan={colCount} className="px-4 py-6 text-center text-sm" role="alert" style={{ color: '#dc2626' }}>{error}</td></tr>
                )}
                {!loading && !error && rows.length === 0 && (
                  <tr><td colSpan={colCount} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                    {empty ?? (filtered ? 'Filtreye uyan kayıt bulunamadı.' : 'Kayıt bulunamadı.')}
                  </td></tr>
                )}
                {!loading && !error && rows.flatMap(r => {
                  const id = key(r)
                  const acikMi = !!expandedRow && expandedRow.key === id
                  // Genişleyen satırda tıklama paneli açar/kapatır; stopRowClick hücreleri (satır içi
                  // düzenleme alanları) zaten rowClick içinde ayıklanıyor → aynı ayıklama burada da geçerli.
                  const tikla = (e: React.MouseEvent | React.KeyboardEvent) => {
                    if (expandedRow) {
                      const hedef = e.target as HTMLElement
                      if (!hedef.closest('[data-stop-row-click]')) expandedRow.onToggle(acikMi ? null : id)
                    }
                    if (onRowClick) rowClick(r, e)
                  }
                  const tiklanabilir = !!onRowClick || !!expandedRow
                  return [(
                  <tr key={id}
                    onClick={tiklanabilir ? e => tikla(e) : undefined}
                    onKeyDown={tiklanabilir ? e => { if (e.key === 'Enter' && e.target === e.currentTarget) tikla(e) } : undefined}
                    tabIndex={tiklanabilir ? 0 : undefined}
                    className={cn('grid-row transition-colors', tiklanabilir && 'cursor-pointer hover:bg-[var(--surface2)] focus:outline-none focus-visible:bg-[var(--surface2)]')}
                    style={{ borderBottom: '1px solid var(--border)' }}>
                    {selection && (
                      <td className="px-3 py-3 w-8" data-stop-row-click=""><input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={selection.selected.has(key(r))} onChange={() => toggleOne(key(r))} aria-label="Satırı seç" /></td>
                    )}
                    {visible.map(c => {
                      const left = frozenLefts.get(c.key)
                      const isFrozen = left !== undefined
                      return (
                        <td key={c.key} data-stop-row-click={c.stopRowClick ? '' : undefined}
                          className={cn('px-4 py-3 text-sm', c.className, c.align === 'right' && 'text-right', c.align === 'center' && 'text-center',
                            isFrozen && 'grid-frozen', isFrozen && c.key === lastFrozenKey && 'grid-frozen-last', c.frozenRight && 'grid-frozen-right')}
                          style={{ color: 'var(--text)', left }}>{c.cell(r)}</td>
                      )
                    })}
                  </tr>
                  ), acikMi ? (
                    <tr key={`${id}-panel`}>
                      <td colSpan={colCount} className="p-0">{expandedRow!.render(r)}</td>
                    </tr>
                  ) : null]
                })}
              </tbody>
            </table>
          </div>
        </div>
        )}
        <div ref={pagRef}>{pagination}</div>
      </div>
      {chrome.header && createPortal(
        <div ref={ghostHeadRef} className="grid-ghost-header" data-ghost-header={gridId} aria-hidden={false}
          style={{ left: chrome.header.left, width: chrome.header.width, top: chrome.header.top, ['--grid-frozen-w' as string]: `${frozenWidth}px` }}
          onWheel={e => { const real = scrollRef.current; if (real && Math.abs(e.deltaX) > Math.abs(e.deltaY)) real.scrollLeft += e.deltaX }}>
          <table className="w-full" style={{ width: chrome.header.tableWidth, minWidth: tableMinWidth }}>
            <colgroup>{chrome.header.colWidths.map((w, i) => <col key={i} style={{ width: w }} />)}</colgroup>
            <thead>{headerRow(true)}</thead>
          </table>
        </div>, document.body)}
      {chrome.pagination && createPortal(
        <div className="grid-ghost-pagination" data-bottom-bar="" data-ghost-pagination={gridId}
          style={{ left: chrome.pagination.left, width: chrome.pagination.width }}>{pagination}</div>, document.body)}
    </div>
  )
}

/** Hücre içi aksiyon sarmalayıcısı: satır tıklaması bu alanda tetiklenmez (stopPropagation kalıbının ortak hali). */
export function RowActions({ children, className }: { children: ReactNode; className?: string }) {
  return <div data-stop-row-click="" className={cn('inline-flex items-center gap-1', className)} onClick={e => e.stopPropagation()} onKeyDown={e => e.stopPropagation()}>{children}</div>
}

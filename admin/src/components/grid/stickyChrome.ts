import { useEffect, useState, type RefObject } from 'react'

// Sticky "chrome" (2026-09-08, kullanıcı isteği): kolon başlıkları üstte, sayfalama altta — ghost yatay scrollbar ile aynı davranış.
// thead yatay kaydırma kabının (overflow) içinde olduğundan window'a göre sticky yapılamaz → grid görünürken ve gerçek başlık/sayfalama
// viewport dışındayken body'ye portal edilen SABİT (fixed) kopyalar gösterilir. Başlık kopyası gerçek kaydırmayla eşlenir (scrollLeft),
// sayfalama kopyası `data-bottom-bar` taşıdığından ghost scrollbar onun üstüne yerleşir [E3]. Sabit üst alanlar `data-top-bar` ile ölçülür.

export const GRID_CHROME_EVENT = 'grid-chrome-change'

/** Sabit üst alanların (data-top-bar: uygulama başlığı) viewport'ta kapladığı yükseklik → ghost başlık offset'i */
export function measureTopOffset(): number {
  let max = 0
  document.querySelectorAll<HTMLElement>('[data-top-bar]').forEach(el => {
    const r = el.getBoundingClientRect()
    if (r.height > 0 && r.top <= 1 && r.bottom > 0) max = Math.max(max, r.bottom)
  })
  return Math.round(max)
}

export interface GhostHeaderGeom { left: number; width: number; top: number; tableWidth: number; colWidths: number[] }
export interface GhostPaginationGeom { left: number; width: number }

interface Refs {
  card: RefObject<HTMLElement | null>
  scroll: RefObject<HTMLElement | null>
  thead: RefObject<HTMLElement | null>
  pagination: RefObject<HTMLElement | null>
}

/**
 * Pencere kaydırma/boyut ve grid içerik değişimlerinde ölçer:
 *  • başlık kopyası: gerçek thead üst sabit alanın altına girmiş VE tablo gövdesi hâlâ görünürken
 *  • sayfalama kopyası: gerçek sayfalama viewport altında kalmış VE kart görünürken
 */
export function useStickyChrome(refs: Refs, deps: { header: boolean; pagination: boolean; version: unknown }) {
  const [header, setHeader] = useState<GhostHeaderGeom | null>(null)
  const [pagination, setPagination] = useState<GhostPaginationGeom | null>(null)
  const { header: headerOn, pagination: pagOn, version } = deps

  useEffect(() => {
    const card = refs.card.current
    let raf = 0
    const measure = () => {
      if (!card || (!headerOn && !pagOn)) { setHeader(null); setPagination(null); return }
      const vh = window.innerHeight
      const top = measureTopOffset()
      const cardR = card.getBoundingClientRect()
      // başlık
      const scrollEl = refs.scroll.current, thead = refs.thead.current
      if (headerOn && scrollEl && thead) {
        const th = thead.getBoundingClientRect()
        const body = scrollEl.getBoundingClientRect()
        const need = th.top < top && body.bottom > top + th.height + 24
        if (need) {
          const cols = Array.from(thead.querySelectorAll<HTMLTableCellElement>('tr:first-child > th')).map(c => c.getBoundingClientRect().width)
          const table = thead.parentElement as HTMLTableElement | null
          setHeader(prev => {
            const next: GhostHeaderGeom = { left: Math.round(body.left), width: Math.round(body.width), top, tableWidth: table?.offsetWidth ?? body.width, colWidths: cols }
            return prev && prev.left === next.left && prev.width === next.width && prev.top === next.top && prev.tableWidth === next.tableWidth
              && prev.colWidths.length === cols.length && prev.colWidths.every((w, i) => Math.abs(w - cols[i]) < 0.5) ? prev : next
          })
        } else setHeader(null)
      } else setHeader(null)
      // sayfalama
      const pag = refs.pagination.current
      if (pagOn && pag) {
        const pr = pag.getBoundingClientRect()
        const need = pr.top >= vh - 1 && cardR.top < vh - pr.height - 24 && cardR.bottom > top
        setPagination(prev => {
          if (!need) return null
          const next = { left: Math.round(cardR.left), width: Math.round(cardR.width) }
          return prev && prev.left === next.left && prev.width === next.width ? prev : next
        })
      } else setPagination(null)
    }
    const schedule = () => { cancelAnimationFrame(raf); raf = requestAnimationFrame(measure) }
    schedule()
    if (!card) return () => cancelAnimationFrame(raf)
    const ro = new ResizeObserver(schedule)
    ro.observe(card)
    if (refs.scroll.current) ro.observe(refs.scroll.current)
    window.addEventListener('scroll', schedule, { passive: true })
    window.addEventListener('resize', schedule)
    return () => { ro.disconnect(); window.removeEventListener('scroll', schedule); window.removeEventListener('resize', schedule); cancelAnimationFrame(raf) }
  }, [refs.card, refs.scroll, refs.thead, refs.pagination, headerOn, pagOn, version])

  // ghost sayfalama açılıp kapandığında ghost scrollbar alt offset'ini yenilesin
  const pagOpen = pagination !== null
  useEffect(() => { window.dispatchEvent(new Event(GRID_CHROME_EVENT)) }, [pagOpen])

  return { header, pagination }
}

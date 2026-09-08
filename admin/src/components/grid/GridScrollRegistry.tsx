import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { GridScrollContext, type GridScrollEntry, type GridScrollRegistryApi } from './gridScrollContext'

// Sticky ("ghost") yatay scrollbar (plan §2.5, E2/E3/E8/E9):
//  • Her DataGrid kaydırma sarmalayıcısını buraya kaydeder ve görünürlüğünü bildirir.
//  • Sayfada birden çok grid olsa da TEK ghost bar render edilir — en görünür / son etkileşimli grid için [E2].
//  • Ghost yalnız grid viewport'tayken VE gerçek alt scrollbar viewport dışındayken görünür [E8].
//  • Sabit alt alanlar (data-bottom-bar) ölçülüp offset uygulanır [E3].

class Registry {
  entries = new Map<string, GridScrollEntry>()
  active(): GridScrollEntry | null {
    let best: GridScrollEntry | null = null
    for (const e of this.entries.values()) {
      if (e.visibility <= 0) continue
      if (!best || e.visibility > best.visibility + 0.05 || (Math.abs(e.visibility - best.visibility) <= 0.05 && e.lastInteraction > best.lastInteraction)) best = e
    }
    return best
  }
}

export function GridScrollProvider({ children }: { children: ReactNode }) {
  const [reg] = useState(() => new Registry())
  const [tick, setTick] = useState(0)
  const bump = useCallback(() => setTick(t => t + 1), [])

  const api = useMemo<GridScrollRegistryApi>(() => ({
    register: (entry) => {
      reg.entries.set(entry.id, entry); bump()
      return () => { reg.entries.delete(entry.id); bump() }
    },
    report: (id, patch) => {
      const e = reg.entries.get(id); if (!e) return
      const changed = (patch.visibility !== undefined && patch.visibility !== e.visibility)
        || (patch.bottomVisible !== undefined && patch.bottomVisible !== e.bottomVisible)
        || (patch.lastInteraction !== undefined && patch.lastInteraction !== e.lastInteraction)
      Object.assign(e, patch)
      if (changed) bump()
    },
  }), [reg, bump])

  // aktif grid: en görünür; eşitlikte son etkileşimli (tick her rapor değişiminde artar)
  const active = useMemo(() => { void tick; return reg.active() }, [reg, tick])

  return (
    <GridScrollContext.Provider value={api}>
      {children}
      <GhostScrollbar entry={active} />
    </GridScrollContext.Provider>
  )
}

/** Sabit alt alanların (data-bottom-bar) viewport'ta kapladığı yükseklik → ghost bar offset'i [E3] */
function measureBottomOffset(): number {
  let max = 0
  document.querySelectorAll<HTMLElement>('[data-bottom-bar]').forEach(el => {
    const r = el.getBoundingClientRect()
    if (r.height > 0 && r.bottom >= window.innerHeight - 1 && r.top < window.innerHeight) max = Math.max(max, window.innerHeight - r.top)
  })
  return Math.round(max)
}

function GhostScrollbar({ entry }: { entry: GridScrollEntry | null }) {
  const ghost = useRef<HTMLDivElement>(null)
  const inner = useRef<HTMLDivElement>(null)
  const [geom, setGeom] = useState<{ left: number; width: number; scrollWidth: number; bottom: number } | null>(null)
  const source = useRef<'ghost' | 'real' | null>(null)
  const raf = useRef(0)

  const needed = !!entry && !entry.bottomVisible && entry.el.scrollWidth > entry.el.clientWidth + 1

  // geometri: grid'in yatay konumu/genişliği + scrollWidth + alt offset; pencere scroll/resize ve içerik değişiminde yenilenir
  useEffect(() => {
    if (!needed || !entry) return   // gerekmiyorsa render zaten null; geom bir sonraki ölçümde tazelenir
    const el = entry.el
    const measure = () => {
      const r = el.getBoundingClientRect()
      setGeom({ left: Math.round(r.left), width: Math.round(r.width), scrollWidth: el.scrollWidth, bottom: measureBottomOffset() })
      if (ghost.current && source.current !== 'ghost') ghost.current.scrollLeft = el.scrollLeft
    }
    raf.current = requestAnimationFrame(measure)
    const ro = new ResizeObserver(measure)
    ro.observe(el)
    const onScrollWin = () => { cancelAnimationFrame(raf.current); raf.current = requestAnimationFrame(measure) }
    window.addEventListener('scroll', onScrollWin, { passive: true })
    window.addEventListener('resize', onScrollWin)
    return () => { ro.disconnect(); window.removeEventListener('scroll', onScrollWin); window.removeEventListener('resize', onScrollWin); cancelAnimationFrame(raf.current) }
  }, [needed, entry])

  // çift yönlü senkron; kaynak bayrağı + rAF ile geri besleme döngüsü yok [E8]
  useEffect(() => {
    if (!needed || !entry || !ghost.current) return
    const real = entry.el; const g = ghost.current
    let pending = 0
    const sync = (from: 'ghost' | 'real') => {
      if (source.current && source.current !== from) return
      source.current = from
      cancelAnimationFrame(pending)
      pending = requestAnimationFrame(() => {
        if (from === 'ghost') real.scrollLeft = g.scrollLeft; else g.scrollLeft = real.scrollLeft
        requestAnimationFrame(() => { source.current = null })
      })
    }
    const onGhost = () => sync('ghost')
    const onReal = () => sync('real')
    g.addEventListener('scroll', onGhost, { passive: true })
    real.addEventListener('scroll', onReal, { passive: true })
    g.scrollLeft = real.scrollLeft
    return () => { g.removeEventListener('scroll', onGhost); real.removeEventListener('scroll', onReal); cancelAnimationFrame(pending) }
  }, [needed, entry, geom?.scrollWidth])

  if (!needed || !geom) return null
  return (
    <div ref={ghost} className="grid-ghost-scroll" role="scrollbar" aria-label="Tablo yatay kaydırma"
      aria-controls={entry?.id} aria-orientation="horizontal"
      style={{ left: geom.left, width: geom.width, bottom: geom.bottom }}>
      <div ref={inner} style={{ width: geom.scrollWidth, height: 1 }} />
    </div>
  )
}

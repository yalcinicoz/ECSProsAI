import { useEffect, useState } from 'react'
import type { GridBreakpoint } from './types'

// Tailwind v4 varsayılanlarıyla hizalı: mobile <768, tablet 768-1023, desktop ≥1024 (index.css .mob-hide = max-width 767px)
const MOBILE = '(max-width: 767px)'
const TABLET = '(max-width: 1023px)'

function current(): GridBreakpoint {
  if (typeof window === 'undefined' || !window.matchMedia) return 'desktop'
  if (window.matchMedia(MOBILE).matches) return 'mobile'
  if (window.matchMedia(TABLET).matches) return 'tablet'
  return 'desktop'
}

export function useBreakpoint(): GridBreakpoint {
  const [bp, setBp] = useState<GridBreakpoint>(current)
  useEffect(() => {
    const m1 = window.matchMedia(MOBILE)
    const m2 = window.matchMedia(TABLET)
    const on = () => setBp(current())
    m1.addEventListener('change', on)
    m2.addEventListener('change', on)
    return () => { m1.removeEventListener('change', on); m2.removeEventListener('change', on) }
  }, [])
  return bp
}

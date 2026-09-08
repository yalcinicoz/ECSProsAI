/**
 * SearchableSelect — tüm select'lerin standart bileşeni.
 * - Arama (filtreleme)
 * - Klavye navigasyonu: ↑↓ gezmek, Enter seçmek, Escape kapatmak
 * - Dışarı tıklayınca kapanır
 */
import { useState, useRef, useEffect, useId } from 'react'
import { createPortal } from 'react-dom'
import { cn } from '@/lib/utils'
import { ChevronDown, X, Search } from 'lucide-react'

export interface SelectOption {
  value: string
  label: string
}

export interface SearchableSelectProps {
  value: string | null
  onChange: (value: string | null) => void
  options: SelectOption[]
  placeholder?: string
  clearable?: boolean
  disabled?: boolean
  className?: string
  hasValue?: boolean
  /** Açılır listeyi document.body'ye taşır (sabit konum): overflow'lu kapsayıcılar (Modal gövdesi) kırpmasın. */
  portal?: boolean
}

export function SearchableSelect({
  value,
  onChange,
  options,
  placeholder = '— Seçin —',
  clearable = false,
  disabled = false,
  className,
  hasValue,
  portal = false,
}: SearchableSelectProps) {
  const [open, setOpen] = useState(false)
  const [openUpward, setOpenUpward] = useState(false)
  const [pos, setPos] = useState<{ top: number; left: number; width: number; bottom: number } | null>(null)
  const dropRef = useRef<HTMLDivElement>(null)
  const [query, setQuery] = useState('')
  const [highlighted, setHighlighted] = useState(0)
  const wrapRef = useRef<HTMLDivElement>(null)
  const searchRef = useRef<HTMLInputElement>(null)
  const listRef = useRef<HTMLUListElement>(null)
  const id = useId()

  const selected = options.find((o) => o.value === value) ?? null
  const filtered = query
    ? options.filter((o) => o.label.toLowerCase().includes(query.toLowerCase()))
    : options

  // Close on outside click
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      const t = e.target as Node
      if (wrapRef.current && !wrapRef.current.contains(t) && !(dropRef.current && dropRef.current.contains(t))) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [])

  function openDropdown() {
    setQuery('')
    setHighlighted(value ? Math.max(0, options.findIndex((o) => o.value === value)) : 0)
    if (wrapRef.current) {
      const rect = wrapRef.current.getBoundingClientRect()
      const spaceBelow = window.innerHeight - rect.bottom
      setOpenUpward(spaceBelow < 280)
      setPos({ top: rect.bottom + 4, left: rect.left, width: rect.width, bottom: window.innerHeight - rect.top + 4 })
    }
    setOpen(true)
    requestAnimationFrame(() => searchRef.current?.focus())
  }

  useEffect(() => {
    if (!portal || !open) return
    const kapat = () => setOpen(false)
    const handleScroll = (event: Event) => {
      // Scrolling options (including keyboard navigation) must not dismiss the list.
      if (event.target instanceof Node && dropRef.current?.contains(event.target)) return
      kapat()
    }
    window.addEventListener('resize', kapat)
    document.addEventListener('scroll', handleScroll, true)
    return () => { window.removeEventListener('resize', kapat); document.removeEventListener('scroll', handleScroll, true) }
  }, [portal, open])

  // Scroll highlighted item into view
  useEffect(() => {
    const el = listRef.current?.children[highlighted] as HTMLElement | undefined
    el?.scrollIntoView({ block: 'nearest' })
  }, [highlighted])

  function handleKeyDown(e: React.KeyboardEvent) {
    if (!open) {
      if (e.key === 'Enter' || e.key === ' ' || e.key === 'ArrowDown') {
        e.preventDefault()
        openDropdown()
      }
      return
    }
    if (e.key === 'ArrowDown') { e.preventDefault(); setHighlighted((h) => Math.min(h + 1, filtered.length - 1)) }
    if (e.key === 'ArrowUp')   { e.preventDefault(); setHighlighted((h) => Math.max(h - 1, 0)) }
    if (e.key === 'Enter') {
      e.preventDefault()
      if (filtered[highlighted]) { onChange(filtered[highlighted].value); setOpen(false) }
    }
    if (e.key === 'Escape') { setOpen(false) }
  }

  // Açılır liste (yerinde ya da portal ile body'de; portal'da sabit konum, z-index modal'ın üstünde)
  const dropdown = (
        <div
          ref={dropRef}
          className={cn('rounded-xl shadow-xl overflow-hidden', portal ? 'fixed z-[1000]' : 'absolute z-50 w-full')}
          style={{
            background: 'var(--surface)',
            border: '1px solid var(--border)',
            minWidth: '180px',
            ...(portal && pos
              ? (openUpward ? { bottom: pos.bottom, left: pos.left, width: pos.width } : { top: pos.top, left: pos.left, width: pos.width })
              : (openUpward ? { bottom: 'calc(100% + 4px)' } : { top: 'calc(100% + 4px)' })),
          }}
        >
          {/* Search */}
          <div className="p-2 border-b" style={{ borderColor: 'var(--border)' }}>
            <div className="relative">
              <Search size={12} className="absolute left-2.5 top-1/2 -translate-y-1/2 pointer-events-none" style={{ color: 'var(--text-s)' }} />
              <input
                ref={searchRef}
                value={query}
                onChange={(e) => { setQuery(e.target.value); setHighlighted(0) }}
                onKeyDown={handleKeyDown}
                placeholder="Ara…"
                className="inp !pl-7 py-1.5 text-xs"
              />
            </div>
          </div>

          {/* List */}
          <ul
            ref={listRef}
            id={id}
            role="listbox"
            className="thin-scroll overflow-y-auto"
            style={{ maxHeight: '220px' }}
          >
            {filtered.length === 0 ? (
              <li className="px-3 py-3 text-xs text-center" style={{ color: 'var(--text-s)' }}>
                Sonuç bulunamadı
              </li>
            ) : (
              filtered.map((opt, idx) => (
                <li
                  key={opt.value}
                  role="option"
                  aria-selected={opt.value === value}
                  onMouseEnter={() => setHighlighted(idx)}
                  onMouseDown={() => { onChange(opt.value); setOpen(false) }}
                  className={cn(
                    'px-3 py-2 text-sm cursor-pointer',
                    idx === highlighted && 'bg-[var(--surface2)]',
                    opt.value === value && 'font-semibold text-[var(--brand)]',
                  )}
                >
                  {opt.label}
                </li>
              ))
            )}
          </ul>
        </div>
  )

  const isOk = hasValue || value != null

  return (
    <div ref={wrapRef} className={cn('relative', className)}>
      {/* Trigger */}
      <button
        type="button"
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={id}
        onClick={() => {
          if (disabled) return
          if (open) setOpen(false)
          else openDropdown()
        }}
        onKeyDown={handleKeyDown}
        className={cn(
          'inp flex items-center justify-between gap-2 text-left cursor-pointer',
          isOk && 'ok',
          disabled && 'cursor-not-allowed',
        )}
      >
        <span className={cn('truncate flex-1', !selected && 'text-[var(--text-s)]')}>
          {selected ? selected.label : placeholder}
        </span>
        <span className="flex items-center gap-1 flex-shrink-0">
          {clearable && selected && (
            <span
              role="button"
              tabIndex={-1}
              onMouseDown={(e) => { e.stopPropagation(); onChange(null) }}
              className="text-[var(--text-s)] hover:text-[var(--text)] p-0.5 rounded"
            >
              <X size={12} />
            </span>
          )}
          <ChevronDown
            size={14}
            className={cn('text-[var(--text-s)] transition-transform', open && 'rotate-180')}
          />
        </span>
      </button>

      {/* Dropdown */}
      {open && (portal && pos ? createPortal(dropdown, document.body) : dropdown)}
    </div>
  )


}

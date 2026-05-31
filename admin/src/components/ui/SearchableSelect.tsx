/**
 * SearchableSelect — tüm select'lerin standart bileşeni.
 * - Arama (filtreleme)
 * - Klavye navigasyonu: ↑↓ gezmek, Enter seçmek, Escape kapatmak
 * - Dışarı tıklayınca kapanır
 */
import { useState, useRef, useEffect, useId } from 'react'
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
}: SearchableSelectProps) {
  const [open, setOpen] = useState(false)
  const [openUpward, setOpenUpward] = useState(false)
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
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [])

  // Focus search on open + detect upward direction
  useEffect(() => {
    if (open) {
      setQuery('')
      setHighlighted(value ? Math.max(0, filtered.findIndex((o) => o.value === value)) : 0)
      if (wrapRef.current) {
        const rect = wrapRef.current.getBoundingClientRect()
        const spaceBelow = window.innerHeight - rect.bottom
        setOpenUpward(spaceBelow < 280)
      }
      requestAnimationFrame(() => searchRef.current?.focus())
    }
  }, [open])

  // Scroll highlighted item into view
  useEffect(() => {
    const el = listRef.current?.children[highlighted] as HTMLElement | undefined
    el?.scrollIntoView({ block: 'nearest' })
  }, [highlighted])

  function handleKeyDown(e: React.KeyboardEvent) {
    if (!open) {
      if (e.key === 'Enter' || e.key === ' ' || e.key === 'ArrowDown') {
        e.preventDefault()
        setOpen(true)
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
        onClick={() => !disabled && setOpen((o) => !o)}
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
      {open && (
        <div
          className="absolute z-50 w-full rounded-xl shadow-xl overflow-hidden"
          style={{
            background: 'var(--surface)',
            border: '1px solid var(--border)',
            minWidth: '180px',
            ...(openUpward ? { bottom: 'calc(100% + 4px)' } : { top: 'calc(100% + 4px)' }),
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
                className="inp pl-7 py-1.5 text-xs"
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
      )}
    </div>
  )
}

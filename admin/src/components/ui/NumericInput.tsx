/**
 * NumericInput — para/miktar girişi için standart bileşen.
 * - Spinner yok
 * - Sadece rakam, virgül (kesir ayracı), eksi kabul eder
 * - Yazarken binler ayracı eklenir (1.234,56)
 * - Cursor pozisyonu korunur
 * - Blur'da 2 hane ondalık formatlanır
 */
import { useState, useRef } from 'react'
import { cn } from '@/lib/utils'
import { applyNumericMask, formatAmount, parseLocaleNumber } from '@/lib/number'

export interface NumericInputProps
  extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'onChange' | 'value' | 'type'> {
  value: number | null
  onChange: (value: number | null) => void
  decimals?: number
  hasValue?: boolean
  align?: 'left' | 'right'
}

export function NumericInput({
  value,
  onChange,
  decimals = 2,
  hasValue,
  align = 'right',
  className,
  onFocus,
  onBlur,
  ...props
}: NumericInputProps) {
  const [editing, setEditing] = useState(false)
  const [raw, setRaw] = useState('')
  const inputRef = useRef<HTMLInputElement>(null)

  const displayValue = editing
    ? raw
    : value != null && !isNaN(value)
    ? formatAmount(value, decimals)
    : ''

  function handleFocus(e: React.FocusEvent<HTMLInputElement>) {
    setEditing(true)
    setRaw(value != null ? String(value).replace('.', ',') : '')
    onFocus?.(e)
  }

  function handleBlur(e: React.FocusEvent<HTMLInputElement>) {
    setEditing(false)
    const parsed = parseLocaleNumber(raw)
    onChange(parsed)
    onBlur?.(e)
  }

  function handleChange(e: React.ChangeEvent<HTMLInputElement>) {
    const el = e.target
    const cursorBefore = el.selectionStart ?? 0
    // Count thousands-sep dots before cursor in current raw value
    const prevDots = (raw.slice(0, cursorBefore).match(/\./g) ?? []).length

    const masked = applyNumericMask(el.value, decimals)
    setRaw(masked)
    onChange(parseLocaleNumber(masked))

    // Restore cursor accounting for thousands separators added/removed
    requestAnimationFrame(() => {
      if (!inputRef.current) return
      const newDots = (masked.slice(0, cursorBefore).match(/\./g) ?? []).length
      const pos = Math.max(0, cursorBefore + (newDots - prevDots))
      inputRef.current.setSelectionRange(pos, pos)
    })
  }

  return (
    <input
      ref={inputRef}
      type="text"
      inputMode="decimal"
      autoComplete="off"
      value={displayValue}
      onChange={handleChange}
      onFocus={handleFocus}
      onBlur={handleBlur}
      className={cn('inp', align === 'right' && 'text-right', (hasValue || value != null) && 'ok', className)}
      {...props}
    />
  )
}

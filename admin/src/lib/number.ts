// Turkish locale: thousands sep = '.', decimal sep = ','
const LOCALE = 'tr-TR'

export function formatAmount(value: number | null | undefined, decimals = 2): string {
  if (value == null || isNaN(value)) return ''
  return value.toLocaleString(LOCALE, {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  })
}

export function formatInteger(value: number | null | undefined): string {
  if (value == null || isNaN(value)) return ''
  return Math.round(value).toLocaleString(LOCALE)
}

// Parse a locale-formatted string (e.g. "1.234,56") to number
export function parseLocaleNumber(str: string): number | null {
  if (!str.trim()) return null
  const normalized = str.replace(/\./g, '').replace(',', '.')
  const n = parseFloat(normalized)
  return isNaN(n) ? null : n
}

/**
 * Apply numeric mask live during typing.
 * decimals=0 → integer mode (no decimal separator allowed)
 * decimals>0 → amount mode (comma as decimal separator)
 * Thousands separator is added to the integer part automatically.
 */
export function applyNumericMask(raw: string, decimals = 2): string {
  if (!raw) return ''

  const isNeg = raw.startsWith('-')
  let work = isNeg ? raw.slice(1) : raw

  let intStr = ''
  let decStr: string | null = null

  if (decimals > 0) {
    // Use the last comma or period as the decimal separator
    const lastComma = work.lastIndexOf(',')
    const lastPeriod = work.lastIndexOf('.')
    const decIdx = Math.max(lastComma, lastPeriod)

    if (decIdx !== -1) {
      intStr = work.slice(0, decIdx).replace(/\D/g, '')
      decStr = work.slice(decIdx + 1).replace(/\D/g, '').slice(0, decimals)
    } else {
      intStr = work.replace(/\D/g, '')
    }
  } else {
    intStr = work.replace(/\D/g, '')
  }

  const intFormatted = intStr ? Number(intStr).toLocaleString(LOCALE) : ''
  const prefix = isNeg ? '-' : ''

  if (decStr !== null) return `${prefix}${intFormatted},${decStr}`
  return `${prefix}${intFormatted}`
}

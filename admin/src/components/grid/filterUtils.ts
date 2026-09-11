import type { GridFilterDef, GridFilterValue } from './types'

// Filtre yardımcıları (plan §2.3): tarih hızlı seçimleri, çip etiketleri, operatör adları.

export interface GridFilterField extends GridFilterDef {
  key: string
  label: string
}

export const TEXT_OPS: { value: string; label: string }[] = [
  { value: 'contains', label: 'içerir' },
  { value: 'eq', label: 'eşittir' },
  { value: 'startswith', label: 'ile başlar' },
]

export const NUMBER_OPS: { value: string; label: string }[] = [
  { value: 'eq', label: '=' },
  { value: 'gt', label: '>' },
  { value: 'gte', label: '≥' },
  { value: 'lt', label: '<' },
  { value: 'lte', label: '≤' },
  { value: 'between', label: 'aralık' },
]

export const DATE_QUICK: { value: string; label: string }[] = [
  { value: 'today', label: 'Bugün' },
  { value: 'yesterday', label: 'Dün' },
  { value: 'last7', label: 'Son 7 gün' },
  { value: 'last30', label: 'Son 30 gün' },
  { value: 'thisMonth', label: 'Bu ay' },
  { value: 'lastMonth', label: 'Geçen ay' },
  { value: 'custom', label: 'Özel aralık' },
]

function ymd(d: Date) {
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

/** Hızlı seçim → "yyyy-MM-dd,yyyy-MM-dd" (gün bazlı; sunucu İstanbul günü, bitiş DAHİL) */
export function quickDateRange(token: string, now = new Date()): string | null {
  const d0 = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const shift = (days: number) => { const x = new Date(d0); x.setDate(x.getDate() + days); return x }
  switch (token) {
    case 'today': return `${ymd(d0)},${ymd(d0)}`
    case 'yesterday': { const y = shift(-1); return `${ymd(y)},${ymd(y)}` }
    case 'last7': return `${ymd(shift(-6))},${ymd(d0)}`
    case 'last30': return `${ymd(shift(-29))},${ymd(d0)}`
    case 'thisMonth': return `${ymd(new Date(d0.getFullYear(), d0.getMonth(), 1))},${ymd(d0)}`
    case 'lastMonth': {
      const a = new Date(d0.getFullYear(), d0.getMonth() - 1, 1)
      const b = new Date(d0.getFullYear(), d0.getMonth(), 0)
      return `${ymd(a)},${ymd(b)}`
    }
    default: return null
  }
}

export function trDate(ymdStr: string): string {
  const [y, m, d] = ymdStr.split('-')
  return y && m && d ? `${d}.${m}.${y}` : ymdStr
}

/** Aktif filtre çipi metni: "Durum: Bekleyen, Onaylı" */
export function filterChipText(field: GridFilterField, f: GridFilterValue): string {
  const v = f.value
  if (field.chipText) return field.chipText(v)
  switch (field.type) {
    case 'enum': {
      const labels = v.split(',').map(x => field.options?.find(o => o.value === x.trim())?.label ?? x)
      return `${field.label}: ${labels.join(', ')}`
    }
    case 'boolean': return `${field.label}: ${v === 'true' ? 'Evet' : 'Hayır'}`
    case 'date': {
      const q = f.quick && f.quick !== 'custom' ? DATE_QUICK.find(x => x.value === f.quick)?.label : null
      if (q) return `${field.label}: ${q}`
      const [a, b] = v.split(',')
      return `${field.label}: ${a ? trDate(a) : '…'} – ${b ? trDate(b) : '…'}`
    }
    case 'number': {
      if (f.op === 'between') { const [a, b] = v.split(';'); return `${field.label}: ${a} – ${b}` }
      const op = NUMBER_OPS.find(o => o.value === f.op)?.label ?? '='
      return `${field.label} ${op} ${v}`
    }
    default: {
      const op = f.op && f.op !== 'auto' && f.op !== 'contains' ? ` (${TEXT_OPS.find(o => o.value === f.op)?.label ?? f.op})` : ''
      return `${field.label}${op}: "${v}"`
    }
  }
}

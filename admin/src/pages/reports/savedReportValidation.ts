// Render guard only. The API always revalidates permissions and query semantics.
export function validSavedPlan(value: unknown): boolean {
  const object = (x: unknown): x is Record<string, unknown> => !!x && typeof x === 'object' && !Array.isArray(x)
  const strings = (x: unknown) => Array.isArray(x) && x.length <= 32 && x.every(v => typeof v === 'string')
  const text = (x: unknown) => x == null || typeof x === 'string'
  const predicate = (x: unknown, depth = 0): boolean => x == null || (depth <= 6 && object(x)
    && typeof x.kind === 'string' && text(x.field) && text(x.operator) && text(x.relation)
    && (x.values == null || strings(x.values))
    && (x.children == null || Array.isArray(x.children) && x.children.length <= 16 && x.children.every(child => predicate(child, depth + 1))))
  if (!object(value) || JSON.stringify(value).length > 16384) return false
  if (value.period != null && (typeof value.period !== 'string' || !['thisMonth', 'lastMonth', 'last30Days', 'last6Months'].includes(value.period))) return false
  if (value.version === 1) return ['stock', 'orders'].includes(String(value.subject)) && strings(value.dimensions) && strings(value.metrics)
    && Array.isArray(value.filters) && value.filters.length <= 32 && value.filters.every(f => object(f) && typeof f.field === 'string' && typeof f.operator === 'string' && strings(f.values))
  if (value.version !== 2 || !['stock', 'orders', 'stockMovements', 'returns', 'customers', 'staffActivities', 'productCards'].includes(String(value.source)) || !predicate(value.predicate)) return false
  if (value.cardWindowMonths != null && (value.source !== 'productCards' || typeof value.cardWindowMonths !== 'number'
    || !Number.isInteger(value.cardWindowMonths) || value.cardWindowMonths < 1 || value.cardWindowMonths > 12)) return false
  if (value.source === 'stock') {
    if (!['variant', 'location'].includes(String(value.stockGrain)) || value.from != null || value.to != null || value.period != null) return false
  } else if (typeof value.from !== 'string' || typeof value.to !== 'string'
    || !Number.isFinite(Date.parse(value.from)) || !Number.isFinite(Date.parse(value.to)) || value.stockGrain != null) return false
  const aggregate = object(value.aggregate), detail = object(value.detail)
  if (aggregate === detail) return false
  const selection = (aggregate ? value.aggregate : value.detail) as Record<string, unknown>
  return text(selection.sort) && ['asc', 'desc'].includes(String(selection.direction))
    && (selection.top == null || typeof selection.top === 'number' && Number.isInteger(selection.top) && selection.top >= 1 && selection.top <= 1000)
    && (aggregate ? strings(selection.dimensions) && strings(selection.measures) : strings(selection.columns))
}

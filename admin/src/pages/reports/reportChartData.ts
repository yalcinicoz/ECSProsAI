type Value = string | number | null
export type ChartInput = {
  columns: string[]; rows: Value[][]; totalCount: number
  dimensions: string[]; metric: string; detail: boolean
}

/** Never aggregate, truncate, or mix currencies in the browser. */
export function prepareReportChart(input: ChartInput) {
  if (input.detail) return { error: 'Grafik için özet rapor hazırlayın.', points: [] }
  if (!input.rows.length) return { error: 'Grafik için sonuç yok.', points: [] }
  if (input.rows.length !== input.totalCount || input.rows.length > 50)
    return { error: 'Grafik için tüm sonuçlar tek sayfada olmalı ve en fazla 50 grup içermeli. Tablo filtresini veya sayfa boyutunu düzenleyin.', points: [] }
  const metricIndex = input.columns.indexOf(input.metric)
  const indexes = input.dimensions.map(id => input.columns.indexOf(id))
  if (metricIndex < 0 || indexes.some(index => index < 0))
    return { error: 'Grafik kolonları sonuçla eşleşmiyor.', points: [] }
  const currencyIndexes = input.columns.flatMap((id, index) => id.endsWith('.currencyCode') ? [index] : [])
  if (currencyIndexes.some(index => new Set(input.rows.map(row => row[index])).size > 1))
    return { error: 'Grafik için tabloyu tek para birimine filtreleyin.', points: [] }
  if (input.rows.some(row => row.length !== input.columns.length || typeof row[metricIndex] !== 'number' || !Number.isFinite(row[metricIndex])))
    return { error: 'Seçilen ölçü geçerli sayısal değerler içermiyor.', points: [] }
  return { error: null, points: input.rows.map(row => ({
    label: indexes.length ? indexes.map(index => String(row[index] ?? 'Belirtilmemiş')).join(' · ') : 'Genel toplam',
    value: row[metricIndex] as number,
  })) }
}

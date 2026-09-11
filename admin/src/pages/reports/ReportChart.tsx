import { useState } from 'react'
import { prepareReportChart } from './reportChartData'

type Props = {
  result: { columns: string[]; rows: (string | number | null)[][]; totalCount: number }
  dimensions: string[]; metrics: string[]; label: (id: string) => string
}

export function ReportChart({ result, dimensions, metrics, label }: Props) {
  const [selected, setSelected] = useState('')
  const metric = metrics.includes(selected) ? selected : metrics[0]
  const chart = prepareReportChart({ ...result, dimensions, metric, detail: false })
  const maximum = Math.max(1, ...chart.points.map(point => Math.abs(point.value)))
  return <details className="rounded-xl border p-4" style={{ background: 'var(--surface)', borderColor: 'var(--border)' }}>
    <summary className="cursor-pointer font-semibold focus-visible:outline">Grafik görünümü</summary>
    <p className="my-3 text-sm">Tablodaki filtrelenmiş özet sonuçlarını gösterir. Yeni AI çağrısı yapılmaz; farklı ölçüler toplanmaz. Negatif değerler sıfırın solundadır.</p>
    <label className="text-sm">Grafik ölçüsü
      <select className="sel ml-3" value={metric} onChange={event => setSelected(event.target.value)}>
        {metrics.map(id => <option key={id} value={id}>{label(id)}</option>)}
      </select>
    </label>
    {chart.error ? <p role="status" className="mt-3 text-sm">{chart.error}</p> :
      <ol aria-label={`${label(metric)} grafiği`} className="mt-4 space-y-3">
        {chart.points.map((point, index) => <li key={index} className="grid gap-2 md:grid-cols-[minmax(0,1fr)_2fr_auto] items-center text-sm">
          <span className="break-words">{point.label}</span>
          <div aria-hidden="true" className="relative h-5 rounded" style={{ background: 'var(--surface2)' }}>
            <span className="absolute h-full border-l" style={{ left: '50%', borderColor: 'var(--text-s)' }} />
            <span className="absolute h-full rounded" style={{
              width: `${Math.abs(point.value) / maximum * 50}%`,
              left: point.value < 0 ? `${50 - Math.abs(point.value) / maximum * 50}%` : '50%',
              background: point.value < 0 ? '#b91c1c' : '#059669',
            }} />
          </div>
          <span className="tabular-nums">{point.value.toLocaleString('tr-TR')}</span>
        </li>)}
      </ol>}
  </details>
}

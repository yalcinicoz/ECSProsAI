import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { DataGrid, useGridState, type GridColumn, type GridFilterField } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { useAuthStore } from '@/store/auth'
import { Link } from 'react-router-dom'

/** FAZ 15.3 R5 — Raf sayımları listesi (DataGrid F4) + sayım detayı/uygulama penceresi. */

export const COUNT_STATUS_MAP: Record<string, { label: string; variant: 'success' | 'warning' | 'neutral' | 'danger' | 'info' }> = {
  open: { label: 'Sayılıyor', variant: 'warning' },
  finished: { label: 'Bitti (uygulanmadı)', variant: 'info' },
  applied: { label: 'Uygulandı', variant: 'success' },
  cancelled: { label: 'İptal', variant: 'neutral' },
}

interface BinCountRow {
  id: string; warehouseId: string; warehouseCode: string; binId: string; binCode: string; binBarcode: string
  status: string; startedAt: string; finishedAt?: string | null; appliedAt?: string | null
  expectedTotal: number; countedTotal: number; diffTotal: number; lineCount: number; notes?: string | null
}
interface CountLine { id: string; variantId: string; expected: number; counted: number; diff: number; productCode?: string | null; productName?: string | null; optionsText?: string | null }
interface CountDetail { header: BinCountRow; lines: CountLine[] }
interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

const EXTRA_FILTERS: GridFilterField[] = [
  { key: 'status', label: 'Durum', type: 'enum', multiple: true, quick: true, options: Object.entries(COUNT_STATUS_MAP).map(([value, v]) => ({ value, label: v.label })) },
]

export function BinCountsPage() {
  const grid = useGridState('bin-counts', { defaultPageSize: 20, defaultSort: 'startedAt', defaultDir: 'desc' })
  const [selected, setSelected] = useState<string | null>(null)

  const { data, isLoading, isFetching, error } = useQuery<PagedResult<BinCountRow>>({
    queryKey: ['bin-counts', ...grid.queryKey],
    queryFn: async () => (await api.get(`/inventory/bin-counts?${grid.toParams({})}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const columns: GridColumn<BinCountRow>[] = [
    { key: 'binCode', header: 'GÖZ', frozen: true, lockVisible: true, sortable: true, minWidth: 120, filter: { type: 'text', label: 'Göz kodu' },
      filters: [{ field: 'binBarcode', label: 'Göz barkodu', type: 'text' }],
      cell: r => <><code className="text-xs font-mono font-medium" style={{ color: 'var(--text)' }}>{r.binCode}</code><div className="text-xs" style={{ color: 'var(--text-s)' }}>{r.warehouseCode}</div></> },
    { key: 'status', header: 'DURUM', lockVisible: true, sortable: true, priority: 1,
      cell: r => { const st = COUNT_STATUS_MAP[r.status] ?? { label: r.status, variant: 'neutral' as const }; return <Badge variant={st.variant}>{st.label}</Badge> } },
    { key: 'expectedTotal', header: 'BEKLENEN', sortable: true, align: 'right', priority: 2, filter: { type: 'number', label: 'Beklenen' }, cell: r => <span className="text-sm">{r.status === 'open' ? '—' : r.expectedTotal}</span> },
    { key: 'countedTotal', header: 'SAYILAN', sortable: true, align: 'right', priority: 2, filter: { type: 'number', label: 'Sayılan' }, cell: r => <span className="text-sm font-medium">{r.countedTotal}</span> },
    { key: 'diffTotal', header: 'FARK', sortable: true, align: 'right', priority: 1, filter: { type: 'number', label: 'Fark' },
      cell: r => r.status === 'open' ? <span style={{ color: 'var(--text-s)' }}>—</span> : <span className="text-sm font-bold" style={{ color: r.diffTotal === 0 ? '#16a34a' : '#dc2626' }}>{r.diffTotal > 0 ? `+${r.diffTotal}` : r.diffTotal}</span> },
    { key: 'startedAt', header: 'BAŞLANGIÇ', sortable: true, priority: 2, filter: { type: 'date', label: 'Başlangıç', quick: true },
      filters: [{ field: 'finishedAt', label: 'Bitiş', type: 'date' }],
      cell: r => <span className="text-xs" style={{ color: 'var(--text-s)' }}>{new Date(r.startedAt).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' })}</span> },
    { key: 'detail', header: '', priority: 3, align: 'right', exportable: false, cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span> },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-lg font-bold" style={{ color: 'var(--text)' }}>Raf Sayımları</h1>
          <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>{(data?.totalCount ?? 0).toLocaleString('tr-TR')} sayım · fark uygulama yetkisi depo sorumlusunda</p>
        </div>
        <Link to="/inventory/shelf?mode=count"><Button size="sm">Sayıma Başla</Button></Link>
      </div>
      <DataGrid<BinCountRow>
        gridId="bin-counts" views grid={grid} columns={columns} extraFilters={EXTRA_FILTERS}
        search={{ placeholder: 'Göz kodu / barkodu…' }}
        rows={data?.items ?? []} totalCount={data?.totalCount ?? 0} loading={isLoading} fetching={isFetching}
        error={error ? errText(error) : null} onRowClick={r => setSelected(r.id)} empty="Sayım bulunamadı." minWidth={720}
        export={{ endpoint: '/inventory/bin-counts/export', named: () => ({}), fallbackFileName: 'raf-sayimlari.xlsx' }}
        compact={{
          title: r => `${r.binCode} · ${r.warehouseCode}`,
          subtitle: r => new Date(r.startedAt).toLocaleDateString('tr-TR'),
          right: r => r.status === 'open' ? `${r.countedTotal} sayıldı` : `fark ${r.diffTotal > 0 ? '+' : ''}${r.diffTotal}`,
          badge: r => { const st = COUNT_STATUS_MAP[r.status] ?? { label: r.status, variant: 'neutral' as const }; return <Badge variant={st.variant}>{st.label}</Badge> },
        }}
      />
      {selected && <CountDetailModal id={selected} onClose={() => setSelected(null)} />}
    </div>
  )
}

function CountDetailModal({ id, onClose }: { id: string; onClose: () => void }) {
  const qc = useQueryClient()
  const canApply = useAuthStore(s => s.hasPermission)('inventory.count.apply')
  const [err, setErr] = useState('')
  const { data } = useQuery<CountDetail>({ queryKey: ['bin-count', id], queryFn: async () => (await api.get(`/inventory/bin-counts/${id}`)).data.data })
  const { data: authority } = useQuery<{ legacyOwnsStock: boolean; message?: string | null }>({ queryKey: ['stock-authority'], queryFn: async () => (await api.get('/inventory/stock-authority')).data.data, staleTime: 60_000 })
  const invalidate = () => { qc.invalidateQueries({ queryKey: ['bin-count', id] }); qc.invalidateQueries({ queryKey: ['bin-counts'] }) }
  const apply = useMutation({ mutationFn: async () => { await api.post(`/inventory/bin-counts/${id}/apply`) }, onSuccess: invalidate, onError: e => setErr(errText(e)) })
  const cancel = useMutation({ mutationFn: async () => { await api.post(`/inventory/bin-counts/${id}/cancel`, { notes: 'Listeden iptal' }) }, onSuccess: invalidate, onError: e => setErr(errText(e)) })
  const h = data?.header
  const st = h ? (COUNT_STATUS_MAP[h.status] ?? { label: h.status, variant: 'neutral' as const }) : null
  return (
    <Modal open onClose={onClose} title={h ? `Sayım — ${h.binCode} (${h.warehouseCode})` : 'Sayım'}>
      {h && st && (
        <>
          <div className="flex flex-wrap items-center gap-3 text-sm mb-3" style={{ color: 'var(--text-m)' }}>
            <Badge variant={st.variant}>{st.label}</Badge>
            <span>başlangıç {new Date(h.startedAt).toLocaleString('tr-TR')}</span>
            {h.finishedAt && <span>· bitiş {new Date(h.finishedAt).toLocaleString('tr-TR')}</span>}
            {h.appliedAt && <span>· uygulandı {new Date(h.appliedAt).toLocaleString('tr-TR')}</span>}
          </div>
          <table className="w-full text-sm">
            <thead><tr style={{ borderBottom: '1px solid var(--border)' }}>
              {['ÜRÜN', 'BEKLENEN', 'SAYILAN', 'FARK'].map(x => <th key={x} className="text-left py-1 text-xs" style={{ color: 'var(--text-s)' }}>{x}</th>)}
            </tr></thead>
            <tbody>
              {data!.lines.map(l => (
                <tr key={l.id} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="py-1.5"><div style={{ color: 'var(--text)' }}>{l.productName ?? l.productCode ?? l.variantId}</div><div className="text-xs" style={{ color: 'var(--text-s)' }}>{[l.productCode, l.optionsText].filter(Boolean).join(' · ')}</div></td>
                  <td className="py-1.5">{h.status === 'open' ? '—' : l.expected}</td>
                  <td className="py-1.5 font-medium">{l.counted}</td>
                  <td className="py-1.5 font-bold" style={{ color: l.diff === 0 ? '#16a34a' : '#dc2626' }}>{h.status === 'open' ? '—' : (l.diff > 0 ? `+${l.diff}` : l.diff)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {h.notes && <p className="text-xs mt-2" style={{ color: 'var(--text-s)' }}>Not: {h.notes}</p>}
          {err && <p className="text-sm mt-2 text-red-500">{err}</p>}
          <div className="flex justify-between gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
            {(h.status === 'open' || h.status === 'finished') ? <Button size="sm" variant="danger" onClick={() => cancel.mutate()} loading={cancel.isPending}>Sayımı İptal Et</Button> : <span />}
            <div className="flex gap-2">
              <Button variant="secondary" onClick={onClose}>Kapat</Button>
              {h.status === 'finished' && canApply && (
                <Button onClick={() => apply.mutate()} loading={apply.isPending} disabled={authority?.legacyOwnsStock} title={authority?.legacyOwnsStock ? authority.message ?? '' : ''}>Farkı Uygula</Button>
              )}
            </div>
          </div>
        </>
      )}
    </Modal>
  )
}

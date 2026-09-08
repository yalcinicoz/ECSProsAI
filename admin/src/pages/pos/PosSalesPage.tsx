import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText, tarihSaat, para } from '@/components/ui/DataTable.utils'
import { cn } from '@/lib/utils'

interface PosSale {
  id: string
  saleNumber: string
  sessionId: string
  registerId: string
  memberId?: string
  status: string
  grandTotal: number
  createdAt: string
}

interface PosSaleDetail extends PosSale {
  subtotal: number
  totalDiscount: number
  totalTax: number
  notes?: string
  items: { id: string; productName: string; barcode?: string; quantity: number; unitPrice: number; lineTotal: number }[]
  payments: { id: string; paymentMethod: string; amount: number; tenderedAmount?: number; changeAmount?: number }[]
}

interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

const DURUM: Record<string, [string, BadgeVariant]> = {
  completed: ['Tamamlandı', 'success'],
  refunded:  ['İade Edildi', 'danger'],
}

const ODEME: Record<string, string> = {
  cash: 'Nakit', credit_card: 'Kredi Kartı', bank_transfer: 'Havale', online_payment: 'Online', pos: 'POS',
}

function DetayModal({ saleId, onClose }: { saleId: string; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [error, setError] = useState('')
  const { data: d, isLoading } = useQuery<PosSaleDetail>({
    queryKey: ['pos-sale', saleId],
    queryFn: async () => (await api.get(`/pos/sales/${saleId}`)).data.data,
  })

  const iade = useMutation({
    mutationFn: async (reason: string) => {
      setError('')
      await api.post(`/pos/sales/${saleId}/refund`, { reason })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['pos-sales'] })
      queryClient.invalidateQueries({ queryKey: ['pos-sale', saleId] })
    },
    onError: (e: unknown) => setError(errText(e)),
  })

  return (
    <Modal open onClose={onClose} title={d ? `Satış: ${d.saleNumber}` : 'Satış Detayı'} size="lg">
      {isLoading && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</p>}
      {d && (
        <div className="space-y-4">
          <div className="flex items-center gap-3 text-sm">
            {(() => { const [l, v] = DURUM[d.status] ?? [d.status, 'neutral' as BadgeVariant]; return <Badge variant={v}>{l}</Badge> })()}
            <span style={{ color: 'var(--text-s)' }}>{tarihSaat(d.createdAt)}</span>
          </div>

          <div>
            <p className="text-xs font-semibold mb-1" style={{ color: 'var(--text-s)' }}>KALEMLER</p>
            <div className="space-y-1">
              {d.items.map(i => (
                <div key={i.id} className="flex items-center gap-3 text-sm px-2 py-1.5 rounded-lg" style={{ background: 'var(--surface2)' }}>
                  <span className="flex-1" style={{ color: 'var(--text)' }}>{i.productName}</span>
                  <span style={{ color: 'var(--text-s)' }}>{i.quantity} × {para(i.unitPrice)}</span>
                  <span className="font-medium" style={{ color: 'var(--text)' }}>{para(i.lineTotal)}</span>
                </div>
              ))}
            </div>
          </div>

          <div>
            <p className="text-xs font-semibold mb-1" style={{ color: 'var(--text-s)' }}>ÖDEMELER</p>
            <div className="space-y-1">
              {d.payments.map(p => (
                <div key={p.id} className="flex items-center gap-3 text-sm px-2 py-1.5 rounded-lg" style={{ background: 'var(--surface2)' }}>
                  <span className="flex-1" style={{ color: 'var(--text)' }}>{ODEME[p.paymentMethod] ?? p.paymentMethod}</span>
                  <span className="font-medium" style={{ color: 'var(--text)' }}>{para(p.amount)}</span>
                  {p.changeAmount != null && p.changeAmount > 0 && (
                    <span className="text-xs" style={{ color: 'var(--text-s)' }}>para üstü {para(p.changeAmount)}</span>
                  )}
                </div>
              ))}
            </div>
          </div>

          <div className="flex justify-end gap-4 text-sm" style={{ color: 'var(--text)' }}>
            <span>Ara toplam: {para(d.subtotal)}</span>
            {d.totalDiscount > 0 && <span>İndirim: -{para(d.totalDiscount)}</span>}
            <span className="font-bold">Genel toplam: {para(d.grandTotal)}</span>
          </div>

          {error && <p className="text-sm text-red-500">{error}</p>}
        </div>
      )}
      <div className="flex items-center gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        {d?.status === 'completed' && (
          <Button variant="danger" size="sm" loading={iade.isPending}
            onClick={() => {
              const neden = window.prompt('İade nedeni:')
              if (neden && neden.trim()) iade.mutate(neden.trim())
            }}>
            İade Et
          </Button>
        )}
        <div className="flex-1" />
        <Button variant="secondary" onClick={onClose}>Kapat</Button>
      </div>
    </Modal>
  )
}

export function PosSalesPage() {
  // DataGrid F4 (mekanik göç): durum sekmesi URL'de `?tab=`, sayfa/sayfa boyu grid'de.
  const grid = useGridState('pos-sales', { defaultPageSize: 20 })
  const [sp] = useSearchParams()
  const tab = sp.get('tab') ?? ''
  const setTab = (v: string) => grid.mutate(n => { if (v) n.set('tab', v); else n.delete('tab') })
  const [detail, setDetail] = useState<string | null>(null)

  const { data, isLoading, isFetching, error } = useQuery<PagedResult<PosSale>>({
    queryKey: ['pos-sales', tab, ...grid.queryKey],
    queryFn: async () => (await api.get(`/pos/sales?${grid.toParams({ status: tab || undefined })}`)).data.data,
    placeholderData: prev => prev,
  })

  const sales = data?.items ?? []
  const columns: GridColumn<PosSale>[] = [
    { key: 'saleNumber', header: 'FİŞ NO', priority: 1, lockVisible: true, cell: s => <code className="text-xs font-mono">{s.saleNumber}</code> },
    { key: 'total', header: 'TUTAR', priority: 1, cell: s => <span className="font-medium">{para(s.grandTotal)}</span> },
    { key: 'createdAt', header: 'TARİH', priority: 2, cell: s => tarihSaat(s.createdAt) },
    { key: 'status', header: 'DURUM', priority: 1, cell: s => { const [l, v] = DURUM[s.status] ?? [s.status, 'neutral' as BadgeVariant]; return <Badge variant={v}>{l}</Badge> } },
    { key: 'detail', header: '', priority: 3, align: 'right', exportable: false, cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span> },
  ]

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>POS Satışları</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{data?.totalCount ?? 0} kayıt</p>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {[['', 'Tümü'], ['completed', 'Tamamlanan'], ['refunded', 'İade']].map(([v, l]) => (
          <button key={v} className={cn('stab', tab === v && 'active')}
            onClick={() => setTab(v)}>{l}</button>
        ))}
      </div>

      <DataGrid<PosSale>
        gridId="pos-sales"
        grid={grid}
        columns={columns}
        rows={sales}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={error ? errText(error) : null}
        empty="POS satışı yok."
        onRowClick={s => setDetail(s.id)}
      />
      {detail && <DetayModal saleId={detail} onClose={() => setDetail(null)} />}
    </div>
  )
}

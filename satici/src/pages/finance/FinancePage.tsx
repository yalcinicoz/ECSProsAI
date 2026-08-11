import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { formatDate } from '@/lib/i18n'

/* Mali Durum (2026-08-11): hakediş bakiyesi + katman izli hakediş satırları + defter
 * hareketleri — partner GET /settlements ve /account/statement ile aynı veriler. */

interface SettlementLine {
  id: string; orderNumber: string; sku: string; productName: string; quantity: number
  grossAmount: number; commissionRate: number; commissionLayer: string
  commissionAmount: number; campaignDiscountShareAmount: number; netAmount: number
  status: string; deliveredAt: string; eligibleAt: string; isReversal: boolean
}
interface Statement {
  balance: number; currency: string
  entries: { date: string; transactionType: string; debit: number; credit: number; balanceAfter: number; description: string | null }[]
}

const para = (v: number) => v.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const KATMAN: Record<string, string> = {
  product: 'Ürün-özel', campaign: 'Kampanya', contract_group: 'Sözleşme',
  group_default: 'Varsayılan', unconfigured: 'Tanımsız',
}
const katmanAdi = (l: string) => {
  const [t, e] = l.split('+')
  return (KATMAN[t] ?? t) + (e === 'turnover' ? ' + ciro' : '')
}
const DURUM: Record<string, { sinif: string; ad: string }> = {
  pending: { sinif: 'badge ba', ad: 'Beklemede' },
  available: { sinif: 'badge bg', ad: 'Bakiyede' },
  paid: { sinif: 'badge bb', ad: 'Ödendi' },
  reversed: { sinif: 'badge br', ad: 'Ters kayıt' },
}
const ISLEM: Record<string, string> = {
  settlement_accrual: 'Hakediş', settlement_reversal: 'İade tersi', settlement_payout: 'Ödeme',
}

export function FinancePage() {
  const [status, setStatus] = useState('')
  const { data: statement } = useQuery<Statement>({
    queryKey: ['supplier-statement'],
    queryFn: async () => (await api.get('/supplier/account/statement')).data.data,
  })
  const { data: settlements } = useQuery<{ items: SettlementLine[]; totalCount: number }>({
    queryKey: ['supplier-settlements', status],
    queryFn: async () => (await api.get('/supplier/settlements', {
      params: { status: status || undefined, pageSize: 100 },
    })).data.data,
  })

  return (
    <div>
      <div className="flex flex-wrap items-center gap-3 mb-4">
        <h1 className="text-lg font-bold flex-1">Mali Durum</h1>
        <div className="card p-3 px-5 text-sm">
          Hakediş bakiyesi: <strong className="text-base">{para(statement?.balance ?? 0)} {statement?.currency ?? 'TRY'}</strong>
        </div>
      </div>
      <p className="text-xs opacity-70 mb-4">
        Teslim edilen satışlar sözleşmenizdeki bekleme süresi sonunda bakiyenize geçer; ödemeler platform
        tarafından dönemsel yapılır. Her satırda uygulanan komisyon oranı ve hangi kuraldan geldiği görünür.
      </p>

      <div className="flex items-center gap-3 mb-3">
        <div className="text-sm font-semibold flex-1">Hakediş Satırları</div>
        <select className="inp !w-40" value={status} onChange={e => setStatus(e.target.value)}>
          <option value="">Tümü</option>
          <option value="pending">Beklemede</option>
          <option value="available">Bakiyede</option>
          <option value="paid">Ödendi</option>
          <option value="reversed">Ters kayıt</option>
        </select>
      </div>
      <div className="card tbl-wrap overflow-x-auto mb-6">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs opacity-70">
              <th className="py-2 px-3">Sipariş / Ürün</th>
              <th className="py-2 px-3 text-right">Brüt</th>
              <th className="py-2 px-3">Oran</th>
              <th className="py-2 px-3 text-right">Komisyon</th>
              <th className="py-2 px-3 text-right">Kampanya payı</th>
              <th className="py-2 px-3 text-right">Net</th>
              <th className="py-2 px-3">Durum</th>
            </tr>
          </thead>
          <tbody>
            {(settlements?.items ?? []).map(l => {
              const d = DURUM[l.status] ?? DURUM.pending
              return (
                <tr key={l.id} className="border-t">
                  <td className="py-2 px-3">
                    <div className="font-medium">{l.orderNumber}{l.isReversal && ' (iade)'}</div>
                    <div className="text-xs opacity-70">{l.sku} × {l.quantity} — teslim {formatDate(l.deliveredAt)}</div>
                  </td>
                  <td className="py-2 px-3 text-right whitespace-nowrap">{para(l.grossAmount)}</td>
                  <td className="py-2 px-3 text-xs whitespace-nowrap">%{l.commissionRate} ({katmanAdi(l.commissionLayer)})</td>
                  <td className="py-2 px-3 text-right whitespace-nowrap">{para(l.commissionAmount)}</td>
                  <td className="py-2 px-3 text-right whitespace-nowrap">{para(l.campaignDiscountShareAmount)}</td>
                  <td className="py-2 px-3 text-right whitespace-nowrap font-medium">{para(l.netAmount)}</td>
                  <td className="py-2 px-3"><span className={d.sinif}>{d.ad}</span></td>
                </tr>
              )
            })}
            {(settlements?.items ?? []).length === 0 && (
              <tr><td colSpan={7} className="py-8 text-center opacity-60">Hakediş satırı yok.</td></tr>
            )}
          </tbody>
        </table>
      </div>

      <div className="text-sm font-semibold mb-3">Hesap Hareketleri</div>
      <div className="card tbl-wrap overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs opacity-70">
              <th className="py-2 px-3">Tarih</th>
              <th className="py-2 px-3">İşlem</th>
              <th className="py-2 px-3">Açıklama</th>
              <th className="py-2 px-3 text-right">Borç</th>
              <th className="py-2 px-3 text-right">Alacak</th>
              <th className="py-2 px-3 text-right">Bakiye</th>
            </tr>
          </thead>
          <tbody>
            {(statement?.entries ?? []).map((e, i) => (
              <tr key={i} className="border-t">
                <td className="py-2 px-3 whitespace-nowrap">{formatDate(e.date)}</td>
                <td className="py-2 px-3">{ISLEM[e.transactionType] ?? e.transactionType}</td>
                <td className="py-2 px-3 text-xs opacity-80">{e.description ?? '—'}</td>
                <td className="py-2 px-3 text-right whitespace-nowrap">{e.debit > 0 ? para(e.debit) : '—'}</td>
                <td className="py-2 px-3 text-right whitespace-nowrap">{e.credit > 0 ? para(e.credit) : '—'}</td>
                <td className="py-2 px-3 text-right whitespace-nowrap font-medium">{para(e.balanceAfter)}</td>
              </tr>
            ))}
            {(statement?.entries ?? []).length === 0 && (
              <tr><td colSpan={6} className="py-8 text-center opacity-60">Hareket yok.</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}

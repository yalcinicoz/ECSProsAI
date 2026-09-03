import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/api/client'
import { Button } from '@/components/ui/Button'
import { cn } from '@/lib/utils'

/*
 * P3a (2026-08-11): Komisyon Yönetimi — TEK merkezi sayfa (kullanıcı kararı).
 * Sekmeler: Varsayılan Oranlar (platform, ürün grubu bazlı — dokümanda yayınlanan taban),
 * Satıcı Sözleşmeleri (hakediş X + ödeme periyodu + kargo modu + ciro dönemi + oran
 * katmanları + ciro basamakları), Kampanyalar (komisyon oranı + indirim paylaşımı + opt-in),
 * Hakedişler (satır mutabakatı + bakiye + ödeme çıkışı).
 */

interface ProductGroup { id: string; code: string; nameI18n: Record<string, string> }
interface Account { id: string; code: string; title: string; accountType: string }
interface ContractDto {
  settlementDelayDays: number
  payoutPeriod: string
  cargoMode: string
  turnoverPeriodType: string
  isActive: boolean
  notes: string | null
  groupRates: { productGroupId: string; ratePercent: number }[]
  productRates: { productId: string; ratePercent: number }[]
  turnoverTiers: { minTurnover: number; rateAdjustmentPercent: number }[]
}
interface CampaignRow {
  id: string; code: string; nameI18n: Record<string, string>
  startsAt: string; endsAt: string | null
  supplierCommissionRate: number | null
  supplierDiscountSharePercent: number
  requiresSupplierOptIn: boolean
  participantCount: number
}
interface SettlementLine {
  id: string; orderNumber: string; sku: string; productName: string; quantity: number
  grossAmount: number; commissionRate: number; commissionLayer: string
  commissionAmount: number; campaignDiscountShareAmount: number; netAmount: number
  status: string; deliveredAt: string; eligibleAt: string; isReversal: boolean
}

const getName = (i18n: Record<string, string> | null | undefined) =>
  i18n ? (i18n['tr'] ?? Object.values(i18n)[0] ?? '') : ''
const para = (v: number) => v.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const KATMAN_ADLARI: Record<string, string> = {
  product: 'Ürün-özel', campaign: 'Kampanya', contract_group: 'Sözleşme (grup)',
  group_default: 'Varsayılan (grup)', unconfigured: 'Tanımsız',
}
const katmanAdi = (layer: string) => {
  const [taban, ek] = layer.split('+')
  return (KATMAN_ADLARI[taban] ?? taban) + (ek === 'turnover' ? ' + ciro ayarı' : '')
}
const DURUM_RENK: Record<string, { bg: string; fg: string; ad: string }> = {
  pending: { bg: '#fef9c3', fg: '#854d0e', ad: 'Beklemede' },
  available: { bg: '#dcfce7', fg: '#166534', ad: 'Bakiyede' },
  paid: { bg: '#dbeafe', fg: '#1e40af', ad: 'Ödendi' },
  reversed: { bg: '#fee2e2', fg: '#991b1b', ad: 'Ters kayıt' },
}

export function CommissionPage() {
  const queryClient = useQueryClient()
  const [tab, setTab] = useState<'defaults' | 'contracts' | 'campaigns' | 'settlements'>('defaults')

  // ── Ortak veriler ──
  const { data: groups = [] } = useQuery<ProductGroup[]>({
    queryKey: ['product-groups-simple'],
    queryFn: async () => (await api.get('/catalog/product-groups')).data.data ?? [],
  })
  const { data: suppliers = [] } = useQuery<Account[]>({
    queryKey: ['supplier-accounts'],
    queryFn: async () =>
      (await api.get('/accounts?accountType=supplier&pageSize=200')).data.data?.items ?? [],
  })

  // ── Sekme 1: Varsayılan oranlar ──
  const [defaults, setDefaults] = useState<Record<string, string>>({})
  const [groupSearch, setGroupSearch] = useState('')
  const [defaultsLoaded, setDefaultsLoaded] = useState(false)
  const { data: groupRates = [] } = useQuery<{ productGroupId: string; ratePercent: number }[]>({
    queryKey: ['commission-group-rates'],
    queryFn: async () => (await api.get('/commission/group-rates')).data.data ?? [],
  })
  if (!defaultsLoaded && groupRates.length > 0) {
    setDefaults(Object.fromEntries(groupRates.map(r => [r.productGroupId, String(r.ratePercent)])))
    setDefaultsLoaded(true)
  }

  const saveDefaults = useMutation({
    mutationFn: async () => {
      const items = Object.entries(defaults)
        .filter(([, v]) => v.trim() !== '' && !isNaN(parseFloat(v)))
        .map(([productGroupId, v]) => ({ productGroupId, ratePercent: parseFloat(v) }))
      await api.put('/commission/group-rates', { items })
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['commission-group-rates'] }),
  })

  // ── Sekme 2: Sözleşme ──
  const [supplierId, setSupplierId] = useState('')
  const [contract, setContract] = useState<ContractDto>(bosSozlesme())
  const [productCodeInput, setProductCodeInput] = useState('')
  const [productRateInput, setProductRateInput] = useState('')
  const [productError, setProductError] = useState('')
  const [contractGroupSearch, setContractGroupSearch] = useState('')
  const { data: loadedContract, isFetched: contractFetched } = useQuery<ContractDto | null>({
    queryKey: ['supplier-contract', supplierId],
    queryFn: async () => (await api.get(`/commission/suppliers/${supplierId}/contract`)).data.data,
    enabled: !!supplierId,
  })
  const [loadedContractState, setLoadedContractState] = useState<{
    supplierId: string
    contract: ContractDto | null | undefined
  } | null>(null)
  if (contractFetched && (
    loadedContractState?.supplierId !== supplierId || loadedContractState.contract !== loadedContract
  )) {
    setLoadedContractState({ supplierId, contract: loadedContract })
    setContract(loadedContract ? { ...bosSozlesme(), ...loadedContract,
      groupRates: loadedContract.groupRates ?? [], productRates: loadedContract.productRates ?? [],
      turnoverTiers: loadedContract.turnoverTiers ?? [] } : bosSozlesme())
  }

  const saveContract = useMutation({
    mutationFn: async () => { await api.put(`/commission/suppliers/${supplierId}/contract`, contract) },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['supplier-contract', supplierId] }),
  })

  const urunEkle = async () => {
    setProductError('')
    const kod = productCodeInput.trim(); const oran = parseFloat(productRateInput)
    if (!kod || isNaN(oran)) { setProductError('Ürün kodu ve oran gerekli.'); return }
    try {
      const { data } = await api.get(`/catalog/products/${encodeURIComponent(kod)}`)
      const pid = data.data?.id
      if (!pid) { setProductError('Ürün bulunamadı.'); return }
      setContract(prev => ({
        ...prev,
        productRates: [...prev.productRates.filter(r => r.productId !== pid), { productId: pid, ratePercent: oran }],
      }))
      setProductCodeInput(''); setProductRateInput('')
    } catch { setProductError('Ürün bulunamadı.') }
  }

  // ── Sekme 3: Kampanyalar ──
  const { data: campaigns = [] } = useQuery<CampaignRow[]>({
    queryKey: ['commission-campaigns'],
    queryFn: async () => (await api.get('/commission/campaigns')).data.data ?? [],
    enabled: tab === 'campaigns',
  })
  type CampaignEdit = { rate: string; share: string; optIn: boolean }
  const [campaignEdits, setCampaignEdits] = useState<Record<string, CampaignEdit>>({})
  const campaignEditOf = (campaign: CampaignRow): CampaignEdit => campaignEdits[campaign.id] ?? {
    rate: campaign.supplierCommissionRate == null ? '' : String(campaign.supplierCommissionRate),
    share: String(campaign.supplierDiscountSharePercent),
    optIn: campaign.requiresSupplierOptIn,
  }
  const campaignTerms = useMutation({
    mutationFn: async (c: CampaignRow) => {
      const e = campaignEditOf(c)
      await api.put(`/commission/campaigns/${c.id}/supplier-terms`, {
        supplierCommissionRate: e.rate.trim() === '' ? null : parseFloat(e.rate),
        supplierDiscountSharePercent: e.share.trim() === '' ? 0 : parseFloat(e.share),
        requiresSupplierOptIn: e.optIn,
      })
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['commission-campaigns'] }),
  })

  // ── Sekme 4: Hakedişler ──
  const [setSupplier, setSetSupplier] = useState('')
  const [setStatus, setSetStatus] = useState('')
  const { data: settlements } = useQuery<{ items: SettlementLine[]; totalCount: number }>({
    queryKey: ['commission-settlements', setSupplier, setStatus],
    queryFn: async () => {
      const p = new URLSearchParams({ supplierAccountId: setSupplier, pageSize: '100' })
      if (setStatus) p.set('status', setStatus)
      return (await api.get(`/commission/settlements?${p}`)).data.data
    },
    enabled: tab === 'settlements' && !!setSupplier,
  })
  const { data: statement } = useQuery<{ balance: number; currency: string }>({
    queryKey: ['commission-statement', setSupplier],
    queryFn: async () => (await api.get(`/commission/suppliers/${setSupplier}/statement?pageSize=1`)).data.data,
    enabled: tab === 'settlements' && !!setSupplier,
  })
  const payout = useMutation({
    mutationFn: async () => (await api.post(`/commission/suppliers/${setSupplier}/payout`)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['commission-settlements'] })
      queryClient.invalidateQueries({ queryKey: ['commission-statement'] })
    },
  })

  const supplierOptions = suppliers.map(s => ({ value: s.id, label: `${s.code} — ${s.title}` }))

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-lg font-bold" style={{ color: 'var(--text)' }}>Komisyon Yönetimi</h1>
          <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
            Pazaryeri satıcı komisyonları: varsayılan oranlar, satıcı sözleşmeleri, kampanya şartları ve hakediş mutabakatı
          </p>
        </div>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        <button className={cn('stab', tab === 'defaults' && 'active')} onClick={() => setTab('defaults')}>Varsayılan Oranlar</button>
        <button className={cn('stab', tab === 'contracts' && 'active')} onClick={() => setTab('contracts')}>Satıcı Sözleşmeleri</button>
        <button className={cn('stab', tab === 'campaigns' && 'active')} onClick={() => setTab('campaigns')}>Kampanyalar</button>
        <button className={cn('stab', tab === 'settlements' && 'active')} onClick={() => setTab('settlements')}>Hakedişler</button>
      </div>

      {tab === 'defaults' && (
        <div className="card p-5 max-w-4xl">
          <div className="flex flex-wrap items-center justify-between gap-3 mb-4">
            <p className="text-xs max-w-xl" style={{ color: 'var(--text-s)' }}>
              Ürün grubu bazlı platform varsayılanları (katman 5) — satıcıya özel oran yoksa bu uygulanır.
              Boş bırakılan grupta oran tanımsız kalır (satış kesintisiz yazılır).
            </p>
            <input className="inp !w-56" placeholder="Grup ara…" value={groupSearch}
              onChange={e => setGroupSearch(e.target.value)} />
          </div>
          <div className="grid gap-x-10 sm:grid-cols-2">
            {groups
              .filter(g => !groupSearch.trim()
                || (getName(g.nameI18n) || g.code).toLocaleLowerCase('tr').includes(groupSearch.toLocaleLowerCase('tr')))
              .map(g => (
                <div key={g.id} className="flex items-center justify-between gap-4 py-1.5"
                  style={{ borderBottom: '1px solid var(--border)' }}>
                  <span className="text-sm truncate" style={{ color: 'var(--text)' }}>{getName(g.nameI18n) || g.code}</span>
                  <div className="flex items-center gap-1.5 shrink-0">
                    <input className="inp !w-20 text-right" type="number" step="0.1" min="0" max="100"
                      value={defaults[g.id] ?? ''} placeholder="—"
                      onChange={e => setDefaults(prev => ({ ...prev, [g.id]: e.target.value }))} />
                    <span className="text-sm w-3" style={{ color: 'var(--text-s)' }}>%</span>
                  </div>
                </div>
              ))}
          </div>
          <div className="mt-5 flex items-center gap-3">
            <Button onClick={() => saveDefaults.mutate()} loading={saveDefaults.isPending}>Kaydet</Button>
            {saveDefaults.isSuccess && <span className="text-xs" style={{ color: '#16a34a' }}>Kaydedildi</span>}
            {saveDefaults.isError && <span className="text-xs" style={{ color: '#dc2626' }}>Kaydedilemedi</span>}
          </div>
        </div>
      )}

      {tab === 'contracts' && (
        <div className="grid gap-4 max-w-4xl">
          <div className="card p-5">
            <label className="flbl">Satıcı (cari hesap)</label>
            <select className="sel w-full" value={supplierId} onChange={e => setSupplierId(e.target.value)}>
              <option value="">Satıcı seçin…</option>
              {supplierOptions.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </div>
          {supplierId && (
            <>
              <div className="card p-5 grid gap-4 sm:grid-cols-2">
                <div>
                  <label className="flbl">Hakediş gecikmesi (teslim + X gün)</label>
                  <input className="inp w-full" type="number" min="0" max="365" value={contract.settlementDelayDays}
                    onChange={e => setContract(p => ({ ...p, settlementDelayDays: parseInt(e.target.value) || 0 }))} />
                </div>
                <div>
                  <label className="flbl">Ödeme periyodu</label>
                  <select className="sel w-full" value={contract.payoutPeriod}
                    onChange={e => setContract(p => ({ ...p, payoutPeriod: e.target.value }))}>
                    <option value="weekly">Haftalık</option>
                    <option value="monthly">Aylık</option>
                    <option value="immediate">Uygunlaşınca</option>
                  </select>
                </div>
                <div>
                  <label className="flbl">Kargo modu (K3)</label>
                  <select className="sel w-full" value={contract.cargoMode}
                    onChange={e => setContract(p => ({ ...p, cargoMode: e.target.value }))}>
                    <option value="platform_contract">Bizim sözleşmemizle biz göndeririz</option>
                    <option value="seller_ships">Satıcı kendi sözleşmesiyle gönderir</option>
                    <option value="seller_contract_we_ship">Satıcı sözleşmesiyle biz göndeririz</option>
                  </select>
                  <p className="text-[11px] mt-1" style={{ color: 'var(--text-s)' }}>
                    "Satıcı gönderir" seçilirse API hesabına kargo bildirimi yetkisi açılır.
                  </p>
                </div>
                <div>
                  <label className="flbl">Ciro basamağı dönemi</label>
                  <select className="sel w-full" value={contract.turnoverPeriodType}
                    onChange={e => setContract(p => ({ ...p, turnoverPeriodType: e.target.value }))}>
                    <option value="monthly">Aylık</option>
                    <option value="yearly">Yıllık</option>
                    <option value="rolling12">Kayan 12 ay</option>
                  </select>
                </div>
                <div className="sm:col-span-2">
                  <label className="flbl">Notlar</label>
                  <input className="inp w-full" value={contract.notes ?? ''}
                    onChange={e => setContract(p => ({ ...p, notes: e.target.value || null }))} />
                </div>
              </div>

              <div className="card p-5">
                <div className="flex flex-wrap items-center justify-between gap-3 mb-3">
                  <div className="text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>
                    Gruba özel oranlar (katman 3) — boş bırakılan grup varsayılanı kullanır
                  </div>
                  <input className="inp !w-56" placeholder="Grup ara…" value={contractGroupSearch}
                    onChange={e => setContractGroupSearch(e.target.value)} />
                </div>
                <div className="grid gap-x-10 sm:grid-cols-2">
                {groups
                  .filter(g => !contractGroupSearch.trim()
                    || (getName(g.nameI18n) || g.code).toLocaleLowerCase('tr').includes(contractGroupSearch.toLocaleLowerCase('tr')))
                  .map(g => {
                  const mevcut = contract.groupRates.find(r => r.productGroupId === g.id)
                  return (
                    <div key={g.id} className="flex items-center justify-between gap-4 py-1.5"
                      style={{ borderBottom: '1px solid var(--border)' }}>
                      <span className="text-sm truncate" style={{ color: 'var(--text)' }}>{getName(g.nameI18n) || g.code}</span>
                      <div className="flex items-center gap-1.5 shrink-0">
                      <input className="inp !w-20 text-right" type="number" step="0.1" min="0" max="100"
                        value={mevcut ? String(mevcut.ratePercent) : ''} placeholder="—"
                        onChange={e => {
                          const v = e.target.value
                          setContract(p => ({
                            ...p,
                            groupRates: v.trim() === ''
                              ? p.groupRates.filter(r => r.productGroupId !== g.id)
                              : [...p.groupRates.filter(r => r.productGroupId !== g.id),
                                 { productGroupId: g.id, ratePercent: parseFloat(v) || 0 }],
                          }))
                        }} />
                      <span className="text-sm w-3" style={{ color: 'var(--text-s)' }}>%</span>
                      </div>
                    </div>
                  )
                })}
                </div>
              </div>

              <div className="card p-5">
                <div className="text-xs font-semibold uppercase tracking-wider mb-3" style={{ color: 'var(--text-s)' }}>
                  Ürüne özel oranlar (katman 1 — her şeyi ezer)
                </div>
                {contract.productRates.map(r => (
                  <div key={r.productId} className="flex items-center gap-3 py-1 text-sm" style={{ color: 'var(--text)' }}>
                    <code className="text-xs flex-1">{r.productId}</code>
                    <span>%{r.ratePercent}</span>
                    <button className="text-xs" style={{ color: '#dc2626' }}
                      onClick={() => setContract(p => ({ ...p, productRates: p.productRates.filter(x => x.productId !== r.productId) }))}>
                      Kaldır
                    </button>
                  </div>
                ))}
                <div className="flex items-center gap-2 mt-2">
                  <input className="inp flex-1" placeholder="Ürün kodu (PRD-…)" value={productCodeInput}
                    onChange={e => setProductCodeInput(e.target.value)} />
                  <input className="inp !w-24 text-right" type="number" step="0.1" placeholder="%" value={productRateInput}
                    onChange={e => setProductRateInput(e.target.value)} />
                  <Button size="sm" variant="secondary" onClick={urunEkle}>Ekle</Button>
                </div>
                {productError && <p className="text-xs mt-1" style={{ color: '#dc2626' }}>{productError}</p>}
              </div>

              <div className="card p-5">
                <div className="text-xs font-semibold uppercase tracking-wider mb-3" style={{ color: 'var(--text-s)' }}>
                  Ciro basamakları (katman 4 — grup oranına puan ayarı; negatif = indirim)
                </div>
                {contract.turnoverTiers.map((t, i) => (
                  <div key={i} className="flex items-center gap-2 py-1">
                    <span className="text-xs" style={{ color: 'var(--text-s)' }}>Ciro ≥</span>
                    <input className="inp !w-32 text-right" type="number" value={t.minTurnover}
                      onChange={e => setContract(p => ({ ...p, turnoverTiers: p.turnoverTiers.map((x, j) => j === i ? { ...x, minTurnover: parseFloat(e.target.value) || 0 } : x) }))} />
                    <span className="text-xs" style={{ color: 'var(--text-s)' }}>TL → oran</span>
                    <input className="inp !w-24 text-right" type="number" step="0.1" value={t.rateAdjustmentPercent}
                      onChange={e => setContract(p => ({ ...p, turnoverTiers: p.turnoverTiers.map((x, j) => j === i ? { ...x, rateAdjustmentPercent: parseFloat(e.target.value) || 0 } : x) }))} />
                    <span className="text-xs" style={{ color: 'var(--text-s)' }}>puan</span>
                    <button className="text-xs ml-2" style={{ color: '#dc2626' }}
                      onClick={() => setContract(p => ({ ...p, turnoverTiers: p.turnoverTiers.filter((_, j) => j !== i) }))}>
                      Kaldır
                    </button>
                  </div>
                ))}
                <Button size="sm" variant="secondary" className="mt-2"
                  onClick={() => setContract(p => ({ ...p, turnoverTiers: [...p.turnoverTiers, { minTurnover: 0, rateAdjustmentPercent: 0 }] }))}>
                  Basamak Ekle
                </Button>
              </div>

              <div className="flex items-center gap-3">
                <Button onClick={() => saveContract.mutate()} loading={saveContract.isPending}>Sözleşmeyi Kaydet</Button>
                {saveContract.isSuccess && <span className="text-xs" style={{ color: '#16a34a' }}>Kaydedildi</span>}
                {saveContract.isError && (
                  <span className="text-xs" style={{ color: '#dc2626' }}>
                    {(saveContract.error as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Kaydedilemedi'}
                  </span>
                )}
              </div>
            </>
          )}
        </div>
      )}

      {tab === 'campaigns' && (
        <div className="card p-5 overflow-x-auto">
          <p className="text-xs mb-3" style={{ color: 'var(--text-s)' }}>
            Kampanya penceresi komisyon oranı (katman 2), indirim yükünün satıcı payı ve satıcı katılım (opt-in) şartı.
          </p>
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-xs uppercase tracking-wider" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>
                <th className="py-2 pr-3 font-semibold">Kampanya</th>
                <th className="py-2 pr-3 font-semibold">Komisyon %</th>
                <th className="py-2 pr-3 font-semibold">İndirim payı %</th>
                <th className="py-2 pr-3 font-semibold">Opt-in</th>
                <th className="py-2 pr-3 font-semibold">Katılım</th>
                <th className="py-2 font-semibold"></th>
              </tr>
            </thead>
            <tbody>
              {campaigns.map(c => {
                const e = campaignEditOf(c)
                return (
                  <tr key={c.id} style={{ borderBottom: '1px solid var(--border)', color: 'var(--text)' }}>
                    <td className="py-2 pr-3">
                      <div className="font-medium">{getName(c.nameI18n) || c.code}</div>
                      <div className="text-xs" style={{ color: 'var(--text-s)' }}>{c.code}</div>
                    </td>
                    <td className="py-2 pr-3">
                      <input className="inp !w-20 text-right" type="number" step="0.1" placeholder="—" value={e.rate}
                        onChange={ev => setCampaignEdits(p => ({ ...p, [c.id]: { ...e, rate: ev.target.value } }))} />
                    </td>
                    <td className="py-2 pr-3">
                      <input className="inp !w-20 text-right" type="number" step="1" value={e.share}
                        onChange={ev => setCampaignEdits(p => ({ ...p, [c.id]: { ...e, share: ev.target.value } }))} />
                    </td>
                    <td className="py-2 pr-3">
                      <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={e.optIn}
                        onChange={ev => setCampaignEdits(p => ({ ...p, [c.id]: { ...e, optIn: ev.target.checked } }))} />
                    </td>
                    <td className="py-2 pr-3 text-xs" style={{ color: 'var(--text-s)' }}>{c.participantCount} satıcı</td>
                    <td className="py-2">
                      <Button size="sm" variant="secondary" onClick={() => campaignTerms.mutate(c)}>Kaydet</Button>
                    </td>
                  </tr>
                )
              })}
              {campaigns.length === 0 && (
                <tr><td colSpan={6} className="py-6 text-center text-sm" style={{ color: 'var(--text-s)' }}>Aktif kampanya yok.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {tab === 'settlements' && (
        <div className="grid gap-4">
          <div className="card p-5 flex flex-wrap items-end gap-3">
            <div className="min-w-[260px]">
              <label className="flbl">Satıcı</label>
              <select className="sel w-full" value={setSupplier} onChange={e => setSetSupplier(e.target.value)}>
                <option value="">Satıcı seçin…</option>
                {supplierOptions.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
            </div>
            <div>
              <label className="flbl">Durum</label>
              <select className="sel" value={setStatus} onChange={e => setSetStatus(e.target.value)}>
                <option value="">Tümü</option>
                <option value="pending">Beklemede</option>
                <option value="available">Bakiyede</option>
                <option value="paid">Ödendi</option>
                <option value="reversed">Ters kayıt</option>
              </select>
            </div>
            {setSupplier && statement && (
              <div className="ml-auto flex items-center gap-4">
                <div className="text-sm" style={{ color: 'var(--text)' }}>
                  Hakediş bakiyesi: <strong>{para(statement.balance)} {statement.currency}</strong>
                </div>
                <Button onClick={() => payout.mutate()} loading={payout.isPending}>Ödeme Çıkışı</Button>
              </div>
            )}
          </div>
          {payout.isError && (
            <div className="text-xs" style={{ color: '#dc2626' }}>
              {(payout.error as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Ödeme kaydedilemedi'}
            </div>
          )}
          {payout.isSuccess && (
            <div className="text-xs" style={{ color: '#16a34a' }}>
              Ödeme kaydedildi: {(payout.data as { data?: { paidLines?: number; totalNet?: number } })?.data?.paidLines} satır,
              net {para((payout.data as { data?: { totalNet?: number } })?.data?.totalNet ?? 0)} TL
            </div>
          )}
          {setSupplier && (
            <div className="card p-5 overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-xs uppercase tracking-wider" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>
                    <th className="py-2 pr-3 font-semibold">Sipariş / SKU</th>
                    <th className="py-2 pr-3 font-semibold text-right">Brüt</th>
                    <th className="py-2 pr-3 font-semibold">Oran (katman)</th>
                    <th className="py-2 pr-3 font-semibold text-right">Komisyon</th>
                    <th className="py-2 pr-3 font-semibold text-right">İndirim payı</th>
                    <th className="py-2 pr-3 font-semibold text-right">Net</th>
                    <th className="py-2 font-semibold">Durum</th>
                  </tr>
                </thead>
                <tbody>
                  {(settlements?.items ?? []).map(l => {
                    const d = DURUM_RENK[l.status] ?? DURUM_RENK.pending
                    return (
                      <tr key={l.id} style={{ borderBottom: '1px solid var(--border)', color: 'var(--text)' }}>
                        <td className="py-2 pr-3">
                          <div className="font-medium">{l.orderNumber}{l.isReversal && ' (iade)'}</div>
                          <div className="text-xs" style={{ color: 'var(--text-s)' }}>{l.sku} × {l.quantity}</div>
                        </td>
                        <td className="py-2 pr-3 text-right whitespace-nowrap">{para(l.grossAmount)}</td>
                        <td className="py-2 pr-3 text-xs">%{l.commissionRate} — {katmanAdi(l.commissionLayer)}</td>
                        <td className="py-2 pr-3 text-right whitespace-nowrap">{para(l.commissionAmount)}</td>
                        <td className="py-2 pr-3 text-right whitespace-nowrap">{para(l.campaignDiscountShareAmount)}</td>
                        <td className="py-2 pr-3 text-right whitespace-nowrap font-medium">{para(l.netAmount)}</td>
                        <td className="py-2">
                          <span className="text-xs px-2 py-0.5 rounded-full whitespace-nowrap" style={{ background: d.bg, color: d.fg }}>{d.ad}</span>
                        </td>
                      </tr>
                    )
                  })}
                  {(settlements?.items ?? []).length === 0 && (
                    <tr><td colSpan={7} className="py-6 text-center text-sm" style={{ color: 'var(--text-s)' }}>Hakediş satırı yok.</td></tr>
                  )}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

function bosSozlesme(): ContractDto {
  return {
    settlementDelayDays: 14, payoutPeriod: 'weekly', cargoMode: 'platform_contract',
    turnoverPeriodType: 'monthly', isActive: true, notes: null,
    groupRates: [], productRates: [], turnoverTiers: [],
  }
}

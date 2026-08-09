import { useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { Link } from 'react-router-dom'
import { ORDER_STATUS_MAP, PAYMENT_STATUS_MAP, PAYMENT_METHOD_MAP } from './OrdersPage'
import { RETURN_STATUS_MAP, type ReturnSummary } from './ReturnsPage'
import { INVOICE_STATUS_MAP, INVOICE_TYPE_MAP, type InvoiceSummary, type InvoiceSeries } from './InvoicesPage'
import { OrderPackagesSection } from './OrderPackagesSection'

interface OrderItem {
  id: string
  variantId: string
  sku: string
  productName: string
  variantInfo: string
  quantity: number
  unitPrice: number
  discountAmount: number
  taxAmount: number
  total: number
  status: string
}

interface OrderPayment {
  id: string
  paymentMethodId: string
  amount: number
  currencyCode: string
  status: string
}

interface AcceptedContract {
  code: string
  title: string
  acceptedAt: string
  contentUpdatedAt?: string
}

interface OrderDetail {
  id: string
  orderNumber: string
  memberId: string | null
  status: string
  paymentStatus: string
  paymentMethod?: string | null
  orderType: string
  currencyCode: string
  subtotal: number
  totalDiscount: number
  totalTax: number
  grandTotal: number
  shippingRecipientName: string
  shippingRecipientPhone: string
  shippingAddressLine: string
  pickingPlanId?: string
  internalNotes?: string
  createdAt: string
  confirmedAt?: string
  items: OrderItem[]
  payments: OrderPayment[]
  firmPlatformId?: string
  totalExpense?: number
  shippingPostalCode?: string
  shippingDeliveryNotes?: string
  shippingCityId?: string
  shippingDistrictId?: string
  shippingNeighborhoodId?: string
  billingSameAsShipping?: boolean
  billingRecipientName?: string
  billingCompanyName?: string
  billingTaxOffice?: string
  billingTaxNumber?: string
  billingAddressLine?: string
  billingCityId?: string
  billingDistrictId?: string
  customerNotes?: { note?: string; acceptedContracts?: AcceptedContract[] }
  requestedCargoIntegrationId?: string | null
  requestedCargoName?: string | null
}

interface ShipmentEvent {
  id: string
  eventCode: string
  eventDescription: string
  eventLocation?: string
  eventDate: string
}

interface Shipment {
  id: string
  firmIntegrationId: string
  shipmentNumber: string
  trackingNumber?: string
  trackingUrl?: string
  status: string
  estimatedDeliveryDate?: string
  deliveredAt?: string
  packageCount: number
  events: ShipmentEvent[]
  createdAt: string
}

interface PaymentMethod {
  id: string
  code: string
  nameI18n: Record<string, string>
}

interface CargoIntegration {
  id: string
  serviceCode: string
  serviceNameI18n: Record<string, string>
  name?: string
  isActive: boolean
}

interface Warehouse {
  id: string
  code: string
  nameI18n?: Record<string, string>
}

// OP1: fulfillment operasyon günlüğü (toplama görevi / dağıtım / toplama olayları)
interface OperationLog {
  id: string
  orderId?: string | null
  orderItemId?: string | null
  pickingPlanId?: string | null
  packageId?: string | null
  action: string
  actorId: string
  at: string
  detail?: Record<string, unknown> | null
}

/** action → Türkçe cümle; detail'daki plan no / sku / raf bilgisi cümleye katılır. */
function opCumle(l: OperationLog, adCoz: (uid?: string | null) => string | null): string {
  const d = l.detail ?? {}
  const s = (k: string) => (typeof d[k] === 'string' && d[k] ? String(d[k]) : null)
  const sku = s('sku')
  const bin = s('bin') ?? s('binCode')
  const ek = [sku ? ` — ${sku}` : '', bin ? ` (raf ${bin})` : ''].join('')
  switch (l.action) {
    case 'task_created':
      return `Toplama görevine eklendi${s('planNumber') ? ` (${s('planNumber')})` : ''}`
    case 'line_assigned': {
      const kime = adCoz(s('assignedTo'))
      return `Kalem toplama için atandı${kime ? ` → ${kime}` : ''}${ek}`
    }
    case 'line_picked':
      return `Kalem toplandı${ek}`
    case 'line_short':
      return `Kalem eksik işaretlendi${ek}`
    case 'line_returned':
      return `Kalem rafa iade edildi${ek}`
    case 'plan_started':
      return 'Toplama görevi başlatıldı'
    case 'plan_completed':
      return 'Toplama görevi tamamlandı'
    default:
      return l.action
  }
}

function money(v: number | undefined, cur: string) {
  if (v === undefined) return '—'
  return `${v.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ${cur === 'TRY' ? '₺' : cur}`
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="card p-4">
      <h2 className="text-sm font-semibold mb-3" style={{ color: 'var(--text)' }}>{title}</h2>
      {children}
    </div>
  )
}

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  if (value === undefined || value === null || value === '') return null
  return (
    <div className="flex gap-2 text-sm py-0.5">
      <span className="shrink-0 w-32" style={{ color: 'var(--text-s)' }}>{label}</span>
      <span style={{ color: 'var(--text)' }}>{value}</span>
    </div>
  )
}

export function OrderDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [confirmOpen, setConfirmOpen] = useState(false)
  const [cancelOpen, setCancelOpen] = useState(false)
  const [processOpen, setProcessOpen] = useState(false)
  const [shipOpen, setShipOpen] = useState(false)
  const [deliverOpen, setDeliverOpen] = useState(false)
  const [warehouseId, setWarehouseId] = useState('')
  const [cancelReason, setCancelReason] = useState('')
  const [shipIntegrationId, setShipIntegrationId] = useState('')
  const [trackingNumber, setTrackingNumber] = useState('')
  const [packageCount, setPackageCount] = useState(1)
  const [actionError, setActionError] = useState('')
  const [invoiceOpen, setInvoiceOpen] = useState(false)
  const [invSeriesId, setInvSeriesId] = useState('')
  const [invType, setInvType] = useState('e_archive')
  const [invDate, setInvDate] = useState(() => new Date().toISOString().slice(0, 10))
  const [invRecipient, setInvRecipient] = useState('')
  const [invAddress, setInvAddress] = useState('')
  const [invTaxOffice, setInvTaxOffice] = useState('')
  const [invTaxNumber, setInvTaxNumber] = useState('')
  const [invCompany, setInvCompany] = useState('')

  const { data: order, isLoading } = useQuery<OrderDetail>({
    queryKey: ['order-detail', id],
    queryFn: async () => (await api.get(`/orders/${id}`)).data.data,
    enabled: !!id,
  })

  const { data: shipments = [] } = useQuery<Shipment[]>({
    queryKey: ['order-shipments', id],
    queryFn: async () => (await api.get(`/orders/${id}/shipments`)).data.data,
    enabled: !!id,
  })

  const { data: paymentMethods = [] } = useQuery<PaymentMethod[]>({
    queryKey: ['payment-methods'],
    queryFn: async () => (await api.get('/core/payment-methods?activeOnly=false')).data.data,
  })

  // Platform adı + firma id çözümü (kargo anlaşmaları firmaya bağlı)
  const { data: firms = [] } = useQuery<{ id: string; nameI18n: Record<string, string> }[]>({
    queryKey: ['firms'],
    queryFn: async () => (await api.get('/core/firms')).data.data,
  })
  const { data: firmPlatforms = [] } = useQuery<{ id: string; firmId: string; nameI18n: Record<string, string> }[]>({
    queryKey: ['all-firm-platforms', firms.map(f => f.id).join(',')],
    queryFn: async () => {
      const all: { id: string; firmId: string; nameI18n: Record<string, string> }[] = []
      for (const f of firms) {
        const { data } = await api.get(`/core/firms/${f.id}/platforms`)
        for (const p of data.data ?? []) all.push({ ...p, firmId: f.id })
      }
      return all
    },
    enabled: firms.length > 0,
  })
  const platform = firmPlatforms.find(p => p.id === order?.firmPlatformId)

  const { data: cargoIntegrations = [] } = useQuery<CargoIntegration[]>({
    queryKey: ['cargo-integrations', platform?.firmId],
    queryFn: async () =>
      (await api.get(`/core/firms/${platform!.firmId}/integrations?serviceType=cargo`)).data.data,
    enabled: !!platform?.firmId,
  })

  const { data: warehouses = [] } = useQuery<Warehouse[]>({
    queryKey: ['warehouses', true],
    queryFn: async () => (await api.get('/inventory/warehouses')).data.data,
    enabled: confirmOpen,
  })

  // K19: iade + fatura bölümleri (ayrı sayfaların sipariş-içi görünümü)
  const { data: orderReturns = [] } = useQuery<ReturnSummary[]>({
    queryKey: ['order-returns', id],
    queryFn: async () => (await api.get(`/orders/returns?orderId=${id}&pageSize=50`)).data.data.items,
    enabled: !!id,
  })
  const { data: orderInvoices = [] } = useQuery<InvoiceSummary[]>({
    queryKey: ['order-invoices', id],
    queryFn: async () => (await api.get(`/orders/invoices?orderId=${id}&pageSize=50`)).data.data.items,
    enabled: !!id,
  })
  const { data: invoiceSeries = [] } = useQuery<InvoiceSeries[]>({
    queryKey: ['invoice-series-active'],
    queryFn: async () => (await api.get('/orders/invoice-series')).data.data,
    enabled: invoiceOpen,
  })

  // P2c: kabul edilen sözleşmelerin metni sonradan değişti mi? (kod → güncel sürüm tarihi)
  const { data: legalPages = [] } = useQuery<{ code: string; lastContentUpdatedAt?: string }[]>({
    queryKey: ['cms-legal-versions', order?.firmPlatformId],
    queryFn: async () =>
      (await api.get(`/cms/pages?pageType=legal&activeOnly=false&firmPlatformId=${order!.firmPlatformId}`)).data.data,
    enabled: !!order?.firmPlatformId && (order?.customerNotes?.acceptedContracts?.length ?? 0) > 0,
    retry: false,
  })

  // Adres id'leri → görünen adlar
  const geoIds = useMemo(() => {
    if (!order) return []
    return [
      order.shippingCityId, order.shippingDistrictId, order.shippingNeighborhoodId,
      order.billingCityId, order.billingDistrictId,
    ].filter((x): x is string => !!x)
  }, [order])

  const { data: geoNames = {} } = useQuery<Record<string, string>>({
    queryKey: ['geo-names', geoIds.join(',')],
    queryFn: async () => (await api.get(`/crm/geo-names?ids=${geoIds.join(',')}`)).data.data,
    enabled: geoIds.length > 0,
    retry: false, // restart öncesi eski binary'de endpoint yok — adres id'siz gösterilir
  })

  // OP1: operasyon geçmişi (toplama görevi olayları) — eski binary'de endpoint yoksa sessiz geç
  const { data: opLogs = [] } = useQuery<OperationLog[]>({
    queryKey: ['order-operation-logs', id],
    queryFn: async () =>
      (await api.get(`/fulfillment/operation-logs?orderId=${id}&pageSize=100`)).data.data.items,
    enabled: !!id,
    retry: false,
  })
  const { data: opUsers = [] } = useQuery<{ id: string; username: string; firstName: string; lastName: string }[]>({
    queryKey: ['iam-users-select-mini'],
    queryFn: async () => (await api.get('/iam/users?page=1&pageSize=200')).data.data.items,
    enabled: opLogs.length > 0,
    retry: false,
  })
  const opAd = (uid?: string | null) => {
    if (!uid) return null
    const u = opUsers.find(x => x.id === uid)
    return u ? (`${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.username) : null
  }

  function actionMutation(path: string, body?: object) {
    return async () => {
      setActionError('')
      try {
        await api.post(`/orders/${id}/${path}`, body ?? {})
        queryClient.invalidateQueries({ queryKey: ['order-detail', id] })
        queryClient.invalidateQueries({ queryKey: ['order-shipments', id] })
        queryClient.invalidateQueries({ queryKey: ['orders'] })
        queryClient.invalidateQueries({ queryKey: ['order-status-counts'] })
        return true
      } catch (e: unknown) {
        const err = e as { response?: { data?: { error?: string } } }
        setActionError(err.response?.data?.error ?? 'İşlem başarısız oldu.')
        throw e
      }
    }
  }

  const cancelMutation = useMutation({
    mutationFn: async () => actionMutation('cancel', { reason: cancelReason || null })(),
    onSuccess: () => setCancelOpen(false),
  })
  const processMutation = useMutation({
    mutationFn: async () => actionMutation('start-processing', { pickingPlanId: null })(),
    onSuccess: () => setProcessOpen(false),
  })
  const shipMutation = useMutation({
    mutationFn: async () => actionMutation('ship', {
      firmIntegrationId: shipIntegrationId || null,
      trackingNumber: trackingNumber || null,
      packageCount,
    })(),
    onSuccess: () => setShipOpen(false),
  })
  const deliverMutation = useMutation({
    mutationFn: async () => actionMutation('deliver', {})(),
    onSuccess: () => setDeliverOpen(false),
  })

  // confirm modal body'si ayrı (warehouseId form state'i)
  const doConfirm = useMutation({
    mutationFn: async () => actionMutation('confirm', { warehouseId })(),
    onSuccess: () => setConfirmOpen(false),
  })

  const createInvoice = useMutation({
    mutationFn: async () => {
      setActionError('')
      try {
        await api.post(`/orders/${id}/invoices`, {
          invoiceSeriesId: invSeriesId,
          invoiceType: invType,
          invoiceDate: new Date(`${invDate}T12:00:00`).toISOString(),
          recipientName: invRecipient,
          recipientAddress: invAddress,
          recipientTaxOffice: invTaxOffice || null,
          recipientTaxNumber: invTaxNumber || null,
          recipientCompanyName: invCompany || null,
        })
        queryClient.invalidateQueries({ queryKey: ['order-invoices', id] })
        queryClient.invalidateQueries({ queryKey: ['invoices'] })
      } catch (e: unknown) {
        const err = e as { response?: { data?: { error?: string } } }
        setActionError(err.response?.data?.error ?? 'Fatura oluşturulamadı.')
        throw e
      }
    },
    onSuccess: () => setInvoiceOpen(false),
  })

  if (isLoading || !order) return <PageSpinner />

  const st = ORDER_STATUS_MAP[order.status] ?? { label: order.status, variant: 'neutral' as const }
  const cur = order.currencyCode
  const contracts = order.customerNotes?.acceptedContracts ?? []
  const customerNote = order.customerNotes?.note
  const cargoName = (fid: string) => {
    const ci = cargoIntegrations.find(c => c.id === fid)
    return ci ? (ci.name || ci.serviceNameI18n?.['tr'] || ci.serviceCode) : null
  }

  // Durum geçmişi — eldeki zaman damgalarından türetilir
  const timeline: { label: string; at: string }[] = [
    { label: 'Sipariş oluşturuldu', at: order.createdAt },
    ...(order.confirmedAt ? [{ label: 'Onaylandı', at: order.confirmedAt }] : []),
    ...shipments.map(s => ({ label: `Kargoya verildi (${s.shipmentNumber})`, at: s.createdAt })),
    ...shipments.filter(s => s.deliveredAt).map(s => ({ label: 'Teslim edildi', at: s.deliveredAt! })),
  ].sort((a, b) => a.at.localeCompare(b.at))

  function openInvoiceModal() {
    setActionError('')
    setInvSeriesId('')
    setInvType('e_archive')
    setInvDate(new Date().toISOString().slice(0, 10))
    const ayrıFatura = order!.billingSameAsShipping === false
    setInvRecipient((ayrıFatura ? order!.billingRecipientName : null) ?? order!.shippingRecipientName)
    setInvAddress((ayrıFatura ? order!.billingAddressLine : null) ?? order!.shippingAddressLine)
    setInvTaxOffice(order!.billingTaxOffice ?? '')
    setInvTaxNumber(order!.billingTaxNumber ?? '')
    setInvCompany(order!.billingCompanyName ?? '')
    setInvoiceOpen(true)
  }

  const geo = (gid?: string) => (gid ? geoNames[gid] : undefined)
  const shippingGeo = [geo(order.shippingNeighborhoodId), geo(order.shippingDistrictId), geo(order.shippingCityId)]
    .filter(Boolean).join(' / ')
  const billingGeo = [geo(order.billingDistrictId), geo(order.billingCityId)].filter(Boolean).join(' / ')

  return (
    <div className="p-6 max-w-5xl">
      {/* Başlık + aksiyonlar */}
      <div className="flex flex-wrap items-center gap-3 mb-1">
        <button onClick={() => navigate('/orders')} className="text-sm" style={{ color: 'var(--text-s)' }}>←</button>
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>{order.orderNumber}</h1>
        <Badge variant={st.variant}>{st.label}</Badge>
        <div className="ml-auto flex flex-wrap gap-2">
          {order.status === 'pending' && (
            <Button size="sm" onClick={() => { setActionError(''); setConfirmOpen(true) }}>Onayla</Button>
          )}
          {order.status === 'confirmed' && (
            <Button size="sm" onClick={() => { setActionError(''); setProcessOpen(true) }}>İşleme Al</Button>
          )}
          {order.status === 'processing' && (
            <Button size="sm" onClick={() => {
              setActionError('')
              // Müşterinin teslimat adımındaki kargo tercihi varsayılan gelir (bağlayıcı değil)
              if (!shipIntegrationId && order?.requestedCargoIntegrationId) setShipIntegrationId(order.requestedCargoIntegrationId)
              setShipOpen(true)
            }}>Kargoya Ver</Button>
          )}
          {order.status === 'shipped' && (
            <Button size="sm" onClick={() => { setActionError(''); setDeliverOpen(true) }}>Teslim Edildi</Button>
          )}
          {['pending', 'confirmed'].includes(order.status) && (
            <Button size="sm" variant="danger" onClick={() => { setActionError(''); setCancelOpen(true) }}>İptal Et</Button>
          )}
        </div>
      </div>

      {/* Üst bilgi satırı */}
      <p className="text-sm mb-5" style={{ color: 'var(--text-s)' }}>
        {order.shippingRecipientName} · {new Date(order.createdAt).toLocaleString('tr-TR')}
        {' · '}Ödeme: {order.paymentMethod
          ? `${PAYMENT_METHOD_MAP[order.paymentMethod] ?? order.paymentMethod} · `
          : ''}{PAYMENT_STATUS_MAP[order.paymentStatus] ?? order.paymentStatus}
        {platform && <> · Platform: {platform.nameI18n?.['tr'] ?? '—'}</>}
        {order.orderType !== 'retail' && <> · Tip: {order.orderType}</>}
      </p>

      <div className="grid gap-4 lg:grid-cols-3">
        {/* Sol sütun: kalemler + ödemeler + adresler + kargo + notlar + geçmiş */}
        <div className="lg:col-span-2 space-y-4">
          <Section title={`Kalemler (${order.items.length})`}>
            <table className="w-full text-sm">
              <thead>
                <tr style={{ borderBottom: '1px solid var(--border)' }}>
                  {['ÜRÜN', 'ADET', 'BİRİM', 'İND.', 'TUTAR'].map(h => (
                    <th key={h} className="text-left pb-2 text-xs font-semibold" style={{ color: 'var(--text-s)' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {order.items.map(i => (
                  <tr key={i.id} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td className="py-2 pr-2">
                      <div style={{ color: 'var(--text)' }}>{i.productName}</div>
                      <div className="text-xs" style={{ color: 'var(--text-s)' }}>
                        {i.sku}{i.variantInfo ? ` · ${i.variantInfo}` : ''}
                      </div>
                    </td>
                    <td className="py-2" style={{ color: 'var(--text-m)' }}>{i.quantity}</td>
                    <td className="py-2" style={{ color: 'var(--text-m)' }}>{money(i.unitPrice, cur)}</td>
                    <td className="py-2" style={{ color: 'var(--text-m)' }}>
                      {i.discountAmount > 0 ? `-${money(i.discountAmount, cur)}` : '—'}
                    </td>
                    <td className="py-2 font-medium" style={{ color: 'var(--text)' }}>{money(i.total, cur)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Section>

          <Section title="Ödemeler">
            {order.payments.length === 0 && (
              <p className="text-sm" style={{ color: 'var(--text-s)' }}>Ödeme kaydı yok.</p>
            )}
            {order.payments.map(p => {
              const pm = paymentMethods.find(m => m.id === p.paymentMethodId)
              return (
                <div key={p.id} className="flex items-center gap-3 text-sm py-1.5"
                  style={{ borderBottom: '1px solid var(--border)' }}>
                  <span style={{ color: 'var(--text)' }}>{pm?.nameI18n?.['tr'] ?? pm?.code ?? 'Ödeme'}</span>
                  <span className="font-medium" style={{ color: 'var(--text)' }}>{money(p.amount, p.currencyCode)}</span>
                  <span className="text-xs ml-auto" style={{ color: 'var(--text-s)' }}>
                    {PAYMENT_STATUS_MAP[p.status] ?? p.status}
                  </span>
                </div>
              )
            })}
          </Section>

          <Section title="Adresler">
            <div className="grid sm:grid-cols-2 gap-4">
              <div>
                <h3 className="text-xs font-semibold mb-1.5" style={{ color: 'var(--text-s)' }}>TESLİMAT</h3>
                <InfoRow label="Alıcı" value={order.shippingRecipientName} />
                <InfoRow label="Telefon" value={order.shippingRecipientPhone} />
                <InfoRow label="Adres" value={order.shippingAddressLine} />
                <InfoRow label="Bölge" value={shippingGeo || undefined} />
                <InfoRow label="Posta Kodu" value={order.shippingPostalCode} />
                <InfoRow label="Teslimat Notu" value={order.shippingDeliveryNotes} />
              </div>
              <div>
                <h3 className="text-xs font-semibold mb-1.5" style={{ color: 'var(--text-s)' }}>FATURA</h3>
                {order.billingSameAsShipping !== false ? (
                  <p className="text-sm" style={{ color: 'var(--text-s)' }}>Teslimat adresiyle aynı.</p>
                ) : (
                  <>
                    <InfoRow label="Ad" value={order.billingRecipientName} />
                    <InfoRow label="Firma" value={order.billingCompanyName} />
                    <InfoRow label="Vergi D." value={order.billingTaxOffice} />
                    <InfoRow label="Vergi No" value={order.billingTaxNumber} />
                    <InfoRow label="Adres" value={order.billingAddressLine} />
                    <InfoRow label="Bölge" value={billingGeo || undefined} />
                  </>
                )}
              </div>
            </div>
          </Section>

          <OrderPackagesSection
            orderId={order.id}
            orderStatus={order.status}
            cargoIntegrations={cargoIntegrations}
          />

          <Section title="Kargo">
            {shipments.length === 0 && (
              <p className="text-sm" style={{ color: 'var(--text-s)' }}>Henüz gönderi yok.</p>
            )}
            {shipments.map(s => (
              <div key={s.id} className="py-2" style={{ borderBottom: '1px solid var(--border)' }}>
                <div className="flex flex-wrap items-center gap-2 text-sm">
                  <code className="text-xs font-mono" style={{ color: 'var(--text)' }}>{s.shipmentNumber}</code>
                  {cargoName(s.firmIntegrationId) && (
                    <span style={{ color: 'var(--text-m)' }}>{cargoName(s.firmIntegrationId)}</span>
                  )}
                  {s.trackingNumber && (
                    s.trackingUrl
                      ? <a href={s.trackingUrl} target="_blank" rel="noreferrer" className="underline"
                          style={{ color: 'var(--brand)' }}>{s.trackingNumber}</a>
                      : <span style={{ color: 'var(--text-m)' }}>{s.trackingNumber}</span>
                  )}
                  <span className="text-xs" style={{ color: 'var(--text-s)' }}>{s.packageCount} paket</span>
                  <span className="text-xs ml-auto" style={{ color: 'var(--text-s)' }}>
                    {new Date(s.createdAt).toLocaleString('tr-TR')}
                  </span>
                </div>
                {s.events.length > 0 && (
                  <ul className="mt-1.5 ml-1 space-y-0.5">
                    {s.events.map(e => (
                      <li key={e.id} className="text-xs" style={{ color: 'var(--text-s)' }}>
                        {new Date(e.eventDate).toLocaleString('tr-TR')} — {e.eventDescription}
                        {e.eventLocation ? ` (${e.eventLocation})` : ''}
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            ))}
          </Section>

          <Section title={`Faturalar (${orderInvoices.length})`}>
            {orderInvoices.map(inv => {
              const ist = INVOICE_STATUS_MAP[inv.status] ?? { label: inv.status, variant: 'neutral' as const }
              return (
                <div key={inv.id} className="flex flex-wrap items-center gap-2 text-sm py-1.5"
                  style={{ borderBottom: '1px solid var(--border)' }}>
                  <code className="text-xs font-mono" style={{ color: 'var(--text)' }}>{inv.invoiceNumber}</code>
                  <span className="text-xs" style={{ color: 'var(--text-s)' }}>
                    {INVOICE_TYPE_MAP[inv.invoiceType] ?? inv.invoiceType}
                  </span>
                  <span className="font-medium" style={{ color: 'var(--text)' }}>{money(inv.grandTotal, cur)}</span>
                  <Badge variant={ist.variant}>{ist.label}</Badge>
                  {inv.hasIntegratorPdf && <span className="text-xs" style={{ color: 'var(--text-s)' }}>PDF ✓</span>}
                  <Link to="/orders/invoices" className="text-xs ml-auto underline" style={{ color: 'var(--brand)' }}>
                    Faturalarda aç →
                  </Link>
                </div>
              )
            })}
            {orderInvoices.length === 0 && (
              <p className="text-sm mb-2" style={{ color: 'var(--text-s)' }}>Bu sipariş için fatura kesilmedi.</p>
            )}
            <div className="mt-2">
              <Button size="sm" variant="secondary" onClick={openInvoiceModal}>+ Fatura Oluştur</Button>
            </div>
          </Section>

          {orderReturns.length > 0 && (
            <Section title={`İadeler (${orderReturns.length})`}>
              {orderReturns.map(r => {
                const rst = RETURN_STATUS_MAP[r.status] ?? { label: r.status, variant: 'neutral' as const }
                return (
                  <Link key={r.id} to={`/orders/returns/${r.id}`}
                    className="flex flex-wrap items-center gap-2 text-sm py-1.5 hover:bg-[var(--surface2)] rounded-lg px-1"
                    style={{ borderBottom: '1px solid var(--border)' }}>
                    <code className="text-xs font-mono" style={{ color: 'var(--text)' }}>{r.returnNumber}</code>
                    <span className="font-medium" style={{ color: 'var(--text)' }}>
                      {r.refundAmount.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺
                    </span>
                    <Badge variant={rst.variant}>{rst.label}</Badge>
                    <span className="text-xs ml-auto" style={{ color: 'var(--text-s)' }}>Detay →</span>
                  </Link>
                )
              })}
            </Section>
          )}

          <Section title="Notlar ve Sözleşmeler">
            <InfoRow label="Müşteri Notu" value={customerNote} />
            <InfoRow label="İç Not" value={order.internalNotes} />
            {contracts.length > 0 && (
              <div className="mt-2">
                <h3 className="text-xs font-semibold mb-1" style={{ color: 'var(--text-s)' }}>KABUL EDİLEN SÖZLEŞMELER</h3>
                {contracts.map(c => {
                  const sayfa = legalPages.find(p => p.code === c.code)
                  const sonradanDegisti = !!(sayfa?.lastContentUpdatedAt && c.contentUpdatedAt
                    && new Date(sayfa.lastContentUpdatedAt) > new Date(c.contentUpdatedAt))
                  return (
                    <div key={c.code} className="text-sm py-0.5" style={{ color: 'var(--text-m)' }}>
                      {c.title} <span className="text-xs" style={{ color: 'var(--text-s)' }}>
                        — {new Date(c.acceptedAt).toLocaleString('tr-TR')}
                        {c.contentUpdatedAt ? ` (metin sürümü: ${new Date(c.contentUpdatedAt).toLocaleDateString('tr-TR')})` : ''}
                      </span>
                      {sonradanDegisti && (
                        <span className="text-xs ml-1 px-1.5 py-0.5 rounded-full"
                          style={{ background: 'var(--surface2)', color: 'var(--text-m)' }}
                          title={`Güncel metin: ${new Date(sayfa!.lastContentUpdatedAt!).toLocaleString('tr-TR')}`}>
                          ⚠ metin bu kabulden sonra güncellendi
                        </span>
                      )}
                    </div>
                  )
                })}
              </div>
            )}
            {!customerNote && !order.internalNotes && contracts.length === 0 && (
              <p className="text-sm" style={{ color: 'var(--text-s)' }}>Not veya sözleşme kaydı yok.</p>
            )}
          </Section>

          <Section title="Durum Geçmişi">
            <ul className="space-y-1">
              {timeline.map((t, idx) => (
                <li key={idx} className="text-sm flex gap-2">
                  <span className="text-xs w-32 shrink-0" style={{ color: 'var(--text-s)' }}>
                    {new Date(t.at).toLocaleString('tr-TR')}
                  </span>
                  <span style={{ color: 'var(--text)' }}>{t.label}</span>
                </li>
              ))}
            </ul>
          </Section>

          <Section title="Operasyon Geçmişi">
            {opLogs.length === 0 ? (
              <p className="text-sm" style={{ color: 'var(--text-s)' }}>Operasyon kaydı yok.</p>
            ) : (
              <ul className="space-y-1">
                {opLogs.map(l => (
                  <li key={l.id} className="text-sm flex gap-2">
                    <span className="text-xs w-32 shrink-0" style={{ color: 'var(--text-s)' }}>
                      {new Date(l.at).toLocaleString('tr-TR')}
                    </span>
                    <span style={{ color: 'var(--text)' }}>
                      {opCumle(l, opAd)}
                      {opAd(l.actorId) && (
                        <span className="text-xs ml-1.5" style={{ color: 'var(--text-s)' }}>· {opAd(l.actorId)}</span>
                      )}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </Section>
        </div>

        {/* Sağ sütun: özet */}
        <div className="space-y-4">
          <Section title="Özet">
            <InfoRow label="Ara Toplam" value={money(order.subtotal, cur)} />
            <InfoRow label="İndirim" value={order.totalDiscount > 0 ? `-${money(order.totalDiscount, cur)}` : undefined} />
            <InfoRow label="Masraf" value={order.totalExpense ? money(order.totalExpense, cur) : undefined} />
            <InfoRow label="Vergi" value={order.totalTax > 0 ? money(order.totalTax, cur) : undefined} />
            <div className="mt-2 pt-2 flex gap-2 text-sm font-bold" style={{ borderTop: '1px solid var(--border)', color: 'var(--text)' }}>
              <span className="w-32 shrink-0">TOPLAM</span>
              <span>{money(order.grandTotal, cur)}</span>
            </div>
          </Section>
          <Section title="Müşteri">
            <InfoRow label="Alıcı" value={order.shippingRecipientName} />
            <InfoRow label="Üye Id" value={order.memberId
              ? <code className="text-xs">{order.memberId}</code>
              : <Badge variant="neutral">Misafir</Badge>} />
            <InfoRow label="Kargo Tercihi" value={order.requestedCargoName} />
          </Section>
        </div>
      </div>

      {/* ── Aksiyon modalları ─────────────────────────────────────────────── */}
      <Modal open={confirmOpen} onClose={() => setConfirmOpen(false)} title="Siparişi Onayla">
        <p className="text-sm mb-3" style={{ color: 'var(--text-m)' }}>
          Onay, seçilen depoda stok rezervasyonu oluşturur.
        </p>
        <label className="flbl">Depo <span className="text-red-500">*</span></label>
        <select className="inp" value={warehouseId} onChange={e => setWarehouseId(e.target.value)}>
          <option value="">Depo seçin</option>
          {warehouses.map(w => (
            <option key={w.id} value={w.id}>{w.nameI18n?.['tr'] ?? w.code}</option>
          ))}
        </select>
        {actionError && <p className="text-sm mt-2 text-red-500">{actionError}</p>}
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setConfirmOpen(false)}>Vazgeç</Button>
          <Button onClick={() => doConfirm.mutate()} loading={doConfirm.isPending} disabled={!warehouseId}>Onayla</Button>
        </div>
      </Modal>

      <Modal open={cancelOpen} onClose={() => setCancelOpen(false)} title="Siparişi İptal Et">
        <p className="text-sm mb-3" style={{ color: 'var(--text-m)' }}>
          İptal, stok rezervasyonlarını serbest bırakır. Bu işlem geri alınamaz.
        </p>
        <label className="flbl">İptal Nedeni <span className="text-xs" style={{ color: 'var(--text-s)' }}>(isteğe bağlı)</span></label>
        <textarea className="ta" rows={2} value={cancelReason} onChange={e => setCancelReason(e.target.value)} />
        {actionError && <p className="text-sm mt-2 text-red-500">{actionError}</p>}
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setCancelOpen(false)}>Vazgeç</Button>
          <Button variant="danger" onClick={() => cancelMutation.mutate()} loading={cancelMutation.isPending}>İptal Et</Button>
        </div>
      </Modal>

      <Modal open={processOpen} onClose={() => setProcessOpen(false)} title="İşleme Al">
        <p className="text-sm" style={{ color: 'var(--text-m)' }}>
          Sipariş "İşlemde" durumuna geçer; toplama/paketleme başlatılabilir.
        </p>
        {actionError && <p className="text-sm mt-2 text-red-500">{actionError}</p>}
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setProcessOpen(false)}>Vazgeç</Button>
          <Button onClick={() => processMutation.mutate()} loading={processMutation.isPending}>İşleme Al</Button>
        </div>
      </Modal>

      <Modal open={shipOpen} onClose={() => setShipOpen(false)} title="Kargoya Ver">
        <div className="space-y-3">
          <div>
            <label className="flbl">Kargo Anlaşması</label>
            <select className="inp" value={shipIntegrationId} onChange={e => setShipIntegrationId(e.target.value)}>
              <option value="">Seçilmedi</option>
              {cargoIntegrations.filter(c => c.isActive).map(c => (
                <option key={c.id} value={c.id}>{c.name || c.serviceNameI18n?.['tr'] || c.serviceCode}</option>
              ))}
            </select>
            {cargoIntegrations.length === 0 && (
              <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
                Bu firma için tanımlı kargo anlaşması yok — Ayarlar → Firmalar → Entegrasyonlar'dan eklenebilir.
              </p>
            )}
          </div>
          <div>
            <label className="flbl">Takip Numarası</label>
            <input className="inp" value={trackingNumber} onChange={e => setTrackingNumber(e.target.value)}
              placeholder="Kargo firmasının takip numarası" />
          </div>
          <div>
            <label className="flbl">Paket Sayısı</label>
            <input type="number" min={1} className="inp" value={packageCount}
              onChange={e => setPackageCount(Math.max(1, parseInt(e.target.value) || 1))} />
          </div>
        </div>
        {actionError && <p className="text-sm mt-2 text-red-500">{actionError}</p>}
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setShipOpen(false)}>Vazgeç</Button>
          <Button onClick={() => shipMutation.mutate()} loading={shipMutation.isPending}>Kargoya Ver</Button>
        </div>
      </Modal>

      <Modal open={invoiceOpen} onClose={() => setInvoiceOpen(false)} title="Fatura Oluştur">
        <div className="space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="flbl">Fatura Serisi <span className="text-red-500">*</span></label>
              <select className="inp" value={invSeriesId} onChange={e => setInvSeriesId(e.target.value)}>
                <option value="">Seri seçin</option>
                {invoiceSeries.map(s => (
                  <option key={s.id} value={s.id}>{s.name ?? s.eArchiveSerial}</option>
                ))}
              </select>
              {invoiceSeries.length === 0 && (
                <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
                  Aktif seri yok — Faturalar sayfasındaki "Fatura Serileri"nden tanımlanır.
                </p>
              )}
            </div>
            <div>
              <label className="flbl">Tip</label>
              <select className="inp" value={invType} onChange={e => setInvType(e.target.value)}>
                {Object.entries(INVOICE_TYPE_MAP).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select>
            </div>
          </div>
          <div>
            <label className="flbl">Fatura Tarihi</label>
            <input type="date" className="inp" value={invDate} onChange={e => setInvDate(e.target.value)} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="flbl">Alıcı Adı <span className="text-red-500">*</span></label>
              <input className="inp" value={invRecipient} onChange={e => setInvRecipient(e.target.value)} />
            </div>
            <div>
              <label className="flbl">Firma Adı</label>
              <input className="inp" value={invCompany} onChange={e => setInvCompany(e.target.value)} />
            </div>
          </div>
          <div>
            <label className="flbl">Adres <span className="text-red-500">*</span></label>
            <textarea className="ta" rows={2} value={invAddress} onChange={e => setInvAddress(e.target.value)} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="flbl">Vergi Dairesi</label>
              <input className="inp" value={invTaxOffice} onChange={e => setInvTaxOffice(e.target.value)} />
            </div>
            <div>
              <label className="flbl">Vergi/TC No</label>
              <input className="inp" value={invTaxNumber} onChange={e => setInvTaxNumber(e.target.value)} />
            </div>
          </div>
          <p className="text-xs" style={{ color: 'var(--text-s)' }}>
            Tutarlar siparişten alınır: {money(order.grandTotal, cur)}. Fatura numarası seriden otomatik üretilir.
          </p>
        </div>
        {actionError && <p className="text-sm mt-2 text-red-500">{actionError}</p>}
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setInvoiceOpen(false)}>Vazgeç</Button>
          <Button onClick={() => createInvoice.mutate()} loading={createInvoice.isPending}
            disabled={!invSeriesId || !invRecipient.trim() || !invAddress.trim()}>Fatura Oluştur</Button>
        </div>
      </Modal>

      <Modal open={deliverOpen} onClose={() => setDeliverOpen(false)} title="Teslim Edildi">
        <p className="text-sm" style={{ color: 'var(--text-m)' }}>
          Sipariş "Teslim" durumuna geçer.
        </p>
        {actionError && <p className="text-sm mt-2 text-red-500">{actionError}</p>}
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setDeliverOpen(false)}>Vazgeç</Button>
          <Button onClick={() => deliverMutation.mutate()} loading={deliverMutation.isPending}>Teslim Edildi</Button>
        </div>
      </Modal>
    </div>
  )
}

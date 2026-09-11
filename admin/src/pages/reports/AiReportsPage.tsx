import { useState } from 'react'
import { isAxiosError } from 'axios'
import { useMutation, useQuery } from '@tanstack/react-query'
import { BarChart3, Play, ShieldCheck } from 'lucide-react'
import api from '@/api/client'
import { useAuthStore } from '@/store/auth'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { ORDER_STATUS_MAP, PAYMENT_STATUS_MAP, PAYMENT_METHOD_MAP } from '@/pages/orders/orderConstants'
import { ReportChart } from './ReportChart'
import { SavedReports } from './SavedReports'
import { validSavedPlan } from './savedReportValidation'

type Field = { id: string; label: string; kind: string; description: string; dataType?: string | null }
type FirmOption = { id: string; code: string; nameI18n: Record<string, string> }
type Catalog = { version: number; enabled: boolean; naturalLanguageEnabled: boolean; fields: Field[]; maxRows: number; subjects?: string[]; dynamicEnabled?: boolean; dynamicFields?: Field[]; dynamicDetailFields?: Field[]; dynamicSources?: Record<string, { fields: Field[]; detailFields: Field[] }> }
type Definition = { version: number; subject: string; metrics: string[]; dimensions: string[]; filters: { field: string; operator: string; values: string[] }[]; presentation: string; limit: number }
type Predicate = { kind: string; field?: string | null; operator?: string | null; values?: string[] | null; relation?: string | null; children?: Predicate[] | null }
type DynamicSelection = { sort: string | null; direction: 'asc' | 'desc'; top?: number | null }
type DynamicPlan = { version: 2; source: string; from?: string | null; to?: string | null; stockGrain?: string | null; cardWindowMonths?: number | null; period?: string | null; predicate: Predicate | null; aggregate: (DynamicSelection & { dimensions: string[]; measures: string[] }) | null; detail?: (DynamicSelection & { columns: string[] }) | null }
type Plan = Definition | DynamicPlan
type Interpretation = { status: string; message: string; definition: Definition | null; dynamicPlan?: DynamicPlan | null; clarification: string }
type ConversationTurn = { prompt: string; clarification: string; message: string }
type Result = { columns: string[]; rows: (string | number | null)[][]; calculatedAtUtc: string; totalCount: number; totals: Record<string, number>; resolvedPeriod?: { from: string; to: string }; currencyTotals?: { currencyCode: string; count: number; amount: number }[] }
type Row = { id: string; values: (string | number | null)[] }

export function AiReportsPage() {
  const hasPermission = useAuthStore(s => s.hasPermission)
  const userId = useAuthStore(s => s.user?.id)
  const canViewStaff = hasPermission('iam.users.view') && (hasPermission('fulfillment.view') || hasPermission('orders.invoices.view'))
  if (!hasPermission('reports.ai.use') || (!hasPermission('inventory.view') && !hasPermission('orders.view') && !hasPermission('orders.returns.view') && !hasPermission('crm.members.view') && !canViewStaff))
    return <div role="alert" className="p-6">AI raporlama ve seçilen kaynağın görüntüleme yetkileri gerekir.</div>
  return <ReportEditor key={userId} userId={userId} initialSubject={hasPermission('inventory.view') ? 'stock' : hasPermission('orders.view') ? 'orders' : hasPermission('orders.returns.view') ? 'returns' : hasPermission('crm.members.view') ? 'customers' : 'staffActivities'} />
}

function ReportEditor({ userId, initialSubject }: { userId?: string; initialSubject: string }) {
  const [subject, setSubject] = useState(initialSubject)
  const canExport = useAuthStore(state => state.hasPermission('reports.ai.export'))
  const [savedError, setSavedError] = useState('')
  const [dimension, setDimension] = useState('productCode')
  const [productCode, setProductCode] = useState('')
  const [stockType, setStockType] = useState('physical')
  const [prompt, setPrompt] = useState('')
  const [consent, setConsent] = useState(false)
  const [selectedFirmId, setFirmId] = useState('')
  const [proposal, setProposal] = useState<Plan | null>(null)
  const [history, setHistory] = useState<ConversationTurn[]>([])
  const [submitted, setSubmitted] = useState<{ recipe: string; revision: number } | null>(null)
  const awaitingAiProposal = (subject !== 'stock' || prompt.trim().length > 0 || history.length > 0) && !proposal
  const grid = useGridState('ai-stock-result', { defaultPageSize: 25 })
  const catalog = useQuery<Catalog>({
    queryKey: ['ai-report-catalog', userId],
    queryFn: async ({ signal }) => (await api.get('/reports/ai/catalog', { signal })).data.data,
    staleTime: 0,
  })
  const firms = useQuery<FirmOption[]>({
    queryKey: ['ai-report-firms', userId],
    queryFn: async ({ signal }) => (await api.get('/reports/ai/firms', { signal })).data.data,
    staleTime: 0,
  })
  const firmId = firms.data?.length === 1 ? firms.data[0].id : selectedFirmId
  const firmAvailable = !firms.isError && firms.data?.some(f => f.id === firmId)
  const dynamicMode = (subject === 'stock' || subject === 'orders' || subject === 'stockMovements' || subject === 'returns' || subject === 'customers' || subject === 'staffActivities' || subject === 'productCards') && catalog.data?.dynamicEnabled === true
  const dynamicFields = catalog.data?.dynamicSources?.[subject]
  const rawFields = dynamicMode ? [...(dynamicFields?.fields ?? (subject === 'orders' ? catalog.data?.dynamicFields : []) ?? []), ...(dynamicFields?.detailFields ?? (subject === 'orders' ? catalog.data?.dynamicDetailFields : []) ?? [])]
    : catalog.data?.fields.filter(f => subject === 'orders' ? f.id.startsWith('orders.') : !f.id.startsWith('orders.') && !f.id.startsWith('movements.') && !f.id.startsWith('returns.') && !f.id.startsWith('customers.') && !f.id.startsWith('staff.') && !f.id.startsWith('cards.') && !f.id.startsWith('cardMovements.')) ?? []
  const availableFields = [...new Map(rawFields.map(field => [field.id, field])).values()]
  // Manual recipes remain V1: do not expose V2-only fields or duplicate metrics there.
  const manualFields = catalog.data?.fields.filter(f => !f.id.startsWith('orders.') && !f.id.startsWith('movements.') && !f.id.startsWith('returns.') && !f.id.startsWith('customers.') && !f.id.startsWith('staff.') && !f.id.startsWith('cards.') && !f.id.startsWith('cardMovements.')) ?? []
  const definition = proposal ?? {
    version: catalog.data?.version ?? 1, subject: 'stock',
    metrics: manualFields.filter(f => f.kind === 'metric').map(f => f.id),
    dimensions: dimension ? [dimension] : [],
    filters: [
      ...(productCode.trim() ? [{ field: 'productCode', operator: 'eq', values: [productCode.trim()] }] : []),
      ...(stockType ? [{ field: 'stockType', operator: 'eq', values: [stockType] }] : []),
    ],
    presentation: 'table', limit: catalog.data?.maxRows ?? 1000,
  }
  // The API may omit null aggregate/detail properties; source is present in either V2 shape.
  const dynamicPlan = 'source' in definition ? definition : null
  const dimensions = 'source' in definition ? definition.aggregate?.dimensions ?? definition.detail?.columns ?? [] : definition.dimensions
  const metrics = 'source' in definition ? definition.aggregate?.measures ?? [] : definition.metrics
  const detailPlan = dynamicPlan?.detail
  const dynamicSelection = detailPlan ?? dynamicPlan?.aggregate
  const fieldType = (id: string) => availableFields.find(f => f.id === id)?.dataType
  const canSearch = detailPlan ? dimensions.some(id => fieldType(id) === 'text') : dimensions.length > 0
  const label = (id: string) => availableFields.find(f => f.id === id)?.label ?? id
  const planAllowed = !dynamicPlan || dynamicMode
  const recipe = JSON.stringify(definition)
  const reportQuery = useQuery({
    queryKey: ['ai-report-grid', userId, submitted, grid.state],
    enabled: submitted?.recipe === recipe && !!catalog.data?.enabled && !catalog.isError && !awaitingAiProposal && planAllowed,
    queryFn: async ({ signal }) => ({
      recipe: submitted!.recipe,
      result: (await api.post('/reports/ai/grid', {
        definition: JSON.parse(submitted!.recipe),
        grid: { ...grid.state, filters: grid.state.filters.map(f => ({ field: f.field, op: f.op, value: f.value })) },
      }, { signal })).data.data as Result,
    }),
    retry: false, gcTime: 0, refetchOnWindowFocus: false,
  })
  const run = { ...reportQuery, isPending: reportQuery.isFetching,
    reset: () => setSubmitted(null),
    mutate: (next: string) => {
      if (submitted?.recipe !== next) grid.mutate(params => {
        for (const key of Array.from(params.keys()))
          if (['page', 'search', 'sort', 'dir'].includes(key) || key.startsWith('f.') || key.startsWith('fq.')) params.delete(key)
      })
      setSubmitted(previous => ({ recipe: next, revision: (previous?.revision ?? 0) + 1 }))
    },
  }
  const interpret = useMutation({
    mutationFn: async (submitted: string) => (await api.post('/reports/ai/interpret', {
      prompt: submitted, transmissionConfirmed: consent, firmId, subject,
      ...(dynamicMode ? { planVersion: 2 } : {}),
      history: history.map(turn => ({ prompt: turn.prompt, clarification: turn.clarification })),
    })).data.data as Interpretation,
    onSuccess: (data, submitted) => {
      if (data.status === 'ready' && data.definition) setProposal(data.definition)
      if (data.status === 'ready' && dynamicMode && data.dynamicPlan) setProposal(data.dynamicPlan)
      if (data.status === 'ready' || data.status === 'clarify') {
        setHistory(previous => [...previous, { prompt: submitted, clarification: data.clarification ?? 'none', message: data.message }])
        setPrompt(''); setConsent(false)
      }
    },
  })
  const exportReport = useMutation({
    mutationFn: async () => {
      try {
        return (await api.post('/reports/ai/export', { definition: JSON.parse(recipe), grid: {
          ...grid.state, filters: grid.state.filters.map(f => ({ field: f.field, op: f.op, value: f.value })),
        } }, { responseType: 'blob' })).data as Blob
      } catch (error) {
        if (isAxiosError(error) && error.response?.data instanceof Blob) {
          const payload = await error.response.data.text()
          try { const parsed = JSON.parse(payload) as { error?: string }; if (typeof parsed.error === 'string') throw new Error(parsed.error) }
          catch (parsedError) { if (parsedError instanceof Error && !(parsedError instanceof SyntaxError)) throw parsedError }
        }
        throw error
      }
    },
    onSuccess: blob => {
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      try { anchor.href = url; anchor.download = 'ai-rapor.xlsx'; document.body.appendChild(anchor); anchor.click() }
      finally { anchor.remove(); URL.revokeObjectURL(url) }
    },
    gcTime: 0,
  })
  const result = !run.isPending && !run.isError && !catalog.isError && catalog.data?.enabled
    && planAllowed && run.data?.recipe === recipe ? run.data.result : undefined
  const columns: GridColumn<Row>[] = [...dimensions, ...metrics].map((id, index) => ({
    key: id, header: label(id),
    sortable: !(id === 'orders.count' && dimensions.includes('orders.orderNumber')),
    filter: detailPlan ? { type: fieldType(id) === 'date' ? 'date' : fieldType(id) === 'number' ? 'number' : 'text', ...(fieldType(id) === 'guid' ? { ops: ['eq'] } : {}) }
      : dynamicPlan ? { type: metrics.includes(id) ? 'number' : 'text' }
      : id === 'orders.count' && dimensions.includes('orders.orderNumber') ? undefined
      : id === 'orders.createdAt' ? { type: 'date' }
      : id === 'orders.status' ? { type: 'enum', options: Object.entries(ORDER_STATUS_MAP).map(([value, x]) => ({ value, label: x.label })) }
      : id === 'orders.paymentStatus' ? { type: 'enum', options: Object.entries(PAYMENT_STATUS_MAP).map(([value, label]) => ({ value, label })) }
      : id === 'orders.paymentMethod' ? { type: 'enum', options: [...Object.entries(PAYMENT_METHOD_MAP).map(([value, label]) => ({ value, label })), { value: 'none', label: 'Kayıtlı değil' }] }
      : { type: metrics.includes(id) ? 'number' : 'text', ...(id === 'orders.firmPlatformId' || id === 'orders.orderType' ? { ops: ['eq'] } : {}) },
    cell: row => {
      const value = row.values[index]
      if (typeof value === 'string') {
        if (id === 'orders.status') return ORDER_STATUS_MAP[value]?.label ?? value
        if (id === 'orders.paymentStatus') return PAYMENT_STATUS_MAP[value] ?? value
        if (id === 'orders.paymentMethod') return PAYMENT_METHOD_MAP[value] ?? value
        if (id === 'orders.createdAt' || fieldType(id) === 'date') return new Date(value).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' })
      }
      return value == null ? 'Belirtilmemiş' : typeof value === 'number' ? value.toLocaleString('tr-TR') : value
    },
  }))
  const rows: Row[] = result?.rows.map((values, index) => ({ id: String(index), values })) ?? []

  return <div className="p-4 md:p-6 space-y-5">
    <header className="rounded-2xl border p-6" style={{ background: 'var(--surface)', borderColor: 'var(--border)' }}>
      <div className="flex items-center gap-3"><BarChart3 size={28} /><h1 className="text-xl font-bold">AI Raporlama</h1></div>
      <p className="mt-2 text-sm" style={{ color: 'var(--text-s)' }}>{catalog.data?.naturalLanguageEnabled ? 'İsteğinizi yazın, taslağı kontrol edin ve raporu çalıştırın.' : 'Rapor hazırlığı · Doğal dil ile AI yorumlama henüz bağlı değil.'}</p>
      <p className="mt-3 text-sm flex items-center gap-2"><ShieldCheck size={16} />Mevcut veri ve kanal yetkileriniz geçerlidir. Bu ekran veri değiştirmez.</p>
    </header>
    {catalog.isError && <div role="alert">{errText(catalog.error)}</div>}
    <SavedReports userId={userId} recipe={recipe} canSave={!awaitingAiProposal && planAllowed && !!catalog.data && !catalog.isError}
      busy={run.isPending || interpret.isPending} onLoad={candidate => {
        if (!validSavedPlan(candidate)) { setSavedError('Kayıtlı rapor biçimi geçersiz; yeni bir taslak hazırlayın.'); return }
        const plan = candidate as Plan
        const source = 'source' in plan ? plan.source : plan.subject
        if (!catalog.data?.subjects?.includes(source) || ('source' in plan && !catalog.data.dynamicEnabled)) {
          setSavedError('Bu rapor kaynağı için yetkiniz yok veya özellik kapalı.'); return
        }
        setSavedError(''); setSubject(source); setProposal(plan); setPrompt(''); setHistory([]); setConsent(false)
        interpret.reset(); run.reset()
      }} />
    {savedError && <p role="alert">{savedError}</p>}
    {catalog.isPending && <p role="status">Rapor alanları yükleniyor…</p>}
    {catalog.data && !catalog.data.enabled && <div role="status" className="rounded-xl border p-4">Hesaplama henüz etkin değil. Aşağıdan rapor düzenini inceleyebilirsiniz; sonuç üretilmez.</div>}
    <section className="rounded-2xl border p-5 space-y-3" style={{ background: 'var(--surface)', borderColor: 'var(--border)' }} aria-label="Rapor isteği">
      <label htmlFor="ai-report-subject" className="block text-sm">Rapor kaynağı</label>
      <select id="ai-report-subject" className="sel w-full" value={subject} disabled={interpret.isPending || run.isPending}
        onChange={e => { setSubject(e.target.value); setPrompt(''); setHistory([]); setProposal(null); setConsent(false); interpret.reset(); run.reset() }}>
        {(catalog.data?.subjects ?? ['stock']).map(s => <option key={s} value={s}>{s === 'productCards' ? 'Ürün kartları ve hareket varlığı' : s === 'staffActivities' ? 'Personel işlem kayıtları' : s === 'customers' ? 'Müşteriler — detay ve özet' : s === 'returns' ? 'İadeler — detay ve özet' : s === 'orders' ? 'Siparişler — detay ve özet' : s === 'stockMovements' ? 'Stok hareketleri — detay ve özet' : 'Güncel stok — detay ve özet'}</option>)}
      </select>
      <label htmlFor="ai-report-firm" className="block text-sm">Raporlama için kullanılacak AI hesabı</label>
      <select id="ai-report-firm" className="sel w-full" value={firmId}
        disabled={firms.isPending || firms.isError || firms.data?.length === 1 || interpret.isPending || run.isPending}
        onChange={e => { setFirmId(e.target.value); setConsent(false); setProposal(null); setHistory([]); setPrompt(''); interpret.reset(); run.reset() }}>
        <option value="">{firms.isPending ? 'Firmalar yükleniyor…' : 'Firma seçin'}</option>
        {firms.data?.map(f => <option key={f.id} value={f.id}>{f.nameI18n.tr || f.code} ({f.code})</option>)}
      </select>
      <p className="text-xs" style={{ color: 'var(--text-s)' }}>AI hesabını yönetici belirler. Bu hesap hizmet ücretini karşılar; görebileceğiniz veriler kendi rapor ve veri yetkilerinizle sınırlıdır.</p>
      {firms.isError && <p role="alert">Firma listesi alınamadı. <button type="button" className="btn" onClick={() => void firms.refetch()}>Tekrar dene</button></p>}
      {firms.isSuccess && firms.data.length === 0 && <p role="status">Kullanılabilir AI hesabı yok. Raporlama hesabı yapılandırmasını yöneticiyle kontrol edin.</p>}
      {history.length > 0 && <div aria-label="Rapor konuşması" className="space-y-3 rounded-xl p-3" style={{ background: 'var(--surface2)' }}>
        {history.map((turn, index) => <div key={index} className="space-y-1 text-sm">
          <p><strong>Siz:</strong> {turn.prompt}</p>
          <p><strong>Rapor asistanı:</strong> {turn.message}</p>
        </div>)}
        <button type="button" className="btn" disabled={interpret.isPending || run.isPending}
          onClick={() => { setPrompt(''); setConsent(false); setProposal(null); setHistory([]); interpret.reset(); run.reset() }}>Yeni rapor başlat</button>
      </div>}
      <label htmlFor="ai-report-prompt" className="font-semibold">{history.length ? 'Yanıtınızı veya raporda istediğiniz değişikliği yazın' : 'Nasıl bir rapor hazırlamak istiyorsunuz?'}</label>
      <textarea id="ai-report-prompt" className="inp w-full" rows={3} maxLength={2000} value={prompt}
        disabled={interpret.isPending || run.isPending}
        placeholder={subject === 'productCards' ? 'Örnek: Geçen ay hiç kayıtlı hareketi olmayan ürün kartlarını kod, ad ve açılış tarihiyle listele.' : subject === 'staffActivities' ? 'Örnek: Bu ayki işlemleri personel kimliği, adı ve işlem türüne göre say.' : subject === 'stock' ? 'Örnek: Stok adedi en az 5 olan ürünleri ürün kodu, barkod, renk, beden ve kart açılış tarihiyle listele.' : subject === 'customers' ? 'Örnek: Son 6 ayda iade kaydı olan müşterileri ad, soyad ve iade sayısıyla listele.' : subject === 'returns' ? 'Örnek: Geçen ay oluşturulan iadeleri durumuna göre grupla; kayıt sayısını göster.' : subject === 'stockMovements' ? 'Örnek: Bu ayki stok hareketlerini hareket tipine göre grupla; kayıt sayısını göster.' : dynamicMode ? 'Örnek: Bu ayki siparişleri duruma göre grupla; adet ve ortalama sipariş tutarını göster.' : subject === 'orders' ? 'Örnek: Geçen ayki tüm siparişleri detay listesi olarak göster.' : 'Örnek: Kadın ürünlerinin fiziksel stoklarını sezona göre göster.'}
        onChange={e => { setPrompt(e.target.value); setConsent(false); setProposal(null); run.reset() }} />
      {interpret.data?.status === 'clarify' && <div className="flex flex-wrap gap-2" aria-label="Hızlı yanıt seçenekleri">
        {(interpret.data.clarification === 'stock_type'
          ? ['Yalnız fiziksel stok', 'Yalnız sanal stok', 'Fiziksel ve sanal ayrı ayrı']
          : interpret.data.clarification === 'grouping'
            ? ['Genel toplam', ...availableFields.filter(f => f.kind === 'dimension' && f.id !== 'stockType').slice(0, 5).map(f => `${f.label} bazında`)]
            : interpret.data.clarification === 'order_dates' ? ['Geçen ay', 'Bu ay', 'Son 7 gün']
              : interpret.data.clarification === 'order_layout' || interpret.data.clarification === 'dynamic_layout' ? ['Tek tek detay listesi', subject === 'stock' ? 'Ürün koduna göre özet' : subject === 'productCards' ? 'Genel kart sayısı' : subject === 'staffActivities' ? 'Personel kimliği ve işlem türüne göre özet' : subject === 'customers' ? 'Kart durumuna göre özet' : subject === 'returns' ? 'İade durumuna göre özet' : subject === 'stockMovements' ? 'Hareket tipine göre özet' : 'Sipariş durumuna göre özet', 'Genel toplam'] : []).map(answer => <button key={answer} type="button" className="rounded-lg border px-3 py-2 text-sm"
              disabled={interpret.isPending || run.isPending}
              onClick={() => { setPrompt(answer); setConsent(false); setProposal(null); run.reset() }}>{answer}</button>)}
      </div>}
      <p className="text-xs" style={{ color: 'var(--text-s)' }}>Müşteri adı, adres, telefon, kimlik bilgisi veya anahtar yazmayın. Otomatik kontrol tüm kişisel bilgileri tespit edemez.</p>
      <p className="text-xs" style={{ color: 'var(--text-s)' }}>Kullanılabilir alanlar: {[...new Set(availableFields.map(f => f.label))].join(', ')}. {subject === 'productCards' ? 'Stok satırı olmayan kartlar da dahildir. Normal dönem hareket tarihidir; ilk N ay modunda dönem kart açılışlarını seçer.' : subject === 'staffActivities' ? 'Yalnız yetkili işlem kayıtlarıdır; süre, satış cirosu veya performans puanı değildir.' : subject === 'customers' ? 'Dönem müşteri açılışına değil sipariş/iade işlemine uygulanır. Sayılar yalnız yetkili kanallardaki kayıtları kapsar.' : subject === 'returns' ? 'İade kayıt tarihi esas alınır. Kayıtlı tutar, ödenmiş geri ödeme anlamına gelmez.' : subject === 'stockMovements' ? 'Tarih aralığı zorunludur. Kayıtlı hareket adedi net stok değişimi veya stok bakiyesi değildir.' : subject === 'orders' ? 'Tarih aralığı zorunludur. Para birimleri ayrı tutulur; tutar net satış veya tahsilat değildir.' : 'Özellikler güncel tanımlardan alınır. Stok türü belirtilmezse fiziksel/sanal ayrı gösterilir.'}</p>
      <label className="flex items-start gap-2 text-sm"><input type="checkbox" checked={consent} disabled={interpret.isPending}
        onChange={e => setConsent(e.target.checked)} />Bu rapor konuşmasındaki mesajlarımın ve izinli rapor alanlarının OpenAI'a gönderileceğini onaylıyorum. Rapor sonuçları gönderilmez.</label>
      {history.length >= 8 && <p role="status">Konuşma sınırına ulaştınız. Seçimleriniz korunuyor; devam etmek için yeni rapor başlatın.</p>}
      <button type="button" className="btn btn-primary" disabled={!catalog.data?.naturalLanguageEnabled || catalog.isError || !firmAvailable || !consent || !prompt.trim() || interpret.isPending || run.isPending || history.length >= 8}
        onClick={() => { setProposal(null); run.reset(); interpret.mutate(prompt) }}>{interpret.isPending ? 'Yanıt hazırlanıyor…' : history.length ? 'Yanıtı gönder' : 'Taslak hazırla'}</button>
      {interpret.isError && <p role="alert">{errText(interpret.error)}</p>}
      {interpret.data && <p role="status">{interpret.data.message}</p>}
    </section>
    <form onSubmit={e => { e.preventDefault(); if (!catalog.data?.enabled || catalog.isError || run.isPending || interpret.isPending || !planAllowed || awaitingAiProposal) return; grid.mutate(params => { if (submitted?.recipe !== recipe) Array.from(params.keys()).forEach(key => { if (['search', 'sort', 'dir'].includes(key) || key.startsWith('f.') || key.startsWith('fq.')) params.delete(key) }) }); run.mutate(recipe) }}
      className="rounded-2xl border p-5 space-y-4" style={{ background: 'var(--surface)', borderColor: 'var(--border)' }}>
      <h2 className="font-semibold">{proposal ? 'AI taslağının kapsamını kontrol edin' : subject !== 'stock' ? 'Rapor taslağı bekleniyor' : 'Elle rapor hazırlama'}</h2>
      {awaitingAiProposal && <div role="status" className="rounded-xl border p-4 text-sm">
        AI taslağı henüz hazır değil. İsteğinizi veya netleştirme yanıtınızı yukarıdan gönderin. Tarih ve kapsam doğrulanmadan rapor çalıştırılmaz.
        {subject === 'stock' && <button type="button" className="btn mt-2" disabled={interpret.isPending || run.isPending}
          onClick={() => { setPrompt(''); setConsent(false); setProposal(null); setHistory([]); interpret.reset(); run.reset() }}>AI isteğini temizle, elle hazırla</button>}
      </div>}
      {subject === 'stock' && <fieldset disabled={run.isPending || interpret.isPending || awaitingAiProposal || !!proposal || !catalog.data || catalog.isError} className="grid gap-4 md:grid-cols-3">
        <label className="text-sm">Gruplama
          <select className="sel w-full mt-2" value={dimension} onChange={e => { setDimension(e.target.value); run.reset() }}>
            <option value="">Genel toplam</option>
            {manualFields.filter(f => f.kind === 'dimension').map(f => <option key={f.id} value={f.id}>{f.label}</option>)}
          </select>
        </label>
        <label className="text-sm">Ürün kodu (tam eşleşme)
          <input className="inp w-full mt-2" value={productCode} maxLength={128} placeholder="Boş bırakılırsa tüm ürünler" onChange={e => { setProductCode(e.target.value); run.reset() }} />
        </label>
        <label className="text-sm">Stok türü
          <select className="sel w-full mt-2" value={stockType} onChange={e => { setStockType(e.target.value); run.reset() }}>
            <option value="physical">Fiziksel</option><option value="virtual">Sanal</option><option value="">Her ikisi (birlikte toplam)</option>
          </select>
        </label>
      </fieldset>}
      {!awaitingAiProposal && <div className="rounded-xl p-4 text-sm space-y-1" style={{ background: 'var(--surface2)' }}>
        <strong>Rapor kapsamı</strong>
        {detailPlan ? <p>Detay kolonları: {dimensions.map(label).join(', ')} · {subject === 'stock' ? (dynamicPlan?.stockGrain === 'variant' ? 'Her satır bir varyant ve stok türüdür; tüm konumların toplamı kullanılır.' : 'Her satır bir stok konumudur; eşik konuma ayrı uygulanır.') : subject === 'productCards' ? 'Her satır bir ürün kartıdır; varyant veya stok satırı değildir.' : subject === 'staffActivities' ? 'Her satır bir işlem kaydıdır; ürün adedi değildir.' : subject === 'customers' ? 'Her satır bir müşteri kartıdır; aynı ad farklı kişilere ait olabilir.' : subject === 'returns' ? 'Her satır bir iade kaydıdır; aynı siparişin birden fazla iadesi olabilir.' : subject === 'stockMovements' ? 'Her satır bir stok hareketidir.' : 'Her satır bir sipariştir.'}</p> : <>
          <p>Ölçüler: {metrics.map(label).join(', ')}</p>
          <p>Gruplama: {dimensions.map(label).join(', ') || 'Genel toplam'}</p>
        </>}
        {dynamicPlan ? <>
          {subject !== 'stock' && <><label className="block">Dönem seçimi
            <select className="inp ml-2" value={dynamicPlan.period ?? ''} disabled={run.isPending || interpret.isPending}
              onChange={event => { setProposal({ ...dynamicPlan, period: event.target.value || null }); run.reset() }}>
              <option value="">Taslağın sabit tarihleri</option>
              <option value="thisMonth">Bu takvim ayı</option>
              <option value="lastMonth">Önceki takvim ayı</option>
              <option value="last30Days">Bugün dahil son 30 gün</option>
              <option value="last6Months">Bu ay dahil son 6 takvim ayı</option>
            </select>
          </label>
          {dynamicPlan.period ? <p>Dönem (Türkiye saati): seçilen göreli dönem her çalıştırmada sunucu tarafından hesaplanır. Kayıtlı tarifte de korunur. Aşağıdaki koşullarda ayrıca yazılmış tarihler değişmez; çakışma olmaması için kontrol edin.</p>
            : <p>Dönem (Türkiye saati): {new Date(dynamicPlan.from ?? '').toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' })} — {new Date(dynamicPlan.to ?? '').toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' })} (bitiş hariç)</p>}</>}
          {subject === 'stock' && <p>Güncel stok · {dynamicPlan.stockGrain === 'variant' ? 'Varyant ve stok türü bazında tüm konumların toplamı' : 'Her stok konumu ayrı'}. Stok miktarı koşulları bu kapsam üzerinde uygulanır; fiziksel ve sanal stok ayrı kalır.</p>}
          {subject === 'productCards' && <p>{dynamicPlan.cardWindowMonths != null ? `Dönem kart açılışlarını seçer. Her kart için açılıştan sonraki ilk ${dynamicPlan.cardWindowMonths} takvim ayı incelenir (Türkiye saati). Yalnız gözlem süresi tamamlanmış kartlar dahildir.` : 'Dönem hareket tarihine uygulanır; kart açılışı ayrı filtredir.'}</p>}
          <p>Sıralama: {label(dynamicSelection?.sort ?? [...dimensions, ...metrics][0])} · {dynamicSelection?.direction === 'desc' ? 'Azalan' : 'Artan'}</p>
          {dynamicSelection?.top != null && <p>Sonuç sınırı: İlk {dynamicSelection.top} {detailPlan ? 'kayıt' : 'grup'}. Tablo filtreleri ve sıralaması yalnız bu sonuçlar içinde çalışır; listeye dışarıdan yeni grup eklemez.</p>}
          <div aria-label="Dinamik rapor koşulları"><strong>Koşullar:</strong> <PredicatePreview predicate={dynamicPlan.predicate} label={label} /></div>
          {subject === 'orders' && <p>İlişkili kayıt koşulları siparişleri seçer; eşleşen ürün satırlarını, ödeme veya iade tutarını toplamaz. Sipariş tutarı siparişin tamamıdır.</p>}
          {subject === 'customers' && <p>İlişkili sipariş koşulları müşteri listesini süzer. Sipariş/iade sayısı kolonları yalnız eşleşen işlemleri değil, dönemdeki tüm yetkili kayıtları sayar.</p>}
        </> : 'filters' in definition && <p>Filtreler: {definition.filters.map(f => `${label(f.field)}: ${f.values.join(', ')}`).join(' · ') || 'Yok — tüm ürünler, fiziksel ve sanal stok birlikte'}</p>}
        <p>{subject === 'productCards' ? 'Hareket yokluğu yalnız kayıtlı veriler için geçerlidir. İlk N ay modunda her kart kendi açılışından itibaren değerlendirilir; gözlem süresi tamamlanmamış kartlar dışarıda kalır.' : subject === 'staffActivities' ? 'Panel faturaları otomatik oluşturulanları da içerir; dış/legacy faturalar hariçtir. Eksik personel adı boş bırakılır. Manuel fatura, süre ve satış performansı ayrımı yapılmaz.' : subject === 'customers' ? 'Kart açılış tarihi ayrıca filtrelenmedikçe sınırlanmaz. Sipariş sayısı iptaller dahil tüm durumları; iade sayısı iade kayıtlarını kapsar. Sıfır, yalnız yetkili kanallarda kayıt bulunmadığını belirtir.' : subject === 'returns' ? 'Tarih bitişi hariçtir. İade kayıt tarihi kullanılır; tutarlar bağlı sipariş para birimiyle ayrı gösterilir.' : subject === 'stockMovements' ? 'Tarih bitişi hariçtir. Adet kayıtlı miktardır; hareket yönü, net değişim veya geçmiş bakiye hesaplanmaz. Depo/varyant alanları kimliktir.' : subject === 'orders' ? 'Tarih bitişi hariçtir. İptal/returned kayıtları filtrelenmedikçe dahildir. Returned durumu gerçek iade tutarı anlamına gelmez.' : 'Geçmiş stok veya maliyet raporu değildir. Eksik özellikler Belirtilmemiş olarak korunur; çoklu özellik stoku çoğaltmaz.'}</p>
        {proposal && subject === 'stock' && <button type="button" className="btn" disabled={run.isPending || interpret.isPending}
          onClick={() => { setPrompt(''); setConsent(false); setProposal(null); setHistory([]); run.reset(); interpret.reset() }}>AI taslağını kaldır, manuel ayarlara dön</button>}
      </div>}
      {!planAllowed && <p role="alert">Dinamik raporlama şu anda kapalı. Yeni bir rapor başlatın.</p>}
      <button className="btn btn-primary inline-flex gap-2 items-center" type="submit"
        disabled={!catalog.data?.enabled || catalog.isError || run.isPending || interpret.isPending || !planAllowed || awaitingAiProposal}>
        <Play size={16} />{run.isPending ? 'Hesaplanıyor…' : 'Raporu çalıştır'}
      </button>
    </form>
    {submitted?.recipe === recipe && !awaitingAiProposal && planAllowed && <section aria-label="Rapor sonucu" className="space-y-3">
      <p className="text-sm">Tablo filtreleri rapor kapsamının tamamında uygulanır. Arama, sıralama ve sayfa değişikliği AI çağrısı yapmaz.</p>
      {canExport && <div className="space-y-2">
        <button type="button" className="btn" disabled={!result || run.isPending || exportReport.isPending || interpret.isPending}
          onClick={() => exportReport.mutate()}>{exportReport.isPending ? 'Excel hazırlanıyor…' : 'Filtrelenmiş raporu Excel indir'}</button>
        <p className="text-xs">En fazla 5.000 filtrelenmiş sonuç, tüm sayfalar dahil. Satır veya metin sınırı aşılırsa kısmi dosya oluşturulmaz. Rapor indirme anında yeniden hesaplanır; tarih hücreleri Türkiye saatidir.</p>
        {exportReport.isError && <p role="alert">{errText(exportReport.error)}</p>}
      </div>}
      {result && <div className="text-sm space-y-1">
        <p>Hesaplama: {new Date(result.calculatedAtUtc).toLocaleString('tr-TR')} · {result.totalCount.toLocaleString('tr-TR')} sonuç</p>
        {Object.keys(result.totals).length > 0 && <p>Filtrelenmiş rapor toplamları: {Object.entries(result.totals).map(([id, value]) => `${catalog.data?.fields.find(f => f.id === id)?.label ?? id}: ${value.toLocaleString('tr-TR')}`).join(' · ')}</p>}
        {result.resolvedPeriod && <p>Hesaplanan dönem: {new Date(result.resolvedPeriod.from).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' })} — {new Date(result.resolvedPeriod.to).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' })} (bitiş hariç)</p>}
        {result.currencyTotals?.map(t => <p key={t.currencyCode}>{t.currencyCode}: {t.count.toLocaleString('tr-TR')} sipariş · {t.amount.toLocaleString('tr-TR')} kayıtlı sipariş tutarı</p>)}
      </div>}
      <DataGrid<Row> gridId="ai-stock-result" grid={grid} columns={columns}
        rows={rows} totalCount={result?.totalCount ?? 0} loading={run.isPending}
        error={run.isError ? errText(run.error) : null} search={dynamicPlan && !canSearch ? false : { placeholder: 'Rapor sonuçlarında ara…' }} advancedFilters
        empty="Bu filtrelerle kayıt bulunamadı." views={false} minWidth={650} />
      {result && !detailPlan && metrics.length > 0 && <ReportChart key={recipe} result={result} dimensions={dimensions} metrics={metrics} label={label} />}
    </section>}
  </div>
}

function PredicatePreview({ predicate, label, depth = 0 }: { predicate: Predicate | null; label: (id: string) => string; depth?: number }) {
  if (!predicate) return <span>Ek koşul yok</span>
  if (depth > 6) return <span>Koşul derinliği görüntülenemiyor.</span>
  const operators: Record<string, string> = { eq: 'eşittir', ne: 'eşit değildir', in: 'şunlardan biri', contains: 'içerir', gt: 'büyüktür', gte: 'en az', lt: 'küçüktür', lte: 'en çok', between: 'arasında (bitiş hariç)', isNull: 'boş', isNotNull: 'dolu' }
  const groups: Record<string, string> = { all: 'Tüm koşullar (VE)', any: 'Koşullardan en az biri (VEYA)', not: 'Şu koşulun DEĞİLİ', exists: 'İlişkili kayıt VAR', notExists: 'İlişkili kayıt YOK (erişebildiğiniz kayıtlar içinde)' }
  return <div className="mt-1 border-l pl-3">
    <p>{groups[predicate.kind] ?? (predicate.field ? label(predicate.field) : predicate.kind)}{predicate.relation ? `: ${label(predicate.relation)}` : ''}
      {predicate.operator ? ` ${operators[predicate.operator] ?? predicate.operator}` : ''}{predicate.values?.length ? `: ${predicate.values.join(', ')}` : ''}</p>
    {predicate.children?.slice(0, 16).map((child, index) => <PredicatePreview key={index} predicate={child} label={label} depth={depth + 1} />)}
  </div>
}

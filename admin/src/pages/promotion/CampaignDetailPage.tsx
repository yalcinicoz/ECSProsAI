import { useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useQuery, useQueries, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { InfoTip } from '@/components/ui/InfoTip'
import { cn } from '@/lib/utils'
import { FilterBuilder, type FilterDef } from '@/components/catalog/FilterBuilder'

// ── Tipler ──────────────────────────────────────────────────────────────────
interface SchemaFieldOption { value: string; labelI18n: Record<string, string> }
interface SchemaFieldCondition { field: string; equals?: string; notEquals?: string }
interface CampaignSchemaField {
  key: string; labelI18n: Record<string, string>; type: string; required: boolean
  unit?: string | null; min?: number | null; max?: number | null; default?: unknown
  options?: SchemaFieldOption[] | null; visibleWhen?: SchemaFieldCondition | null
  helpI18n?: Record<string, string> | null
}
interface CampaignType {
  id: string; code: string; nameI18n: Record<string, string>
  scope: string; requiresProducts: boolean; productPriceDisplay: boolean
  isStackable: boolean; isActive: boolean; settingsSchema?: CampaignSchemaField[] | null
}
interface Firm { id: string; nameI18n: Record<string, string> }
interface Channel { id: string; code?: string; nameI18n?: Record<string, string>; firmId?: string; firmName?: string }
interface ProductSimple { id: string; code: string; nameI18n: Record<string, string> }

const tr = (m?: Record<string, string> | null) => m?.['tr'] ?? Object.values(m ?? {})[0] ?? ''
const TABS = ['Genel', 'Parametreler', 'Ürün Kapsamı'] as const
type Tab = typeof TABS[number]
const FILL_OPTS = [
  { value: 'all', label: 'Tüm ürünler' },
  { value: 'manual', label: 'Manuel — ürünler elle eklenir' },
  { value: 'filter', label: 'Filtre — kural tabanlı' },
  { value: 'mixed', label: 'Karma — filtre + manuel' },
]

// Bant/rozet renk paleti — backend CampaignBadgePalette ile birebir (anahtar + hex).
// '' = varsayılan marka rengi (sitede --ms-renk-primary).
const BADGE_COLORS = [
  { key: '', hex: '#9d7856', label: 'Varsayılan (marka)' },
  { key: 'kirmizi', hex: '#DC2626', label: 'Kırmızı — indirim' },
  { key: 'turuncu', hex: '#EA580C', label: 'Turuncu — fırsat' },
  { key: 'amber', hex: '#D97706', label: 'Amber — sınırlı süre' },
  { key: 'yesil', hex: '#16A34A', label: 'Yeşil — kargo' },
  { key: 'mavi', hex: '#2563EB', label: 'Mavi — sepet/bilgi' },
  { key: 'mor', hex: '#7C3AED', label: 'Mor — özel' },
  { key: 'pembe', hex: '#DB2777', label: 'Pembe — sezon' },
  { key: 'siyah', hex: '#111827', label: 'Siyah — premium' },
]

function errText(e: unknown) {
  return (e as { response?: { data?: { error?: string } } }).response?.data?.error ?? 'İşlem başarısız oldu.'
}

// Alan açıklamaları (etiket yanındaki ⓘ). Parametre alanları için şemadaki helpI18n öncelikli; yoksa buradaki metin.
const GENEL_ACIKLAMA: Record<string, string> = {
  platform: 'Kampanyanın geçerli olduğu satış kanalı. Kampanya yalnız bu kanalın sitesinde/uygulamasında çalışır.',
  tip: 'Kampanyanın çalışma şekli (indirim, kargo, X al Y öde, kombin…). Tip seçilince Parametreler sekmesi o tipe göre oluşur.',
  ad: 'Panelde ve raporlarda görünen kampanya adı. Zorunludur. Kampanya kodu sistem tarafından otomatik verilir.',
  aciklama: 'İç not; müşteriye gösterilmez.',
  rozet: 'Ürün kartında ve ürün detayında görünen kısa etiket (örn. "Süper Fırsat"). Boşsa rozet çıkmaz.',
  oncelik: 'Aynı ürüne birden çok kampanya uyuyorsa yüksek öncelikli olan kazanır. Büyük sayı = önce uygulanır.',
  bantRengi: 'Sitedeki kampanya bandının/rozetinin arka plan rengi. Varsayılan, sitenin marka rengidir.',
  baslangic: 'Kampanyanın sitede görünmeye başlayacağı gün (dahil). Bugünden önceki tarih verilirse hemen başlar.',
  bitis: 'Kampanyanın son günü (dahil). Boş bırakılırsa süresiz devam eder.',
  aktif: 'Pasif kampanya sitede çalışmaz, tarih aralığında olsa bile. Silmek yerine pasife almak kullanım geçmişini korur.',
  doldurma: 'Kampanyanın hangi ürünlere uygulanacağı: tüm ürünler, elle seçilen ürünler, filtre kuralı ya da ikisinin karışımı.',
  filtre: 'Kategori/özellik/fiyat gibi kurallarla ürün kümesi tanımlanır; kurala yeni uyan ürünler otomatik kapsama girer.',
  manuel: 'Ürün kodlarını yazarak ya da dosyadan yükleyerek kapsam listesi oluşturulur.',
}
const PARAM_ACIKLAMA: Record<string, string> = {
  thresholdType: 'Kargo indiriminin koşulu: her sepette (koşulsuz) ya da belirli bir sepet tutarının üzerinde.',
  thresholdValue: 'İndirimler düşüldükten sonra ödenecek ürün tutarı bu eşiğe ulaşınca kargo kampanyası devreye girer.',
  paymentMethods: 'Kampanya yalnız seçilen ödeme yöntemiyle geçerli olur. Ödeme yöntemi seçilmeden sepette gösterilmez.',
  coverage: 'Ücretsiz: kargo bedeli sıfırlanır. Yüzde/Tutar: kargo bedelinden o kadar düşülür. Kanal ayarıyla karşılaştırılıp müşteri lehine olan uygulanır.',
  coverageValue: 'Yüzde seçildiyse 0-100 arası oran, Tutar seçildiyse ₺ cinsinden indirim.',
  applyTo: 'İndirimin uygulanacağı yer: kapsamdaki ürünler ya da sepet toplamı.',
  conditionType: 'İndirimin devreye girmesi için gereken koşul türü (adet, tutar) — koşulsuz da olabilir.',
  conditionValue: 'Seçilen koşulun eşik değeri (adet ya da ₺).',
  benefitType: 'İndirimin şekli: yüzde, sabit tutar ya da sabit fiyat.',
  benefitValue: 'İndirim şekline göre oran (%) ya da tutar (₺).',
  maxDiscountAmount: 'Yüzde indirimde bir siparişte verilebilecek en yüksek indirim tutarı (opsiyonel tavan).',
  buyQuantity: 'Tam fiyat ödenecek ürün adedi (X).',
  getQuantity: 'İndirimli ya da bedava verilecek ürün adedi (Y).',
  getBenefitType: 'Y ürünlerine uygulanacak avantaj: bedava, yüzde ya da tutar indirimi.',
  getBenefitValue: 'Y ürünlerine uygulanan indirimin değeri.',
  sameProduct: 'Açıksa X ve Y aynı üründen olmalı; kapalıysa kapsamdaki farklı ürünler birlikte sayılır.',
  cheapestGetsBenefit: 'Açıksa indirim en ucuz ürün(ler)e uygulanır.',
  buyThresholdType: 'Hediye hakkı için alım koşulunun türü (adet ya da tutar).',
  buyThresholdValue: 'Alım koşulunun eşik değeri.',
  giftQuantity: 'Koşul sağlanınca hediye grubundan verilecek adet.',
  giftBenefitType: 'Hediye grubuna uygulanan avantaj: bedava, yüzde ya da tutar.',
  giftBenefitValue: 'Hediye grubuna uygulanan indirimin değeri.',
  minBundleItems: 'Kombin fiyatının geçerli olması için sepette bulunması gereken en az ürün adedi.',
  bundleBenefitType: 'Kombin avantajının şekli: sabit kombin fiyatı, yüzde ya da tutar indirimi.',
  bundleBenefitValue: 'Kombin avantajının değeri.',
}

// Şema alanı için etkin değer: girilen değer yoksa şemadaki varsayılan (görünürlük koşulu da buna bakar —
// aksi hâlde "Ücretsiz" varsayılan seçiliyken "İndirim değeri" alanı görünüyordu).
function effectiveValue(schema: CampaignSchemaField[] | null | undefined, settings: Record<string, unknown>, key: string): string {
  const v = settings[key]
  if (v != null && v !== '') return String(v)
  const d = schema?.find(f => f.key === key)?.default
  return d == null ? '' : String(d)
}

// Koşullu görünürlük
function fieldVisible(f: CampaignSchemaField, schema: CampaignSchemaField[] | null | undefined, settings: Record<string, unknown>): boolean {
  const c = f.visibleWhen
  if (!c) return true
  const cur = effectiveValue(schema, settings, c.field)
  if (c.equals != null) return cur === c.equals
  if (c.notEquals != null) return cur !== c.notEquals
  return true
}

// Şema varsayılanlarını ayarlara yazar (tip seçildiğinde) — sunucuya seçili görünen değer gider.
function schemaDefaults(schema: CampaignSchemaField[] | null | undefined): Record<string, unknown> {
  const out: Record<string, unknown> = {}
  for (const f of schema ?? []) if (f.default != null && f.default !== '') out[f.key] = f.default
  return out
}

// Etiket + zorunlu yıldızı + bilgi ikonu. <label> değil <div>: InfoTip düğmesi label içinde çalışmaz.
function Lbl({ children, tip, required }: { children: React.ReactNode; tip?: string; required?: boolean }) {
  return (
    <div className="flbl flex items-center">
      <span>{children}{required && <span className="text-red-500"> *</span>}</span>
      {tip && <InfoTip text={tip} />}
    </div>
  )
}

// Yayında mı: aktif + bugün başlangıç-bitiş aralığında (bitiş boş = süresiz).
function yayinda(isActive: boolean, starts: string, ends: string): boolean {
  if (!isActive || !starts) return false
  const bugun = new Date().toISOString().slice(0, 10)
  return starts <= bugun && (!ends || ends >= bugun)
}

export function CampaignDetailPage() {
  const { id } = useParams<{ id: string }>()
  const isNew = !id || id === 'new'
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [tab, setTab] = useState<Tab>('Genel')
  const [err, setErr] = useState('')
  const [saving, setSaving] = useState(false)     // düğme metni mutation durumuna değil buna bağlı — hata sonrası "Kaydediliyor..." takılmasın

  // Form state
  const [firmPlatformId, setFirmPlatformId] = useState('')
  const [typeId, setTypeId] = useState('')
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [badge, setBadge] = useState('')
  const [badgeColor, setBadgeColor] = useState('')
  const [starts, setStarts] = useState(new Date().toISOString().slice(0, 10))
  const [ends, setEnds] = useState('')
  const [priority, setPriority] = useState(0)
  const [isActive, setIsActive] = useState(true)
  const [settings, setSettings] = useState<Record<string, unknown>>({})
  const [fillType, setFillType] = useState('all')
  const [filterDef, setFilterDef] = useState<FilterDef>({})
  const [manualIds, setManualIds] = useState<string[]>([])
  const [loaded, setLoaded] = useState(false)

  // Manuel ürün kapsamı: kod girişi / dosyadan yükleme (Id→ad gösterimi manualInfo'da)
  const [manualInfo, setManualInfo] = useState<Record<string, { code: string; name: string }>>({})
  const [codesInput, setCodesInput] = useState('')
  const [notFoundCodes, setNotFoundCodes] = useState<string[]>([])
  const [lookupBusy, setLookupBusy] = useState(false)
  const fileRef = useRef<HTMLInputElement>(null)

  const applyLookup = (items: ProductSimple[]) => {
    setManualInfo(m => {
      const next = { ...m }
      for (const it of items) next[it.id] = { code: it.code, name: tr(it.nameI18n) }
      return next
    })
  }

  const addByCodes = async (raw: string[]) => {
    const codes = [...new Set(raw.map(c => c.trim()).filter(Boolean))]
    if (!codes.length) return
    setLookupBusy(true); setErr('')
    try {
      const { data } = await api.post('/catalog/products/lookup', { codes })
      const items: ProductSimple[] = data.data?.items ?? []
      applyLookup(items)
      setManualIds(ids => [...new Set([...ids, ...items.map(it => it.id)])])
      setNotFoundCodes(data.data?.notFoundCodes ?? [])
      setCodesInput('')
    } catch (e) { setErr(errText(e)) } finally { setLookupBusy(false) }
  }

  const resolveIds = async (ids: string[]) => {
    if (!ids.length) return
    try {
      const { data } = await api.post('/catalog/products/lookup', { ids })
      applyLookup(data.data?.items ?? [])
    } catch { /* adlar çözülemezse satırda Id gösterilir */ }
  }

  const onFileSelected = async (f: File) => {
    setErr('')
    try {
      let raw: string[]
      if (/\.xlsx?$/i.test(f.name)) {
        const XLSX = await import('xlsx')
        const wb = XLSX.read(await f.arrayBuffer())
        const rows = XLSX.utils.sheet_to_json<unknown[]>(wb.Sheets[wb.SheetNames[0]], { header: 1 })
        raw = rows.map(r => String(r?.[0] ?? '').trim())
      } else {
        raw = (await f.text()).split(/[\s,;]+/)
      }
      await addByCodes(raw)
    } catch { setErr('Dosya okunamadı. Kod listesi için .txt/.csv ya da .xlsx yükleyin.') }
  }

  // Tipler
  const { data: types = [] } = useQuery<CampaignType[]>({
    queryKey: ['campaign-types', 'all'],
    queryFn: async () => (await api.get('/promotion/campaign-types?activeOnly=false')).data.data,
  })
  const selType = types.find(t => t.id === typeId)

  // Platformlar (firma → platform, düzleştir)
  const { data: firms = [] } = useQuery<Firm[]>({
    queryKey: ['firms'], queryFn: async () => (await api.get('/core/firms')).data.data ?? [],
  })
  const platformQueries = useQueries({
    queries: firms.map(firm => ({
      queryKey: ['firm-platforms', firm.id],
      queryFn: async (): Promise<Channel[]> => {
        const { data } = await api.get(`/core/firms/${firm.id}/platforms`)
        return (data.data ?? []).map((ch: Channel) => ({ ...ch, firmId: firm.id, firmName: tr(firm.nameI18n) }))
      },
      enabled: firms.length > 0,
    })),
  })
  const channels: Channel[] = platformQueries.flatMap(q => q.data ?? [])

  // Mevcut kampanya (düzenleme)
  useQuery({
    queryKey: ['campaign-detail', id],
    enabled: !isNew && !loaded,
    queryFn: async () => {
      const { data } = await api.get(`/promotion/campaigns/${id}`)
      const c = data.data
      setFirmPlatformId(c.firmPlatformId); setTypeId(c.campaignTypeId); setCode(c.code)
      setName(tr(c.nameI18n)); setDescription(tr(c.descriptionI18n)); setBadge(c.badgeLabel ?? '')
      setBadgeColor(c.badgeColor ?? '')
      setStarts((c.startsAt ?? '').slice(0, 10)); setEnds(c.endsAt ? c.endsAt.slice(0, 10) : '')
      setPriority(c.priority); setIsActive(c.isActive); setSettings(c.settings ?? {})
      setFillType(c.fillType ?? 'all'); setFilterDef((c.filterDef ?? {}) as FilterDef)
      const mids: string[] = c.manualProductIds ?? []
      setManualIds(mids); void resolveIds(mids); setLoaded(true)
      return c
    },
  })

  const showProductScope = selType?.requiresProducts ?? false

  const save = useMutation({
    mutationFn: async () => {
      const body = {
        firmPlatformId, campaignTypeId: typeId, code: isNew ? null : code,   // kod sunucuda üretilir
        nameI18n: { tr: name.trim() }, descriptionI18n: description ? { tr: description } : null,
        badgeLabel: badge || null, badgeColor: badgeColor || null, startsAt: starts, endsAt: ends || null,
        priority, isActive, settings,
        fillType: showProductScope ? fillType : 'all',
        filterDef: (fillType === 'filter' || fillType === 'mixed') ? filterDef : null,
        manualProductIds: manualIds, excludedProductIds: [] as string[],
      }
      if (isNew) return (await api.post('/promotion/campaigns', body)).data.data.id as string
      await api.put(`/promotion/campaigns/${id}`, body); return id!
    },
    onSuccess: (cid) => {
      queryClient.invalidateQueries({ queryKey: ['campaigns'] })
      navigate(`/promotion/campaigns`)
      void cid
    },
    onError: (e) => setErr(errText(e)),
    onSettled: () => setSaving(false),
  })

  // İstemci doğrulaması: sunucu da aynı kuralları uygular (ad zorunlu), burada erken ve sekmeden bağımsız uyarı.
  const kaydet = () => {
    setErr('')
    if (!firmPlatformId) { setTab('Genel'); setErr('Platform seçin.'); return }
    if (!typeId) { setTab('Genel'); setErr('Kampanya tipi seçin.'); return }
    if (!name.trim()) { setTab('Genel'); setErr('Kampanya adı zorunludur.'); return }
    if (ends && starts && ends < starts) { setTab('Genel'); setErr('Bitiş tarihi başlangıçtan önce olamaz.'); return }
    setSaving(true)
    save.mutate()
  }

  const remove = useMutation({
    mutationFn: async () => { await api.delete(`/promotion/campaigns/${id}`) },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['campaigns'] }); navigate('/promotion/campaigns') },
    onError: (e) => setErr(errText(e)),
  })
  const sil = () => {
    setErr('')
    const mesaj = yayinda(isActive, starts, ends)
      ? `DİKKAT: '${name || code}' kampanyası şu anda SİTEDE YAYINDA (aktif ve tarih aralığında). Silinirse müşteriler bu kampanyayı anında görmez.\n\nYine de silinsin mi? Bu işlem geri alınamaz.`
      : `'${name || code}' kampanyası silinsin mi? Bu işlem geri alınamaz.`
    if (window.confirm(mesaj)) remove.mutate()
  }

  const copy = useMutation({
    mutationFn: async () => {
      const newCode = prompt('Yeni kampanya kodu:', `${code}-KOPYA`)
      if (!newCode) return null
      return (await api.post(`/promotion/campaigns/${id}/copy`, { newCode, targetFirmPlatformId: null })).data.data.id as string
    },
    onSuccess: (cid) => { if (cid) { queryClient.invalidateQueries({ queryKey: ['campaigns'] }); navigate(`/promotion/campaigns/${cid}`) } },
    onError: (e) => setErr(errText(e)),
  })

  const setField = (k: string, v: unknown) => setSettings(s => ({ ...s, [k]: v }))

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>
            {isNew ? 'Yeni Kampanya' : `Kampanya: ${code}`}
          </h1>
          {selType && <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{tr(selType.nameI18n)}</p>}
        </div>
        <div className="flex gap-2">
          {!isNew && <Button size="sm" variant="secondary" onClick={() => copy.mutate()}>Kopyala</Button>}
          {!isNew && <Button size="sm" variant="secondary" onClick={sil} loading={remove.isPending} style={{ color: '#b91c1c' }}>Sil</Button>}
          <Button size="sm" onClick={kaydet} disabled={saving}>
            {saving ? 'Kaydediliyor...' : 'Kaydet'}
          </Button>
        </div>
      </div>

      {err && <div className="mb-3 px-3 py-2 rounded text-sm" style={{ background: 'var(--danger-bg,#fef2f2)', color: '#b91c1c' }}>{err}</div>}

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABS.filter(t => t !== 'Ürün Kapsamı' || showProductScope).map(t => (
          <button key={t} className={cn('stab', tab === t && 'active')} onClick={() => setTab(t)}>{t}</button>
        ))}
      </div>

      {/* GENEL */}
      {tab === 'Genel' && (
        <div className="card p-4 space-y-3 max-w-2xl">
          <div>
            <Lbl required tip={GENEL_ACIKLAMA.platform}>Platform</Lbl>
            <select className="inp" value={firmPlatformId} onChange={e => setFirmPlatformId(e.target.value)}>
              <option value="">Platform seçin</option>
              {channels.map(c => <option key={c.id} value={c.id}>{tr(c.nameI18n) || c.code} ({c.firmName})</option>)}
            </select>
          </div>
          <div>
            <Lbl required tip={GENEL_ACIKLAMA.tip}>Kampanya Tipi</Lbl>
            <select className="inp" value={typeId} onChange={e => { setTypeId(e.target.value); setSettings(schemaDefaults(types.find(t => t.id === e.target.value)?.settingsSchema)) }}>
              <option value="">Tip seçin</option>
              {types.map(t => <option key={t.id} value={t.id}>{tr(t.nameI18n)}</option>)}
            </select>
          </div>
          <div><Lbl required tip={GENEL_ACIKLAMA.ad}>Ad</Lbl>
            <input className="inp" value={name} onChange={e => setName(e.target.value)} /></div>
          <div><Lbl tip={GENEL_ACIKLAMA.aciklama}>Açıklama</Lbl>
            <input className="inp" value={description} onChange={e => setDescription(e.target.value)} /></div>
          <div className="grid grid-cols-2 gap-3">
            <div><Lbl tip={GENEL_ACIKLAMA.rozet}>Etiket/Rozet</Lbl>
              <input className="inp" value={badge} onChange={e => setBadge(e.target.value)} placeholder="ör. Süper Fırsat" /></div>
            <div><Lbl tip={GENEL_ACIKLAMA.oncelik}>Öncelik</Lbl>
              <input className="inp" type="number" value={priority} onChange={e => setPriority(Number(e.target.value))} /></div>
          </div>
          <div>
            <Lbl tip={GENEL_ACIKLAMA.bantRengi}>Bant Rengi</Lbl>
            <div className="flex flex-wrap gap-2">
              {BADGE_COLORS.map(r => (
                <button key={r.key} type="button" title={r.label}
                  onClick={() => setBadgeColor(r.key)}
                  className="flex items-center gap-1.5 rounded px-2 py-1 text-xs"
                  style={{
                    border: badgeColor === r.key ? '2px solid var(--brand)' : '1px solid var(--border)',
                    background: 'var(--surface2)', color: 'var(--text)',
                  }}>
                  <span className="inline-block h-4 w-4 rounded" style={{ background: r.hex }} />
                  {r.label}
                </button>
              ))}
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div><Lbl tip={GENEL_ACIKLAMA.baslangic}>Başlangıç</Lbl>
              <input className="inp" type="date" value={starts} onChange={e => setStarts(e.target.value)} /></div>
            <div><Lbl tip={GENEL_ACIKLAMA.bitis}>Bitiş (boş = süresiz)</Lbl>
              <input className="inp" type="date" value={ends} onChange={e => setEnds(e.target.value)} /></div>
          </div>
          <div className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
            <label className="flex items-center gap-2">
              <input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} /> Aktif
            </label>
            <InfoTip text={GENEL_ACIKLAMA.aktif} />
          </div>
        </div>
      )}

      {/* PARAMETRELER — SettingsSchema'dan üretilir */}
      {tab === 'Parametreler' && (
        <div className="card p-4 space-y-3 max-w-2xl">
          {!selType && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Önce Genel sekmesinden kampanya tipi seçin.</p>}
          {selType && (selType.settingsSchema ?? []).filter(f => fieldVisible(f, selType.settingsSchema, settings)).map(f => (
            <div key={f.key}>
              <Lbl required={f.required} tip={tr(f.helpI18n) || PARAM_ACIKLAMA[f.key]}>{tr(f.labelI18n)}{f.unit ? ` (${f.unit})` : ''}</Lbl>
              {f.type === 'boolean' ? (
                <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
                  <input type="checkbox" checked={!!settings[f.key]} onChange={e => setField(f.key, e.target.checked)} /> Evet
                </label>
              ) : f.type === 'select' ? (
                <select className="inp" value={String(settings[f.key] ?? f.default ?? '')} onChange={e => setField(f.key, e.target.value)}>
                  <option value="">Seçin</option>
                  {(f.options ?? []).map(o => <option key={o.value} value={o.value}>{tr(o.labelI18n)}</option>)}
                </select>
              ) : (
                <input className="inp" type="number" value={String(settings[f.key] ?? '')}
                  onChange={e => setField(f.key, e.target.value === '' ? '' : Number(e.target.value))} />
              )}
            </div>
          ))}
        </div>
      )}

      {/* ÜRÜN KAPSAMI */}
      {tab === 'Ürün Kapsamı' && showProductScope && (
        <div className="card p-4 space-y-4 max-w-3xl">
          <div className="max-w-md">
            <Lbl tip={GENEL_ACIKLAMA.doldurma}>Doldurma tipi</Lbl>
            <select className="inp" value={fillType} onChange={e => setFillType(e.target.value)}>
              {FILL_OPTS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </div>

          {(fillType === 'filter' || fillType === 'mixed') && (
            <div>
              <div className="text-xs font-semibold mb-2 flex items-center" style={{ color: 'var(--text-s)' }}>FİLTRE KURALLARI <InfoTip text={GENEL_ACIKLAMA.filtre} /></div>
              <FilterBuilder value={filterDef} onChange={(def) => setFilterDef(def)} />
            </div>
          )}

          {(fillType === 'manual' || fillType === 'mixed') && (
            <div>
              <div className="text-xs font-semibold mb-2 flex items-center" style={{ color: 'var(--text-s)' }}>MANUEL ÜRÜNLER ({manualIds.length}) <InfoTip text={GENEL_ACIKLAMA.manuel} /></div>
              <div className="flex gap-2 mb-1">
                <input className="inp flex-1" placeholder="Ürün kodları — virgülle ayırın (örn. ABC001, ABC002)"
                  value={codesInput} onChange={e => setCodesInput(e.target.value)}
                  onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); void addByCodes(codesInput.split(/[\s,;]+/)) } }} />
                <Button size="sm" variant="secondary" disabled={lookupBusy}
                  onClick={() => void addByCodes(codesInput.split(/[\s,;]+/))}>
                  {lookupBusy ? 'Ekleniyor…' : 'Ekle'}
                </Button>
                <Button size="sm" variant="secondary" disabled={lookupBusy} onClick={() => fileRef.current?.click()}>Dosyadan Yükle</Button>
                <input ref={fileRef} type="file" accept=".txt,.csv,.xls,.xlsx" className="hidden"
                  onChange={e => { const f = e.target.files?.[0]; e.target.value = ''; if (f) void onFileSelected(f) }} />
              </div>
              <p className="text-xs mb-2" style={{ color: 'var(--text-s)' }}>
                Dosya: .txt/.csv — kodlar virgül, boşluk ya da satır sonuyla ayrılır; Excel (.xlsx) — kodlar ilk sütundan okunur.
              </p>
              {notFoundCodes.length > 0 && (
                <div className="text-xs mb-2" style={{ color: '#b91c1c' }}>
                  Bulunamayan kodlar ({notFoundCodes.length}): {notFoundCodes.join(', ')}
                </div>
              )}
              <div className="space-y-1">
                {manualIds.map(pid => (
                  <div key={pid} className="flex items-center justify-between px-3 py-1.5 rounded text-sm"
                    style={{ background: 'var(--surface2)', color: 'var(--text)' }}>
                    <span>{manualInfo[pid]?.name ?? 'Bilinmeyen ürün'} <code className="text-xs" style={{ color: 'var(--text-s)' }}>{manualInfo[pid]?.code ?? pid}</code></span>
                    <button className="text-xs" style={{ color: '#b91c1c' }} onClick={() => setManualIds(ids => ids.filter(x => x !== pid))}>Kaldır</button>
                  </div>
                ))}
              </div>
            </div>
          )}
          {fillType === 'all' && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Kampanya tüm ürünlere uygulanır.</p>}
        </div>
      )}
    </div>
  )
}

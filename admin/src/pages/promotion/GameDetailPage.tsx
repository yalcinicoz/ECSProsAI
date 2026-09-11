import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useQuery, useQueries, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { InfoTip } from '@/components/ui/InfoTip'
import { cn } from '@/lib/utils'
import { GAME_TYPES, LIMIT_PERIODS } from './GamesPage'

// Şans oyunu tanımı (docs/BACKEND_OYUNLAR.md): Genel · Ödüller · Metinler · Oynanışlar. Sonuç sunucuda belirlenir;
// panelde ödül ağırlıkları (olasılık), "herkes kazanır" modu, hak dönemi ve TÜM kullanıcı metinleri tanımlanır.

interface Prize {
  id?: string | null; label: string; shortLabel?: string | null; kind: string; color?: string | null; iconUrl?: string | null
  description?: string | null; weight: number; sortOrder: number; isActive: boolean
  couponType?: string | null; couponValue?: number | null; minimumCartTotal?: number | null; points?: number | null
}
interface Game {
  id: string; firmPlatformId: string; code: string; type: string
  titleI18n: Record<string, string>; subtitleI18n?: Record<string, string> | null; descriptionI18n?: Record<string, string> | null; rulesTextI18n?: Record<string, string> | null
  ctaLabel?: string | null; imageUrl?: string | null; themeColor?: string | null; accentColor?: string | null
  requiresLogin: boolean; alwaysWin: boolean; startsAt: string; endsAt?: string | null; isActive: boolean
  limitPeriod: string; limitCount: number; couponValidDays: number; sortOrder: number
  labelAvailable: string; labelCooldown: string; labelExhausted: string; labelLoginRequired: string; labelEnded: string
  winMessage: string; winSubMessage: string; loseMessage: string; loseSubMessage: string
  prizes: Prize[]; playCount: number; winCount: number
}
interface Play { id: string; memberId: string; playedAt: string; periodKey: string; won: boolean; prizeLabel?: string | null; couponCode?: string | null; pointsGiven?: number | null }
interface Firm { id: string; nameI18n: Record<string, string> }
interface Channel { id: string; code?: string; nameI18n?: Record<string, string>; firmName?: string }

const tr = (m?: Record<string, string> | null) => m?.['tr'] ?? Object.values(m ?? {})[0] ?? ''
const errText = (e: unknown) => (e as { response?: { data?: { error?: string } } }).response?.data?.error ?? 'İşlem başarısız oldu.'
const TABS = ['Genel', 'Ödüller', 'Metinler', 'Oynanışlar'] as const
type Tab = typeof TABS[number]
const KINDS = [
  { value: 'coupon', label: 'Kupon (indirim)' },
  { value: 'points', label: 'Puan' },
  { value: 'none', label: 'Pas (kaybetti)' },
]
const PALET = ['#7C3AED', '#DB2777', '#F59E0B', '#16A34A', '#2563EB', '#DC2626', '#0D9488', '#1F2937']

const yeniOdul = (i: number): Prize => ({ label: '', shortLabel: '', kind: 'coupon', color: PALET[i % PALET.length], weight: 1, sortOrder: i, isActive: true, couponType: 'fixed', couponValue: 50, minimumCartTotal: null, points: null })

function Lbl({ children, tip, required }: { children: React.ReactNode; tip?: string; required?: boolean }) {
  return <div className="flbl flex items-center"><span>{children}{required && <span className="text-red-500"> *</span>}</span>{tip && <InfoTip text={tip} />}</div>
}

export function GameDetailPage() {
  const { id } = useParams<{ id: string }>()
  const isNew = !id || id === 'new'
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [tab, setTab] = useState<Tab>('Genel')
  const [err, setErr] = useState('')
  const [saving, setSaving] = useState(false)
  const [loaded, setLoaded] = useState(false)

  const [firmPlatformId, setFirmPlatformId] = useState('')
  const [code, setCode] = useState('')
  const [type, setType] = useState('wheel')
  const [title, setTitle] = useState('')
  const [subtitle, setSubtitle] = useState('')
  const [description, setDescription] = useState('')
  const [rulesText, setRulesText] = useState('')
  const [ctaLabel, setCtaLabel] = useState('')
  const [imageUrl, setImageUrl] = useState('')
  const [themeColor, setThemeColor] = useState('')
  const [accentColor, setAccentColor] = useState('')
  const [alwaysWin, setAlwaysWin] = useState(false)
  const [starts, setStarts] = useState(new Date().toISOString().slice(0, 10))
  const [ends, setEnds] = useState('')
  const [isActive, setIsActive] = useState(true)
  const [limitPeriod, setLimitPeriod] = useState('day')
  const [limitCount, setLimitCount] = useState(1)
  const [couponValidDays, setCouponValidDays] = useState(7)
  const [sortOrder, setSortOrder] = useState(0)
  const [labels, setLabels] = useState({
    labelAvailable: 'Bugün {n} hakkın var', labelCooldown: 'Yarın tekrar gel', labelExhausted: 'Hakkın bitti',
    labelLoginRequired: 'Oynamak için giriş yap', labelEnded: 'Kampanya bitti',
    winMessage: 'Tebrikler!', winSubMessage: '{prize} kazandın. Kuponlarım\'a eklendi.', loseMessage: 'Bu sefer olmadı', loseSubMessage: 'Bir dahaki sefere bol şans!',
  })
  const [prizes, setPrizes] = useState<Prize[]>([yeniOdul(0), yeniOdul(1), { ...yeniOdul(2), label: 'Bir dahaki sefere', shortLabel: 'Pas', kind: 'none', color: '#1F2937', couponType: null, couponValue: null }])
  const [stats, setStats] = useState({ playCount: 0, winCount: 0 })

  const { data: firms = [] } = useQuery<Firm[]>({ queryKey: ['firms'], queryFn: async () => (await api.get('/core/firms')).data.data ?? [] })
  const platformQueries = useQueries({
    queries: firms.map(firm => ({
      queryKey: ['firm-platforms', firm.id],
      queryFn: async (): Promise<Channel[]> => ((await api.get(`/core/firms/${firm.id}/platforms`)).data.data ?? []).map((ch: Channel) => ({ ...ch, firmName: tr(firm.nameI18n) })),
      enabled: firms.length > 0,
    })),
  })
  const channels: Channel[] = platformQueries.flatMap(q => q.data ?? [])

  useQuery({
    queryKey: ['game-detail', id],
    enabled: !isNew && !loaded,
    queryFn: async () => {
      const g: Game = (await api.get(`/promotion/games/${id}`)).data.data
      setFirmPlatformId(g.firmPlatformId); setCode(g.code); setType(g.type)
      setTitle(tr(g.titleI18n)); setSubtitle(tr(g.subtitleI18n)); setDescription(tr(g.descriptionI18n)); setRulesText(tr(g.rulesTextI18n))
      setCtaLabel(g.ctaLabel ?? ''); setImageUrl(g.imageUrl ?? ''); setThemeColor(g.themeColor ?? ''); setAccentColor(g.accentColor ?? '')
      setAlwaysWin(g.alwaysWin); setStarts((g.startsAt ?? '').slice(0, 10)); setEnds(g.endsAt ? g.endsAt.slice(0, 10) : ''); setIsActive(g.isActive)
      setLimitPeriod(g.limitPeriod); setLimitCount(g.limitCount); setCouponValidDays(g.couponValidDays); setSortOrder(g.sortOrder)
      setLabels({ labelAvailable: g.labelAvailable, labelCooldown: g.labelCooldown, labelExhausted: g.labelExhausted, labelLoginRequired: g.labelLoginRequired, labelEnded: g.labelEnded,
        winMessage: g.winMessage, winSubMessage: g.winSubMessage, loseMessage: g.loseMessage, loseSubMessage: g.loseSubMessage })
      setPrizes(g.prizes); setStats({ playCount: g.playCount, winCount: g.winCount }); setLoaded(true)
      return g
    },
  })

  const { data: plays } = useQuery<{ items: Play[]; totalCount: number }>({
    queryKey: ['game-plays', id],
    enabled: !isNew && tab === 'Oynanışlar',
    queryFn: async () => (await api.get(`/promotion/games/${id}/plays?pageSize=100`)).data.data,
  })

  const save = useMutation({
    mutationFn: async () => {
      const body = {
        firmPlatformId, code: code.trim().toLowerCase(), type,
        titleI18n: { tr: title.trim() }, subtitleI18n: subtitle ? { tr: subtitle } : null, descriptionI18n: description ? { tr: description } : null,
        rulesTextI18n: rulesText ? { tr: rulesText } : null, ctaLabel: ctaLabel || null, imageUrl: imageUrl || null, themeColor: themeColor || null, accentColor: accentColor || null,
        alwaysWin, startsAt: starts, endsAt: ends || null, isActive, limitPeriod, limitCount, couponValidDays, sortOrder, ...labels,
        prizes: prizes.map((p, i) => ({ ...p, sortOrder: i, id: p.id ?? null })),
      }
      if (isNew) return (await api.post('/promotion/games', body)).data.data.id as string
      await api.put(`/promotion/games/${id}`, body); return id!
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['games'] }); navigate('/promotion/games') },
    onError: (e) => setErr(errText(e)),
    onSettled: () => setSaving(false),
  })
  const kaydet = () => {
    setErr('')
    if (!firmPlatformId) { setTab('Genel'); setErr('Platform seçin.'); return }
    if (!code.trim()) { setTab('Genel'); setErr('Oyun kodu zorunludur.'); return }
    if (!title.trim()) { setTab('Genel'); setErr('Oyun adı zorunludur.'); return }
    if (prizes.some(p => !p.label.trim())) { setTab('Ödüller'); setErr('Her ödülün adı olmalıdır.'); return }
    if (alwaysWin && prizes.some(p => p.isActive && p.kind === 'none')) { setTab('Ödüller'); setErr('Herkes kazanır modunda "Pas" ödül olamaz.'); return }
    if (type === 'scratch' && prizes.filter(p => p.isActive && p.kind !== 'none').length < 3) { setTab('Ödüller'); setErr('Kazı kazanda en az 3 farklı kazandıran ödül gerekir.'); return }
    setSaving(true); save.mutate()
  }
  const remove = useMutation({
    mutationFn: async () => { await api.delete(`/promotion/games/${id}`) },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['games'] }); navigate('/promotion/games') },
    onError: (e) => setErr(errText(e)),
  })

  const setPrize = (i: number, patch: Partial<Prize>) => setPrizes(ps => ps.map((p, j) => j === i ? { ...p, ...patch } : p))
  const movePrize = (i: number, dir: -1 | 1) => setPrizes(ps => { const n = [...ps]; const j = i + dir; if (j < 0 || j >= n.length) return ps; [n[i], n[j]] = [n[j], n[i]]; return n })
  const toplamAgirlik = prizes.filter(p => p.isActive && p.weight > 0 && (!alwaysWin || p.kind !== 'none')).reduce((s, p) => s + p.weight, 0)
  const olasilik = (p: Prize) => (!p.isActive || p.weight <= 0 || (alwaysWin && p.kind === 'none') || toplamAgirlik === 0) ? 0 : Math.round(p.weight / toplamAgirlik * 1000) / 10
  const inp = 'inp'

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>{isNew ? 'Yeni Şans Oyunu' : `${title || code}`}</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{GAME_TYPES[type]}{!isNew && ` · ${stats.playCount} oynanış / ${stats.winCount} kazanan`}</p>
        </div>
        <div className="flex gap-2">
          {!isNew && <Button size="sm" variant="secondary" style={{ color: '#b91c1c' }} loading={remove.isPending}
            onClick={() => { if (window.confirm(`'${title || code}' oyunu silinsin mi? Oynanmış oyun silinemez, pasife alınır.`)) remove.mutate() }}>Sil</Button>}
          <Button size="sm" onClick={kaydet} disabled={saving}>{saving ? 'Kaydediliyor...' : 'Kaydet'}</Button>
        </div>
      </div>
      {err && <div className="mb-3 px-3 py-2 rounded text-sm" style={{ background: 'var(--danger-bg,#fef2f2)', color: '#b91c1c' }}>{err}</div>}
      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABS.filter(t => t !== 'Oynanışlar' || !isNew).map(t => <button key={t} className={cn('stab', tab === t && 'active')} onClick={() => setTab(t)}>{t}</button>)}
      </div>

      {tab === 'Genel' && (
        <div className="card p-4 space-y-3 max-w-2xl">
          <div className="grid grid-cols-2 gap-3">
            <div><Lbl required tip="Oyunun çıkacağı satış kanalı (mobil uygulamanın kanalı).">Platform</Lbl>
              <select className={inp} value={firmPlatformId} onChange={e => setFirmPlatformId(e.target.value)}>
                <option value="">Platform seçin</option>
                {channels.map(c => <option key={c.id} value={c.id}>{tr(c.nameI18n) || c.code} ({c.firmName})</option>)}
              </select></div>
            <div><Lbl required tip="Çark: ödüller dilim sırasıyla döner. Salla kazan: kutudan tek ödül çıkar. Kazı kazan: 6 kutucuk, 3 aynı = kazandı; en az 3 farklı kazandıran ödül gerekir.">Oyun tipi</Lbl>
              <select className={inp} value={type} onChange={e => setType(e.target.value)}>
                {Object.entries(GAME_TYPES).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select></div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div><Lbl required tip="Mobil derin link ve push anahtarı: /oyunlar/{kod}. Küçük harf, rakam ve tire. Kanal içinde benzersiz.">Kod</Lbl>
              <input className={inp} value={code} onChange={e => setCode(e.target.value)} placeholder="cark" /></div>
            <div><Lbl required tip="Oyun ekranının başlığı.">Ad</Lbl>
              <input className={inp} value={title} onChange={e => setTitle(e.target.value)} placeholder="Çarkıfelek" /></div>
          </div>
          <div><Lbl tip="Kart ve oyun ekranı alt başlığı.">Alt başlık</Lbl><input className={inp} value={subtitle} onChange={e => setSubtitle(e.target.value)} placeholder="Çevir, indirim kuponunu kap!" /></div>
          <div><Lbl tip="Kurallar panelinin giriş metni.">Açıklama</Lbl><input className={inp} value={description} onChange={e => setDescription(e.target.value)} /></div>
          <div className="grid grid-cols-3 gap-3">
            <div><Lbl tip="Bugün: gün başına (İstanbul günü). Hafta: ISO haftası. Toplam: kampanya boyunca.">Hak dönemi</Lbl>
              <select className={inp} value={limitPeriod} onChange={e => setLimitPeriod(e.target.value)}>
                {Object.entries(LIMIT_PERIODS).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select></div>
            <div><Lbl tip="Dönem başına oynama hakkı.">Hak sayısı</Lbl><input className={inp} type="number" min={1} value={limitCount} onChange={e => setLimitCount(Math.max(1, Number(e.target.value)))} /></div>
            <div><Lbl tip="Kazanılan kuponun geçerlilik süresi (gün).">Kupon geçerliliği (gün)</Lbl><input className={inp} type="number" min={1} value={couponValidDays} onChange={e => setCouponValidDays(Math.max(1, Number(e.target.value)))} /></div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div><Lbl tip="Oyunun sitede/uygulamada görünmeye başlayacağı gün.">Başlangıç</Lbl><input className={inp} type="date" value={starts} onChange={e => setStarts(e.target.value)} /></div>
            <div><Lbl tip="Son gün (dahil). Boş = süresiz.">Bitiş</Lbl><input className={inp} type="date" value={ends} onChange={e => setEnds(e.target.value)} /></div>
          </div>
          <div className="grid grid-cols-3 gap-3">
            <div><Lbl tip="Ana sayfadaki yuvarlak ikonun görseli (kare/yuvarlak, en az 128px). Boşsa tipin ikonu.">İkon görseli (URL)</Lbl><input className={inp} value={imageUrl} onChange={e => setImageUrl(e.target.value)} placeholder="https://…/cark.png" /></div>
            <div><Lbl tip="Oyun ekranı zemin rengi (#RRGGBB). Boşsa tema rengi.">Tema rengi</Lbl><input className={inp} value={themeColor} onChange={e => setThemeColor(e.target.value)} placeholder="#5B21B6" /></div>
            <div><Lbl tip="Ok, rozet ve ödül vurgu rengi (#RRGGBB).">Vurgu rengi</Lbl><input className={inp} value={accentColor} onChange={e => setAccentColor(e.target.value)} placeholder="#F59E0B" /></div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div><Lbl tip="Ana düğme metni. Boşsa mobil varsayılanı (Oyna / Çevir / Kartı Kazı).">Düğme metni</Lbl><input className={inp} value={ctaLabel} onChange={e => setCtaLabel(e.target.value)} placeholder="Çarkı Çevir" /></div>
            <div><Lbl tip="Aynı anda birden çok oyun yayındaysa ikon sırası.">Sıra</Lbl><input className={inp} type="number" value={sortOrder} onChange={e => setSortOrder(Number(e.target.value))} /></div>
          </div>
          <div className="flex flex-wrap items-center gap-6 text-sm" style={{ color: 'var(--text)' }}>
            <label className="flex items-center gap-2"><input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} /> Aktif</label>
            <span className="inline-flex items-center"><label className="flex items-center gap-2"><input type="checkbox" checked={alwaysWin} onChange={e => setAlwaysWin(e.target.checked)} /> Herkes kazanır</label>
              <InfoTip text="Açıkken 'Pas' ödül tanımlanamaz ve her oynanış mutlaka kazanır. Kapalıyken kaybetme olasılığı Pas ödülünün ağırlığıyla belirlenir." /></span>
            <span className="text-xs" style={{ color: 'var(--text-s)' }}>Misafir oynayamaz (kupon üyeye bağlanır); misafir kartı görür, girişe yönlenir.</span>
          </div>
        </div>
      )}

      {tab === 'Ödüller' && (
        <div className="card p-4 space-y-3 max-w-5xl">
          <p className="text-xs" style={{ color: 'var(--text-s)' }}>
            Çarkta sıra = dilim sırası (saat yönünde, üstten). Ağırlık = olasılık payı (0 = hiç çıkmaz; kazı kazanda yalnız dolgu). Kupon ödülü oynanınca üyeye özel, tek kullanımlık kupon üretilir ve Kuponlarım'a düşer.
          </p>
          <div className="tbl-wrap">
            <table className="w-full" style={{ minWidth: 980 }}>
              <thead><tr style={{ background: 'var(--surface2)' }}>
                {['#', 'AD', 'KISA', 'TÜR', 'DEĞER', 'ASGARİ SEPET', 'KOŞUL METNİ', 'RENK', 'AĞIRLIK', 'OLASILIK', 'AKTİF', ''].map(h => <th key={h} className="px-2 py-2 text-left text-xs font-semibold" style={{ color: 'var(--text-s)' }}>{h}</th>)}
              </tr></thead>
              <tbody>
                {prizes.map((p, i) => (
                  <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td className="px-2 py-1 text-xs" style={{ color: 'var(--text-s)' }}>
                      <button type="button" className="px-1" onClick={() => movePrize(i, -1)} title="Yukarı">▲</button>
                      <button type="button" className="px-1" onClick={() => movePrize(i, 1)} title="Aşağı">▼</button>
                    </td>
                    <td className="px-2 py-1"><input className="inp text-sm py-1" value={p.label} onChange={e => setPrize(i, { label: e.target.value })} placeholder="50 TL İndirim" /></td>
                    <td className="px-2 py-1"><input className="inp text-sm py-1 w-20" value={p.shortLabel ?? ''} onChange={e => setPrize(i, { shortLabel: e.target.value })} placeholder="50 TL" /></td>
                    <td className="px-2 py-1"><select className="inp text-sm py-1" value={p.kind} onChange={e => setPrize(i, { kind: e.target.value, couponType: e.target.value === 'coupon' ? (p.couponType ?? 'fixed') : null, couponValue: e.target.value === 'coupon' ? (p.couponValue ?? 50) : null, points: e.target.value === 'points' ? (p.points ?? 100) : null })}>
                      {KINDS.map(k => <option key={k.value} value={k.value}>{k.label}</option>)}</select></td>
                    <td className="px-2 py-1">
                      {p.kind === 'coupon' && <div className="flex items-center gap-1">
                        <select className="inp text-sm py-1 w-20" value={p.couponType ?? 'fixed'} onChange={e => setPrize(i, { couponType: e.target.value })}><option value="fixed">TL</option><option value="percentage">%</option></select>
                        <input className="inp text-sm py-1 w-20" type="number" min={0} value={p.couponValue ?? ''} onChange={e => setPrize(i, { couponValue: Number(e.target.value) })} /></div>}
                      {p.kind === 'points' && <input className="inp text-sm py-1 w-24" type="number" min={1} value={p.points ?? ''} onChange={e => setPrize(i, { points: Number(e.target.value) })} placeholder="puan" />}
                      {p.kind === 'none' && <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span>}
                    </td>
                    <td className="px-2 py-1">{p.kind === 'coupon' ? <input className="inp text-sm py-1 w-24" type="number" min={0} value={p.minimumCartTotal ?? ''} onChange={e => setPrize(i, { minimumCartTotal: e.target.value === '' ? null : Number(e.target.value) })} placeholder="TL" /> : '—'}</td>
                    <td className="px-2 py-1"><input className="inp text-sm py-1" value={p.description ?? ''} onChange={e => setPrize(i, { description: e.target.value })} placeholder="300 TL üzeri" /></td>
                    <td className="px-2 py-1"><div className="flex items-center gap-1"><input type="color" value={p.color || '#7C3AED'} onChange={e => setPrize(i, { color: e.target.value })} /><input className="inp text-xs py-1 w-24 font-mono" value={p.color ?? ''} onChange={e => setPrize(i, { color: e.target.value })} placeholder="#RRGGBB" /></div></td>
                    <td className="px-2 py-1"><input className="inp text-sm py-1 w-16" type="number" min={0} value={p.weight} onChange={e => setPrize(i, { weight: Math.max(0, Number(e.target.value)) })} /></td>
                    <td className="px-2 py-1 text-sm tabular-nums" style={{ color: 'var(--text-m)' }}>%{olasilik(p)}</td>
                    <td className="px-2 py-1"><input type="checkbox" checked={p.isActive} onChange={e => setPrize(i, { isActive: e.target.checked })} /></td>
                    <td className="px-2 py-1 text-right"><button type="button" className="text-xs" style={{ color: '#b91c1c' }} onClick={() => setPrizes(ps => ps.filter((_, j) => j !== i))}>Kaldır</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Button size="sm" variant="secondary" onClick={() => setPrizes(ps => [...ps, yeniOdul(ps.length)])}>+ Ödül Ekle</Button>
        </div>
      )}

      {tab === 'Metinler' && (
        <div className="card p-4 space-y-3 max-w-2xl">
          <p className="text-xs" style={{ color: 'var(--text-s)' }}>Mobil metin üretmez; burada yazılan aynen görünür. Yer tutucular: <code>{'{n}'}</code> kalan hak, <code>{'{next}'}</code> sonraki hak günü, <code>{'{prize}'}</code> ödül adı.</p>
          {([
            ['labelAvailable', 'Hak varken (durum etiketi)'], ['labelCooldown', 'Hak bitti, dönem sonunda yenilenecek'], ['labelExhausted', 'Toplam hak bitti'],
            ['labelLoginRequired', 'Giriş gerekli'], ['labelEnded', 'Kampanya bitti'],
            ['winMessage', 'Kazandı — başlık'], ['winSubMessage', 'Kazandı — açıklama'], ['loseMessage', 'Kaybetti — başlık'], ['loseSubMessage', 'Kaybetti — açıklama'],
          ] as [keyof typeof labels, string][]).map(([k, l]) => (
            <div key={k}><Lbl>{l}</Lbl><input className={inp} value={labels[k]} onChange={e => setLabels(s => ({ ...s, [k]: e.target.value }))} /></div>
          ))}
          <div><Lbl tip="Satır sonlu düz metin; mobil (i) ikonuyla açar.">Kurallar metni</Lbl>
            <textarea className={cn(inp, 'min-h-[100px]')} value={rulesText} onChange={e => setRulesText(e.target.value)} placeholder={'• Günde 1 çevirme hakkı.\n• Kuponlar 7 gün geçerlidir.'} /></div>
        </div>
      )}

      {tab === 'Oynanışlar' && (
        <div className="card overflow-hidden max-w-4xl">
          <table className="w-full">
            <thead><tr style={{ background: 'var(--surface2)' }}>
              {['TARİH', 'ÜYE', 'DÖNEM', 'SONUÇ', 'ÖDÜL', 'KUPON / PUAN'].map(h => <th key={h} className="px-3 py-2 text-left text-xs font-semibold" style={{ color: 'var(--text-s)' }}>{h}</th>)}
            </tr></thead>
            <tbody>
              {(plays?.items ?? []).map(p => (
                <tr key={p.id} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-3 py-2 text-xs" style={{ color: 'var(--text-s)' }}>{new Date(p.playedAt).toLocaleString('tr-TR')}</td>
                  <td className="px-3 py-2 text-xs font-mono" style={{ color: 'var(--text-m)' }}>{p.memberId.slice(0, 8)}</td>
                  <td className="px-3 py-2 text-xs" style={{ color: 'var(--text-s)' }}>{p.periodKey}</td>
                  <td className="px-3 py-2"><Badge variant={p.won ? 'success' : 'neutral'}>{p.won ? 'Kazandı' : 'Pas'}</Badge></td>
                  <td className="px-3 py-2 text-sm" style={{ color: 'var(--text)' }}>{p.prizeLabel ?? '—'}</td>
                  <td className="px-3 py-2 text-xs font-mono" style={{ color: 'var(--text-m)' }}>{p.couponCode ?? (p.pointsGiven ? `${p.pointsGiven} puan` : '—')}</td>
                </tr>
              ))}
              {(plays?.items ?? []).length === 0 && <tr><td colSpan={6} className="px-3 py-6 text-center text-sm" style={{ color: 'var(--text-s)' }}>Henüz oynanmadı.</td></tr>}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

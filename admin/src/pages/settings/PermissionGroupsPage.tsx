import { useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText as gridErrText } from '@/components/ui/DataTable.utils'
import { cn } from '@/lib/utils'

/**
 * Y4 (2026-09-09) — Yetki Grupları.
 * Grup = toplu yetki verme aracıdır, ayrı bir otorite değildir: yalnız VERİR, yasaklamaz.
 * Yasaklama tek yerdedir: kullanıcı istisnası (Kullanıcı Yetkileri ekranı).
 * Kanal kapsamlı yetkilerde "Tüm kanallar" seçimi O ANKİ kanalların listesi olarak kaydedilir —
 * sonradan açılan kanal otomatik kapsanmaz (default deny).
 */
interface Grup {
  id: string; code: string; ad: string; aciklama?: string
  aktif: boolean; sistem: boolean; kullaniciSayisi: number; yetkiSayisi: number; gecisGrubu: boolean
}
interface KatalogSatiri {
  id: string; code: string; ad: string; aciklama?: string; modul: string; sayfa?: string
  tur: string; kanalKapsamli: boolean; aktif: boolean; koddaTanimli: boolean
}
interface GrupYetkisi { permissionId: string; code: string; kanallar: string[] }
interface GrupUyesi { userId: string; adSoyad: string; email: string; superAdmin: boolean }
interface GrupDetay {
  id: string; code: string; ad: string; aciklama?: string; aktif: boolean; sistem: boolean
  yetkiler: GrupYetkisi[]; uyeler: GrupUyesi[]
}
interface Kanal { id: string; code: string; nameI18n?: Record<string, string> }
interface Sablon { kod: string; ad: string; aciklama: string; yetkiSayisi: number; kurulu: boolean; grupId?: string }
interface GecisKullanicisi {
  userId: string; adSoyad: string; email: string; departman?: string
  superAdmin: boolean; gruplar: string[]; yetkiSayisi: number
}
interface GecisDurumu {
  aktifKullanici: number; superAdminSayisi: number; tamErisimliKullanici: number
  gecisGrubuUyesi: number; gecisGrubuVar: boolean; gecisGrubuId?: string
  aktifYetkiSayisi: number; dikkatGerektiren: GecisKullanicisi[]
}
interface Kullanici { id: string; firstName: string; lastName: string; email?: string }
interface Sayfali<T> { items: T[]; totalCount: number; page: number; pageSize: number }

const BOS_KANAL: Kanal[] = []
const turEtiketi = (t: string) => (t === 'page' ? 'Sayfa' : t === 'field' ? 'Alan' : 'İşlem')

function hataMetni(e: unknown) {
  const err = e as { response?: { data?: { error?: string } } }
  return err.response?.data?.error ?? 'İşlem başarısız oldu.'
}

function GrupDetayModal({ grupId, onClose }: { grupId: string; onClose: () => void }) {
  const qc = useQueryClient()
  const [sekme, setSekme] = useState<'yetkiler' | 'uyeler'>('yetkiler')
  const [hata, setHata] = useState('')
  const [arama, setArama] = useState('')
  const [kanalAcik, setKanalAcik] = useState<string | null>(null)

  const { data: detay } = useQuery<GrupDetay>({
    queryKey: ['yetki-grubu', grupId],
    queryFn: async () => (await api.get(`/iam/permission-groups/${grupId}`)).data.data,
  })
  const { data: katalog = [] } = useQuery<KatalogSatiri[]>({
    queryKey: ['yetki-katalogu', 'aktif'],
    queryFn: async () => (await api.get('/iam/permissions?activeOnly=true')).data.data,
  })
  const { data: kanallar = BOS_KANAL } = useQuery<Kanal[]>({
    queryKey: ['kanallar-yetki'],
    // 2026-09-10: /core/firm-platforms diye bir uç yoktu (404 → kanal seçimi boş kalıyordu);
    // liste yetki API'sinden gelir, "tüm kanallar (bugünkü liste)" ile aynı kaynak.
    queryFn: async () => (await api.get('/iam/permission-channels')).data.data ?? BOS_KANAL,
  })
  const { data: kullanicilar } = useQuery<{ items: Kullanici[] }>({
    queryKey: ['kullanicilar-yetki'],
    queryFn: async () => (await api.get('/iam/users?pageSize=200')).data.data,
    enabled: sekme === 'uyeler',
  })

  // seçim durumu: permissionId → kanal listesi (kanal kapsamsızda boş dizi)
  const [secim, setSecim] = useState<Record<string, string[]> | null>(null)
  const mevcutSecim = useMemo(() => {
    if (secim) return secim
    if (!detay) return {}
    const s: Record<string, string[]> = {}
    for (const y of detay.yetkiler) s[y.permissionId] = y.kanallar ?? []
    return s
  }, [secim, detay])

  const degistir = (yeni: Record<string, string[]>) => setSecim(yeni)

  const kaydet = useMutation({
    mutationFn: async () => {
      setHata('')
      await api.put(`/iam/permission-groups/${grupId}/permissions`, {
        yetkiler: Object.entries(mevcutSecim).map(([permissionId, channelIds]) => ({ permissionId, channelIds })),
      })
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['yetki-grubu', grupId] })
      qc.invalidateQueries({ queryKey: ['yetki-gruplari'] })
      setSecim(null)
    },
    onError: (e: unknown) => setHata(hataMetni(e)),
  })

  const uyelik = useMutation({
    mutationFn: async ({ userId, ekle }: { userId: string; ekle: boolean }) => {
      setHata('')
      if (ekle) await api.post(`/iam/permission-groups/${grupId}/members/${userId}`)
      else await api.delete(`/iam/permission-groups/${grupId}/members/${userId}`)
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['yetki-grubu', grupId] }),
    onError: (e: unknown) => setHata(hataMetni(e)),
  })

  // modül → sayfa → yetkiler
  const agac = useMemo(() => {
    const filtre = arama.trim().toLowerCase()
    const uygun = katalog.filter(k =>
      !filtre || k.ad.toLowerCase().includes(filtre) || k.code.toLowerCase().includes(filtre))
    const m = new Map<string, KatalogSatiri[]>()
    for (const k of uygun) {
      const liste = m.get(k.modul) ?? []
      liste.push(k); m.set(k.modul, liste)
    }
    return [...m.entries()]
  }, [katalog, arama])

  const kanalAdi = (id: string) =>
    kanallar.find(k => k.id === id)?.nameI18n?.['tr'] ?? kanallar.find(k => k.id === id)?.code ?? id.slice(0, 8)

  const toggle = (k: KatalogSatiri) => {
    const y = { ...mevcutSecim }
    if (k.id in y) delete y[k.id]
    // Yeni seçimde kanal kapsamlı yetkiye O ANKİ tüm kanallar yazılır (Ek-2: anlık liste).
    else y[k.id] = k.kanalKapsamli ? kanallar.map(x => x.id) : []
    degistir(y)
  }

  const modulToggle = (satirlar: KatalogSatiri[], sec: boolean) => {
    const y = { ...mevcutSecim }
    for (const k of satirlar) {
      if (sec) y[k.id] = k.kanalKapsamli ? (y[k.id] ?? kanallar.map(x => x.id)) : []
      else delete y[k.id]
    }
    degistir(y)
  }

  return (
    <Modal open onClose={onClose} title={`Yetki Grubu: ${detay?.ad ?? ''}`} size="lg">
      <div className="tab-scroll flex gap-1 mb-3" style={{ borderBottom: '1px solid var(--border)' }}>
        <button className={cn('stab', sekme === 'yetkiler' && 'active')} onClick={() => setSekme('yetkiler')}>
          Yetkiler ({Object.keys(mevcutSecim).length})
        </button>
        <button className={cn('stab', sekme === 'uyeler' && 'active')} onClick={() => setSekme('uyeler')}>
          Üyeler ({detay?.uyeler.length ?? 0})
        </button>
      </div>

      {sekme === 'yetkiler' && (
        <div>
          <input className="inp mb-3" placeholder="Yetki ara…" value={arama} onChange={e => setArama(e.target.value)} />
          <div className="max-h-[52vh] overflow-y-auto space-y-3">
            {agac.map(([modul, satirlar]) => (
              <div key={modul}>
                <div className="flex items-center gap-2 mb-1">
                  <strong className="text-sm" style={{ color: 'var(--text)' }}>{modul}</strong>
                  <button className="text-xs underline" style={{ color: 'var(--brand)' }}
                    onClick={() => modulToggle(satirlar, true)}>tümünü seç</button>
                  <button className="text-xs underline" style={{ color: 'var(--text-s)' }}
                    onClick={() => modulToggle(satirlar, false)}>kaldır</button>
                </div>
                <div className="space-y-1">
                  {satirlar.map(k => {
                    const secili = k.id in mevcutSecim
                    const kanalSayisi = mevcutSecim[k.id]?.length ?? 0
                    return (
                      <div key={k.id} className="flex items-center gap-2 px-2 py-1.5 rounded-lg"
                        style={{ background: secili ? 'var(--surface2)' : 'transparent' }}>
                        <input type="checkbox" checked={secili} onChange={() => toggle(k)} />
                        <span className="text-sm flex-1" style={{ color: 'var(--text)' }}>
                          {k.ad}
                          <span className="text-xs ml-2" style={{ color: 'var(--text-s)' }}>{turEtiketi(k.tur)}</span>
                          {!k.koddaTanimli && (
                            <span className="text-xs ml-2" style={{ color: '#DC2626' }}>uygulamada karşılığı yok</span>
                          )}
                        </span>
                        {k.kanalKapsamli && secili && (
                          <button className="text-xs px-2 py-0.5 rounded-lg"
                            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}
                            onClick={() => setKanalAcik(kanalAcik === k.id ? null : k.id)}>
                            Kanallar: {kanalSayisi}/{kanallar.length}
                          </button>
                        )}
                        {!k.kanalKapsamli && (
                          <span className="text-xs" style={{ color: 'var(--text-s)' }}>kanaldan bağımsız</span>
                        )}
                      </div>
                    )
                  })}
                  {satirlar.filter(k => kanalAcik === k.id).map(k => (
                    <div key={`kanal-${k.id}`} className="ml-6 mb-2 p-2 rounded-lg"
                      style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
                      <div className="text-xs mb-1" style={{ color: 'var(--text-s)' }}>
                        {k.ad} — hangi kanallarda geçerli?
                      </div>
                      {kanallar.map(kn => {
                        const secili = (mevcutSecim[k.id] ?? []).includes(kn.id)
                        return (
                          <label key={kn.id} className="flex items-center gap-2 text-sm py-0.5"
                            style={{ color: 'var(--text)' }}>
                            <input type="checkbox" checked={secili} onChange={() => {
                              const y = { ...mevcutSecim }
                              const mevcut = new Set(y[k.id] ?? [])
                              if (secili) mevcut.delete(kn.id); else mevcut.add(kn.id)
                              y[k.id] = [...mevcut]
                              degistir(y)
                            }} />
                            {kanalAdi(kn.id)}
                          </label>
                        )
                      })}
                      <button className="text-xs underline mt-1" style={{ color: 'var(--brand)' }}
                        onClick={() => degistir({ ...mevcutSecim, [k.id]: kanallar.map(x => x.id) })}>
                        tüm kanallar (bugünkü liste)
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {sekme === 'uyeler' && (
        <div className="max-h-[52vh] overflow-y-auto">
          {(detay?.uyeler ?? []).map(u => (
            <div key={u.userId} className="flex items-center gap-2 px-2 py-1.5 rounded-lg"
              style={{ background: 'var(--surface2)', marginBottom: 4 }}>
              <span className="text-sm flex-1" style={{ color: 'var(--text)' }}>
                {u.adSoyad} <span className="text-xs" style={{ color: 'var(--text-s)' }}>{u.email}</span>
                {u.superAdmin && <Badge variant="info">Süper Admin</Badge>}
              </span>
              <button className="text-xs underline" style={{ color: '#DC2626' }}
                onClick={() => uyelik.mutate({ userId: u.userId, ekle: false })}>çıkar</button>
            </div>
          ))}
          <div className="mt-3">
            <label className="flbl">Gruba kullanıcı ekle</label>
            <select className="inp" defaultValue="" onChange={e => {
              if (e.target.value) { uyelik.mutate({ userId: e.target.value, ekle: true }); e.target.value = '' }
            }}>
              <option value="">— kullanıcı seçin —</option>
              {(kullanicilar?.items ?? [])
                .filter(u => !(detay?.uyeler ?? []).some(x => x.userId === u.id))
                .map(u => <option key={u.id} value={u.id}>{u.firstName} {u.lastName}</option>)}
            </select>
          </div>
        </div>
      )}

      {hata && <p className="text-sm text-red-500 mt-2">{hata}</p>}

      <div className="flex items-center gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        <span className="text-xs" style={{ color: 'var(--text-s)' }}>
          Gruplar yalnız yetki VERİR. Bir kullanıcıda tekil kısıtlama gerekiyorsa Kullanıcı Yetkileri ekranından istisna tanımlayın.
        </span>
        <div className="flex-1" />
        <Button variant="secondary" onClick={onClose}>Kapat</Button>
        {sekme === 'yetkiler' && (
          <Button onClick={() => kaydet.mutate()} loading={kaydet.isPending} disabled={secim === null}>
            Yetkileri Kaydet
          </Button>
        )}
      </div>
    </Modal>
  )
}

export function PermissionGroupsPage() {
  const qc = useQueryClient()
  const [detayId, setDetayId] = useState<string | null>(null)
  const [yeniAcik, setYeniAcik] = useState(false)
  const [sablonAcik, setSablonAcik] = useState(false)
  const [kopyaGrup, setKopyaGrup] = useState<Grup | null>(null)
  const [kopyaAd, setKopyaAd] = useState('')
  const [silinecek, setSilinecek] = useState<Grup | null>(null)
  const [ad, setAd] = useState(''); const [aciklama, setAciklama] = useState(''); const [hata, setHata] = useState('')

  // DataGrid (2026-09-09, tur 11): sunucu filtre/sıralama/arama (YetkiGrubuGrid.Schema) + Excel + görünümler.
  // ★ Ayrı uç: /iam/permission-groups TAM liste döner (Kullanıcı Yetkileri ekranının grup kaynağı);
  // bu ekran sayfalı /iam/permission-groups/grid kullanır.
  const [sp] = useSearchParams()
  const yalnizAktif = sp.get('activeOnly') === 'true'
  const sistemHaric = sp.get('sistemHaric') === 'true'
  const grid = useGridState('permission-groups', { defaultPageSize: 50, defaultSort: 'ad', defaultDir: 'asc' })
  const setNamed = (k: string, v: string) => grid.mutate(n => { if (v) n.set(k, v); else n.delete(k) })
  const named = () => ({
    activeOnly: yalnizAktif ? 'true' : undefined,
    sistemHaric: sistemHaric ? 'true' : undefined,
  })

  const { data, isLoading, isFetching, error: listError } = useQuery<Sayfali<Grup>>({
    queryKey: ['yetki-gruplari-grid', yalnizAktif, sistemHaric, ...grid.queryKey],
    queryFn: async () => (await api.get(`/iam/permission-groups/grid?${grid.toParams(named())}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })
  const gruplar = data?.items ?? []
  const { data: sablonlar = [] } = useQuery<Sablon[]>({
    queryKey: ['grup-sablonlari'],
    queryFn: async () => (await api.get('/iam/group-templates')).data.data,
  })
  const { data: gecis } = useQuery<GecisDurumu>({
    queryKey: ['gecis-durumu'],
    queryFn: async () => (await api.get('/iam/transition-status')).data.data,
  })

  const yenile = () => {
    qc.invalidateQueries({ queryKey: ['yetki-gruplari'] })       // düz liste (Kullanıcı Yetkileri kaynağı)
    qc.invalidateQueries({ queryKey: ['yetki-gruplari-grid'] })  // bu ekranın sayfalı listesi
    qc.invalidateQueries({ queryKey: ['grup-sablonlari'] })
    qc.invalidateQueries({ queryKey: ['gecis-durumu'] })
  }

  const sablondanKur = useMutation({
    mutationFn: async (kod: string) => { setHata(''); await api.post(`/iam/group-templates/${kod}`) },
    onSuccess: yenile,
    onError: (e: unknown) => setHata(hataMetni(e)),
  })
  const kopyala = useMutation({
    mutationFn: async () => { setHata(''); await api.post(`/iam/permission-groups/${kopyaGrup!.id}/copy`, { ad: kopyaAd }) },
    onSuccess: () => { yenile(); setKopyaGrup(null); setKopyaAd('') },
    onError: (e: unknown) => setHata(hataMetni(e)),
  })
  const kaldir = useMutation({
    mutationFn: async () => { setHata(''); await api.delete(`/iam/permission-groups/${silinecek!.id}`) },
    onSuccess: () => { yenile(); setSilinecek(null) },
    onError: (e: unknown) => setHata(hataMetni(e)),
  })

  const olustur = useMutation({
    mutationFn: async () => { setHata(''); await api.post('/iam/permission-groups', { ad, aciklama, aktif: true }) },
    onSuccess: () => { yenile(); setYeniAcik(false); setAd(''); setAciklama('') },
    onError: (e: unknown) => setHata(hataMetni(e)),
  })

  const columns: GridColumn<Grup>[] = [
    { key: 'ad', header: 'GRUP', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 260,
      filter: { type: 'text', label: 'Grup adı' },
      filters: [{ field: 'code', label: 'Kod', type: 'text' }],
      cell: g => <div className="flex items-center gap-2 flex-wrap">
        <span className="text-sm" style={{ color: 'var(--text)' }}>{g.ad}</span>
        {g.gecisGrubu && <Badge variant="warning">geçici</Badge>}
        {g.sistem && <Badge variant="info">sistem</Badge>}
        {gecis && g.yetkiSayisi >= gecis.aktifYetkiSayisi && <Badge variant="warning">tam erişim</Badge>}
      </div> },
    { key: 'aciklama', header: 'AÇIKLAMA', priority: 2, sortable: true, minWidth: 240,
      filter: { type: 'text', label: 'Açıklama' },
      cell: g => <span className="text-sm" style={{ color: 'var(--text-s)' }}>{g.aciklama ?? '—'}</span> },
    { key: 'kullaniciSayisi', header: 'KULLANICI', priority: 1, align: 'right', sortable: true,
      filter: { type: 'number', label: 'Kullanıcı sayısı' },
      filters: [{ field: 'bosGrup', label: 'Üyesi olmayan', type: 'boolean' }],
      cell: g => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{g.kullaniciSayisi}</span> },
    { key: 'yetkiSayisi', header: 'YETKİ', priority: 1, align: 'right', sortable: true,
      filter: { type: 'number', label: 'Yetki sayısı' },
      cell: g => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{g.yetkiSayisi}</span> },
    { key: 'aktif', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'boolean', label: 'Aktif' },
      filters: [
        { field: 'sistem', label: 'Sistem grubu', type: 'boolean' },
        { field: 'gecisGrubu', label: 'Geçici grup', type: 'boolean' }],
      cell: g => <Badge variant={g.aktif ? 'success' : 'neutral'}>{g.aktif ? 'Aktif' : 'Pasif'}</Badge> },
    { key: 'islem', header: '', priority: 2, align: 'right', exportable: false, stopRowClick: true, minWidth: 190,
      cell: g => <span className="whitespace-nowrap">
        <button className="text-xs mr-3 hover:underline" style={{ color: 'var(--text-s)' }}
          onClick={() => { setHata(''); setKopyaGrup(g); setKopyaAd(`${g.ad} (kopya)`) }}>
          Kopyala
        </button>
        {!g.sistem && (
          <button className="text-xs mr-3 hover:underline"
            style={{ color: g.kullaniciSayisi > 0 ? 'var(--text-s)' : '#dc2626', opacity: g.kullaniciSayisi > 0 ? 0.5 : 1 }}
            title={g.kullaniciSayisi > 0 ? 'Önce kullanıcıları başka gruba taşıyın' : 'Grubu kaldır'}
            onClick={() => { if (g.kullaniciSayisi === 0) { setHata(''); setSilinecek(g) } }}>
            Kaldır
          </button>
        )}
        <span className="text-xs" style={{ color: 'var(--text-s)' }}>Düzenle →</span>
      </span> },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Yetki Grupları</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {(data?.totalCount ?? 0).toLocaleString('tr-TR')} grup{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''} — grup, kullanıcılara toplu yetki vermenin kolay yoludur
          </p>
        </div>
        <div className="flex gap-2">
          <Button size="sm" variant="secondary" onClick={() => setSablonAcik(true)}>Şablondan Kur</Button>
          <Button size="sm" onClick={() => setYeniAcik(true)}>+ Yeni Grup</Button>
        </div>
      </div>

      {/* Y8 (K8): geçiş panosu — "kimler hâlâ her şeyi görüyor?" sorusunun tek yanıtı. */}
      {gecis && (
        <div className="card p-4 mb-4">
          <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-sm">
            <span style={{ color: 'var(--text-s)' }}>
              Aktif kullanıcı <b style={{ color: 'var(--text)' }}>{gecis.aktifKullanici}</b>
            </span>
            <span style={{ color: 'var(--text-s)' }}>
              Süper admin <b style={{ color: 'var(--text)' }}>{gecis.superAdminSayisi}</b>
            </span>
            <span style={{ color: 'var(--text-s)' }}>
              Tam erişimli <b style={{ color: gecis.tamErisimliKullanici > gecis.superAdminSayisi ? 'var(--danger, #dc2626)' : 'var(--text)' }}>
                {gecis.tamErisimliKullanici}
              </b> / {gecis.aktifKullanici}
            </span>
            {gecis.gecisGrubuVar && (
              <span style={{ color: 'var(--text-s)' }}>
                Geçiş grubunda <b style={{ color: 'var(--text)' }}>{gecis.gecisGrubuUyesi}</b> kişi
              </span>
            )}
            {!gecis.gecisGrubuVar && <Badge variant="success">geçiş grubu kaldırıldı</Badge>}
          </div>
          {gecis.dikkatGerektiren.length > 0 && (
            <div className="mt-3 pt-3" style={{ borderTop: '1px solid var(--border)' }}>
              <p className="text-xs mb-2" style={{ color: 'var(--text-s)' }}>
                Süper admin olmadığı hâlde neredeyse tüm yetkileri taşıyan kullanıcılar — K8 adım 4'te
                bunlar gerçek departman gruplarına taşınmalı:
              </p>
              <div className="flex flex-wrap gap-2">
                {gecis.dikkatGerektiren.map(k => (
                  <span key={k.userId} className="text-xs px-2 py-1 rounded"
                    style={{ background: 'var(--surface2)', color: 'var(--text-m)' }}>
                    {k.adSoyad}{k.departman ? ` · ${k.departman}` : ''} — {k.yetkiSayisi}/{gecis.aktifYetkiSayisi} yetki
                    {k.gruplar.length > 0 && ` (${k.gruplar.join(', ')})`}
                  </span>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      <DataGrid<Grup>
        gridId="permission-groups"
        views
        grid={grid}
        columns={columns}
        rows={gruplar}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? gridErrText(listError) : null}
        onRowClick={g => setDetayId(g.id)}
        empty='Ölçütlere uyan grup yok. "+ Yeni Grup" ile departman gruplarınızı oluşturun.'
        search={{ placeholder: 'Grup adı, kodu veya açıklaması ara…' }}
        minWidth={1040}
        pageSizes={[50, 100, 200]}
        filterLeading={
          <>
            <label className="flex items-center gap-1.5 text-sm whitespace-nowrap" style={{ color: 'var(--text)' }}>
              <input type="checkbox" checked={yalnizAktif}
                onChange={e => setNamed('activeOnly', e.target.checked ? 'true' : '')} />
              Yalnız aktif
            </label>
            <label className="flex items-center gap-1.5 text-sm whitespace-nowrap" style={{ color: 'var(--text)' }}>
              <input type="checkbox" checked={sistemHaric}
                onChange={e => setNamed('sistemHaric', e.target.checked ? 'true' : '')} />
              Sistem gruplarını gizle
            </label>
          </>
        }
        export={{ endpoint: '/iam/permission-groups/export', named, fallbackFileName: 'yetki-gruplari.xlsx' }}
        compact={{
          title: g => g.ad,
          subtitle: g => `${g.kullaniciSayisi} kullanıcı · ${g.yetkiSayisi} yetki`,
          right: g => g.code,
          badge: g => <Badge variant={g.aktif ? 'success' : 'neutral'}>{g.aktif ? 'Aktif' : 'Pasif'}</Badge>,
        }}
      />

      {detayId && <GrupDetayModal grupId={detayId} onClose={() => setDetayId(null)} />}

      {/* Şablondan departman grubu kurma (K8 adım 3) */}
      {sablonAcik && (
        <Modal open onClose={() => setSablonAcik(false)} title="Şablondan Departman Grubu Kur">
          <p className="text-sm mb-3" style={{ color: 'var(--text-s)' }}>
            Şablon yalnız <b>başlangıç</b> değeridir: grup kurulduktan sonra yetkilerini serbestçe
            düzenlersiniz, şablon bir daha dokunmaz. Kullanıcı/yetki yönetimi ve sistem ayarları
            hiçbir şablonda yoktur — onlar süper adminde kalır.
          </p>
          <div className="space-y-2 max-h-[55vh] overflow-y-auto">
            {sablonlar.map(s => (
              <div key={s.kod} className="p-3 rounded flex items-start gap-3"
                style={{ border: '1px solid var(--border)' }}>
                <div className="flex-1 min-w-0">
                  <div className="text-sm font-medium flex items-center gap-2" style={{ color: 'var(--text)' }}>
                    {s.ad}
                    {s.kurulu && <Badge variant="success">kurulu</Badge>}
                  </div>
                  <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>{s.aciklama}</p>
                  <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>{s.yetkiSayisi} yetki</p>
                </div>
                <Button size="sm" variant={s.kurulu ? 'secondary' : 'primary'} disabled={s.kurulu}
                  loading={sablondanKur.isPending && sablondanKur.variables === s.kod}
                  onClick={() => sablondanKur.mutate(s.kod)}>
                  {s.kurulu ? 'Kurulu' : 'Kur'}
                </Button>
              </div>
            ))}
          </div>
          {hata && <p className="text-sm text-red-500 mt-3">{hata}</p>}
          <div className="flex justify-end mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
            <Button variant="secondary" onClick={() => setSablonAcik(false)}>Kapat</Button>
          </div>
        </Modal>
      )}

      {/* Grup kopyalama (K8 adım 4'ün aracı) */}
      {kopyaGrup && (
        <Modal open onClose={() => setKopyaGrup(null)} title={`"${kopyaGrup.ad}" grubunu kopyala`}>
          <div className="space-y-3">
            <p className="text-sm" style={{ color: 'var(--text-s)' }}>
              Yetkiler ve kanal kümeleri birebir kopyalanır; <b>üyeler kopyalanmaz</b> — yeni grup boş başlar.
            </p>
            <div>
              <label className="flbl">Yeni Grup Adı <span className="text-red-500">*</span></label>
              <input className="inp" value={kopyaAd} onChange={e => setKopyaAd(e.target.value)} />
            </div>
            {hata && <p className="text-sm text-red-500">{hata}</p>}
          </div>
          <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
            <Button variant="secondary" onClick={() => setKopyaGrup(null)}>Vazgeç</Button>
            <Button onClick={() => kopyala.mutate()} loading={kopyala.isPending}
              disabled={kopyaAd.trim().length < 2}>Kopyala</Button>
          </div>
        </Modal>
      )}

      {/* Grup kaldırma (K8 adım 6) */}
      {silinecek && (
        <Modal open onClose={() => setSilinecek(null)} title={`"${silinecek.ad}" grubunu kaldır`}>
          <p className="text-sm" style={{ color: 'var(--text-s)' }}>
            Grup kaldırılacak ve verdiği <b>{silinecek.yetkiSayisi} yetki</b> düşecek. Grup boş olduğu için
            kimse yetkisiz kalmaz. Kayıt silinmez, denetim izi korunur
            {silinecek.gecisGrubu && '; geçiş grubu bir daha açılışta kurulmaz'}.
          </p>
          {hata && <p className="text-sm text-red-500 mt-3">{hata}</p>}
          <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
            <Button variant="secondary" onClick={() => setSilinecek(null)}>Vazgeç</Button>
            <Button onClick={() => kaldir.mutate()} loading={kaldir.isPending}>Kaldır</Button>
          </div>
        </Modal>
      )}

      {yeniAcik && (
        <Modal open onClose={() => setYeniAcik(false)} title="Yeni Yetki Grubu">
          <div className="space-y-3">
            <div>
              <label className="flbl">Grup Adı <span className="text-red-500">*</span></label>
              <input className="inp" value={ad} onChange={e => setAd(e.target.value)} placeholder="Müşteri Hizmetleri" />
            </div>
            <div>
              <label className="flbl">Açıklama</label>
              <input className="inp" value={aciklama} onChange={e => setAciklama(e.target.value)} />
            </div>
            {hata && <p className="text-sm text-red-500">{hata}</p>}
          </div>
          <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
            <Button variant="secondary" onClick={() => setYeniAcik(false)}>Vazgeç</Button>
            <Button onClick={() => olustur.mutate()} loading={olustur.isPending} disabled={ad.trim().length < 2}>Oluştur</Button>
          </div>
        </Modal>
      )}
    </div>
  )
}

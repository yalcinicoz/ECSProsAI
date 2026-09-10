import { useMemo, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { NAV_SECTIONS, permittedSections } from '@/components/layout/sidebarNavigation'

/**
 * Y4 (2026-09-09) — Kullanıcı Yetkileri (tasarım §H).
 * Ekranın tek işi şu iki soruya cevap vermek: "Bu kullanıcı bunu neden yapabiliyor?" /
 * "Neden yapamıyor?" — bu yüzden KAPALI yetkiler de sebebiyle gösterilir.
 * Panelde ALLOW/DENY/INHERIT gibi teknik kavram yoktur: "bu kullanıcıda aç / kapat".
 */
interface YetkiSatiri {
  permissionId: string; code: string; ad: string; modul: string; sayfa?: string; tur: string
  kanalKapsamli: boolean; acik: boolean; kanallar: string[]; kaynaklar: string[]; kapaliSebebi?: string
}
interface Grup { id: string; ad: string; gecisGrubu: boolean }
interface Istisna { permissionId: string; code: string; ad: string; mod: string; kanallar: string[] }
interface KullaniciYetkileri {
  userId: string; adSoyad: string; email: string; aktif: boolean; superAdmin: boolean
  gruplar: Grup[]; yetkiler: YetkiSatiri[]; istisnalar: Istisna[]
}
interface Kanal { id: string; code: string; nameI18n?: Record<string, string> }
interface SimYetki { code: string; ad: string; modul: string; sayfa?: string; tumKanallar: boolean; kanallar: string[] }
interface Simulasyon {
  userId: string; adSoyad: string; superAdmin: boolean; gruplar: string[]
  sayfalar: SimYetki[]; aksiyonlar: SimYetki[]
  gorunenAlanlar: string[]; gizliAlanlar: string[]; kanallar: string[]; tumYetkiKodlari: string[]
}
const BOS_KANAL: Kanal[] = []

const turEtiketi = (t: string) => (t === 'page' ? 'Sayfa' : t === 'field' ? 'Alan' : 'İşlem')
function hataMetni(e: unknown) {
  const err = e as { response?: { data?: { error?: string } } }
  return err.response?.data?.error ?? 'İşlem başarısız oldu.'
}

export function UserPermissionsPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [sekme, setSekme] = useState<'yetkiler' | 'simulasyon'>('yetkiler')
  const [yalnizAcik, setYalnizAcik] = useState(false)
  const [arama, setArama] = useState('')
  const [hata, setHata] = useState('')
  const [kanalDuzenle, setKanalDuzenle] = useState<string | null>(null)

  const { data, isLoading } = useQuery<KullaniciYetkileri>({
    queryKey: ['kullanici-yetkileri', id],
    queryFn: async () => (await api.get(`/iam/users/${id}/permissions`)).data.data,
    enabled: !!id,
  })
  const { data: kanallar = BOS_KANAL } = useQuery<Kanal[]>({
    queryKey: ['kanallar-yetki'],
    // 2026-09-10: /core/firm-platforms diye bir uç yoktu (404 → kanal seçimi boş kalıyordu);
    // liste yetki API'sinden gelir, "tüm kanallar (bugünkü liste)" ile aynı kaynak.
    queryFn: async () => (await api.get('/iam/permission-channels')).data.data ?? BOS_KANAL,
  })
  const { data: gruplar = [] } = useQuery<{ id: string; ad: string }[]>({
    queryKey: ['yetki-gruplari'],
    queryFn: async () => (await api.get('/iam/permission-groups')).data.data,
  })

  // Y7 (tasarım §I): salt okunur önizleme — hesaba GİRİŞ yok, sonuç gerçek yetki servisinden gelir.
  // Sorgu yalnız sekme açılınca çalışır: her çağrı denetim kaydına yazılır.
  const { data: sim, isLoading: simYukleniyor } = useQuery<Simulasyon>({
    queryKey: ['kullanici-simulasyon', id],
    queryFn: async () => (await api.get(`/iam/users/${id}/simulation`)).data.data,
    enabled: !!id && sekme === 'simulasyon',
    staleTime: 0,
  })

  const istisna = useMutation({
    mutationFn: async (p: { permissionId: string; mod: string; channelIds?: string[] }) => {
      setHata('')
      await api.put(`/iam/users/${id}/permissions/${p.permissionId}`, { mod: p.mod, channelIds: p.channelIds ?? null })
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['kullanici-yetkileri', id] }),
    onError: (e: unknown) => setHata(hataMetni(e)),
  })

  const grupDegis = useMutation({
    mutationFn: async (p: { groupId: string; ekle: boolean }) => {
      setHata('')
      if (p.ekle) await api.post(`/iam/permission-groups/${p.groupId}/members/${id}`)
      else await api.delete(`/iam/permission-groups/${p.groupId}/members/${id}`)
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['kullanici-yetkileri', id] }),
    onError: (e: unknown) => setHata(hataMetni(e)),
  })

  const kanalAdi = (kid: string) =>
    kanallar.find(k => k.id === kid)?.nameI18n?.['tr'] ?? kanallar.find(k => k.id === kid)?.code ?? kid.slice(0, 8)

  const satirlar = useMemo(() => {
    const f = arama.trim().toLowerCase()
    return (data?.yetkiler ?? []).filter(y =>
      (!yalnizAcik || y.acik) &&
      (!f || y.ad.toLowerCase().includes(f) || y.code.toLowerCase().includes(f) || y.modul.toLowerCase().includes(f)))
  }, [data, yalnizAcik, arama])

  const istisnaModu = (permissionId: string) =>
    data?.istisnalar.find(i => i.permissionId === permissionId)?.mod

  if (isLoading) return <div className="p-6 text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</div>
  if (!data) return <div className="p-6 text-sm" style={{ color: 'var(--text-s)' }}>Kullanıcı bulunamadı.</div>

  return (
    <div className="p-6">
      <div className="flex items-center gap-3 mb-4">
        <button onClick={() => navigate('/settings/users')} className="text-sm" style={{ color: 'var(--text-s)' }}>←</button>
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>
            {data.adSoyad} — Yetkiler {data.superAdmin && <Badge variant="info">Süper Admin</Badge>}
            {!data.aktif && <Badge variant="neutral">Pasif</Badge>}
          </h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{data.email}</p>
        </div>
      </div>

      {data.superAdmin && (
        <div className="card p-3 mb-4 text-sm" style={{ color: 'var(--text)' }}>
          Bu kullanıcı <strong>süper admin</strong>: tüm yetkileri ve tüm kanalları kullanır, aşağıdaki
          kurallar ona uygulanmaz. (İşlemleri yine de denetim kaydına yazılır.)
        </div>
      )}

      {/* Yetki grupları */}
      <div className="card p-4 mb-4">
        <p className="text-xs font-semibold mb-2" style={{ color: 'var(--text-s)' }}>YETKİ GRUPLARI</p>
        <div className="flex flex-wrap items-center gap-2">
          {data.gruplar.length === 0 && <span className="text-sm" style={{ color: 'var(--text-s)' }}>Grubu yok.</span>}
          {data.gruplar.map(g => (
            <span key={g.id} className="px-2 py-1 rounded-lg text-sm flex items-center gap-2"
              style={{ background: 'var(--surface2)', color: 'var(--text)' }}>
              {g.ad}{g.gecisGrubu && <Badge variant="warning">geçici</Badge>}
              <button className="text-xs underline" style={{ color: '#DC2626' }}
                onClick={() => grupDegis.mutate({ groupId: g.id, ekle: false })}>çıkar</button>
            </span>
          ))}
          <select className="inp text-sm py-1 px-2 h-auto" style={{ minWidth: 180 }} defaultValue=""
            onChange={e => { if (e.target.value) { grupDegis.mutate({ groupId: e.target.value, ekle: true }); e.target.value = '' } }}>
            <option value="">+ Gruba ekle…</option>
            {gruplar.filter(g => !data.gruplar.some(x => x.id === g.id))
              .map(g => <option key={g.id} value={g.id}>{g.ad}</option>)}
          </select>
        </div>
      </div>

      {hata && <p className="text-sm text-red-500 mb-3">{hata}</p>}

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        <button className={sekme === 'yetkiler' ? 'stab active' : 'stab'} onClick={() => setSekme('yetkiler')}>
          Efektif Yetkiler
        </button>
        <button className={sekme === 'simulasyon' ? 'stab active' : 'stab'} onClick={() => setSekme('simulasyon')}>
          Simülasyon
        </button>
      </div>

      {sekme === 'simulasyon' && (
        <div>
          <p className="text-xs mb-3" style={{ color: 'var(--text-s)' }}>
            Bu ekran <strong>salt okunurdur</strong>: kullanıcının hesabına giriş yapılmaz, yalnız panelde ne
            göreceği hesaplanır. Her görüntüleme yetki loglarına yazılır.
          </p>
          {simYukleniyor && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Hesaplanıyor…</p>}
          {sim && (
            <div className="grid gap-4" style={{ gridTemplateColumns: 'minmax(260px, 1fr) 2fr' }}>
              <div className="card p-4">
                <p className="text-xs font-semibold mb-2" style={{ color: 'var(--text-s)' }}>GÖRDÜĞÜ MENÜ</p>
                {sim.superAdmin && (
                  <p className="text-sm mb-2" style={{ color: 'var(--text)' }}>Süper admin — tüm menü açık.</p>
                )}
                {permittedSections(NAV_SECTIONS, (perm) => sim.superAdmin || sim.tumYetkiKodlari.includes(perm))
                  .map(bolum => (
                    <div key={bolum.id} className="mb-2">
                      <div className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>{bolum.label}</div>
                      {bolum.items.map(k => (
                        <div key={k.to} className="text-sm pl-2" style={{ color: 'var(--text)' }}>· {k.label}</div>
                      ))}
                    </div>
                  ))}
                {!sim.superAdmin
                  && permittedSections(NAV_SECTIONS, (perm) => sim.tumYetkiKodlari.includes(perm)).length === 0 && (
                  <p className="text-sm" style={{ color: '#DC2626' }}>
                    Hiçbir menü kalemi görünmüyor — bu kullanıcı panele girince boş bir ekranla karşılaşır.
                  </p>
                )}
              </div>

              <div className="space-y-4">
                <div className="card p-4">
                  <p className="text-xs font-semibold mb-2" style={{ color: 'var(--text-s)' }}>
                    KULLANABİLECEĞİ İŞLEMLER ({sim.aksiyonlar.length})
                  </p>
                  {sim.aksiyonlar.length === 0 && (
                    <p className="text-sm" style={{ color: 'var(--text-s)' }}>Hiçbir işlem yetkisi yok (yalnız görüntüleme).</p>
                  )}
                  <div className="max-h-64 overflow-y-auto">
                    {sim.aksiyonlar.map(a => (
                      <div key={a.code} className="text-sm py-0.5" style={{ color: 'var(--text)' }}>
                        · {a.ad}
                        <span className="text-xs ml-2" style={{ color: 'var(--text-s)' }}>
                          {a.modul}{a.tumKanallar ? '' : ` · ${a.kanallar.join(', ') || 'kanal yok'}`}
                        </span>
                      </div>
                    ))}
                  </div>
                </div>

                <div className="card p-4">
                  <p className="text-xs font-semibold mb-2" style={{ color: 'var(--text-s)' }}>HASSAS ALANLAR</p>
                  <div className="text-sm" style={{ color: 'var(--text)' }}>
                    <div><strong>Görebildiği:</strong> {sim.gorunenAlanlar.length ? sim.gorunenAlanlar.join(', ') : '—'}</div>
                    <div><strong>Göremediği:</strong> {sim.gizliAlanlar.length ? sim.gizliAlanlar.join(', ') : '—'}</div>
                  </div>
                </div>

                <div className="card p-4">
                  <p className="text-xs font-semibold mb-2" style={{ color: 'var(--text-s)' }}>ERİŞTİĞİ KANALLAR</p>
                  <div className="text-sm" style={{ color: 'var(--text)' }}>
                    {sim.superAdmin ? 'Tüm kanallar (süper admin)'
                      : sim.kanallar.length ? sim.kanallar.join(', ')
                      : 'Kanal kapsamlı yetkisi yok'}
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {sekme === 'yetkiler' && (
      <>
      <div className="flex items-center gap-3 mb-3">
        <input className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 240 }}
          placeholder="Yetki ara…" value={arama} onChange={e => setArama(e.target.value)} />
        <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
          <input type="checkbox" checked={yalnizAcik} onChange={e => setYalnizAcik(e.target.checked)} />
          Yalnız açık olanlar
        </label>
        <span className="text-xs" style={{ color: 'var(--text-s)' }}>
          {data.yetkiler.filter(y => y.acik).length} açık / {data.yetkiler.length} yetki
        </span>
      </div>

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['YETKİ', 'KANALLAR', 'KAYNAK', 'DURUM', ''].map(h => (
                <th key={h} className="px-4 py-3 text-xs font-semibold text-left" style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {satirlar.map(y => {
              const mod = istisnaModu(y.permissionId)
              return (
                <tr key={y.permissionId} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-4 py-2.5 text-sm" style={{ color: 'var(--text)' }}>
                    {y.ad}
                    <span className="text-xs ml-2" style={{ color: 'var(--text-s)' }}>{y.modul} · {turEtiketi(y.tur)}</span>
                  </td>
                  <td className="px-4 py-2.5 text-sm" style={{ color: 'var(--text-m)' }}>
                    {!y.kanalKapsamli ? <span className="text-xs" style={{ color: 'var(--text-s)' }}>kanaldan bağımsız</span>
                      : y.kanallar.length === 0 ? <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span>
                      : (
                        <button className="text-xs underline" style={{ color: 'var(--brand)' }}
                          onClick={() => setKanalDuzenle(kanalDuzenle === y.permissionId ? null : y.permissionId)}>
                          {y.kanallar.map(kanalAdi).join(', ')}
                        </button>
                      )}
                    {kanalDuzenle === y.permissionId && (
                      <div className="mt-1 p-2 rounded-lg" style={{ background: 'var(--surface2)' }}>
                        <div className="text-xs mb-1" style={{ color: 'var(--text-s)' }}>
                          Bu kullanıcıda hangi kanallarda geçerli olsun? (kaydedince kullanıcıya özel istisna olur)
                        </div>
                        {kanallar.map(kn => {
                          const secili = y.kanallar.includes(kn.id)
                          return (
                            <label key={kn.id} className="flex items-center gap-2 text-sm py-0.5" style={{ color: 'var(--text)' }}>
                              <input type="checkbox" checked={secili} onChange={() => {
                                const yeni = secili ? y.kanallar.filter(x => x !== kn.id) : [...y.kanallar, kn.id]
                                istisna.mutate({ permissionId: y.permissionId, mod: 'ver', channelIds: yeni })
                              }} />
                              {kanalAdi(kn.id)}
                            </label>
                          )
                        })}
                      </div>
                    )}
                  </td>
                  <td className="px-4 py-2.5 text-xs" style={{ color: 'var(--text-s)' }}>
                    {y.kaynaklar.length ? y.kaynaklar.join(' + ') : '—'}
                  </td>
                  <td className="px-4 py-2.5">
                    {y.acik
                      ? <Badge variant="success">Açık</Badge>
                      : <span className="text-xs" style={{ color: 'var(--text-s)' }}>{y.kapaliSebebi}</span>}
                  </td>
                  <td className="px-4 py-2.5 text-right whitespace-nowrap">
                    {mod && (
                      <button className="text-xs underline mr-2" style={{ color: 'var(--text-s)' }}
                        onClick={() => istisna.mutate({ permissionId: y.permissionId, mod: 'yok' })}>
                        istisnayı kaldır
                      </button>
                    )}
                    {y.acik ? (
                      <button className="text-xs underline" style={{ color: '#DC2626' }}
                        onClick={() => istisna.mutate({
                          permissionId: y.permissionId, mod: 'kaldir',
                          channelIds: y.kanalKapsamli ? y.kanallar : undefined,
                        })}>bu kullanıcıda kapat</button>
                    ) : (
                      <button className="text-xs underline" style={{ color: 'var(--brand)' }}
                        onClick={() => istisna.mutate({
                          permissionId: y.permissionId, mod: 'ver',
                          channelIds: y.kanalKapsamli ? kanallar.map(k => k.id) : undefined,
                        })}>bu kullanıcıya aç</button>
                    )}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      <p className="text-xs mt-3" style={{ color: 'var(--text-s)' }}>
        Gruplar yalnız yetki verir; bu ekrandaki "kapat" kararı gruptan geleni de geçersiz kılar ve kullanıcıya
        özel istisna olarak saklanır. Değişiklik anında geçerli olur — kullanıcının yeniden giriş yapması gerekmez.
      </p>
      </>
      )}
    </div>
  )
}

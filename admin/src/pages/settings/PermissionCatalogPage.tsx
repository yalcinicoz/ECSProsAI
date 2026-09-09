import { useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { cn } from '@/lib/utils'

/**
 * Yetki İçerikleri (tasarım §F.1) — KATALOG EKRANI.
 *
 * Yetkiler koda aittir (karar K4): bu ekran yeni yetki ÜRETMEZ, var olanların panelde nasıl
 * görüneceğini yönetir (ad, açıklama, sayfa grubu, sıra, aktiflik ve kanal kapsamı bayrağı).
 * Teknik anahtar (key) hiçbir koşulda değişmez — audit geçmişi ona bağlıdır.
 */
interface KatalogSatiri {
  id: string; code: string; ad: string; aciklama?: string; modul: string; sayfa?: string
  tur: string; kanalKapsamli: boolean; aktif: boolean; koddaTanimli: boolean; sira: number
  grupSayisi: number; kullaniciSayisi: number
}

const turEtiketi = (t: string) => (t === 'page' ? 'Sayfa' : t === 'field' ? 'Alan' : 'İşlem')
const turRengi = (t: string): BadgeVariant => (t === 'page' ? 'info' : t === 'field' ? 'warning' : 'neutral')

function hataMetni(e: unknown) {
  const err = e as { response?: { data?: { error?: string } } }
  return err.response?.data?.error ?? 'İşlem başarısız oldu.'
}

function DuzenleModal({ satir, onClose }: { satir: KatalogSatiri; onClose: () => void }) {
  const qc = useQueryClient()
  const [ad, setAd] = useState(satir.ad)
  const [aciklama, setAciklama] = useState(satir.aciklama ?? '')
  const [sayfa, setSayfa] = useState(satir.sayfa ?? '')
  const [sira, setSira] = useState(String(satir.sira))
  const [aktif, setAktif] = useState(satir.aktif)
  const [kanalKapsamli, setKanalKapsamli] = useState(satir.kanalKapsamli)
  const [hata, setHata] = useState('')

  const kaydet = useMutation({
    mutationFn: async () => {
      setHata('')
      await api.put(`/iam/permissions/${satir.id}`, {
        ad: ad.trim(),
        aciklama: aciklama.trim() || null,
        sayfa: sayfa.trim() || null,
        sira: parseInt(sira) || 0,
        aktif,
        kanalKapsamli: kanalKapsamli !== satir.kanalKapsamli ? kanalKapsamli : null,
      })
    },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['yetki-katalogu'] }); onClose() },
    onError: (e: unknown) => setHata(hataMetni(e)),
  })

  const kullanimda = satir.grupSayisi > 0 || satir.kullaniciSayisi > 0

  return (
    <Modal open onClose={onClose} title={`Yetki: ${satir.ad}`}>
      <div className="space-y-3">
        <div className="p-2 rounded-lg text-xs" style={{ background: 'var(--surface2)', color: 'var(--text-s)' }}>
          Teknik anahtar: <code style={{ color: 'var(--text)' }}>{satir.code}</code> — değiştirilemez.
          Tür ({turEtiketi(satir.tur)}) ve modül ({satir.modul}) uygulamadan gelir.
        </div>

        <div>
          <label className="flbl">Görünen Ad <span className="text-red-500">*</span></label>
          <input className="inp" value={ad} onChange={e => setAd(e.target.value)} />
        </div>
        <div>
          <label className="flbl">Açıklama</label>
          <input className="inp" value={aciklama} onChange={e => setAciklama(e.target.value)}
            placeholder="Bu yetki neyi açar? (panelde yetki seçerken görünür)" />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Sayfa Grubu</label>
            <input className="inp" value={sayfa} onChange={e => setSayfa(e.target.value)} placeholder="Siparişler" />
          </div>
          <div>
            <label className="flbl">Sıra</label>
            <input type="number" className="inp" value={sira} onChange={e => setSira(e.target.value)} />
          </div>
        </div>

        <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
          <input type="checkbox" checked={aktif} onChange={e => setAktif(e.target.checked)} />
          Aktif — pasif yetki yeni gruplara/kullanıcılara verilemez ve mevcut atamaları etkisizleşir
        </label>

        <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
          <input type="checkbox" checked={kanalKapsamli} onChange={e => setKanalKapsamli(e.target.checked)} />
          Satış kanalı bazında verilsin
        </label>
        {kanalKapsamli !== satir.kanalKapsamli && (
          <p className="text-xs" style={{ color: '#D97706' }}>
            Bu bayrağı değiştirirseniz uygulamanın varsayılanı bir daha uygulanmaz; karar panelde kalır.
            {kanalKapsamli
              ? ' Kanal kapsamlı yapılan yetkide mevcut atamaların kanal listesi boş kalabilir — grupları gözden geçirin.'
              : ' Kanal kapsamı kaldırılırsa bu yetki tüm kanallarda geçerli olur.'}
          </p>
        )}

        {kullanimda && (
          <p className="text-xs" style={{ color: 'var(--text-s)' }}>
            Kullanımda: {satir.grupSayisi} grup, {satir.kullaniciSayisi} kullanıcı istisnası.
          </p>
        )}
        {!satir.koddaTanimli && (
          <p className="text-xs" style={{ color: '#DC2626' }}>
            Bu yetkinin uygulamada karşılığı kalmamış (kod kataloğunda yok). Otomatik pasife alındı;
            geçmiş kayıtlar korunuyor.
          </p>
        )}
        {hata && <p className="text-sm text-red-500">{hata}</p>}
      </div>

      <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
        <Button onClick={() => kaydet.mutate()} loading={kaydet.isPending} disabled={!ad.trim()}>Kaydet</Button>
      </div>
    </Modal>
  )
}

export function PermissionCatalogPage() {
  const [arama, setArama] = useState('')
  const [tur, setTur] = useState('')
  const [yalnizSorunlu, setYalnizSorunlu] = useState(false)
  const [duzenle, setDuzenle] = useState<KatalogSatiri | null>(null)

  const { data: satirlar = [], isLoading } = useQuery<KatalogSatiri[]>({
    queryKey: ['yetki-katalogu'],
    queryFn: async () => (await api.get('/iam/permissions')).data.data,
  })

  const gruplu = useMemo(() => {
    const f = arama.trim().toLowerCase()
    const uygun = satirlar.filter(s =>
      (!f || s.ad.toLowerCase().includes(f) || s.code.toLowerCase().includes(f)) &&
      (!tur || s.tur === tur) &&
      (!yalnizSorunlu || !s.koddaTanimli || !s.aktif))
    const m = new Map<string, KatalogSatiri[]>()
    for (const s of uygun) {
      const l = m.get(s.modul) ?? []
      l.push(s); m.set(s.modul, l)
    }
    return [...m.entries()]
  }, [satirlar, arama, tur, yalnizSorunlu])

  const sorunlu = satirlar.filter(s => !s.koddaTanimli).length

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Yetki İçerikleri</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          {satirlar.length} yetki — adlandırma, gruplama ve aktiflik buradan yönetilir
        </p>
      </div>

      <div className="card p-3 mb-4 text-sm" style={{ color: 'var(--text-m)' }}>
        Yetkiler <strong>uygulamadan gelir</strong>: yeni bir yetki ancak kodda karşılığı yazıldığında listeye
        eklenir. Bu ekranda yetkinin <strong>görünen adı, açıklaması, sayfa grubu, sırası, aktifliği</strong> ve
        gerekiyorsa <strong>kanal kapsamı</strong> düzenlenir; teknik anahtar değişmez. Yeni bir yetkiye
        ihtiyaç varsa geliştiriciye iletin — panelden oluşturulan ama koda bağlı olmayan yetki hiçbir şeyi
        korumaz, yalnız yanlış güven verir.
      </div>

      <div className="flex flex-wrap items-center gap-2 mb-4">
        <input className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 240 }}
          placeholder="Yetki adı veya anahtarı ara…" value={arama} onChange={e => setArama(e.target.value)} />
        <select className="inp text-sm py-1.5 px-3 h-auto" value={tur} onChange={e => setTur(e.target.value)}>
          <option value="">Tüm türler</option>
          <option value="page">Sayfa</option>
          <option value="action">İşlem</option>
          <option value="field">Alan</option>
        </select>
        <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
          <input type="checkbox" checked={yalnizSorunlu} onChange={e => setYalnizSorunlu(e.target.checked)} />
          Yalnız pasif / karşılığı olmayanlar {sorunlu > 0 && <Badge variant="warning">{sorunlu}</Badge>}
        </label>
      </div>

      {isLoading && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</p>}

      {gruplu.map(([modul, liste]) => (
        <div key={modul} className="card overflow-hidden mb-4">
          <div className="px-4 py-2 text-xs font-semibold"
            style={{ background: 'var(--surface2)', color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>
            {modul} ({liste.length})
          </div>
          <table className="w-full">
            <tbody>
              {liste.map(s => (
                <tr key={s.id} onClick={() => setDuzenle(s)}
                  className={cn('cursor-pointer hover:bg-[var(--surface2)] transition-colors')}
                  style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-4 py-2.5 text-sm" style={{ color: 'var(--text)' }}>
                    {s.ad}
                    {s.aciklama && (
                      <span className="text-xs block" style={{ color: 'var(--text-s)' }}>{s.aciklama}</span>
                    )}
                  </td>
                  <td className="px-4 py-2.5">
                    <code className="text-xs" style={{ color: 'var(--text-s)' }}>{s.code}</code>
                  </td>
                  <td className="px-4 py-2.5"><Badge variant={turRengi(s.tur)}>{turEtiketi(s.tur)}</Badge></td>
                  <td className="px-4 py-2.5 text-xs" style={{ color: 'var(--text-s)' }}>
                    {s.kanalKapsamli ? 'kanal bazlı' : 'kanaldan bağımsız'}
                  </td>
                  <td className="px-4 py-2.5 text-xs" style={{ color: 'var(--text-s)' }}>
                    {s.grupSayisi} grup{s.kullaniciSayisi > 0 ? ` · ${s.kullaniciSayisi} istisna` : ''}
                  </td>
                  <td className="px-4 py-2.5">
                    {!s.koddaTanimli
                      ? <Badge variant="warning">uygulamada yok</Badge>
                      : <Badge variant={s.aktif ? 'success' : 'neutral'}>{s.aktif ? 'Aktif' : 'Pasif'}</Badge>}
                  </td>
                  <td className="px-4 py-2.5 text-right text-xs" style={{ color: 'var(--text-s)' }}>Düzenle →</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ))}

      {!isLoading && gruplu.length === 0 && (
        <p className="text-sm" style={{ color: 'var(--text-s)' }}>Ölçütlere uyan yetki yok.</p>
      )}

      {duzenle && <DuzenleModal satir={duzenle} onClose={() => setDuzenle(null)} />}
    </div>
  )
}

import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'

/**
 * Y5 (2026-09-09) — Yetki Logları (tasarım §J.3).
 * Ham JSON değil, insan diliyle cümle gösterilir; teknik ayrıntı (önce/sonra) satır açılınca görünür.
 * Kayıtlar yalnız EKLENİR: bu ekranda düzenleme/silme yoktur.
 */
interface LogSatiri {
  id: string; tarih: string; olay: string; ozet: string
  aktor?: string; hedefKullanici?: string; hedefGrup?: string; yetki?: string
  oncesi?: string; sonrasi?: string; ip?: string
}
interface Sayfali { items: LogSatiri[]; totalCount: number; page: number; pageSize: number }

const OLAYLAR: { deger: string; etiket: string }[] = [
  { deger: '', etiket: 'Tüm olaylar' },
  { deger: 'yetki.grup.olustur', etiket: 'Grup oluşturma' },
  { deger: 'yetki.grup.guncelle', etiket: 'Grup güncelleme' },
  { deger: 'yetki.grup.yetkiler', etiket: 'Grup yetkileri' },
  { deger: 'yetki.grup.uye.ekle', etiket: 'Gruba üye ekleme' },
  { deger: 'yetki.grup.uye.cikar', etiket: 'Gruptan üye çıkarma' },
  { deger: 'yetki.kullanici.istisna', etiket: 'Kullanıcı istisnası' },
  { deger: 'yetki.katalog.guncelle', etiket: 'Yetki içeriği' },
  { deger: 'yetki.superadmin.ver', etiket: 'Süper admin verme' },
  { deger: 'yetki.superadmin.kaldir', etiket: 'Süper admin kaldırma' },
  { deger: 'yetki.simulasyon', etiket: 'Simülasyon' },
  { deger: 'yetki.grup.sablon', etiket: 'Şablondan grup kurma' },
  { deger: 'yetki.grup.kopyala', etiket: 'Grup kopyalama' },
  { deger: 'yetki.grup.kaldir', etiket: 'Grup kaldırma' },
  { deger: 'yetki.reddedildi', etiket: 'Yetkisiz erişim denemesi' },
]

const kritik = (olay: string) => olay.startsWith('yetki.superadmin.')
// Reddedilen erişim ayrı bir renkte: "biri yapamadığı bir şeyi denedi" — hata değil, sinyal.
const reddedilen = (olay: string) => olay === 'yetki.reddedildi'

export function PermissionLogsPage() {
  const [olay, setOlay] = useState('')
  const [arama, setArama] = useState('')
  const [uygulanan, setUygulanan] = useState('')
  const [sayfa, setSayfa] = useState(1)
  const [acik, setAcik] = useState<string | null>(null)

  const { data, isLoading } = useQuery<Sayfali>({
    queryKey: ['yetki-loglari', olay, uygulanan, sayfa],
    queryFn: async () => {
      const p = new URLSearchParams({ page: String(sayfa), pageSize: '50' })
      if (olay) p.set('olay', olay)
      if (uygulanan) p.set('search', uygulanan)
      return (await api.get(`/iam/permission-logs?${p}`)).data.data
    },
  })

  const satirlar = data?.items ?? []
  const toplamSayfa = Math.ceil((data?.totalCount ?? 0) / 50)

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Yetki Logları</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          {(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt — yetki değişikliklerinin tam geçmişi (yalnız eklenir, silinemez)
        </p>
      </div>

      <div className="flex flex-wrap items-center gap-2 mb-4">
        <select className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 200 }}
          value={olay} onChange={e => { setOlay(e.target.value); setSayfa(1) }}>
          {OLAYLAR.map(o => <option key={o.deger} value={o.deger}>{o.etiket}</option>)}
        </select>
        <input className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 260 }}
          placeholder="Özette ara (kullanıcı, grup, yetki)…" value={arama}
          onChange={e => setArama(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') { setUygulanan(arama.trim()); setSayfa(1) } }} />
        <button onClick={() => { setUygulanan(arama.trim()); setSayfa(1) }}
          className="px-3 py-1.5 rounded-lg text-sm"
          style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Ara</button>
      </div>

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['TARİH', 'İŞLEM', 'YAPAN', ''].map(h => (
                <th key={h} className="px-4 py-3 text-xs font-semibold text-left" style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && <tr><td colSpan={4} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</td></tr>}
            {!isLoading && satirlar.length === 0 && (
              <tr><td colSpan={4} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Kayıt yok. Yetki grubu/kullanıcı yetkisi değişiklikleri burada görünür.
              </td></tr>
            )}
            {satirlar.map(l => (
              <>
                <tr key={l.id} onClick={() => setAcik(acik === l.id ? null : l.id)}
                  className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                  style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-4 py-2.5 text-xs whitespace-nowrap" style={{ color: 'var(--text-s)' }}>
                    {new Date(l.tarih).toLocaleString('tr-TR')}
                  </td>
                  <td className="px-4 py-2.5 text-sm" style={{ color: 'var(--text)' }}>
                    {kritik(l.olay) && <Badge variant="warning">kritik</Badge>}
                    {reddedilen(l.olay) && <Badge variant="danger">reddedildi</Badge>} {l.ozet}
                  </td>
                  <td className="px-4 py-2.5 text-sm" style={{ color: 'var(--text-m)' }}>{l.aktor ?? '—'}</td>
                  <td className="px-4 py-2.5 text-right text-xs" style={{ color: 'var(--text-s)' }}>
                    {acik === l.id ? 'gizle' : 'ayrıntı'}
                  </td>
                </tr>
                {acik === l.id && (
                  <tr key={`${l.id}-d`} style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
                    <td colSpan={4} className="px-4 py-3 text-xs" style={{ color: 'var(--text-m)' }}>
                      <div><strong>Olay:</strong> <code>{l.olay}</code></div>
                      {l.hedefKullanici && <div><strong>Hedef kullanıcı:</strong> {l.hedefKullanici}</div>}
                      {l.hedefGrup && <div><strong>Hedef grup:</strong> {l.hedefGrup}</div>}
                      {l.yetki && <div><strong>Yetki:</strong> <code>{l.yetki}</code></div>}
                      {l.oncesi && <div><strong>Önce:</strong> {l.oncesi}</div>}
                      {l.sonrasi && <div><strong>Sonra:</strong> {l.sonrasi}</div>}
                      {l.ip && <div><strong>IP:</strong> {l.ip}</div>}
                    </td>
                  </tr>
                )}
              </>
            ))}
          </tbody>
        </table>
      </div>

      {toplamSayfa > 1 && (
        <div className="flex items-center justify-center gap-2 mt-4">
          <button onClick={() => setSayfa(p => Math.max(1, p - 1))} disabled={sayfa === 1}
            className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>← Önceki</button>
          <span className="text-sm" style={{ color: 'var(--text-s)' }}>{sayfa} / {toplamSayfa}</span>
          <button onClick={() => setSayfa(p => Math.min(toplamSayfa, p + 1))} disabled={sayfa === toplamSayfa}
            className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Sonraki →</button>
        </div>
      )}
    </div>
  )
}

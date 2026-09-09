import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'

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
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (YetkiLogGrid.Schema) + görünümler.
  // ★ Açılır ayrıntı satırı DataGrid'de yok → ayrıntı MODALA taşındı (hiçbir alan kaybolmadı).
  // ★ Aktör/hedef adları handler'da ayrı sorgularla çözülüyor (join yok) → o kolonlar SIRALANAMAZ.
  const [sp] = useSearchParams()
  const olay = sp.get('olay') ?? ''
  const grid = useGridState('permission-logs', { defaultPageSize: 50, defaultSort: 'tarih', defaultDir: 'desc' })
  const [acik, setAcik] = useState<LogSatiri | null>(null)

  const { data, isLoading, isFetching, error: listError } = useQuery<Sayfali>({
    queryKey: ['yetki-loglari', olay, ...grid.queryKey],
    queryFn: async () => (await api.get(`/iam/permission-logs?${grid.toParams({ olay: olay || undefined })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const satirlar = data?.items ?? []

  const columns: GridColumn<LogSatiri>[] = [
    { key: 'tarih', header: 'TARİH', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 150,
      filter: { type: 'date', label: 'Tarih', quick: true },
      cell: l => <span className="text-xs whitespace-nowrap" style={{ color: 'var(--text-s)' }}>
        {new Date(l.tarih).toLocaleString('tr-TR')}</span> },
    // Özet metni Context jsonb'sinde (Dictionary<string,object>) → SQL'e çevrilemez, sıralanamaz.
    // Özet içeriğinde arama, üstteki arama kutusundan yapılır: terim kullanıcı/grup/yetki kaydına
    // çözülüp DB'de kimlik üzerinden süzülür (sayımla tutarlı).
    { key: 'ozet', header: 'İŞLEM', priority: 1, lockVisible: true, minWidth: 380,
      filter: { type: 'text', label: 'Olay kodu', field: 'olay' },
      cell: l => <span className="text-sm" style={{ color: 'var(--text)' }}>
        {kritik(l.olay) && <Badge variant="warning">kritik</Badge>}
        {reddedilen(l.olay) && <Badge variant="danger">reddedildi</Badge>} {l.ozet}</span> },
    // Aktör adı sunucuda ayrı sorguyla çözülüyor: filtre/sıralama şemada YOK (bilinçli).
    { key: 'aktor', header: 'YAPAN', priority: 1,
      cell: l => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{l.aktor ?? '—'}</span> },
    { key: 'ip', header: 'IP', priority: 3, defaultVisible: false, sortable: true, filter: { type: 'text', label: 'IP' },
      cell: l => <span className="text-xs font-mono" style={{ color: 'var(--text-s)' }}>{l.ip ?? '—'}</span> },
    { key: 'detay', header: '', priority: 2, align: 'right', exportable: false,
      cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>ayrıntı →</span> },
  ]

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Yetki Logları</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          {(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''} — yetki değişikliklerinin tam geçmişi (yalnız eklenir, silinemez)
        </p>
      </div>

      <DataGrid<LogSatiri>
        gridId="permission-logs"
        views
        grid={grid}
        columns={columns}
        rows={satirlar}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        onRowClick={l => setAcik(l)}
        empty="Kayıt yok. Yetki grubu/kullanıcı yetkisi değişiklikleri burada görünür."
        search={{ placeholder: 'Özette ara (kullanıcı, grup, yetki)…' }}
        minWidth={900}
        pageSizes={[50, 100, 200]}
        filterLeading={
          <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={olay} aria-label="Olay"
            onChange={e => grid.mutate(n => { if (e.target.value) n.set('olay', e.target.value); else n.delete('olay') })}>
            {OLAYLAR.map(o => <option key={o.deger} value={o.deger}>{o.etiket}</option>)}
          </select>
        }
        compact={{
          title: l => l.ozet,
          subtitle: l => `${l.aktor ?? '—'} · ${new Date(l.tarih).toLocaleString('tr-TR')}`,
          badge: l => kritik(l.olay) ? <Badge variant="warning">kritik</Badge>
            : reddedilen(l.olay) ? <Badge variant="danger">reddedildi</Badge> : null,
        }}
      />

      {acik && (
        <Modal open onClose={() => setAcik(null)} title="Log ayrıntısı">
          <div className="space-y-1.5 text-sm" style={{ color: 'var(--text-m)' }}>
            <div><strong>Tarih:</strong> {new Date(acik.tarih).toLocaleString('tr-TR')}</div>
            <div><strong>Olay:</strong> <code>{acik.olay}</code></div>
            <div><strong>Özet:</strong> {acik.ozet}</div>
            {acik.aktor && <div><strong>Yapan:</strong> {acik.aktor}</div>}
            {acik.hedefKullanici && <div><strong>Hedef kullanıcı:</strong> {acik.hedefKullanici}</div>}
            {acik.hedefGrup && <div><strong>Hedef grup:</strong> {acik.hedefGrup}</div>}
            {acik.yetki && <div><strong>Yetki:</strong> <code>{acik.yetki}</code></div>}
            {acik.oncesi && <div><strong>Önce:</strong> {acik.oncesi}</div>}
            {acik.sonrasi && <div><strong>Sonra:</strong> {acik.sonrasi}</div>}
            {acik.ip && <div><strong>IP:</strong> <code>{acik.ip}</code></div>}
          </div>
        </Modal>
      )}
    </div>
  )
}

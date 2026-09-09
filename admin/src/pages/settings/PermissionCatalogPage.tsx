import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText as gridErrText } from '@/components/ui/DataTable.utils'

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

interface Sayfali<T> { items: T[]; totalCount: number; page: number; pageSize: number }

export function PermissionCatalogPage() {
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (YetkiKatalogGrid.Schema) + Excel + görünümler.
  // ★ Ayrı uç: /iam/permissions TAM liste döner (yetki grubu + kullanıcı yetkisi ekranlarının kaynağı);
  // bu ekran sayfalı /iam/permissions/grid kullanır.
  // ★ Eskiden satırlar MODÜLE göre gruplanıp ayrı kartlarda gösteriliyordu; DataGrid düz tablo olduğu
  // için gruplama "MODÜL" sütunu + filtresi olarak korundu (varsayılan sıra: modül, sonra kod).
  const [sp] = useSearchParams()
  const tur = sp.get('tur') ?? ''
  const yalnizSorunlu = sp.get('yalnizSorunlu') === 'true'
  const grid = useGridState('permission-catalog', { defaultPageSize: 50, defaultSort: 'modul', defaultDir: 'asc' })
  const setNamed = (k: string, v: string) => grid.mutate(n => { if (v) n.set(k, v); else n.delete(k) })
  const [duzenle, setDuzenle] = useState<KatalogSatiri | null>(null)

  const named = () => ({ tur: tur || undefined, yalnizSorunlu: yalnizSorunlu ? 'true' : undefined })

  const { data, isLoading, isFetching, error: listError } = useQuery<Sayfali<KatalogSatiri>>({
    queryKey: ['yetki-katalogu-grid', tur, yalnizSorunlu, ...grid.queryKey],
    queryFn: async () => (await api.get(`/iam/permissions/grid?${grid.toParams(named())}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const satirlar = data?.items ?? []

  const columns: GridColumn<KatalogSatiri>[] = [
    { key: 'ad', header: 'AD', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 260,
      filter: { type: 'text', label: 'Ad' },
      filters: [{ field: 'aciklama', label: 'Açıklama', type: 'text' }],
      cell: s => <div>
        <span className="text-sm" style={{ color: 'var(--text)' }}>{s.ad}</span>
        {s.aciklama && <span className="text-xs block" style={{ color: 'var(--text-s)' }}>{s.aciklama}</span>}
      </div> },
    { key: 'code', header: 'ANAHTAR', priority: 1, frozen: true, sortable: true, minWidth: 200,
      filter: { type: 'text', label: 'Anahtar', ops: ['startswith', 'contains', 'eq'] },
      cell: s => <code className="text-xs" style={{ color: 'var(--text-s)' }}>{s.code}</code> },
    { key: 'modul', header: 'MODÜL', priority: 1, sortable: true, filter: { type: 'enum', label: 'Modül' },
      filters: [{ field: 'sayfa', label: 'Sayfa grubu', type: 'text' }],
      cell: s => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{s.modul}</span> },
    { key: 'tur', header: 'TÜR', priority: 1, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Tür', options: [
        { value: 'page', label: 'Sayfa' }, { value: 'action', label: 'İşlem' }, { value: 'field', label: 'Alan' }] },
      cell: s => <Badge variant={turRengi(s.tur)}>{turEtiketi(s.tur)}</Badge> },
    { key: 'kanalKapsamli', header: 'KAPSAM', priority: 2, sortable: true,
      filter: { type: 'boolean', label: 'Kanal bazlı' },
      cell: s => <span className="text-xs" style={{ color: 'var(--text-s)' }}>
        {s.kanalKapsamli ? 'kanal bazlı' : 'kanaldan bağımsız'}</span> },
    { key: 'grupSayisi', header: 'KULLANIM', priority: 2, align: 'right', sortable: true,
      filter: { type: 'number', label: 'Grup sayısı' },
      filters: [
        { field: 'kullaniciSayisi', label: 'İstisna sayısı', type: 'number' },
        { field: 'kullanimda', label: 'Kullanımda', type: 'boolean' }],
      cell: s => <span className="text-xs" style={{ color: 'var(--text-s)' }}>
        {s.grupSayisi} grup{s.kullaniciSayisi > 0 ? ` · ${s.kullaniciSayisi} istisna` : ''}</span> },
    { key: 'aktif', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'boolean', label: 'Aktif' },
      filters: [
        { field: 'koddaTanimli', label: 'Uygulamada var', type: 'boolean' },
        { field: 'sorunlu', label: 'Pasif / karşılığı yok', type: 'boolean' }],
      cell: s => !s.koddaTanimli
        ? <Badge variant="warning">uygulamada yok</Badge>
        : <Badge variant={s.aktif ? 'success' : 'neutral'}>{s.aktif ? 'Aktif' : 'Pasif'}</Badge> },
    { key: 'sira', header: 'SIRA', priority: 3, align: 'center', sortable: true, filter: { type: 'number', label: 'Sıra' },
      cell: s => <span className="text-xs" style={{ color: 'var(--text-s)' }}>{s.sira}</span> },
    { key: 'duzenle', header: '', priority: 3, align: 'right', exportable: false,
      cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Düzenle →</span> },
  ]

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Yetki İçerikleri</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          {(data?.totalCount ?? 0).toLocaleString('tr-TR')} yetki{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''} — adlandırma, gruplama ve aktiflik buradan yönetilir
        </p>
      </div>

      <div className="card p-3 mb-4 text-sm" style={{ color: 'var(--text-m)' }}>
        Yetkiler <strong>uygulamadan gelir</strong>: yeni bir yetki ancak kodda karşılığı yazıldığında listeye
        eklenir. Bu ekranda yetkinin <strong>görünen adı, açıklaması, sayfa grubu, sırası, aktifliği</strong> ve
        gerekiyorsa <strong>kanal kapsamı</strong> düzenlenir; teknik anahtar değişmez. Yeni bir yetkiye
        ihtiyaç varsa geliştiriciye iletin — panelden oluşturulan ama koda bağlı olmayan yetki hiçbir şeyi
        korumaz, yalnız yanlış güven verir.
      </div>

      <DataGrid<KatalogSatiri>
        gridId="permission-catalog"
        views
        grid={grid}
        columns={columns}
        rows={satirlar}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? gridErrText(listError) : null}
        onRowClick={s => setDuzenle(s)}
        empty="Ölçütlere uyan yetki yok."
        search={{ placeholder: 'Yetki adı veya anahtarı ara…' }}
        minWidth={1120}
        pageSizes={[50, 100, 200]}
        filterLeading={
          <>
            <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={tur} aria-label="Tür"
              onChange={e => setNamed('tur', e.target.value)}>
              <option value="">Tüm türler</option>
              <option value="page">Sayfa</option>
              <option value="action">İşlem</option>
              <option value="field">Alan</option>
            </select>
            <label className="flex items-center gap-1.5 text-sm whitespace-nowrap" style={{ color: 'var(--text)' }}>
              <input type="checkbox" checked={yalnizSorunlu}
                onChange={e => setNamed('yalnizSorunlu', e.target.checked ? 'true' : '')} />
              Yalnız pasif / karşılığı olmayanlar
            </label>
          </>
        }
        export={{ endpoint: '/iam/permissions/export', named, fallbackFileName: 'yetki-icerikleri.xlsx' }}
        compact={{
          title: s => s.ad,
          subtitle: s => `${s.code} · ${s.modul}`,
          right: s => `${s.grupSayisi} grup`,
          badge: s => !s.koddaTanimli
            ? <Badge variant="warning">uygulamada yok</Badge>
            : <Badge variant={s.aktif ? 'success' : 'neutral'}>{s.aktif ? 'Aktif' : 'Pasif'}</Badge>,
        }}
      />

      {duzenle && <DuzenleModal satir={duzenle} onClose={() => setDuzenle(null)} />}
    </div>
  )
}

import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { errText } from '@/components/ui/DataTable.utils'
import { useAuthStore } from '@/store/auth'

type Saved = { slot: number; value: { name: string; definition: unknown; savedAtUtc: string; shareToken?: string } }
export function SavedReports({ userId, recipe, canSave, busy, onLoad }: {
  userId?: string; recipe: string; canSave: boolean; busy: boolean; onLoad: (definition: unknown) => void
}) {
  const [name, setName] = useState('')
  const canShare = useAuthStore(state => state.hasPermission('reports.ai.share'))
  const [shareLink, setShareLink] = useState('')
  const [incoming, setIncoming] = useState(() => window.location.hash.startsWith('#ai-report=') ? window.location.href : '')
  const [confirmation, setConfirmation] = useState<{ item: Saved; kind: 'update' | 'remove' } | null>(null)
  const query = useQuery<Saved[]>({ queryKey: ['ai-saved-reports', userId],
    queryFn: async () => (await api.get('/reports/ai/saved')).data.data,
    enabled: !!userId, retry: false, gcTime: 0,
  })
  const save = useMutation({
    mutationFn: async () => {
      const latest = await query.refetch()
      if (latest.error || !latest.data) throw new Error('Rapor listesi alınamadı.')
      const slot = Array.from({ length: 20 }, (_, index) => index).find(index => !latest.data.some(report => report.slot === index))
      if (slot === undefined) throw new Error('En fazla 20 kişisel rapor kaydedilebilir.')
      await api.post(`/reports/ai/saved/${slot}`, { name: name.trim(), definition: JSON.parse(recipe) })
    },
    onSuccess: async () => { setName(''); await query.refetch() },
  })
  const manage = useMutation({
    mutationFn: async (action: { item: Saved; kind: 'update' | 'remove' }) => {
      if (action.kind === 'remove') await api.post(`/reports/ai/saved/${action.item.slot}/remove`, { expected: action.item.value })
      else await api.put(`/reports/ai/saved/${action.item.slot}`, {
        name: name.trim() || action.item.value.name, definition: JSON.parse(recipe), expected: action.item.value,
      })
    },
    onSuccess: async () => { setConfirmation(null); await query.refetch() },
  })
  const share = useMutation({
    mutationFn: async ({ item, enabled }: { item: Saved; enabled: boolean }) =>
      (await api.post(`/reports/ai/saved/${item.slot}/sharing`, { expected: item.value, enabled })).data.data as { ownerId: string; slot: number; token: string | null },
    onSuccess: async data => {
      setShareLink(data.token ? `${window.location.origin}${window.location.pathname}#ai-report=${encodeURIComponent(JSON.stringify(data))}` : '')
      await query.refetch()
    },
  })
  const openShared = useMutation({
    mutationFn: async () => {
      const url = new URL(incoming)
      if (!url.hash.startsWith('#ai-report=')) throw new Error('Geçerli rapor paylaşım bağlantısı girin.')
      const data = JSON.parse(decodeURIComponent(url.hash.slice('#ai-report='.length))) as { ownerId: string; slot: number; token: string }
      return (await api.post('/reports/ai/saved/shared', { ownerId: data.ownerId, slot: data.slot, token: data.token })).data.data.definition as unknown
    },
    onSuccess: data => { onLoad(data); setIncoming('') },
  })
  const pending = busy || save.isPending || manage.isPending || share.isPending || openShared.isPending
  return <section className="rounded-xl border p-4 space-y-3" aria-label="Kişisel raporlar" style={{ background: 'var(--surface)', borderColor: 'var(--border)' }}>
    <h2 className="font-semibold">Kayıtlı raporlarım</h2>
    <p className="text-sm">Yalnız rapor tarifi saklanır; sonuçlar ve AI konuşması saklanmaz. Göreli dönem seçildiyse her çalıştırmada yenilenir; diğer tarihler sabit kalır. Açtıktan sonra kapsamı kontrol edip çalıştırın; güncel yetkileriniz uygulanır. Paylaşımda yetki gruplarınızdan gelen izinler ve kullanıcıya özel kısıtlamalar geçerlidir; alıcı yalnız kendi veri kapsamını görür. Bağlantı ek yetki vermez.</p>
    <div className="flex flex-wrap gap-2">
      <input aria-label="Kaydedilecek rapor adı" className="inp" maxLength={100} value={name} onChange={event => setName(event.target.value)} placeholder="Rapor adı" disabled={pending || !!confirmation} />
      <button type="button" className="btn" disabled={!canSave || pending || !!confirmation || !name.trim() || query.isError}
        onClick={() => save.mutate()}>Tarifi kaydet</button>
      <button type="button" className="btn" disabled={query.isFetching || pending} onClick={() => { setConfirmation(null); void query.refetch() }}>Listeyi yenile</button>
    </div>
    {(query.isError || save.isError || manage.isError || share.isError || openShared.isError) && <p role="alert">{errText(query.error ?? save.error ?? manage.error ?? share.error ?? openShared.error)}</p>}
    {save.isSuccess && <p role="status">Rapor tarifi kaydedildi.</p>}
    <ul className="space-y-2">{query.data?.map(item => <li key={item.slot} className="flex items-center justify-between gap-3">
      <span>{typeof item.value?.name === 'string' ? item.value.name : 'Geçersiz kayıt'}</span>
      <div className="flex flex-wrap gap-2">
        <button type="button" className="btn" disabled={pending || !!confirmation} onClick={() => onLoad(item.value?.definition)}>Taslağı aç</button>
        <button type="button" className="btn" disabled={pending || !canSave || !!confirmation} onClick={() => setConfirmation({ item, kind: 'update' })}>Mevcut taslakla güncelle</button>
        <button type="button" className="btn text-red-600" disabled={pending || !!confirmation} onClick={() => setConfirmation({ item, kind: 'remove' })}>Kaldır</button>
        {canShare && <button type="button" className="btn" disabled={pending || !!confirmation} onClick={() => share.mutate({ item, enabled: true })}>Paylaşım bağlantısı oluştur</button>}
        {item.value?.shareToken && <button type="button" className="btn" disabled={pending || !!confirmation} onClick={() => share.mutate({ item, enabled: false })}>Paylaşımı kapat</button>}
      </div>
    </li>)}</ul>
    {confirmation && <div className="rounded-lg border p-3 space-y-2" role="alert">
      <p>“{confirmation.item.value.name}” {confirmation.kind === 'remove' ? 'kişisel kaydınızdan kaldırılacak. Kaynak veriler silinmez.' : 'ekrandaki mevcut rapor taslağıyla değiştirilecek. Eski tarifin üzerine yazılır.'} Onaylıyor musunuz?</p>
      <button type="button" className="btn" disabled={pending} onClick={() => setConfirmation(null)}>Vazgeç</button>
      <button type="button" className="btn" disabled={pending || confirmation.kind === 'update' && !canSave} onClick={() => manage.mutate(confirmation)}>Onayla</button>
    </div>}
    {query.isSuccess && !query.data.length && <p className="text-sm">Henüz kayıtlı rapor yok.</p>}
    {shareLink && <label className="block text-sm">Paylaşım bağlantısı (kopyalayabilirsiniz)
      <input className="inp w-full" readOnly value={shareLink} onFocus={event => event.target.select()} />
    </label>}
    <p className="text-xs">Yeni bağlantı oluşturma, kayıt güncelleme veya paylaşımı kapatma eski bağlantıyı geçersiz kılar. Önceden kendi hesabına kopyalanmış tarifleri geri almaz; hiçbir bağlantı veri yetkisi vermez.</p>
    <label className="block text-sm">Bana gönderilen rapor bağlantısı
      <input className="inp w-full" maxLength={2048} value={incoming} disabled={pending} onChange={event => setIncoming(event.target.value)} />
    </label>
    <button type="button" className="btn" disabled={pending || !!confirmation || !incoming.trim()} onClick={() => openShared.mutate()}>Paylaşılan taslağı aç</button>
  </section>
}

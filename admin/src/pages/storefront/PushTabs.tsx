// Mobil push (docs/PUSH_BILDIRIM_ENTEGRASYONU.md, 2026-09-07): şablon yönetimi + gönderim logu + tek cihaza test.
import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'

interface PushTemplate { id: string; type: string; class: string; name: string; title: string; body: string; linkTemplate: string; enabled: boolean; ttlSeconds: number; priority: string; description?: string | null }
interface PushLogItem { id: string; memberId: string | null; deviceId: string; platform: string; type: string; class: string; dedupId: string; title: string; body: string; link: string; status: string; errorCode: string | null; attempts: number; scheduledAt: string; sentAt: string | null; openedAt: string | null; createdAt: string }
interface PushLog { items: PushLogItem[]; totalCount: number; page: number; pageSize: number; last24h: { status: string; count: number }[] }

const STATUS_BADGE: Record<string, 'success' | 'warning' | 'danger' | 'neutral' | 'info'> = { sent: 'success', queued: 'warning', failed: 'danger', skipped: 'neutral' }
const fmt = (s: string | null | undefined) => (s ? new Date(s).toLocaleString('tr-TR') : '—')
const err = (e: unknown, f: string) => (e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? f

export function PushTemplatesTab() {
  const qc = useQueryClient()
  const { data: list = [] } = useQuery<PushTemplate[]>({ queryKey: ['push-templates'], queryFn: async () => (await api.get('/store-notifications/push-templates')).data.data ?? [] })
  const [edit, setEdit] = useState<PushTemplate | null>(null)
  const [error, setError] = useState('')
  const save = useMutation({
    mutationFn: async () => api.put(`/store-notifications/push-templates/${edit!.type}`, edit),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['push-templates'] }); setEdit(null); setError('') },
    onError: (e: unknown) => setError(err(e, 'Kaydedilemedi.')),
  })
  const toggle = useMutation({
    mutationFn: async (t: PushTemplate) => api.put(`/store-notifications/push-templates/${t.type}`, { ...t, enabled: !t.enabled }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['push-templates'] }),
  })
  return (
    <div className="card overflow-hidden p-0">
      <p className="text-xs px-4 py-2" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>
        Yer tutucular: {'{orderNumber} {orderId} {productName} {productCode} {cargoName} {trackingNumber} {newPrice} {oldPrice} {n} {couponCode} {discountText} {expiresAt} {amount} {variantInfo} {returnStatusLabel}'}.
        Pazarlama sınıfı: üye izni (push) + sessiz saat 22:00-09:00 + günde 2 / aynı tip haftada 2. Link uygulamanın tanıdığı yollardan olmalı (/odeme, /teslimat kullanılamaz).
      </p>
      <table className="w-full text-sm">
        <thead><tr style={{ background: 'var(--surface2)' }}>{['Senaryo', 'Sınıf', 'Başlık / Gövde', 'Link', 'Durum', ''].map((h) => <th key={h} className="px-3 py-2 text-left text-xs" style={{ color: 'var(--text-s)' }}>{h}</th>)}</tr></thead>
        <tbody>
          {list.map((t) => (
            <tr key={t.type} style={{ borderTop: '1px solid var(--border)', opacity: t.enabled ? 1 : 0.55 }}>
              <td className="px-3 py-2" style={{ color: 'var(--text)' }}><b>{t.name}</b><br /><span className="font-mono text-[11px]" style={{ color: 'var(--text-s)' }}>{t.type}</span>{t.description && <><br /><span className="text-[11px]" style={{ color: 'var(--text-s)' }}>{t.description}</span></>}</td>
              <td className="px-3 py-2"><Badge variant={t.class === 'marketing' ? 'info' : 'neutral'}>{t.class === 'marketing' ? 'pazarlama' : 'işlemsel'}</Badge></td>
              <td className="px-3 py-2" style={{ color: 'var(--text)' }}><b>{t.title}</b><br /><span className="text-xs" style={{ color: 'var(--text-m)' }}>{t.body}</span></td>
              <td className="px-3 py-2 font-mono text-xs" style={{ color: 'var(--text-m)' }}>{t.linkTemplate}</td>
              <td className="px-3 py-2"><button className="text-xs underline" style={{ color: t.enabled ? '#15803d' : '#ef4444' }} onClick={() => toggle.mutate(t)}>{t.enabled ? 'açık' : 'kapalı'}</button></td>
              <td className="px-3 py-2 text-right"><Button size="sm" variant="secondary" onClick={() => setEdit({ ...t })}>Düzenle</Button></td>
            </tr>
          ))}
          {list.length === 0 && <tr><td colSpan={6} className="px-3 py-6 text-center text-xs" style={{ color: 'var(--text-s)' }}>Şablon yok — API açılış seed'i (restart) oluşturur.</td></tr>}
        </tbody>
      </table>
      {edit && (
        <Modal open onClose={() => setEdit(null)} title={`Şablon — ${edit.name}`} size="lg"
          footer={<><Button variant="secondary" onClick={() => setEdit(null)}>Vazgeç</Button><Button onClick={() => save.mutate()} loading={save.isPending}>Kaydet</Button></>}>
          <div className="space-y-3">
            <div><label className="flbl">Ad (panel)</label><input className="inp w-full" value={edit.name} onChange={(e) => setEdit({ ...edit, name: e.target.value })} /></div>
            <div><label className="flbl">Başlık</label><input className="inp w-full" value={edit.title} onChange={(e) => setEdit({ ...edit, title: e.target.value })} /></div>
            <div><label className="flbl">Gövde</label><textarea className="inp w-full" rows={3} value={edit.body} onChange={(e) => setEdit({ ...edit, body: e.target.value })} /></div>
            <div className="grid grid-cols-3 gap-3">
              <div><label className="flbl">Link</label><input className="inp w-full font-mono" value={edit.linkTemplate} onChange={(e) => setEdit({ ...edit, linkTemplate: e.target.value })} /></div>
              <div><label className="flbl">TTL (sn)</label><input type="number" className="inp w-full" value={edit.ttlSeconds} onChange={(e) => setEdit({ ...edit, ttlSeconds: Number(e.target.value) })} /></div>
              <div><label className="flbl">Öncelik</label><select className="inp w-full" value={edit.priority} onChange={(e) => setEdit({ ...edit, priority: e.target.value })}><option value="high">high</option><option value="normal">normal</option></select></div>
            </div>
            <label className="flex items-center gap-1.5 text-sm" style={{ color: 'var(--text)' }}><input type="checkbox" checked={edit.enabled} onChange={(e) => setEdit({ ...edit, enabled: e.target.checked })} /> Açık (kapalıysa bu senaryo gönderilmez)</label>
            {error && <p className="text-sm" style={{ color: '#dc2626' }}>{error}</p>}
          </div>
        </Modal>
      )}
    </div>
  )
}

export function PushLogTab() {
  const [status, setStatus] = useState('')
  const [type, setType] = useState('')
  const [page, setPage] = useState(1)
  const { data } = useQuery<PushLog>({
    queryKey: ['push-log', status, type, page],
    queryFn: async () => (await api.get('/store-notifications/push-log', { params: { status: status || undefined, type: type || undefined, page, pageSize: 50 } })).data.data,
    refetchInterval: 30_000,
  })
  const [test, setTest] = useState<{ deviceId: string; memberId: string; type: string; title: string; body: string; link: string }>({ deviceId: '', memberId: '', type: '', title: 'Deneme bildirimi', body: 'Bu bir deneme bildirimidir.', link: '/' })
  const [testSonuc, setTestSonuc] = useState('')
  const gonder = useMutation({
    mutationFn: async () => (await api.post('/store-notifications/push-test', { deviceId: test.deviceId || null, memberId: test.memberId || null, type: test.type || null, title: test.title || null, body: test.body || null, link: test.link || null })).data.data as { queued: number; note: string },
    onSuccess: (d) => setTestSonuc(`${d.queued} bildirim — ${d.note}`),
    onError: (e: unknown) => setTestSonuc(err(e, 'Gönderilemedi.')),
  })
  return (
    <div className="space-y-4">
      <div className="card p-4">
        <h3 className="text-sm font-semibold mb-2" style={{ color: 'var(--text)' }}>Tek cihaza / üyeye deneme gönder</h3>
        <p className="text-xs mb-2" style={{ color: 'var(--text-s)' }}>Firebase konsolundan toplu kampanya ASLA gönderilmez (proje canlı mağazayla ortak). Cihaz kimliğini üye detayı › Mobil Bildirim Cihazları'ndan ya da aşağıdaki "Push Cihazları" listesinden alın. Senaryo seçilirse şablon metni örnek değerlerle gider.</p>
        <div className="flex flex-wrap items-end gap-2">
          <div><label className="flbl">Cihaz Id</label><input className="inp font-mono" style={{ width: 300 }} value={test.deviceId} onChange={(e) => setTest({ ...test, deviceId: e.target.value })} placeholder="push_devices.Id" /></div>
          <div><label className="flbl">veya Üye Id</label><input className="inp font-mono" style={{ width: 300 }} value={test.memberId} onChange={(e) => setTest({ ...test, memberId: e.target.value })} /></div>
          <div><label className="flbl">Senaryo (boş = serbest)</label><input className="inp font-mono" style={{ width: 170 }} value={test.type} onChange={(e) => setTest({ ...test, type: e.target.value })} placeholder="order_shipped" /></div>
          <div><label className="flbl">Başlık</label><input className="inp" style={{ width: 200 }} value={test.title} onChange={(e) => setTest({ ...test, title: e.target.value })} /></div>
          <div><label className="flbl">Gövde</label><input className="inp" style={{ width: 260 }} value={test.body} onChange={(e) => setTest({ ...test, body: e.target.value })} /></div>
          <div><label className="flbl">Link</label><input className="inp font-mono" style={{ width: 200 }} value={test.link} onChange={(e) => setTest({ ...test, link: e.target.value })} /></div>
          <Button size="sm" onClick={() => gonder.mutate()} loading={gonder.isPending} disabled={!test.deviceId && !test.memberId}>Gönder</Button>
        </div>
        {testSonuc && <p className="text-xs mt-2" style={{ color: 'var(--text-m)' }}>{testSonuc}</p>}
      </div>
      <div className="card overflow-hidden p-0">
        <div className="flex flex-wrap items-center gap-2 px-3 py-2" style={{ borderBottom: '1px solid var(--border)' }}>
          {(data?.last24h ?? []).map((x) => <Badge key={x.status} variant={STATUS_BADGE[x.status] ?? 'neutral'}>{x.status} {x.count} (24s)</Badge>)}
          <div className="ml-auto flex gap-2">
            <select className="inp text-sm py-1 h-auto" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}><option value="">Tüm durumlar</option>{['queued', 'sent', 'failed', 'skipped'].map((s) => <option key={s} value={s}>{s}</option>)}</select>
            <input className="inp text-sm py-1 h-auto font-mono" style={{ width: 180 }} placeholder="senaryo" value={type} onChange={(e) => { setType(e.target.value); setPage(1) }} />
          </div>
        </div>
        <table className="w-full text-xs">
          <thead><tr style={{ background: 'var(--surface2)' }}>{['Zaman', 'Senaryo', 'Başlık', 'Platform', 'Durum', 'Hata', 'Gönderim', 'Açılma'].map((h) => <th key={h} className="px-2 py-2 text-left" style={{ color: 'var(--text-s)' }}>{h}</th>)}</tr></thead>
          <tbody>
            {(data?.items ?? []).map((n) => (
              <tr key={n.id} style={{ borderTop: '1px solid var(--border)', color: 'var(--text)' }}>
                <td className="px-2 py-1 whitespace-nowrap">{fmt(n.createdAt)}</td>
                <td className="px-2 py-1 font-mono">{n.type}<br /><span style={{ color: 'var(--text-s)' }}>{n.class === 'marketing' ? 'pazarlama' : 'işlemsel'}</span></td>
                <td className="px-2 py-1"><b>{n.title}</b><br /><span style={{ color: 'var(--text-m)' }}>{n.body}</span><br /><span className="font-mono" style={{ color: 'var(--text-s)' }}>{n.link}</span></td>
                <td className="px-2 py-1">{n.platform}</td>
                <td className="px-2 py-1"><Badge variant={STATUS_BADGE[n.status] ?? 'neutral'}>{n.status}</Badge>{n.attempts > 1 && <span className="ml-1" style={{ color: 'var(--text-s)' }}>×{n.attempts}</span>}</td>
                <td className="px-2 py-1 font-mono" style={{ color: '#dc2626' }}>{n.errorCode ?? ''}</td>
                <td className="px-2 py-1 whitespace-nowrap">{n.sentAt ? fmt(n.sentAt) : `planlı ${fmt(n.scheduledAt)}`}</td>
                <td className="px-2 py-1 whitespace-nowrap">{fmt(n.openedAt)}</td>
              </tr>
            ))}
            {data && data.items.length === 0 && <tr><td colSpan={8} className="px-3 py-6 text-center" style={{ color: 'var(--text-s)' }}>Kayıt yok.</td></tr>}
          </tbody>
        </table>
        {data && data.totalCount > 50 && (
          <div className="flex items-center gap-2 px-3 py-2 text-xs" style={{ borderTop: '1px solid var(--border)', color: 'var(--text-s)' }}>
            <button className="underline" disabled={page <= 1} onClick={() => setPage(page - 1)}>← önceki</button>
            <span>{page} / {Math.ceil(data.totalCount / 50)}</span>
            <button className="underline" disabled={page * 50 >= data.totalCount} onClick={() => setPage(page + 1)}>sonraki →</button>
          </div>
        )}
      </div>
    </div>
  )
}

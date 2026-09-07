// Müşteri İlişkileri ayarları: durumlar (gizli/çözüldü/mükerrer muaf/varsayılan) + konu başlıkları (tür, sıra, zorunlu alanlar).
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { FIELD_LABEL, apiErrorMessage, useTicketSettings, type TicketStatus, type TicketSubject } from './ticketShared'

const ALANLAR = ['orderNumber', 'callerName', 'callerPhone', 'body', 'image']

export function TicketSettingsPage() {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { data } = useTicketSettings(true)
  const [subject, setSubject] = useState<Partial<TicketSubject> | null>(null)
  const [status, setStatus] = useState<Partial<TicketStatus> | null>(null)
  const [error, setError] = useState('')
  const done = () => { qc.invalidateQueries({ queryKey: ['ticket-settings'] }); setSubject(null); setStatus(null); setError('') }
  const saveSubject = useMutation({ mutationFn: async () => api.post('/crm/tickets/settings/subjects', subject), onSuccess: done, onError: (e: unknown) => setError(apiErrorMessage(e, 'Kaydedilemedi.')) })
  const saveStatus = useMutation({ mutationFn: async () => api.post('/crm/tickets/settings/statuses', status), onSuccess: done, onError: (e: unknown) => setError(apiErrorMessage(e, 'Kaydedilemedi.')) })

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center gap-2 text-sm" style={{ color: 'var(--text-s)' }}>
        <button className="underline" onClick={() => navigate('/crm/tickets')}>Müşteri İlişkileri</button><span>/</span><span>Ayarlar</span>
      </div>
      <div className="card p-4">
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>Konu Başlıkları</h2>
          <Button size="sm" onClick={() => setSubject({ name: '', type: 'request', sortOrder: 99, isActive: true, requiredFields: ['orderNumber', 'callerName', 'callerPhone', 'body'] })}>+ Konu</Button>
        </div>
        <table className="w-full text-sm">
          <thead><tr style={{ background: 'var(--surface2)' }}>{['Sıra', 'Konu', 'Tür', 'Zorunlu alanlar', 'Durum'].map((h) => <th key={h} className="px-3 py-2 text-left text-xs" style={{ color: 'var(--text-s)' }}>{h}</th>)}</tr></thead>
          <tbody>
            {(data?.subjects ?? []).map((s) => (
              <tr key={s.id} className="cursor-pointer hover:opacity-80" style={{ borderTop: '1px solid var(--border)', opacity: s.isActive ? 1 : 0.5 }} onClick={() => setSubject({ ...s })}>
                <td className="px-3 py-1.5" style={{ color: 'var(--text-s)' }}>{s.sortOrder}</td>
                <td className="px-3 py-1.5" style={{ color: 'var(--text)' }}>{s.name}</td>
                <td className="px-3 py-1.5" style={{ color: s.type === 'complaint' ? '#ef4444' : '#3b82f6' }}>{s.type === 'complaint' ? 'Şikayet' : 'Talep'}</td>
                <td className="px-3 py-1.5 text-xs" style={{ color: 'var(--text-m)' }}>{s.requiredFields.map((f) => FIELD_LABEL[f] ?? f).join(', ') || '—'}</td>
                <td className="px-3 py-1.5 text-xs">{s.isActive ? 'aktif' : 'pasif'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="card p-4">
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>Durumlar</h2>
          <Button size="sm" onClick={() => setStatus({ code: '', name: '', color: '#6b7280', sortOrder: 99, isHidden: false, isResolved: false, exemptFromDuplicateCheck: false, isDefault: false })}>+ Durum</Button>
        </div>
        <table className="w-full text-sm">
          <thead><tr style={{ background: 'var(--surface2)' }}>{['Sıra', 'Kod', 'Ad', 'Renk', 'Özellikler'].map((h) => <th key={h} className="px-3 py-2 text-left text-xs" style={{ color: 'var(--text-s)' }}>{h}</th>)}</tr></thead>
          <tbody>
            {(data?.statuses ?? []).map((s) => (
              <tr key={s.id} className="cursor-pointer hover:opacity-80" style={{ borderTop: '1px solid var(--border)' }} onClick={() => setStatus({ ...s })}>
                <td className="px-3 py-1.5" style={{ color: 'var(--text-s)' }}>{s.sortOrder}</td>
                <td className="px-3 py-1.5 font-mono text-xs" style={{ color: 'var(--text)' }}>{s.code}</td>
                <td className="px-3 py-1.5" style={{ color: 'var(--text)' }}>{s.name}</td>
                <td className="px-3 py-1.5"><span className="inline-block w-4 h-4 rounded" style={{ background: s.color }} /></td>
                <td className="px-3 py-1.5 text-xs" style={{ color: 'var(--text-m)' }}>
                  {[s.isDefault && 'varsayılan', s.isHidden && 'gizli (listede görünmez, seçilemez)', s.isResolved && 'çözüldü', s.exemptFromDuplicateCheck && 'mükerrer kontrolünden muaf'].filter(Boolean).join(' · ') || '—'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {subject && (
        <Modal open onClose={() => setSubject(null)} title={subject.id ? 'Konu Düzenle' : 'Yeni Konu'}
          footer={<><Button variant="secondary" onClick={() => setSubject(null)}>Vazgeç</Button><Button onClick={() => saveSubject.mutate()} loading={saveSubject.isPending}>Kaydet</Button></>}>
          <div className="space-y-3">
            <div><label className="flbl">Konu adı</label><input className="inp w-full" value={subject.name ?? ''} onChange={(e) => setSubject({ ...subject, name: e.target.value })} /></div>
            <div className="grid grid-cols-2 gap-3">
              <div><label className="flbl">Tür</label><select className="inp w-full" value={subject.type} onChange={(e) => setSubject({ ...subject, type: e.target.value as 'complaint' | 'request' })}><option value="complaint">Şikayet</option><option value="request">Talep</option></select></div>
              <div><label className="flbl">Sıra</label><input type="number" className="inp w-full" value={subject.sortOrder ?? 0} onChange={(e) => setSubject({ ...subject, sortOrder: Number(e.target.value) })} /></div>
            </div>
            <div><label className="flbl">Zorunlu alanlar</label>
              <div className="flex flex-wrap gap-3">
                {ALANLAR.map((f) => (
                  <label key={f} className="flex items-center gap-1.5 text-sm" style={{ color: 'var(--text)' }}>
                    <input type="checkbox" checked={subject.requiredFields?.includes(f) ?? false}
                      onChange={(e) => setSubject({ ...subject, requiredFields: e.target.checked ? [...(subject.requiredFields ?? []), f] : (subject.requiredFields ?? []).filter((x) => x !== f) })} />
                    {FIELD_LABEL[f]}
                  </label>
                ))}
              </div>
            </div>
            <label className="flex items-center gap-1.5 text-sm" style={{ color: 'var(--text)' }}><input type="checkbox" checked={subject.isActive ?? true} onChange={(e) => setSubject({ ...subject, isActive: e.target.checked })} /> Aktif</label>
            {error && <p className="text-sm" style={{ color: '#dc2626' }}>{error}</p>}
          </div>
        </Modal>
      )}
      {status && (
        <Modal open onClose={() => setStatus(null)} title={status.id ? 'Durum Düzenle' : 'Yeni Durum'}
          footer={<><Button variant="secondary" onClick={() => setStatus(null)}>Vazgeç</Button><Button onClick={() => saveStatus.mutate()} loading={saveStatus.isPending}>Kaydet</Button></>}>
          <div className="space-y-3">
            <div className="grid grid-cols-2 gap-3">
              <div><label className="flbl">Kod</label><input className="inp w-full font-mono" value={status.code ?? ''} onChange={(e) => setStatus({ ...status, code: e.target.value })} disabled={!!status.id} /></div>
              <div><label className="flbl">Ad</label><input className="inp w-full" value={status.name ?? ''} onChange={(e) => setStatus({ ...status, name: e.target.value })} /></div>
              <div><label className="flbl">Renk</label><input type="color" className="inp w-full h-9" value={status.color ?? '#6b7280'} onChange={(e) => setStatus({ ...status, color: e.target.value })} /></div>
              <div><label className="flbl">Sıra</label><input type="number" className="inp w-full" value={status.sortOrder ?? 0} onChange={(e) => setStatus({ ...status, sortOrder: Number(e.target.value) })} /></div>
            </div>
            {([['isDefault', 'Varsayılan (yeni kayıt bu durumla açılır)'], ['isHidden', 'Gizli (varsayılan listede görünmez, işlemde seçilemez)'], ['isResolved', 'Çözüldü anlamı (yeşil)'], ['exemptFromDuplicateCheck', 'Mükerrer kontrolünden muaf']] as const).map(([k, l]) => (
              <label key={k} className="flex items-center gap-1.5 text-sm" style={{ color: 'var(--text)' }}><input type="checkbox" checked={!!status[k]} onChange={(e) => setStatus({ ...status, [k]: e.target.checked })} /> {l}</label>
            ))}
            {error && <p className="text-sm" style={{ color: '#dc2626' }}>{error}</p>}
          </div>
        </Modal>
      )}
    </div>
  )
}

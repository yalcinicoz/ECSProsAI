import { useState, type ReactNode } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import {
  STATUS_META, STATUS_TRANSITIONS, CATEGORY_LABELS, PRIORITY_META,
  isTerminal, isOverdue, type RequestDetail, type RequestActivity,
} from './constants'

interface AdminUser { id: string; fullName: string }

function apiErrorMessage(error: unknown, fallback: string): string {
  if (typeof error !== 'object' || error === null || !('response' in error)) return fallback
  const response = error.response
  if (typeof response !== 'object' || response === null || !('data' in response)) return fallback
  const data = response.data
  if (typeof data !== 'object' || data === null || !('error' in data)) return fallback
  return typeof data.error === 'string' ? data.error : fallback
}

function useAdminUsers() {
  return useQuery<AdminUser[]>({
    queryKey: ['iam-users-mini'],
    queryFn: async () => {
      const res = (await api.get('/iam/users?pageSize=100&activeOnly=true')).data.data
      return (res.items ?? res) as AdminUser[]
    },
    staleTime: 5 * 60 * 1000,
  })
}

function ActivityRow({ a }: { a: RequestActivity }) {
  const tarih = new Date(a.createdAt).toLocaleString('tr-TR')
  let icerik: ReactNode
  switch (a.activityType) {
    case 'created':
      icerik = <span>talebi oluşturdu.</span>
      break
    case 'status_change':
      icerik = (
        <span>
          durumu <Badge variant={STATUS_META[a.oldValue ?? '']?.badge ?? 'neutral'}>{STATUS_META[a.oldValue ?? '']?.label ?? a.oldValue}</Badge>
          {' → '}
          <Badge variant={STATUS_META[a.newValue ?? '']?.badge ?? 'neutral'}>{STATUS_META[a.newValue ?? '']?.label ?? a.newValue}</Badge>
          {' olarak değiştirdi.'}
        </span>
      )
      break
    case 'assignment':
      icerik = a.newValue
        ? <span>talebi <b>{a.newValue}</b> kişisine atadı{a.oldValue ? <> (önceki: {a.oldValue})</> : null}.</span>
        : <span>atamayı kaldırdı{a.oldValue ? <> (önceki: {a.oldValue})</> : null}.</span>
      break
    case 'updated':
      icerik = <span>talep bilgilerini güncelledi.</span>
      break
    default:
      icerik = <span>yorum yazdı:</span>
  }

  return (
    <div className="flex gap-3 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
      <div className="w-8 h-8 rounded-full flex items-center justify-center text-xs font-semibold shrink-0"
        style={{ background: 'var(--surface2)', color: 'var(--text-m)' }}>
        {a.userName.split(' ').map(p => p[0]).slice(0, 2).join('').toUpperCase()}
      </div>
      <div className="min-w-0 flex-1">
        <div className="text-sm" style={{ color: 'var(--text)' }}>
          <b>{a.userName}</b> {icerik}
          <span className="ml-2 text-xs" style={{ color: 'var(--text-s)' }}>{tarih}</span>
        </div>
        {(a.comment || a.attachments.length > 0) && (
          <div className="mt-1.5 text-sm rounded-lg p-3 whitespace-pre-wrap"
            style={{ background: 'var(--surface2)', color: 'var(--text)' }}>
            {a.comment}
            {a.attachments.length > 0 && (
              <div className="flex gap-2 flex-wrap mt-2">
                {a.attachments.map(f => (
                  <a key={f} href={f} target="_blank" rel="noreferrer" className="text-xs underline"
                    style={{ color: 'var(--brand)' }}>📎 {f.split('/').pop()}</a>
                ))}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  )
}

function EditModal({ r, onClose }: { r: RequestDetail; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [title, setTitle] = useState(r.title)
  const [category, setCategory] = useState(r.category)
  const [priority, setPriority] = useState(r.priority)
  const [dueDate, setDueDate] = useState(r.dueDate ?? '')
  const [description, setDescription] = useState(r.description)
  const [error, setError] = useState('')

  const save = useMutation({
    mutationFn: async () => api.put(`/requests/${r.id}`, {
      title, description, category, priority, dueDate: dueDate || null,
    }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['request', r.id] })
      queryClient.invalidateQueries({ queryKey: ['requests'] })
      onClose()
    },
    onError: (error: unknown) => setError(apiErrorMessage(error, 'Kaydedilemedi.')),
  })

  return (
    <Modal open onClose={onClose} title={`${r.code} — Düzenle`} size="lg">
      <div className="space-y-3">
        <div>
          <div className="flbl">Başlık *</div>
          <input className="inp w-full" value={title} onChange={e => setTitle(e.target.value)} />
        </div>
        <div className="grid grid-cols-3 gap-3">
          <div>
            <div className="flbl">Kategori</div>
            <select className="inp w-full" value={category} onChange={e => setCategory(e.target.value)}>
              {Object.entries(CATEGORY_LABELS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
            </select>
          </div>
          <div>
            <div className="flbl">Öncelik</div>
            <select className="inp w-full" value={priority} onChange={e => setPriority(e.target.value)}>
              {Object.entries(PRIORITY_META).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
            </select>
          </div>
          <div>
            <div className="flbl">Termin</div>
            <input type="date" className="inp w-full" value={dueDate} onChange={e => setDueDate(e.target.value)} />
          </div>
        </div>
        <div>
          <div className="flbl">Açıklama</div>
          <textarea className="inp w-full" rows={6} value={description} onChange={e => setDescription(e.target.value)} />
        </div>
        {error && <div className="text-sm" style={{ color: '#dc2626' }}>{error}</div>}
      </div>
      <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
        <Button loading={save.isPending} disabled={!title.trim()} onClick={() => save.mutate()}>Kaydet</Button>
      </div>
    </Modal>
  )
}

function StatusChangeModal({ r, newStatus, onClose }: { r: RequestDetail; newStatus: string; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [comment, setComment] = useState('')
  const [error, setError] = useState('')

  const change = useMutation({
    mutationFn: async () => api.post(`/requests/${r.id}/status`, { status: newStatus, comment: comment || null }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['request', r.id] })
      queryClient.invalidateQueries({ queryKey: ['requests'] })
      onClose()
    },
    onError: (error: unknown) => setError(apiErrorMessage(error, 'Durum değiştirilemedi.')),
  })

  return (
    <Modal open onClose={onClose}
      title={`Durum: ${STATUS_META[r.status]?.label} → ${STATUS_META[newStatus]?.label}`}>
      <div className="space-y-3">
        <div>
          <div className="flbl">Not (opsiyonel — zaman akışına yazılır)</div>
          <textarea className="inp w-full" rows={3} value={comment} onChange={e => setComment(e.target.value)}
            placeholder={newStatus === 'rejected' ? 'Red gerekçesi yazmanız önerilir.' : 'Örn: sprint 12 kapsamına alındı.'} autoFocus />
        </div>
        {error && <div className="text-sm" style={{ color: '#dc2626' }}>{error}</div>}
      </div>
      <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
        <Button loading={change.isPending} onClick={() => change.mutate()}>Onayla</Button>
      </div>
    </Modal>
  )
}

export function RequestDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { data: users = [] } = useAdminUsers()

  const [comment, setComment] = useState('')
  const [files, setFiles] = useState<string[]>([])
  const [uploading, setUploading] = useState(false)
  const [commentError, setCommentError] = useState('')
  const [showEdit, setShowEdit] = useState(false)
  const [statusTarget, setStatusTarget] = useState<string | null>(null)

  const { data: r, isLoading, error } = useQuery<RequestDetail>({
    queryKey: ['request', id],
    queryFn: async () => (await api.get(`/requests/${id}`)).data.data,
    enabled: !!id,
  })

  const addComment = useMutation({
    mutationFn: async () => api.post(`/requests/${id}/comments`, { comment, attachments: files }),
    onSuccess: () => {
      setComment(''); setFiles([]); setCommentError('')
      queryClient.invalidateQueries({ queryKey: ['request', id] })
      queryClient.invalidateQueries({ queryKey: ['requests'] })
    },
    onError: (error: unknown) => setCommentError(apiErrorMessage(error, 'Yorum gönderilemedi.')),
  })

  const assign = useMutation({
    mutationFn: async (u: AdminUser | null) =>
      api.post(`/requests/${id}/assign`, { assignedTo: u?.id ?? null, assignedToName: u?.fullName ?? null }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['request', id] })
      queryClient.invalidateQueries({ queryKey: ['requests'] })
    },
  })

  async function uploadFile(f: File) {
    setUploading(true)
    setCommentError('')
    try {
      const form = new FormData()
      form.append('file', f)
      const res = await api.post('/requests/media', form, { headers: { 'Content-Type': 'multipart/form-data' } })
      setFiles(prev => [...prev, res.data.data.url])
    } catch (error: unknown) {
      setCommentError(apiErrorMessage(error, 'Dosya yüklenemedi.'))
    } finally {
      setUploading(false)
    }
  }

  if (isLoading) return <div className="p-6 text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</div>
  if (error || !r) return (
    <div className="p-6">
      <div className="text-sm" style={{ color: '#dc2626' }}>Talep bulunamadı.</div>
      <button className="mt-2 text-sm underline" style={{ color: 'var(--brand)' }}
        onClick={() => navigate('/requests')}>← Talep listesine dön</button>
    </div>
  )

  const gecisler = STATUS_TRANSITIONS[r.status] ?? []

  return (
    <div className="p-6">
      <div className="flex items-center gap-2 mb-4 text-sm" style={{ color: 'var(--text-s)' }}>
        <button className="underline" onClick={() => navigate('/requests')}>Proje Talepleri</button>
        <span>/</span>
        <span className="font-mono">{r.code}</span>
      </div>

      <div className="grid grid-cols-3 gap-4">
        {/* Sol: içerik + zaman akışı */}
        <div className="col-span-2 space-y-4">
          <div className="card p-5">
            <div className="flex items-start justify-between gap-3">
              <h1 className="text-lg font-bold" style={{ color: 'var(--text)' }}>{r.title}</h1>
              {!isTerminal(r.status) && (
                <Button variant="secondary" size="sm" onClick={() => setShowEdit(true)}>Düzenle</Button>
              )}
            </div>
            <div className="flex items-center gap-2 mt-2">
              <Badge variant={STATUS_META[r.status]?.badge ?? 'neutral'}>{STATUS_META[r.status]?.label ?? r.status}</Badge>
              <Badge variant={PRIORITY_META[r.priority]?.badge ?? 'neutral'}>{PRIORITY_META[r.priority]?.label ?? r.priority}</Badge>
              <Badge variant="neutral">{CATEGORY_LABELS[r.category] ?? r.category}</Badge>
            </div>
            <div className="mt-3 text-sm whitespace-pre-wrap" style={{ color: 'var(--text-m)' }}>
              {r.description || <i style={{ color: 'var(--text-s)' }}>Açıklama girilmemiş.</i>}
            </div>
          </div>

          <div className="card p-5">
            <h2 className="text-sm font-semibold mb-2" style={{ color: 'var(--text)' }}>
              Süreç Akışı ({r.activities.length})
            </h2>
            <div>
              {r.activities.map(a => <ActivityRow key={a.id} a={a} />)}
            </div>
            <div className="mt-4">
              <textarea className="inp w-full" rows={3} value={comment}
                onChange={e => setComment(e.target.value)} placeholder="Yorum yazın…" />
              <div className="flex items-center justify-between mt-2">
                <div className="flex items-center gap-2 flex-wrap">
                  {files.map(f => (
                    <a key={f} href={f} target="_blank" rel="noreferrer" className="text-xs underline"
                      style={{ color: 'var(--brand)' }}>📎 {f.split('/').pop()}</a>
                  ))}
                  <label className="px-3 py-1.5 rounded-lg text-xs cursor-pointer"
                    style={{ border: '1px dashed var(--border)', color: 'var(--text-m)' }}>
                    {uploading ? 'Yükleniyor…' : '+ Ek'}
                    <input type="file" className="hidden" accept="image/jpeg,image/png,image/webp,image/gif,application/pdf"
                      onChange={e => { const f = e.target.files?.[0]; if (f) uploadFile(f); e.target.value = '' }} />
                  </label>
                </div>
                <Button size="sm" loading={addComment.isPending}
                  disabled={!comment.trim() && files.length === 0}
                  onClick={() => addComment.mutate()}>Gönder</Button>
              </div>
              {commentError && <div className="text-sm mt-1" style={{ color: '#dc2626' }}>{commentError}</div>}
            </div>
          </div>
        </div>

        {/* Sağ: özet + işlemler */}
        <div className="space-y-4">
          <div className="card p-5">
            <h2 className="text-sm font-semibold mb-3" style={{ color: 'var(--text)' }}>Durum İşlemleri</h2>
            <div className="flex flex-col gap-2">
              {gecisler.map(g => (
                <Button key={g} variant={g === 'rejected' || g === 'cancelled' ? 'secondary' : 'primary'}
                  size="sm" onClick={() => setStatusTarget(g)}>
                  {STATUS_META[g]?.label ?? g}{g === 'in_progress' && r.status === 'testing' ? ' (teste dönüş)' : ''}
                </Button>
              ))}
              {gecisler.length === 0 && (
                <div className="text-sm" style={{ color: 'var(--text-s)' }}>
                  Talep kapandı — başka durum geçişi yok.
                </div>
              )}
            </div>
          </div>

          <div className="card p-5 space-y-3 text-sm">
            <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>Bilgiler</h2>
            <div>
              <div className="flbl">Atanan</div>
              <select className="inp w-full" disabled={isTerminal(r.status) || assign.isPending}
                value={r.assignedTo ?? ''}
                onChange={e => {
                  const u = users.find(x => x.id === e.target.value) ?? null
                  assign.mutate(u)
                }}>
                <option value="">— Atanmadı —</option>
                {users.map(u => <option key={u.id} value={u.id}>{u.fullName}</option>)}
              </select>
            </div>
            <div>
              <div className="flbl">Talep Eden</div>
              <div style={{ color: 'var(--text)' }}>{r.requestedByName}</div>
            </div>
            <div>
              <div className="flbl">Termin</div>
              <div style={{ color: isOverdue(r) ? '#dc2626' : 'var(--text)', fontWeight: isOverdue(r) ? 600 : 400 }}>
                {r.dueDate ? new Date(r.dueDate).toLocaleDateString('tr-TR') : '—'}
                {isOverdue(r) && ' ⚠ Gecikti'}
              </div>
            </div>
            <div>
              <div className="flbl">Oluşturulma</div>
              <div style={{ color: 'var(--text)' }}>{new Date(r.createdAt).toLocaleString('tr-TR')}</div>
            </div>
            <div>
              <div className="flbl">Kapanış</div>
              <div style={{ color: 'var(--text)' }}>
                {r.completedAt ? new Date(r.completedAt).toLocaleString('tr-TR') : '—'}
              </div>
            </div>
          </div>
        </div>
      </div>

      {showEdit && <EditModal r={r} onClose={() => setShowEdit(false)} />}
      {statusTarget && <StatusChangeModal r={r} newStatus={statusTarget} onClose={() => setStatusTarget(null)} />}
    </div>
  )
}

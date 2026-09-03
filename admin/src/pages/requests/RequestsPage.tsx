import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { cn } from '@/lib/utils'
import { useAuthStore } from '@/store/auth'
import {
  STATUS_META, CATEGORY_LABELS, PRIORITY_META,
  isOverdue, type RequestListItem,
} from './constants'

interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

interface ListResponse {
  requests: PagedResult<RequestListItem>
  statusCounts: Record<string, number>
}

const TABS = ['', 'new', 'evaluation', 'planned', 'in_progress', 'testing', 'done', 'rejected', 'cancelled']

function apiErrorMessage(error: unknown, fallback: string): string {
  if (typeof error !== 'object' || error === null || !('response' in error)) return fallback
  const response = error.response
  if (typeof response !== 'object' || response === null || !('data' in response)) return fallback
  const data = response.data
  if (typeof data !== 'object' || data === null || !('error' in data)) return fallback
  return typeof data.error === 'string' ? data.error : fallback
}

function CreateRequestModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: string) => void }) {
  const [title, setTitle] = useState('')
  const [category, setCategory] = useState('yeni_ozellik')
  const [priority, setPriority] = useState('normal')
  const [dueDate, setDueDate] = useState('')
  const [description, setDescription] = useState('')
  const [files, setFiles] = useState<string[]>([])
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState('')

  async function uploadFile(f: File) {
    setUploading(true)
    setError('')
    try {
      const form = new FormData()
      form.append('file', f)
      const res = await api.post('/requests/media', form, { headers: { 'Content-Type': 'multipart/form-data' } })
      setFiles(prev => [...prev, res.data.data.url])
    } catch (error: unknown) {
      setError(apiErrorMessage(error, 'Dosya yüklenemedi.'))
    } finally {
      setUploading(false)
    }
  }

  const create = useMutation({
    mutationFn: async () =>
      (await api.post('/requests', {
        title, description, category, priority,
        dueDate: dueDate || null,
        attachments: files,
      })).data.data as { id: string },
    onSuccess: d => onCreated(d.id),
    onError: (error: unknown) => setError(apiErrorMessage(error, 'Talep oluşturulamadı.')),
  })

  return (
    <Modal open onClose={onClose} title="Yeni Talep" size="lg">
      <div className="space-y-3">
        <div>
          <div className="flbl">Başlık *</div>
          <input className="inp w-full" value={title} onChange={e => setTitle(e.target.value)}
            placeholder="Talebin kısa özeti" autoFocus />
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
            <div className="flbl">Termin (opsiyonel)</div>
            <input type="date" className="inp w-full" value={dueDate} onChange={e => setDueDate(e.target.value)} />
          </div>
        </div>
        <div>
          <div className="flbl">Açıklama</div>
          <textarea className="inp w-full" rows={5} value={description}
            onChange={e => setDescription(e.target.value)}
            placeholder="Ne isteniyor, neden gerekli? Mümkünse örnek/senaryo ekleyin." />
        </div>
        <div>
          <div className="flbl">Ekler</div>
          <div className="flex items-center gap-2 flex-wrap">
            {files.map(f => (
              <a key={f} href={f} target="_blank" rel="noreferrer" className="text-xs underline"
                style={{ color: 'var(--brand)' }}>{f.split('/').pop()}</a>
            ))}
            <label className="px-3 py-1.5 rounded-lg text-xs cursor-pointer"
              style={{ border: '1px dashed var(--border)', color: 'var(--text-m)' }}>
              {uploading ? 'Yükleniyor…' : '+ Dosya ekle (görsel/PDF)'}
              <input type="file" className="hidden" accept="image/jpeg,image/png,image/webp,image/gif,application/pdf"
                onChange={e => { const f = e.target.files?.[0]; if (f) uploadFile(f); e.target.value = '' }} />
            </label>
          </div>
        </div>
        {error && <div className="text-sm" style={{ color: 'var(--danger, #dc2626)' }}>{error}</div>}
      </div>
      <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
        <Button loading={create.isPending} disabled={!title.trim()} onClick={() => create.mutate()}>
          Talebi Oluştur
        </Button>
      </div>
    </Modal>
  )
}

export function RequestsPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const user = useAuthStore(s => s.user)

  const [tab, setTab] = useState('')
  const [category, setCategory] = useState('')
  const [priority, setPriority] = useState('')
  const [mineOnly, setMineOnly] = useState<'' | 'requested' | 'assigned'>('')
  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [page, setPage] = useState(1)
  const [showCreate, setShowCreate] = useState(false)

  const { data, isLoading } = useQuery<ListResponse>({
    queryKey: ['requests', tab, category, priority, mineOnly, appliedSearch, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (tab) params.set('status', tab)
      if (category) params.set('category', category)
      if (priority) params.set('priority', priority)
      if (mineOnly === 'requested' && user) params.set('requestedBy', user.id)
      if (mineOnly === 'assigned' && user) params.set('assignedTo', user.id)
      if (appliedSearch) params.set('search', appliedSearch)
      return (await api.get(`/requests?${params}`)).data.data
    },
  })

  const rows = data?.requests.items ?? []
  const totalPages = Math.ceil((data?.requests.totalCount ?? 0) / 20)
  const counts = data?.statusCounts ?? {}
  const allCount = Object.values(counts).reduce((a, b) => a + b, 0)

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Proje Talepleri</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            Personelin proje ile ilgili istekleri — girin, izleyin, güncelleyin
          </p>
        </div>
        <Button onClick={() => setShowCreate(true)}>+ Yeni Talep</Button>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABS.map(t => (
          <button key={t} className={cn('stab', tab === t && 'active')}
            onClick={() => { setTab(t); setPage(1) }}>
            {t === '' ? 'Tümü' : STATUS_META[t].label}
            <span className="ml-1 text-xs opacity-70">({t === '' ? allCount : counts[t] ?? 0})</span>
          </button>
        ))}
      </div>

      <div className="flex items-center gap-2 mb-4 flex-wrap">
        <select className="inp text-sm py-1.5 px-3 h-auto" value={category}
          onChange={e => { setCategory(e.target.value); setPage(1) }}>
          <option value="">Tüm kategoriler</option>
          {Object.entries(CATEGORY_LABELS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </select>
        <select className="inp text-sm py-1.5 px-3 h-auto" value={priority}
          onChange={e => { setPriority(e.target.value); setPage(1) }}>
          <option value="">Tüm öncelikler</option>
          {Object.entries(PRIORITY_META).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
        </select>
        <div className="flex rounded-lg overflow-hidden" style={{ border: '1px solid var(--border)' }}>
          {([['', 'Herkes'], ['requested', 'Benim taleplerim'], ['assigned', 'Bana atananlar']] as const).map(([k, l]) => (
            <button key={k} onClick={() => { setMineOnly(k); setPage(1) }}
              className="px-3 py-1.5 text-sm"
              style={{
                background: mineOnly === k ? 'var(--brand)' : 'transparent',
                color: mineOnly === k ? '#fff' : 'var(--text-m)',
              }}>{l}</button>
          ))}
        </div>
        <input className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 200 }}
          placeholder="Kod, başlık veya açıklama ara…" value={search}
          onChange={e => setSearch(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') { setAppliedSearch(search.trim()); setPage(1) } }} />
        <button onClick={() => { setAppliedSearch(search.trim()); setPage(1) }}
          className="px-3 py-1.5 rounded-lg text-sm"
          style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Ara</button>
      </div>

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['KOD', 'BAŞLIK', 'KATEGORİ', 'ÖNCELİK', 'DURUM', 'TALEP EDEN', 'ATANAN', 'TERMİN', 'TARİH'].map(h => (
                <th key={h} className="px-4 py-3 text-xs font-semibold text-left"
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={9} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
            )}
            {!isLoading && rows.length === 0 && (
              <tr><td colSpan={9} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Talep bulunamadı. Sağ üstten yeni talep girebilirsiniz.
              </td></tr>
            )}
            {rows.map(r => (
              <tr key={r.id} onClick={() => navigate(`/requests/${r.id}`)}
                className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3 text-xs font-mono whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{r.code}</td>
                <td className="px-4 py-3 text-sm max-w-sm" style={{ color: 'var(--text)' }}>
                  <span className="font-medium">{r.title}</span>
                  {r.commentCount > 0 && (
                    <span className="ml-2 text-xs" style={{ color: 'var(--text-s)' }}>💬 {r.commentCount}</span>
                  )}
                </td>
                <td className="px-4 py-3 text-xs whitespace-nowrap" style={{ color: 'var(--text-m)' }}>
                  {CATEGORY_LABELS[r.category] ?? r.category}
                </td>
                <td className="px-4 py-3">
                  <Badge variant={PRIORITY_META[r.priority]?.badge ?? 'neutral'}>
                    {PRIORITY_META[r.priority]?.label ?? r.priority}
                  </Badge>
                </td>
                <td className="px-4 py-3">
                  <Badge variant={STATUS_META[r.status]?.badge ?? 'neutral'}>
                    {STATUS_META[r.status]?.label ?? r.status}
                  </Badge>
                </td>
                <td className="px-4 py-3 text-xs whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{r.requestedByName}</td>
                <td className="px-4 py-3 text-xs whitespace-nowrap" style={{ color: r.assignedToName ? 'var(--text-m)' : 'var(--text-s)' }}>
                  {r.assignedToName ?? '—'}
                </td>
                <td className="px-4 py-3 text-xs whitespace-nowrap"
                  style={{ color: isOverdue(r) ? '#dc2626' : 'var(--text-s)', fontWeight: isOverdue(r) ? 600 : 400 }}>
                  {r.dueDate ? new Date(r.dueDate).toLocaleDateString('tr-TR') : '—'}
                  {isOverdue(r) && ' ⚠'}
                </td>
                <td className="px-4 py-3 text-xs whitespace-nowrap" style={{ color: 'var(--text-s)' }}>
                  {new Date(r.createdAt).toLocaleDateString('tr-TR')}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 mt-4">
          <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}
            className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>← Önceki</button>
          <span className="text-sm" style={{ color: 'var(--text-s)' }}>{page} / {totalPages}</span>
          <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}
            className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Sonraki →</button>
        </div>
      )}

      {showCreate && (
        <CreateRequestModal
          onClose={() => setShowCreate(false)}
          onCreated={id => {
            setShowCreate(false)
            queryClient.invalidateQueries({ queryKey: ['requests'] })
            navigate(`/requests/${id}`)
          }}
        />
      )}
    </div>
  )
}

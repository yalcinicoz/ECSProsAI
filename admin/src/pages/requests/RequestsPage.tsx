import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
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
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (RequestGrid.Schema) + Excel export + görünümler.
  // Sekme ?tab=<durum> (boş = tümü); kategori/öncelik/"benim talepleri" adlandırılmış filtre olarak gider.
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const user = useAuthStore(s => s.user)
  const [sp] = useSearchParams()

  const tab = sp.get('tab') ?? ''
  const category = sp.get('category') ?? ''
  const priority = sp.get('priority') ?? ''
  const mineOnly = (sp.get('mine') ?? '') as '' | 'requested' | 'assigned'
  const grid = useGridState('requests', { defaultPageSize: 20, defaultSort: 'createdAt', defaultDir: 'desc' })
  const setNamed = (k: string, v: string) => grid.mutate(n => { if (v) n.set(k, v); else n.delete(k) })
  const [showCreate, setShowCreate] = useState(false)

  const named = () => ({
    status: tab || undefined,
    category: category || undefined,
    priority: priority || undefined,
    requestedBy: mineOnly === 'requested' && user ? user.id : undefined,
    assignedTo: mineOnly === 'assigned' && user ? user.id : undefined,
  })

  const { data, isLoading, isFetching, error: listError } = useQuery<ListResponse>({
    queryKey: ['requests', tab, category, priority, mineOnly, ...grid.queryKey],
    queryFn: async () => (await api.get(`/requests?${grid.toParams(named())}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const rows = data?.requests.items ?? []
  const counts = data?.statusCounts ?? {}
  const allCount = Object.values(counts).reduce((a, b) => a + b, 0)

  const columns: GridColumn<RequestListItem>[] = [
    { key: 'code', header: 'KOD', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 130,
      filter: { type: 'text', label: 'Kod', ops: ['startswith', 'contains', 'eq'] },
      cell: r => <span className="text-xs font-mono whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{r.code}</span> },
    { key: 'title', header: 'BAŞLIK', priority: 1, frozen: true, sortable: true, minWidth: 240,
      filter: { type: 'text', label: 'Başlık' },
      filters: [{ field: 'description', label: 'Açıklama', type: 'text' }, { field: 'commentCount', label: 'Yorum sayısı', type: 'number' }],
      cell: r => <span className="text-sm" style={{ color: 'var(--text)' }}>
        <span className="font-medium">{r.title}</span>
        {r.commentCount > 0 && <span className="ml-2 text-xs" style={{ color: 'var(--text-s)' }}>💬 {r.commentCount}</span>}
      </span> },
    { key: 'category', header: 'KATEGORİ', priority: 2, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Kategori', options: Object.entries(CATEGORY_LABELS).map(([value, label]) => ({ value, label })) },
      cell: r => <span className="text-xs whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{CATEGORY_LABELS[r.category] ?? r.category}</span> },
    { key: 'priority', header: 'ÖNCELİK', priority: 1, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Öncelik', options: Object.entries(PRIORITY_META).map(([value, v]) => ({ value, label: v.label })) },
      cell: r => <Badge variant={PRIORITY_META[r.priority]?.badge ?? 'neutral'}>{PRIORITY_META[r.priority]?.label ?? r.priority}</Badge> },
    { key: 'status', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Durum', options: Object.entries(STATUS_META).map(([value, v]) => ({ value, label: v.label })) },
      cell: r => <Badge variant={STATUS_META[r.status]?.badge ?? 'neutral'}>{STATUS_META[r.status]?.label ?? r.status}</Badge> },
    { key: 'requestedByName', header: 'TALEP EDEN', priority: 2, sortable: true, filter: { type: 'text', label: 'Talep eden' },
      cell: r => <span className="text-xs whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{r.requestedByName}</span> },
    { key: 'assignedToName', header: 'ATANAN', priority: 2, sortable: true, filter: { type: 'text', label: 'Atanan' },
      filters: [{ field: 'assigned', label: 'Atanmış', type: 'boolean' }],
      cell: r => <span className="text-xs whitespace-nowrap" style={{ color: r.assignedToName ? 'var(--text-m)' : 'var(--text-s)' }}>{r.assignedToName ?? '—'}</span> },
    { key: 'dueDate', header: 'TERMİN', priority: 2, sortable: true, filter: { type: 'date', label: 'Termin' },
      filters: [{ field: 'overdue', label: 'Termini geçmiş', type: 'boolean' }],
      cell: r => <span className="text-xs whitespace-nowrap"
        style={{ color: isOverdue(r) ? '#dc2626' : 'var(--text-s)', fontWeight: isOverdue(r) ? 600 : 400 }}>
        {r.dueDate ? new Date(r.dueDate).toLocaleDateString('tr-TR') : '—'}{isOverdue(r) && ' ⚠'}</span> },
    { key: 'createdAt', header: 'TARİH', priority: 2, sortable: true, filter: { type: 'date', label: 'Oluşturma', quick: true },
      filters: [{ field: 'completedAt', label: 'Kapanış', type: 'date' }],
      cell: r => <span className="text-xs whitespace-nowrap" style={{ color: 'var(--text-s)' }}>{new Date(r.createdAt).toLocaleDateString('tr-TR')}</span> },
  ]

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
          <button key={t} className={cn('stab', tab === t && 'active')} onClick={() => setNamed('tab', t)}>
            {t === '' ? 'Tümü' : STATUS_META[t].label}
            <span className="ml-1 text-xs opacity-70">({t === '' ? allCount : counts[t] ?? 0})</span>
          </button>
        ))}
      </div>

      <DataGrid<RequestListItem>
        gridId="requests"
        views
        grid={grid}
        columns={columns}
        rows={rows}
        totalCount={data?.requests.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        onRowClick={r => navigate(`/requests/${r.id}`)}
        empty="Talep bulunamadı. Sağ üstten yeni talep girebilirsiniz."
        search={{ placeholder: 'Kod, başlık veya açıklama ara…' }}
        minWidth={1040}
        filterLeading={
          <>
            <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={category} aria-label="Kategori"
              onChange={e => setNamed('category', e.target.value)}>
              <option value="">Tüm kategoriler</option>
              {Object.entries(CATEGORY_LABELS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
            </select>
            <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={priority} aria-label="Öncelik"
              onChange={e => setNamed('priority', e.target.value)}>
              <option value="">Tüm öncelikler</option>
              {Object.entries(PRIORITY_META).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
            </select>
            <div className="flex rounded-lg overflow-hidden" style={{ border: '1px solid var(--border)' }}>
              {([['', 'Herkes'], ['requested', 'Benim taleplerim'], ['assigned', 'Bana atananlar']] as const).map(([k, l]) => (
                <button key={k} onClick={() => setNamed('mine', k)} className="px-3 py-1.5 text-sm"
                  style={{ background: mineOnly === k ? 'var(--brand)' : 'transparent', color: mineOnly === k ? '#fff' : 'var(--text-m)' }}>{l}</button>
              ))}
            </div>
          </>
        }
        export={{ endpoint: '/requests/export', named, fallbackFileName: 'talepler.xlsx' }}
        compact={{
          title: r => `${r.code} · ${r.title}`,
          subtitle: r => `${CATEGORY_LABELS[r.category] ?? r.category} · ${r.assignedToName ?? 'atanmadı'}`,
          right: r => r.dueDate ? new Date(r.dueDate).toLocaleDateString('tr-TR') : '—',
          badge: r => <Badge variant={STATUS_META[r.status]?.badge ?? 'neutral'}>{STATUS_META[r.status]?.label ?? r.status}</Badge>,
        }}
      />

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

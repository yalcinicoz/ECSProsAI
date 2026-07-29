import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { PageSpinner } from '@/components/ui/Spinner'

// 2026-07-22: vitrin geçmişi ayrı sayfaya taşındı — yönetim ekranı sadeleşti.
// Yayın versiyonları (rollback dahil) + yayın geçmişi + değişiklik geçmişi (G13 audit).
// Platform, yönetim sayfasından ?platformId= ile gelir.

interface SnapshotRow { id: string; version: number; publishedAt: string; isActive: boolean; status: string; note: string | null }
interface PublishLogRow { id: string; version: number; previousVersion: number | null; publishedAt: string; status: string; errorMessage: string | null; note: string | null }
// G13: değişiklik geçmişi (vitrin audit kayıtları — iam.audit_logs)
interface AuditRow {
  id: string; action: string; entityType: string; entityId: string
  createdAt: string; userName: string | null; title: string | null
}

const AUDIT_ACTION_TR: Record<string, string> = {
  Created: 'Oluşturuldu', Updated: 'Güncellendi', Deleted: 'Silindi',
  Activated: 'Aktifleştirildi', Deactivated: 'Pasifleştirildi',
  Published: 'Yayınlandı', Rollback: 'Geri Dönüş', Previewed: 'Önizlendi',
}

export function PagesHistoryPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [params] = useSearchParams()
  const platformId = params.get('platformId') ?? sessionStorage.getItem('pages.platformId') ?? ''

  const { data: snapshots = [], isLoading: l1 } = useQuery<SnapshotRow[]>({
    queryKey: ['page-snapshots', platformId],
    queryFn: async () => (await api.get('/pages/snapshots', { params: { firmPlatformId: platformId } })).data.data ?? [],
    enabled: !!platformId,
  })
  const { data: publishLogs = [], isLoading: l2 } = useQuery<PublishLogRow[]>({
    queryKey: ['publish-logs', platformId],
    queryFn: async () => (await api.get('/pages/publish-logs', { params: { firmPlatformId: platformId } })).data.data ?? [],
    enabled: !!platformId,
  })
  const { data: auditLogs = [] } = useQuery<AuditRow[]>({
    queryKey: ['page-audit-logs', platformId],
    queryFn: async () => (await api.get('/pages/audit-logs', { params: { firmPlatformId: platformId } })).data.data ?? [],
    enabled: !!platformId,
  })

  const rollback = useMutation({
    mutationFn: async (targetVersion: number) =>
      api.post('/pages/rollback', { firmPlatformId: platformId, targetVersion }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['page-snapshots', platformId] })
      queryClient.invalidateQueries({ queryKey: ['publish-logs', platformId] })
      queryClient.invalidateQueries({ queryKey: ['page-audit-logs', platformId] })
    },
  })

  if (!platformId) {
    return (
      <div className="space-y-4">
        <p className="text-sm text-[var(--text-m)]">Platform seçilmedi — vitrin yönetiminden gelin.</p>
        <Button variant="secondary" onClick={() => navigate('/storefront/pages')}>← Vitrin Yönetimi</Button>
      </div>
    )
  }
  if (l1 || l2) return <PageSpinner />

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">Vitrin — Geçmiş & Versiyonlar</h1>
        <Button size="sm" variant="secondary" onClick={() => navigate('/storefront/pages')}>← Vitrin Yönetimi</Button>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
          <h2 className="mb-3 text-sm font-semibold">Yayın Versiyonları</h2>
          <div className="space-y-2">
            {snapshots.map((s) => (
              <div key={s.id} className="flex items-center justify-between rounded-lg bg-[var(--surface2)] px-3 py-2 text-sm">
                <div>
                  <span className="font-medium">v{s.version}</span>
                  <span className="ml-2 text-[var(--text-m)]">{new Date(s.publishedAt).toLocaleString('tr-TR')}</span>
                  {s.note && <span className="ml-2 text-xs text-[var(--text-s)]">{s.note}</span>}
                </div>
                <div className="flex items-center gap-2">
                  {s.isActive
                    ? <Badge variant="success">Aktif Yayın</Badge>
                    : <Button size="sm" onClick={() => rollback.mutate(s.version)} disabled={rollback.isPending}>Bu versiyona dön</Button>}
                </div>
              </div>
            ))}
            {snapshots.length === 0 && <p className="text-sm text-[var(--text-m)]">Henüz yayın yapılmadı.</p>}
          </div>
        </div>

        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
          <h2 className="mb-3 text-sm font-semibold">Yayın Geçmişi</h2>
          <div className="max-h-72 space-y-2 overflow-y-auto">
            {publishLogs.map((l) => (
              <div key={l.id} className="rounded-lg bg-[var(--surface2)] px-3 py-2 text-sm">
                <div className="flex items-center justify-between">
                  <span>
                    v{l.version}
                    {l.previousVersion != null && <span className="text-[var(--text-m)]"> (önceki v{l.previousVersion})</span>}
                  </span>
                  <Badge variant={l.status === 'success' ? 'success' : l.status === 'rollback' ? 'warning' : 'danger'}>
                    {l.status === 'success' ? 'Yayınlandı' : l.status === 'rollback' ? 'Geri Dönüş' : 'Başarısız'}
                  </Badge>
                </div>
                <div className="text-xs text-[var(--text-m)]">{new Date(l.publishedAt).toLocaleString('tr-TR')}{l.note ? ` · ${l.note}` : ''}</div>
                {l.errorMessage && <div className="mt-1 text-xs text-red-600">{l.errorMessage}</div>}
              </div>
            ))}
            {publishLogs.length === 0 && <p className="text-sm text-[var(--text-m)]">Kayıt yok.</p>}
          </div>
        </div>
      </div>

      {/* G13: değişiklik geçmişi — kim, neyi, ne zaman (spec audit ekranı) */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
        <h2 className="mb-3 text-sm font-semibold">Değişiklik Geçmişi</h2>
        <div className="max-h-80 space-y-1 overflow-y-auto">
          {auditLogs.map((l) => (
            <div key={l.id} className="flex items-center justify-between rounded-lg bg-[var(--surface2)] px-3 py-1.5 text-sm">
              <div className="min-w-0 truncate">
                <Badge variant={l.action === 'Deleted' || l.action === 'Deactivated' ? 'danger' : l.action === 'Created' || l.action === 'Published' ? 'success' : 'neutral'}>
                  {AUDIT_ACTION_TR[l.action] ?? l.action}
                </Badge>
                <span className="ml-2 font-medium">{l.title ?? '—'}</span>
                <span className="ml-2 text-xs text-[var(--text-m)]">{l.entityType}</span>
              </div>
              <div className="ml-3 shrink-0 text-xs text-[var(--text-m)]">
                {l.userName || 'bilinmeyen'} · {new Date(l.createdAt).toLocaleString('tr-TR')}
              </div>
            </div>
          ))}
          {auditLogs.length === 0 && <p className="text-sm text-[var(--text-m)]">Henüz kayıt yok.</p>}
        </div>
      </div>
    </div>
  )
}

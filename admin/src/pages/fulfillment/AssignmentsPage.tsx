import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { errText } from '@/components/ui/DataTable.utils'

/** FAZ 15.4e (2026-09-10, eski "Kullanıcı Toplama Yönetimi"): aktif görevlerde personel başına atanan/toplanan/kalan;
 * atanmamış havuzdan sayıyla dağıtım ("N satır ver") ve personelden personele aktarım. Satır bazlı seçim Görev Detayı'nda. */

interface PlanRow { planId: string; planNumber: string; status: string; plannedAt: string; unassigned: number; assigned: number; picked: number; total: number }
interface PersonRow { planId: string; userId: string; assigned: number; picked: number; remaining: number }
interface Summary { plans: PlanRow[]; people: PersonRow[] }
interface User { id: string; username: string; firstName: string; lastName: string }

export function AssignmentsPage() {
  const qc = useQueryClient()
  const [planId, setPlanId] = useState('')
  const [target, setTarget] = useState('')
  const [count, setCount] = useState(10)
  const [from, setFrom] = useState('')
  const [err, setErr] = useState('')

  const { data: users = [] } = useQuery<User[]>({ queryKey: ['iam-users-select-mini'], queryFn: async () => (await api.get('/iam/users?page=1&pageSize=200')).data.data.items, staleTime: 5 * 60_000 })
  const ad = (id?: string | null) => { const u = users.find(x => x.id === id); return u ? (`${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.username) : (id ? id.slice(0, 8) : '—') }
  const { data, isLoading, refetch } = useQuery<Summary>({ queryKey: ['assignment-summary'], queryFn: async () => (await api.get('/fulfillment/assignment-summary')).data.data, refetchInterval: 30_000 })

  const plans = data?.plans ?? []
  const secili = plans.find(p => p.planId === planId) ?? plans[0]
  const people = (data?.people ?? []).filter(p => p.planId === secili?.planId)

  const assign = useMutation({
    mutationFn: async (body: { assignTo: string; count: number; fromAssignee?: string | null }) =>
      (await api.post(`/fulfillment/picking-plans/${secili!.planId}/auto-assign`, body)).data.data.assigned as number,
    onSuccess: (n) => { setErr(''); qc.invalidateQueries({ queryKey: ['assignment-summary'] }); qc.invalidateQueries({ queryKey: ['picking-plan-lines'] }); alert(`${n} satır dağıtıldı.`) },
    onError: (e) => setErr(errText(e)),
  })

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4 gap-3 flex-wrap">
        <div>
          <h1 className="text-lg font-bold" style={{ color: 'var(--text)' }}>Personele Dağıt</h1>
          <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>Aktif toplama görevlerinde personel yükü; havuzdan sayıyla dağıtım ve personeller arası aktarım. Satır seçerek dağıtım Görev Detayı'nda.</p>
        </div>
        <Button size="sm" variant="secondary" onClick={() => refetch()}>Yenile</Button>
      </div>

      {isLoading ? <p className="text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</p> : plans.length === 0 ? (
        <div className="card p-6 text-sm" style={{ color: 'var(--text-s)' }}>Aktif (bekleyen/toplanan) görev yok. <Link to="/fulfillment/tasks/new" className="underline" style={{ color: 'var(--brand)' }}>Yeni görev oluştur</Link></div>
      ) : (
        <div className="grid gap-4 lg:grid-cols-3">
          <div className="card p-0 overflow-hidden lg:col-span-1">
            <div className="px-4 py-2 text-xs font-semibold uppercase" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>Aktif görevler</div>
            {plans.map(p => (
              <button key={p.planId} type="button" onClick={() => setPlanId(p.planId)}
                className="block w-full text-left px-4 py-2.5 hover:bg-[var(--surface2)]"
                style={{ borderBottom: '1px solid var(--border)', background: secili?.planId === p.planId ? 'var(--brand-bg)' : undefined }}>
                <div className="flex items-center gap-2"><span className="font-mono text-sm font-medium" style={{ color: 'var(--text)' }}>{p.planNumber}</span><Badge variant={p.status === 'picking' ? 'success' : 'warning'}>{p.status === 'picking' ? 'toplanıyor' : 'bekliyor'}</Badge></div>
                <div className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>{new Date(p.plannedAt).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' })} · {p.total} satır · <b style={{ color: p.unassigned > 0 ? '#d97706' : undefined }}>{p.unassigned} atanmamış</b> · {p.picked} toplandı</div>
              </button>
            ))}
          </div>

          {secili && (
            <div className="lg:col-span-2 space-y-4">
              <div className="card p-4">
                <div className="flex flex-wrap items-center gap-3 mb-3">
                  <h2 className="text-sm font-bold font-mono" style={{ color: 'var(--text)' }}>{secili.planNumber}</h2>
                  <span className="text-sm" style={{ color: 'var(--text-m)' }}>atanmamış <b style={{ color: '#d97706' }}>{secili.unassigned}</b> · atanmış bekleyen <b>{secili.assigned}</b> · toplanan <b>{secili.picked}</b> / {secili.total}</span>
                  <Link to={`/fulfillment/tasks/${secili.planId}`} className="ml-auto text-xs underline" style={{ color: 'var(--brand)' }}>Görev detayı →</Link>
                </div>
                <table className="w-full text-sm">
                  <thead><tr style={{ borderBottom: '1px solid var(--border)' }}>
                    {['PERSONEL', 'ATANAN', 'TOPLANAN', 'KALAN', ''].map(h => <th key={h} className="text-left py-1.5 text-xs font-semibold" style={{ color: 'var(--text-s)' }}>{h}</th>)}
                  </tr></thead>
                  <tbody>
                    {people.length === 0 && <tr><td colSpan={5} className="py-3 text-sm" style={{ color: 'var(--text-s)' }}>Bu görevde henüz atama yok.</td></tr>}
                    {people.map(p => (
                      <tr key={p.userId} style={{ borderBottom: '1px solid var(--border)' }}>
                        <td className="py-1.5" style={{ color: 'var(--text)' }}>{ad(p.userId)}</td>
                        <td className="py-1.5">{p.assigned}</td>
                        <td className="py-1.5">{p.picked}</td>
                        <td className="py-1.5 font-bold" style={{ color: p.remaining > 0 ? '#d97706' : '#16a34a' }}>{p.remaining}</td>
                        <td className="py-1.5 text-right">
                          <button type="button" className="text-xs underline" style={{ color: 'var(--brand)' }} onClick={() => { setFrom(p.userId); setTarget('') }}>bundan aktar</button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="card p-4">
                <h3 className="text-sm font-bold mb-2" style={{ color: 'var(--text)' }}>{from ? `${ad(from)} → başka personele aktar` : 'Havuzdan dağıt'}</h3>
                <div className="grid grid-cols-1 md:grid-cols-4 gap-2 items-end">
                  {from && <div><label className="flbl">Kaynak</label><div className="flex items-center gap-2 text-sm"><span>{ad(from)}</span><button type="button" className="text-xs underline" onClick={() => setFrom('')}>havuza dön</button></div></div>}
                  <div><label className="flbl">Hedef personel</label>
                    <select className="inp" value={target} onChange={e => setTarget(e.target.value)}><option value="">Seçin</option>{users.filter(u => u.id !== from).map(u => <option key={u.id} value={u.id}>{ad(u.id)}</option>)}</select></div>
                  <div><label className="flbl">Satır sayısı</label><input className="inp" type="number" min={1} value={count} onChange={e => setCount(Math.max(1, parseInt(e.target.value) || 1))} /></div>
                  <div><Button onClick={() => assign.mutate({ assignTo: target, count, fromAssignee: from || null })} loading={assign.isPending} disabled={!target}>{from ? 'Aktar' : 'Dağıt'}</Button></div>
                </div>
                <p className="text-xs mt-2" style={{ color: 'var(--text-s)' }}>Satırlar rota sırasına göre seçilir; toplanmış satırlar aktarılmaz. Kaynak seçiliyse yalnız o personelin bekleyen satırlarından alınır.</p>
                {err && <p className="text-sm mt-2 text-red-500">{err}</p>}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

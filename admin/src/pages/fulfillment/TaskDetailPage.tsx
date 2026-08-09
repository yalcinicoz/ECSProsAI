import { useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { PageSpinner } from '@/components/ui/Spinner'
import { errText, tarihSaat } from '@/components/ui/DataTable'
import { PLAN_DURUM, PLAN_TIP, dagitimDurum, ToplanmaIlerleme } from './PickingPlansPage'
import { cn } from '@/lib/utils'

interface PlanDetail {
  id: string
  planNumber: string
  warehouseId: string
  planType: string
  status: string
  plannedAt: string
  startedAt?: string
  completedAt?: string
}

interface PlanLine {
  id: string
  orderId: string
  orderItemId: string
  orderNumber: string
  displayName: string
  sku: string
  variantBarcode: string
  quantity: number
  pickedQuantity: number
  sourceBinCode?: string | null
  pickedBinCode?: string | null
  assignedTo?: string | null
  assignedAt?: string | null
  pickedBy?: string | null
  pickedAt?: string | null
  status: string
  routeOrder: number
}

interface IamUser { id: string; username: string; firstName: string; lastName: string; isActive: boolean }
interface Warehouse { id: string; code: string; nameI18n?: Record<string, string> }

const SATIR_DURUM: Record<string, [string, BadgeVariant]> = {
  pending:  ['Bekliyor', 'neutral'],
  assigned: ['Atandı', 'info'],
  picked:   ['Toplandı', 'success'],
  short:    ['Eksik', 'warning'],
  returned: ['İade', 'danger'],
}

export function TaskDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [secili, setSecili] = useState<Set<string>>(new Set())
  const [personelId, setPersonelId] = useState<string | null>(null)
  const [paylasOpen, setPaylasOpen] = useState(false)
  const [paylasPersonel, setPaylasPersonel] = useState<string[]>([])
  const [error, setError] = useState('')

  const { data: plan, isLoading } = useQuery<PlanDetail>({
    queryKey: ['picking-plan-detail', id],
    queryFn: async () => (await api.get(`/fulfillment/picking-plans/${id}`)).data.data,
    enabled: !!id,
  })

  const { data: lines = [], isLoading: linesLoading } = useQuery<PlanLine[]>({
    queryKey: ['picking-plan-lines', id],
    queryFn: async () => (await api.get(`/fulfillment/picking-plans/${id}/lines`)).data.data,
    enabled: !!id,
  })

  const { data: users = [] } = useQuery<IamUser[]>({
    queryKey: ['iam-users-select'],
    queryFn: async () => (await api.get('/iam/users?page=1&pageSize=200')).data.data.items,
  })
  const kullaniciAd = (uid?: string | null) => {
    if (!uid) return '—'
    const u = users.find(x => x.id === uid)
    return u ? (`${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.username) : uid.slice(0, 8)
  }

  const { data: warehouses = [] } = useQuery<Warehouse[]>({
    queryKey: ['warehouses'],
    queryFn: async () => (await api.get('/inventory/warehouses')).data.data,
  })
  const depoAd = warehouses.find(w => w.id === plan?.warehouseId)
  const depoLabel = depoAd ? (depoAd.nameI18n?.['tr'] ?? depoAd.code) : null

  // ── Türetilmiş sayılar ──
  const totalLines = lines.length
  const assignedLines = lines.filter(l => l.assignedTo).length
  const pickedLines = lines.filter(l => l.status === 'picked').length
  const dagitim = dagitimDurum(assignedLines, totalLines)

  // Atanabilir satırlar (toplanmamış/kapanmamış)
  const atanabilir = useMemo(() => lines.filter(l => l.status === 'pending' || l.status === 'assigned'), [lines])
  const atanmamis = useMemo(() => lines.filter(l => l.status === 'pending' && !l.assignedTo), [lines])

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['picking-plan-detail', id] })
    queryClient.invalidateQueries({ queryKey: ['picking-plan-lines', id] })
    queryClient.invalidateQueries({ queryKey: ['picking-plans'] })
  }

  const aksiyon = useMutation({
    mutationFn: async (path: string) => {
      setError('')
      await api.post(`/fulfillment/picking-plans/${id}/${path}`, {})
    },
    onSuccess: invalidate,
    onError: (e: unknown) => setError(errText(e)),
  })

  const ata = useMutation({
    mutationFn: async () => {
      setError('')
      await api.post(`/fulfillment/picking-plans/${id}/assign-lines`, {
        lineIds: Array.from(secili),
        assignTo: personelId,
      })
    },
    onSuccess: () => { setSecili(new Set()); invalidate() },
    onError: (e: unknown) => setError(errText(e)),
  })

  // Round-robin: atanmamış satırlar seçili personellere sırayla, personel başına bir POST
  const paylas = useMutation({
    mutationFn: async () => {
      setError('')
      const perLine = new Map<string, string[]>()
      atanmamis.forEach((l, idx) => {
        const uid = paylasPersonel[idx % paylasPersonel.length]
        perLine.set(uid, [...(perLine.get(uid) ?? []), l.id])
      })
      for (const [uid, lineIds] of perLine) {
        await api.post(`/fulfillment/picking-plans/${id}/assign-lines`, { lineIds, assignTo: uid })
      }
    },
    onSuccess: () => { setPaylasOpen(false); setPaylasPersonel([]); invalidate() },
    onError: (e: unknown) => { invalidate(); setError(errText(e)) },
  })

  if (isLoading || !plan) return <PageSpinner />

  const [durumLabel, durumVariant] = PLAN_DURUM[plan.status] ?? [plan.status, 'neutral' as BadgeVariant]
  const tumuSecili = atanabilir.length > 0 && atanabilir.every(l => secili.has(l.id))

  const toggleSatir = (lineId: string) => setSecili(prev => {
    const next = new Set(prev)
    if (next.has(lineId)) next.delete(lineId); else next.add(lineId)
    return next
  })
  const toggleTumu = () => setSecili(tumuSecili ? new Set() : new Set(atanabilir.map(l => l.id)))
  const togglePaylasPersonel = (uid: string) =>
    setPaylasPersonel(prev => (prev.includes(uid) ? prev.filter(x => x !== uid) : [...prev, uid]))

  const aktifPersonel = users.filter(u => u.isActive)

  return (
    <div className="p-6">
      {/* ── Başlık ── */}
      <div className="flex flex-wrap items-center gap-3 mb-1">
        <button onClick={() => navigate('/fulfillment/picking-plans')} className="text-sm" style={{ color: 'var(--text-s)' }}>←</button>
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>{plan.planNumber}</h1>
        <Badge variant={plan.planType === 'single_item' ? 'info' : 'neutral'}>{PLAN_TIP[plan.planType] ?? plan.planType}</Badge>
        <Badge variant={durumVariant}>{durumLabel}</Badge>
        <Badge variant={dagitim.variant}>{dagitim.label}</Badge>
        <div className="ml-auto flex gap-2">
          {plan.status === 'pending' && (
            <Button size="sm" onClick={() => aksiyon.mutate('start')} loading={aksiyon.isPending}>Başlat</Button>
          )}
          {plan.status === 'picking' && (
            <Button size="sm" onClick={() => aksiyon.mutate('complete')} loading={aksiyon.isPending}>Tamamla</Button>
          )}
        </div>
      </div>
      <div className="flex flex-wrap items-center gap-x-4 gap-y-1 mb-5 text-sm" style={{ color: 'var(--text-s)' }}>
        <span>Planlama: {tarihSaat(plan.plannedAt)}</span>
        {plan.startedAt && <span>Başlama: {tarihSaat(plan.startedAt)}</span>}
        {plan.completedAt && <span>Bitiş: {tarihSaat(plan.completedAt)}</span>}
        {depoLabel && <span>Depo: {depoLabel}</span>}
        <span className="inline-flex items-center gap-2">
          Toplanma: <ToplanmaIlerleme picked={pickedLines} total={totalLines} />
        </span>
      </div>

      {error && <p className="text-sm text-red-500 mb-3">{error}</p>}

      {/* ── Dağıtım araç çubuğu ── */}
      {['pending', 'picking'].includes(plan.status) && (
        <div className="card p-3 mb-4 flex flex-wrap items-center gap-3">
          <span className="text-sm" style={{ color: 'var(--text)' }}>
            <b>{secili.size}</b> satır seçili
          </span>
          <SearchableSelect
            className="w-56"
            value={personelId}
            onChange={setPersonelId}
            placeholder="Personel seçin"
            options={aktifPersonel.map(u => ({
              value: u.id,
              label: `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.username,
            }))}
          />
          <Button size="sm" onClick={() => ata.mutate()} loading={ata.isPending}
            disabled={secili.size === 0 || !personelId}>
            Seçilenleri Ata
          </Button>
          <Button size="sm" variant="secondary"
            onClick={() => { setError(''); setPaylasPersonel([]); setPaylasOpen(true) }}
            disabled={atanmamis.length === 0}>
            Atanmamışları Eşit Paylaştır ({atanmamis.length})
          </Button>
        </div>
      )}

      {/* ── Satır tablosu (rota sıralı) ── */}
      <div className="card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
                <th className="px-3 py-3 w-8">
                  <input type="checkbox" checked={tumuSecili} onChange={toggleTumu}
                    disabled={atanabilir.length === 0} title="Tümünü seç" />
                </th>
                {['#', 'RAF', 'SKU', 'ÜRÜN', 'SİPARİŞ NO', 'ADET', 'DURUM', 'ATANAN'].map(h => (
                  <th key={h} className="px-3 py-3 text-xs font-semibold text-left whitespace-nowrap"
                    style={{ color: 'var(--text-s)' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {linesLoading && (
                <tr><td colSpan={9} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
              )}
              {!linesLoading && lines.length === 0 && (
                <tr><td colSpan={9} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                  Bu görevde satır yok (eski akışla oluşturulmuş olabilir).
                </td></tr>
              )}
              {!linesLoading && lines.map(l => {
                const secilebilir = l.status === 'pending' || l.status === 'assigned'
                const [sl, sv] = SATIR_DURUM[l.status] ?? [l.status, 'neutral' as BadgeVariant]
                return (
                  <tr key={l.id}
                    onClick={secilebilir ? () => toggleSatir(l.id) : undefined}
                    className={cn('transition-colors', secilebilir && 'cursor-pointer hover:bg-[var(--surface2)]')}
                    style={{ borderBottom: '1px solid var(--border)' }}>
                    <td className="px-3 py-2.5">
                      <input type="checkbox" checked={secili.has(l.id)} disabled={!secilebilir}
                        onClick={e => e.stopPropagation()} onChange={() => toggleSatir(l.id)} />
                    </td>
                    <td className="px-3 py-2.5 text-xs" style={{ color: 'var(--text-s)' }}>{l.routeOrder}</td>
                    <td className="px-3 py-2.5">
                      {l.sourceBinCode
                        ? <code className="text-xs font-mono" style={{ color: 'var(--text)' }}>{l.sourceBinCode}</code>
                        : <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span>}
                    </td>
                    <td className="px-3 py-2.5 text-xs font-mono" style={{ color: 'var(--text-m)' }}>{l.sku || '—'}</td>
                    <td className="px-3 py-2.5 text-sm" style={{ color: 'var(--text)' }}>{l.displayName}</td>
                    <td className="px-3 py-2.5">
                      <code className="text-xs font-mono" style={{ color: 'var(--text-m)' }}>{l.orderNumber}</code>
                    </td>
                    <td className="px-3 py-2.5 text-sm whitespace-nowrap" style={{ color: 'var(--text)' }}>
                      {l.pickedQuantity > 0 ? `${l.pickedQuantity}/${l.quantity}` : l.quantity}
                    </td>
                    <td className="px-3 py-2.5"><Badge variant={sv}>{sl}</Badge></td>
                    <td className="px-3 py-2.5 text-sm whitespace-nowrap" style={{ color: 'var(--text-m)' }}>
                      {kullaniciAd(l.assignedTo)}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      </div>

      {/* ── Eşit paylaştırma modalı ── */}
      <Modal open={paylasOpen} onClose={() => setPaylasOpen(false)} title="Atanmamışları Eşit Paylaştır">
        <p className="text-sm mb-3" style={{ color: 'var(--text-m)' }}>
          <b>{atanmamis.length}</b> atanmamış satır, seçilen personellere rota sırasıyla dönüşümlü (round-robin) dağıtılır.
        </p>
        <div className="thin-scroll rounded-xl p-2 overflow-y-auto" style={{ border: '1px solid var(--border)', maxHeight: 240 }}>
          {aktifPersonel.map(u => (
            <label key={u.id} className="flex items-center gap-2 px-1 py-1.5 text-sm cursor-pointer" style={{ color: 'var(--text)' }}>
              <input type="checkbox" checked={paylasPersonel.includes(u.id)} onChange={() => togglePaylasPersonel(u.id)} />
              {`${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.username}
            </label>
          ))}
          {aktifPersonel.length === 0 && (
            <p className="text-xs px-1 py-2" style={{ color: 'var(--text-s)' }}>Aktif kullanıcı bulunamadı.</p>
          )}
        </div>
        {paylasPersonel.length > 0 && (
          <p className="text-xs mt-2" style={{ color: 'var(--text-s)' }}>
            Personel başına ~{Math.ceil(atanmamis.length / paylasPersonel.length)} satır düşer.
          </p>
        )}
        {error && <p className="text-sm mt-2 text-red-500">{error}</p>}
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setPaylasOpen(false)}>Vazgeç</Button>
          <Button onClick={() => paylas.mutate()} loading={paylas.isPending} disabled={paylasPersonel.length === 0}>
            Paylaştır
          </Button>
        </div>
      </Modal>
    </div>
  )
}

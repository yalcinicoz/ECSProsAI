import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { useAuthStore } from '@/store/auth'

// ── Types ─────────────────────────────────────────────────────────────────────

export interface PackageRow {
  id: string
  orderId: string
  shipmentId?: string
  packageNumber: string
  sequenceInOrder: number
  supplierId?: string
  barcode: string
  cargoIntegrationCode?: string
  cargoIntegrationCodeSource?: string
  weight?: number
  desi?: number
  status: string
  packedAt?: string
  labelPrintedAt?: string
  items: { id: string; orderItemId: string; variantId: string; quantity: number }[]
}

interface CodeHistoryRow {
  id: string
  oldPackageNumber?: string
  oldCargoIntegrationCode?: string
  changeType: string
  reason: string
  changedAt: string
}

interface CargoIntegrationOpt {
  id: string
  serviceCode: string
  serviceNameI18n: Record<string, string>
  name?: string
  isActive: boolean
}

const PKG_STATUS: Record<string, { label: string; variant: 'success' | 'info' | 'neutral' | 'warning' }> = {
  packed: { label: 'Paketlendi', variant: 'info' },
  merged: { label: 'Birleştirildi', variant: 'neutral' },
  shipped: { label: 'Kargoda', variant: 'success' },
}

const CHANGE_TYPE_LABEL: Record<string, string> = {
  merge: 'Birleştirme',
  renumber: 'Yeniden numaralandırma',
  cargo_change: 'Kargo kodu değişimi',
}

// ── Component ─────────────────────────────────────────────────────────────────

export function OrderPackagesSection({ orderId, orderStatus, cargoIntegrations }: {
  orderId: string
  orderStatus: string
  cargoIntegrations: CargoIntegrationOpt[]
}) {
  const queryClient = useQueryClient()
  const canMerge = useAuthStore(s => s.hasPermission)('order.packages.merge')

  const [selected, setSelected] = useState<string[]>([])
  const [error, setError] = useState('')
  const [historyFor, setHistoryFor] = useState<string | null>(null)

  // Modallar
  const [renumberFor, setRenumberFor] = useState<PackageRow | null>(null)
  const [reason, setReason] = useState('')
  const [mergeOpen, setMergeOpen] = useState(false)
  const [cargoFor, setCargoFor] = useState<PackageRow | null>(null)
  const [cargoIntegrationId, setCargoIntegrationId] = useState('')
  const [externalCode, setExternalCode] = useState('')

  const { data: packages = [] } = useQuery<PackageRow[]>({
    queryKey: ['order-packages', orderId],
    queryFn: async () => (await api.get(`/fulfillment/packages?orderId=${orderId}`)).data.data ?? [],
  })

  const { data: history = [] } = useQuery<CodeHistoryRow[]>({
    queryKey: ['package-code-history', historyFor],
    enabled: !!historyFor,
    queryFn: async () =>
      (await api.get(`/fulfillment/packages/${historyFor}/code-history`)).data.data ?? [],
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['order-packages', orderId] })
  const fail = (err: any, fallback: string) => setError(err?.response?.data?.error ?? fallback)

  const splitMutation = useMutation({
    mutationFn: async () => { await api.post('/fulfillment/packages/split', { orderId }) },
    onSuccess: () => { setError(''); refresh() },
    onError: (e: any) => fail(e, 'Paketleme başarısız.'),
  })

  const renumberMutation = useMutation({
    mutationFn: async () => {
      await api.post(`/fulfillment/packages/${renumberFor!.id}/renumber`, { reason })
    },
    onSuccess: () => { setRenumberFor(null); setError(''); refresh() },
    onError: (e: any) => fail(e, 'Yeniden numaralandırma başarısız.'),
  })

  const mergeMutation = useMutation({
    mutationFn: async () => {
      await api.post('/fulfillment/packages/merge', { packageIds: selected, reason })
    },
    onSuccess: () => { setMergeOpen(false); setSelected([]); setError(''); refresh() },
    onError: (e: any) => fail(e, 'Birleştirme başarısız.'),
  })

  const cargoMutation = useMutation({
    mutationFn: async () => {
      await api.post(`/fulfillment/packages/${cargoFor!.id}/cargo-code`, {
        firmPlatformIntegrationId: externalCode ? null : (cargoIntegrationId || null),
        externalCode: externalCode || null,
        reason: reason || null,
      })
    },
    onSuccess: () => { setCargoFor(null); setError(''); refresh() },
    onError: (e: any) => fail(e, 'Kargo kodu atanamadı.'),
  })

  const aktifPaketler = packages.filter(p => p.status !== 'merged')
  const locked = (p: PackageRow) => !!p.shipmentId || !!p.labelPrintedAt
  const toggle = (id: string) =>
    setSelected(s => s.includes(id) ? s.filter(x => x !== id) : [...s, id])

  const cargoIntName = (p: PackageRow) =>
    p.cargoIntegrationCodeSource === 'external' ? 'dış kod' : 'üretildi'

  return (
    <div className="card p-4">
      <div className="flex items-center gap-2 mb-3">
        <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>
          Paketler ({aktifPaketler.length})
        </h2>
        <div className="ml-auto flex gap-2">
          {packages.length === 0 && ['confirmed', 'processing'].includes(orderStatus) && (
            <Button size="sm" loading={splitMutation.isPending}
              onClick={() => splitMutation.mutate()}>
              Tedarikçiye Göre Paketle
            </Button>
          )}
          {canMerge && selected.length >= 2 && (
            <Button size="sm" variant="danger"
              onClick={() => { setReason(''); setError(''); setMergeOpen(true) }}>
              Seçilenleri Birleştir ({selected.length})
            </Button>
          )}
        </div>
      </div>

      {packages.length === 0 && (
        <p className="text-sm" style={{ color: 'var(--text-s)' }}>
          Henüz paket yok. Normal akış: sipariş onaylandıktan sonra tedarikçiye göre bölünür,
          her pakete ayrı fatura ve kargo düzenlenir.
        </p>
      )}

      {packages.map(p => {
        const st = PKG_STATUS[p.status] ?? { label: p.status, variant: 'neutral' as const }
        return (
          <div key={p.id} className="py-2" style={{ borderBottom: '1px solid var(--border)' }}>
            <div className="flex flex-wrap items-center gap-2 text-sm">
              {canMerge && p.status === 'packed' && !locked(p) && (
                <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
                  checked={selected.includes(p.id)} onChange={() => toggle(p.id)} />
              )}
              <code className="text-xs font-mono font-semibold" style={{ color: 'var(--text)' }}>
                {p.packageNumber}
              </code>
              <span className="text-xs" style={{ color: 'var(--text-s)' }}>#{p.sequenceInOrder}</span>
              <Badge variant={st.variant}>{st.label}</Badge>
              {p.labelPrintedAt && <Badge variant="warning">Etiket basıldı</Badge>}
              {p.cargoIntegrationCode && (
                <span className="text-xs" style={{ color: 'var(--text-m)' }}>
                  Kargo kodu: <code>{p.cargoIntegrationCode}</code> ({cargoIntName(p)})
                </span>
              )}
              <span className="text-xs" style={{ color: 'var(--text-s)' }}>
                {p.items.length} kalem{p.weight ? ` · ${p.weight} kg` : ''}{p.desi ? ` · ${p.desi} desi` : ''}
              </span>
              <div className="ml-auto flex gap-1.5">
                <button className="text-xs px-2 py-1 rounded-lg"
                  style={{ color: 'var(--text-m)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
                  onClick={() => setHistoryFor(historyFor === p.id ? null : p.id)}>
                  Geçmiş
                </button>
                {p.status === 'packed' && !locked(p) && (
                  <>
                    <button className="text-xs px-2 py-1 rounded-lg"
                      style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
                      onClick={() => { setCargoFor(p); setCargoIntegrationId(''); setExternalCode(''); setReason(''); setError('') }}>
                      Kargo Kodu
                    </button>
                    <button className="text-xs px-2 py-1 rounded-lg"
                      style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
                      onClick={() => { setRenumberFor(p); setReason(''); setError('') }}>
                      Yeni No
                    </button>
                  </>
                )}
              </div>
            </div>

            {historyFor === p.id && (
              <div className="mt-2 ml-1 rounded-lg p-2" style={{ background: 'var(--surface2)' }}>
                {history.length === 0
                  ? <p className="text-xs" style={{ color: 'var(--text-s)' }}>Kod değişikliği yok.</p>
                  : history.map(h => (
                    <p key={h.id} className="text-xs py-0.5" style={{ color: 'var(--text-s)' }}>
                      {new Date(h.changedAt).toLocaleString('tr-TR')} — {CHANGE_TYPE_LABEL[h.changeType] ?? h.changeType}
                      {h.oldPackageNumber && <> · eski no: <code>{h.oldPackageNumber}</code></>}
                      {h.oldCargoIntegrationCode && <> · eski kargo kodu: <code>{h.oldCargoIntegrationCode}</code></>}
                      {' · '}{h.reason}
                    </p>
                  ))}
              </div>
            )}
          </div>
        )
      })}

      {error && <p className="text-sm mt-2" style={{ color: '#ef4444' }}>{error}</p>}

      {/* Yeniden numaralandırma */}
      <Modal open={!!renumberFor} onClose={() => setRenumberFor(null)}
        title={`Yeni Numara — ${renumberFor?.packageNumber ?? ''}`}
        footer={
          <>
            <Button variant="secondary" onClick={() => setRenumberFor(null)}>İptal</Button>
            <Button loading={renumberMutation.isPending} disabled={!reason.trim()}
              onClick={() => renumberMutation.mutate()}>Yeni Numara Ver</Button>
          </>
        }>
        <p className="text-sm mb-3" style={{ color: 'var(--text-s)' }}>
          Pakete seriden yeni numara verilir; eski numara geçmişe yazılır ve bir daha kullanılmaz.
          Bağlı kargo kodu varsa temizlenir, yeniden atanması gerekir.
        </p>
        <label className="flbl">Gerekçe (zorunlu)</label>
        <input className="inp" value={reason} onChange={e => setReason(e.target.value)}
          placeholder="örn. Paket içeriği değişti" />
        {error && <p className="text-sm mt-2" style={{ color: '#ef4444' }}>{error}</p>}
      </Modal>

      {/* Birleştirme (istisna akışı) */}
      <Modal open={mergeOpen} onClose={() => setMergeOpen(false)} title="Paketleri Birleştir — İstisna İşlemi"
        footer={
          <>
            <Button variant="secondary" onClick={() => setMergeOpen(false)}>Vazgeç</Button>
            <Button variant="danger" loading={mergeMutation.isPending} disabled={!reason.trim()}
              onClick={() => mergeMutation.mutate()}>Birleştirmeyi Onayla</Button>
          </>
        }>
        <p className="text-sm mb-3" style={{ color: 'var(--text-s)' }}>
          Normal akış <strong>paket başına ayrı fatura ve kargodur</strong>. Birleştirme bilinçli bir
          istisnadır: seçilen {selected.length} paket kapatılır, kalemler yeni tek pakete taşınır,
          eski numaralar geçmişe yazılır ve geri kullanılmaz.
        </p>
        <label className="flbl">Gerekçe (zorunlu)</label>
        <input className="inp" value={reason} onChange={e => setReason(e.target.value)}
          placeholder="örn. Müşteri tek kargo talep etti" />
        {error && <p className="text-sm mt-2" style={{ color: '#ef4444' }}>{error}</p>}
      </Modal>

      {/* Kargo kodu atama */}
      <Modal open={!!cargoFor} onClose={() => setCargoFor(null)}
        title={`Kargo Kodu — ${cargoFor?.packageNumber ?? ''}`}
        footer={
          <>
            <Button variant="secondary" onClick={() => setCargoFor(null)}>İptal</Button>
            <Button loading={cargoMutation.isPending}
              disabled={!externalCode.trim() && !cargoIntegrationId}
              onClick={() => cargoMutation.mutate()}>Kodu Ata</Button>
          </>
        }>
        <div className="space-y-3">
          <div>
            <label className="flbl">Kargo Anlaşması (kod üretmek için)</label>
            <select className="sel" value={cargoIntegrationId} disabled={!!externalCode}
              onChange={e => setCargoIntegrationId(e.target.value)}>
              <option value="">— seçin</option>
              {cargoIntegrations.filter(c => c.isActive).map(c => (
                <option key={c.id} value={c.id}>
                  {c.name || c.serviceNameI18n?.['tr'] || c.serviceCode}
                </option>
              ))}
            </select>
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              Kod, taşıyıcının kuralına göre üretilir (serbest / kurallı / tahsisli aralık).
            </p>
          </div>
          <div>
            <label className="flbl">veya Dış Kod (pazaryeri/taşıyıcı verdiyse)</label>
            <input className="inp" value={externalCode}
              onChange={e => setExternalCode(e.target.value)}
              placeholder="Dış sistemin verdiği kod aynen yazılır" />
          </div>
          {cargoFor?.cargoIntegrationCode && (
            <div>
              <label className="flbl">Gerekçe (mevcut kod değişecek)</label>
              <input className="inp" value={reason} onChange={e => setReason(e.target.value)}
                placeholder="örn. Taşıyıcı değişti" />
            </div>
          )}
          {error && <p className="text-sm" style={{ color: '#ef4444' }}>{error}</p>}
        </div>
      </Modal>
    </div>
  )
}

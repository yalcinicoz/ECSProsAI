import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { DataTable } from '@/components/ui/DataTable'
import { errText, i18nAd } from '@/components/ui/DataTable.utils'

interface PackingStation {
  id: string
  warehouseId: string
  stationCode: string
  stationName?: string
  slotCount: number
  isObm: boolean
  status: string
}

interface Warehouse { id: string; code: string; nameI18n: Record<string, string> }

function StationModal({ station, onClose }: { station: PackingStation | 'new'; onClose: () => void }) {
  const queryClient = useQueryClient()
  const isNew = station === 'new'
  const s = isNew ? undefined : station

  const [warehouseId, setWarehouseId] = useState(s?.warehouseId ?? '')
  const [stationCode, setStationCode] = useState(s?.stationCode ?? '')
  const [stationName, setStationName] = useState(s?.stationName ?? '')
  const [slotCount, setSlotCount] = useState(s ? String(s.slotCount) : '12')
  const [isObm, setIsObm] = useState(s?.isObm ?? false)
  const [aktif, setAktif] = useState(s ? s.status === 'active' : true)
  const [error, setError] = useState('')

  const { data: warehouses } = useQuery<Warehouse[]>({
    queryKey: ['warehouses-select'],
    queryFn: async () => (await api.get('/inventory/warehouses')).data.data,
  })

  const save = useMutation({
    mutationFn: async () => {
      setError('')
      if (isNew) {
        await api.post('/fulfillment/packing-stations', {
          warehouseId,
          stationCode: stationCode.trim().toUpperCase(),
          barcode: stationCode.trim().toUpperCase(),
          stationName: stationName.trim() || null,
          slotCount: parseInt(slotCount) || 0,
          isObm,
        })
      } else {
        await api.put(`/fulfillment/packing-stations/${s!.id}`, {
          stationName: stationName.trim() || null,
          slotCount: parseInt(slotCount) || 0,
          isObm,
          assignedTo: null,
          status: aktif ? 'active' : 'inactive',
        })
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['packing-stations'] })
      onClose()
    },
    onError: (e: unknown) => setError(errText(e)),
  })

  const valid = (isNew ? warehouseId && stationCode.trim().length >= 2 : true) && parseInt(slotCount) > 0

  return (
    <Modal open onClose={onClose} title={isNew ? 'Yeni Paketleme İstasyonu' : `İstasyon: ${s?.stationCode}`}>
      <div className="space-y-3">
        {isNew && (
          <div>
            <label className="flbl">Depo <span className="text-red-500">*</span></label>
            <select className="inp" value={warehouseId} onChange={e => setWarehouseId(e.target.value)}>
              <option value="">Seçin…</option>
              {(warehouses ?? []).map(w => <option key={w.id} value={w.id}>{i18nAd(w.nameI18n)}</option>)}
            </select>
          </div>
        )}
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">İstasyon Kodu <span className="text-red-500">*</span></label>
            <input className="inp font-mono" value={stationCode} disabled={!isNew}
              onChange={e => setStationCode(e.target.value.toUpperCase())} placeholder="PACK-01" />
          </div>
          <div>
            <label className="flbl">Ad</label>
            <input className="inp" value={stationName} onChange={e => setStationName(e.target.value)} />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Göz (Slot) Sayısı</label>
            <input type="number" min="1" className="inp" value={slotCount}
              onChange={e => setSlotCount(e.target.value)} />
          </div>
          <div className="flex items-end pb-2 gap-4">
            <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
              <input type="checkbox" checked={isObm} onChange={e => setIsObm(e.target.checked)} />
              OBM
            </label>
            {!isNew && (
              <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
                <input type="checkbox" checked={aktif} onChange={e => setAktif(e.target.checked)} />
                Aktif
              </label>
            )}
          </div>
        </div>
        {error && <p className="text-sm text-red-500">{error}</p>}
      </div>
      <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
        <Button onClick={() => save.mutate()} loading={save.isPending} disabled={!valid}>Kaydet</Button>
      </div>
    </Modal>
  )
}

export function PackingStationsPage() {
  const [editing, setEditing] = useState<PackingStation | 'new' | null>(null)

  const { data, isLoading } = useQuery<PackingStation[]>({
    queryKey: ['packing-stations'],
    queryFn: async () => (await api.get('/fulfillment/packing-stations?activeOnly=false')).data.data,
  })

  const stations = data ?? []

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Paketleme İstasyonları</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{stations.length} kayıt</p>
        </div>
        <Button size="sm" onClick={() => setEditing('new')}>+ Yeni İstasyon</Button>
      </div>

      <DataTable<PackingStation>
        columns={[
          { header: 'KOD', cell: s => <code className="text-xs font-mono font-medium">{s.stationCode}</code> },
          { header: 'AD', cell: s => s.stationName ?? '—' },
          { header: 'GÖZ SAYISI', cell: s => s.slotCount },
          { header: 'OBM', cell: s => (s.isObm ? 'Evet' : 'Hayır') },
          { header: 'DURUM', cell: s => <Badge variant={s.status === 'active' ? 'success' : 'neutral'}>{s.status === 'active' ? 'Aktif' : 'Pasif'}</Badge> },
          { header: '', className: 'text-right', cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Düzenle →</span> },
        ]}
        rows={stations}
        loading={isLoading}
        empty='Paketleme istasyonu yok. "+ Yeni İstasyon" ile ekleyin.'
        onRowClick={s => setEditing(s)}
      />

      {editing !== null && <StationModal station={editing} onClose={() => setEditing(null)} />}
    </div>
  )
}

import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { DataTable } from '@/components/ui/DataTable'

interface PosRegister {
  id: string
  code: string
  name: string
  receiptPrefix: string
  warehouseId: string
  isActive: boolean
}

export function RegistersPage() {
  const { data, isLoading } = useQuery<PosRegister[]>({
    queryKey: ['pos-registers'],
    queryFn: async () => (await api.get('/pos/registers?activeOnly=false')).data.data,
  })

  const registers = data ?? []

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Kasalar</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{registers.length} kayıt</p>
      </div>

      <DataTable<PosRegister>
        columns={[
          { header: 'KOD', cell: r => <code className="text-xs font-mono font-medium">{r.code}</code> },
          { header: 'AD', cell: r => r.name },
          { header: 'FİŞ ÖNEKİ', cell: r => <code className="text-xs font-mono">{r.receiptPrefix}</code> },
          { header: 'DURUM', cell: r => <Badge variant={r.isActive ? 'success' : 'neutral'}>{r.isActive ? 'Aktif' : 'Pasif'}</Badge> },
        ]}
        rows={registers}
        loading={isLoading}
        empty="Tanımlı kasa yok."
      />
    </div>
  )
}

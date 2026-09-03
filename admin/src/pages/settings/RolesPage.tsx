import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { DataTable } from '@/components/ui/DataTable'
import { i18nAd } from '@/components/ui/DataTable.utils'

interface Role {
  id: string
  code: string
  nameI18n: Record<string, string>
  isSystem: boolean
  isActive: boolean
}

export function RolesPage() {
  const { data, isLoading } = useQuery<Role[]>({
    queryKey: ['iam-roles'],
    queryFn: async () => (await api.get('/iam/roles')).data.data,
  })

  const roles = data ?? []

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Roller</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          {roles.length} kayıt — kullanıcıya rol ataması Kullanıcılar ekranından yapılır
        </p>
      </div>

      <DataTable<Role>
        columns={[
          { header: 'KOD', cell: r => <code className="text-xs font-mono font-medium">{r.code}</code> },
          { header: 'AD', cell: r => i18nAd(r.nameI18n) },
          { header: 'TİP', cell: r => (r.isSystem ? <Badge variant="info">Sistem</Badge> : <Badge variant="neutral">Özel</Badge>) },
          { header: 'DURUM', cell: r => <Badge variant={r.isActive ? 'success' : 'neutral'}>{r.isActive ? 'Aktif' : 'Pasif'}</Badge> },
        ]}
        rows={roles}
        loading={isLoading}
        empty="Tanımlı rol yok."
      />
    </div>
  )
}

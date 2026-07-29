import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { DataTable } from '@/components/ui/DataTable'

interface Language {
  id: string
  code: string
  nativeName: string
  direction: string
  isDefault: boolean
  isActive: boolean
  sortOrder: number
}

export function LanguagesPage() {
  const { data, isLoading } = useQuery<Language[]>({
    queryKey: ['languages'],
    queryFn: async () => (await api.get('/core/languages?activeOnly=false')).data.data,
  })

  const languages = data ?? []

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Diller</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          {languages.length} kayıt — çok dilli içerik alanları bu dillere göre doldurulur
        </p>
      </div>

      <DataTable<Language>
        columns={[
          { header: 'KOD', cell: l => <code className="text-xs font-mono font-medium">{l.code}</code> },
          { header: 'AD', cell: l => l.nativeName },
          { header: 'YÖN', cell: l => (l.direction === 'rtl' ? 'Sağdan sola' : 'Soldan sağa') },
          { header: 'VARSAYILAN', cell: l => (l.isDefault ? <Badge variant="info">Varsayılan</Badge> : '—') },
          { header: 'DURUM', cell: l => <Badge variant={l.isActive ? 'success' : 'neutral'}>{l.isActive ? 'Aktif' : 'Pasif'}</Badge> },
        ]}
        rows={languages}
        loading={isLoading}
        empty="Tanımlı dil yok."
      />
    </div>
  )
}

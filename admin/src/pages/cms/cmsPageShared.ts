import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'

export const PAGE_TYPE_MAP: Record<string, string> = {
  legal: 'Yasal',
  corporate: 'Kurumsal',
  landing: 'Landing',
}

export interface FirmPlatform {
  id: string
  firmId: string
  nameI18n: Record<string, string>
}

export function useFirmPlatforms() {
  const { data: firms = [] } = useQuery<{ id: string; nameI18n: Record<string, string> }[]>({
    queryKey: ['firms'],
    queryFn: async () => (await api.get('/core/firms')).data.data,
  })
  return useQuery<FirmPlatform[]>({
    queryKey: ['all-firm-platforms', firms.map(f => f.id).join(',')],
    queryFn: async () => {
      const all: FirmPlatform[] = []
      for (const f of firms) {
        const { data } = await api.get(`/core/firms/${f.id}/platforms`)
        for (const p of data.data ?? []) all.push({ ...p, firmId: f.id })
      }
      return all
    },
    enabled: firms.length > 0,
  })
}

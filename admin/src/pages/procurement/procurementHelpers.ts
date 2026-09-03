import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'

export interface SupplierOpt { id: string; title: string; code: string }
export interface WarehouseOpt { id: string; nameI18n: Record<string, string>; code: string }

export const PO_STATUS: Record<string, { label: string; variant: 'success' | 'info' | 'warning' | 'danger' | 'neutral' }> = {
  draft: { label: 'Taslak', variant: 'neutral' },
  ordered: { label: 'Sipariş Verildi', variant: 'info' },
  receiving: { label: 'Teslim Alınıyor', variant: 'warning' },
  closed: { label: 'Kapandı', variant: 'success' },
  cancelled: { label: 'İptal', variant: 'danger' },
}

export const RB_STATUS: Record<string, { label: string; variant: 'success' | 'info' | 'warning' | 'neutral' }> = {
  received: { label: 'Teslim Alındı', variant: 'info' },
  sorting: { label: 'Ayrıştırılıyor', variant: 'warning' },
  completed: { label: 'Tamamlandı', variant: 'success' },
}

export function apiErrorMessage(error: unknown, fallback: string): string {
  if (typeof error !== 'object' || error === null || !('response' in error)) return fallback
  const response = error.response
  if (typeof response !== 'object' || response === null || !('data' in response)) return fallback
  const data = response.data
  if (typeof data !== 'object' || data === null || !('error' in data)) return fallback
  return typeof data.error === 'string' ? data.error : fallback
}

export function useSuppliers() {
  return useQuery<SupplierOpt[]>({
    queryKey: ['suppliers-simple'],
    queryFn: async () => {
      const { data } = await api.get<{ data: SupplierOpt[] | { items?: SupplierOpt[] } }>(
        '/accounts?accountType=supplier&isActive=true&pageSize=500',
      )
      return Array.isArray(data.data) ? data.data : (data.data.items ?? [])
    },
    staleTime: 60_000,
  })
}

export function useWarehouses() {
  return useQuery<WarehouseOpt[]>({
    queryKey: ['warehouses-simple'],
    queryFn: async () => {
      const { data } = await api.get<{ data: (WarehouseOpt & { isActive?: boolean })[] }>('/inventory/warehouses')
      return (data.data ?? []).filter(warehouse => warehouse.isActive !== false)
    },
    staleTime: 60_000,
  })
}

export const whName = (warehouse?: WarehouseOpt) =>
  warehouse ? (warehouse.nameI18n?.['tr'] ?? warehouse.code) : '—'

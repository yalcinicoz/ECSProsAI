import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { PageSpinner } from '@/components/ui/Spinner'
import { ShoppingCart, Package, Users, CreditCard } from 'lucide-react'

interface Stats {
  totalOrders: number
  pendingOrders: number
  totalProducts: number
  totalMembers: number
  posSalesToday: number
}

export function DashboardPage() {
  const { data, isLoading } = useQuery<Stats>({
    queryKey: ['dashboard-stats'],
    queryFn: async () => {
      const [orders, pos] = await Promise.allSettled([
        api.get('/orders?pageSize=1'),
        api.get('/pos/sales?pageSize=1'),
      ])
      return {
        totalOrders: orders.status === 'fulfilled' ? orders.value.data.data?.totalCount ?? 0 : 0,
        pendingOrders: 0,
        totalProducts: 0,
        totalMembers: 0,
        posSalesToday: pos.status === 'fulfilled' ? pos.value.data.data?.totalCount ?? 0 : 0,
      }
    },
  })

  if (isLoading) return <PageSpinner />

  const cards = [
    { label: 'Toplam Sipariş',   value: data?.totalOrders ?? 0,   icon: <ShoppingCart size={20} />, color: '#3b82f6' },
    { label: 'Bekleyen Sipariş', value: data?.pendingOrders ?? 0,  icon: <ShoppingCart size={20} />, color: '#f59e0b' },
    { label: 'Toplam Ürün',      value: data?.totalProducts ?? 0,  icon: <Package size={20} />,      color: 'var(--brand)' },
    { label: 'Toplam Üye',       value: data?.totalMembers ?? 0,   icon: <Users size={20} />,        color: '#8b5cf6' },
    { label: 'Bugün POS Satış',  value: data?.posSalesToday ?? 0,  icon: <CreditCard size={20} />,   color: '#ec4899' },
  ]

  return (
    <div className="p-6">
      <h1 className="text-xl font-bold mb-6" style={{ color: 'var(--text)' }}>Dashboard</h1>

      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-5 gap-4">
        {cards.map((c) => (
          <div key={c.label} className="card p-4">
            <div
              className="w-10 h-10 rounded-xl flex items-center justify-center mb-3"
              style={{ background: `${c.color}15`, color: c.color }}
            >
              {c.icon}
            </div>
            <div className="text-2xl font-bold" style={{ color: 'var(--text)' }}>
              {c.value.toLocaleString('tr-TR')}
            </div>
            <div className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>{c.label}</div>
          </div>
        ))}
      </div>
    </div>
  )
}

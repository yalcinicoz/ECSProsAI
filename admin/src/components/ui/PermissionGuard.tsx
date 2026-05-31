import { useAuthStore } from '@/store/auth'

interface Props {
  permission: string
  children?: React.ReactNode
  /** İzin yoksa render edilecek fallback. Verilmezse hiçbir şey render edilmez. */
  fallback?: React.ReactNode
}

/**
 * Kullanıcının belirtilen permission'a sahip olup olmadığını kontrol eder.
 * Yoksa `fallback` render edilir (varsayılan: null).
 */
export function PermissionGuard({ permission, children, fallback = null }: Props) {
  const hasPermission = useAuthStore(s => s.hasPermission)
  return hasPermission(permission) ? <>{children}</> : <>{fallback}</>
}

/** Sayfa/bölüm başlığına eklenen "Salt Okunur" rozeti */
export function ReadOnlyBadge() {
  return (
    <span
      className="inline-flex items-center px-2 py-0.5 rounded text-[10px] font-semibold uppercase tracking-wide"
      style={{ background: 'var(--surface2)', color: 'var(--text-s)', border: '1px solid var(--border)' }}
    >
      Salt Okunur
    </span>
  )
}

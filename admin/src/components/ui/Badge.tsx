import { cn } from '@/lib/utils'

export type BadgeVariant = 'default' | 'success' | 'warning' | 'danger' | 'info' | 'neutral'

const styles: Record<BadgeVariant, string> = {
  default:  'bg-[var(--brand-bg)] text-[var(--brand)]',
  success:  'bg-green-50 text-green-700',
  warning:  'bg-amber-50 text-amber-700',
  danger:   'bg-red-50 text-red-600',
  info:     'bg-blue-50 text-blue-700',
  neutral:  'bg-[var(--surface2)] text-[var(--text-m)]',
}

interface BadgeProps {
  variant?: BadgeVariant
  children: React.ReactNode
  className?: string
}

export function Badge({ variant = 'default', children, className }: BadgeProps) {
  return (
    <span className={cn('badge', styles[variant], className)}>
      {children}
    </span>
  )
}

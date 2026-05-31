import { forwardRef } from 'react'
import { cn } from '@/lib/utils'

export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost'
  size?: 'sm' | 'md' | 'lg'
  loading?: boolean
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  ({ variant = 'primary', size = 'md', loading, disabled, children, className, ...props }, ref) => {
    const variants: Record<string, string> = {
      primary: 'bg-[var(--brand)] text-white hover:opacity-90 focus:ring-[var(--brand-b)]',
      secondary: 'border border-[var(--border)] text-[var(--text-m)] hover:bg-[var(--surface2)] focus:ring-[var(--border)]',
      danger: 'bg-red-500 text-white hover:bg-red-600 focus:ring-red-200',
      ghost: 'text-[var(--text-m)] hover:bg-[var(--surface2)] focus:ring-[var(--border)]',
    }
    const sizes: Record<string, string> = {
      sm: 'text-xs px-3 py-1.5 rounded-lg',
      md: 'text-sm px-4 py-2 rounded-xl',
      lg: 'text-sm px-5 py-2.5 rounded-xl',
    }
    return (
      <button
        ref={ref}
        disabled={disabled || loading}
        className={cn(
          'inline-flex items-center justify-center gap-2 font-semibold transition-all',
          'focus:outline-none focus:ring-2 focus:ring-offset-1',
          'disabled:opacity-50 disabled:cursor-not-allowed',
          variants[variant],
          sizes[size],
          className,
        )}
        {...props}
      >
        {loading && (
          <span className="w-3.5 h-3.5 border-2 border-current border-t-transparent rounded-full animate-spin" />
        )}
        {children}
      </button>
    )
  },
)
Button.displayName = 'Button'

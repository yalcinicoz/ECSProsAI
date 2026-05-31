import { cn } from '@/lib/utils'

export function Spinner({ className }: { className?: string }) {
  return (
    <span
      className={cn(
        'inline-block w-5 h-5 border-2 border-[var(--border)] border-t-[var(--brand)] rounded-full animate-spin',
        className,
      )}
    />
  )
}

export function PageSpinner() {
  return (
    <div className="flex items-center justify-center py-20">
      <Spinner className="w-8 h-8" />
    </div>
  )
}

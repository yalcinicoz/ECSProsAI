import { forwardRef } from 'react'
import { cn } from '@/lib/utils'

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  hasValue?: boolean
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ hasValue, className, ...props }, ref) => (
    <input ref={ref} className={cn('inp', hasValue && 'ok', className)} {...props} />
  ),
)
Input.displayName = 'Input'

import { forwardRef } from 'react'
import { cn } from '@/lib/utils'

export interface TextareaProps extends React.TextareaHTMLAttributes<HTMLTextAreaElement> {
  hasValue?: boolean
}

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(
  ({ hasValue, className, ...props }, ref) => (
    <textarea ref={ref} className={cn('ta', hasValue && 'ok', className)} {...props} />
  ),
)
Textarea.displayName = 'Textarea'

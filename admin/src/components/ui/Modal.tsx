import { useEffect, useRef } from 'react'
import { createPortal } from 'react-dom'
import { X } from 'lucide-react'
import { cn } from '@/lib/utils'

interface ModalProps {
  open: boolean
  onClose: () => void
  title?: string
  children: React.ReactNode
  footer?: React.ReactNode
  size?: 'sm' | 'md' | 'lg' | 'xl'
}

const sizes = { sm: 'max-w-sm', md: 'max-w-lg', lg: 'max-w-2xl', xl: 'max-w-4xl' }

export function Modal({ open, onClose, title, children, footer, size = 'md' }: ModalProps) {
  const panelRef = useRef<HTMLDivElement>(null)

  // Close on Escape
  useEffect(() => {
    if (!open) return
    function handler(e: KeyboardEvent) { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [open, onClose])

  // Trap scroll
  useEffect(() => {
    if (open) document.body.style.overflow = 'hidden'
    else document.body.style.overflow = ''
    return () => { document.body.style.overflow = '' }
  }, [open])

  if (!open) return null

  return createPortal(
    <div
      className="fixed inset-0 z-50 flex items-end md:items-center justify-center p-0 md:p-4"
      style={{ background: 'rgba(15,23,42,.55)', backdropFilter: 'blur(3px)' }}
      onMouseDown={(e) => { if (e.target === e.currentTarget) onClose() }}
    >
      <div
        ref={panelRef}
        role="dialog"
        aria-modal
        className={cn(
          'w-full rounded-t-2xl md:rounded-2xl shadow-2xl flex flex-col',
          'max-h-[92dvh]',
          sizes[size],
        )}
        style={{ background: 'var(--surface)' }}
      >
        {/* Header */}
        {title && (
          <div
            className="flex items-center justify-between px-5 py-4 border-b flex-shrink-0"
            style={{ borderColor: 'var(--border)' }}
          >
            <h2 className="text-base font-bold" style={{ color: 'var(--text)' }}>{title}</h2>
            <button
              type="button"
              onClick={onClose}
              className="w-8 h-8 flex items-center justify-center rounded-xl hover:bg-[var(--surface2)] transition-colors"
              style={{ color: 'var(--text-m)' }}
            >
              <X size={16} />
            </button>
          </div>
        )}

        {/* Body */}
        <div className="overflow-y-auto thin-scroll flex-1 p-5">
          {children}
        </div>

        {/* Footer */}
        {footer && (
          <div
            className="flex justify-end gap-3 px-5 py-4 border-t flex-shrink-0"
            style={{ borderColor: 'var(--border)' }}
          >
            {footer}
          </div>
        )}
      </div>
    </div>,
    document.body,
  )
}

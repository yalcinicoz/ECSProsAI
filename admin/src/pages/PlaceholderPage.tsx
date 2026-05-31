import { Construction } from 'lucide-react'

export function PlaceholderPage({ title }: { title: string }) {
  return (
    <div className="flex flex-col items-center justify-center py-24 gap-4">
      <div
        className="w-16 h-16 rounded-2xl flex items-center justify-center"
        style={{ background: 'var(--brand-bg)', color: 'var(--brand)' }}
      >
        <Construction size={28} />
      </div>
      <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>{title}</h1>
      <p className="text-sm" style={{ color: 'var(--text-s)' }}>Bu sayfa henüz geliştirilme aşamasında.</p>
    </div>
  )
}

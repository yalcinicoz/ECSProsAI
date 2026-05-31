import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuthStore } from '@/store/auth'
import { Button } from '@/components/ui/Button'

export function LoginPage() {
  const navigate = useNavigate()
  const login = useAuthStore((s) => s.login)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      await login(email, password)
      navigate('/', { replace: true })
    } catch {
      setError('E-posta veya şifre hatalı.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div
      className="min-h-dvh flex items-center justify-center p-4"
      style={{ background: 'var(--sidebar)' }}
    >
      <div className="w-full max-w-sm">
        {/* Logo */}
        <div className="text-center mb-8">
          <span className="text-2xl font-bold text-white tracking-tight">ECSPros</span>
          <p className="text-sm mt-1" style={{ color: 'var(--sidebar-t)' }}>Yönetim Paneli</p>
        </div>

        {/* Card */}
        <div className="card p-6 space-y-5">
          <h1 className="text-lg font-bold" style={{ color: 'var(--text)' }}>Giriş Yap</h1>

          {error && (
            <div className="text-sm px-3 py-2 rounded-lg bg-red-50 text-red-600 border border-red-100">
              {error}
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="flbl">Kullanıcı Adı</label>
              <input
                className="inp"
                type="text"
                autoComplete="username"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
              />
            </div>
            <div>
              <label className="flbl">Şifre</label>
              <input
                className="inp"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
              />
            </div>
            <Button type="submit" className="w-full mt-2" loading={loading}>
              Giriş Yap
            </Button>
          </form>
        </div>
      </div>
    </div>
  )
}

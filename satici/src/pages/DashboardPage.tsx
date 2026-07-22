import { useEffect } from 'react'
import { useAuthStore } from '@/store/auth'

/**
 * S2 iskeleti — panel özeti yer tutucusu.
 * S3'te gerçek özet kartları gelir (bekleyen onay, sipariş, düşük stok, cari bakiye).
 */
export function DashboardPage() {
  const user = useAuthStore((s) => s.user)
  const account = useAuthStore((s) => s.account)
  const fetchMe = useAuthStore((s) => s.fetchMe)

  // Sayfa açılışında introspection tazele (token geçersizse 401 → login'e döner)
  useEffect(() => {
    fetchMe().catch(() => { /* 401 interceptor'ı yönlendirir */ })
  }, [fetchMe])

  return (
    <>
      <div className="vh">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Panel Özeti</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          Hoş geldiniz{user ? `, ${user.fullName}` : ''}
        </p>
      </div>
      <div className="vc">
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 max-w-4xl">
          {/* Firma kartı — /api/supplier/me doğrulaması */}
          <div className="stat sm:col-span-2 lg:col-span-3">
            <div className="text-xs font-semibold uppercase tracking-wide mb-2" style={{ color: 'var(--text-s)' }}>
              Firma Bilgileri
            </div>
            {account ? (
              <div className="space-y-1.5 text-sm" style={{ color: 'var(--text-m)' }}>
                <div><span className="font-semibold" style={{ color: 'var(--text)' }}>{account.title}</span></div>
                <div>Cari Kodu: <span className="font-medium">{account.code}</span></div>
                <div>
                  Satıcı Tipi:{' '}
                  <span className={`badge ${account.supplierKind === 'marketplace' ? 'bg' : 'bx'}`}>
                    {account.supplierKind === 'marketplace' ? 'Pazaryeri Satıcısı' : account.supplierKind}
                  </span>
                </div>
                <div>Para Birimi: {account.currency}</div>
              </div>
            ) : (
              <div className="text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</div>
            )}
          </div>

          {/* S3 yer tutucuları */}
          {['Ürünlerim', 'Siparişlerim', 'Cari Hesabım'].map((t) => (
            <div key={t} className="stat opacity-60">
              <div className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{t}</div>
              <div className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>Yakında</div>
            </div>
          ))}
        </div>
      </div>
    </>
  )
}

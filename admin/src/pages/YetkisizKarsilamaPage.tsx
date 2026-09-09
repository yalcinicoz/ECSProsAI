import { useAuthStore } from '@/store/auth'

/**
 * Y8 / tasarım §O.12 — hiç yetkisi olmayan kullanıcının karşılama ekranı.
 *
 * Default-deny'ın kaçınılmaz yan etkisi: yeni ya da henüz gruba alınmamış kullanıcı panele
 * girdiğinde bomboş bir menüyle karşılaşır ve bunu "panel bozuldu" diye okur. Bu ekran
 * durumu açıkça söyler ve ne yapılacağını gösterir; destek yükünü buradan düşürüyoruz.
 */
export function YetkisizKarsilamaPage() {
  const user = useAuthStore((s) => s.user)

  return (
    <div className="flex-1 flex items-center justify-center p-8">
      <div className="card p-8 max-w-lg text-center">
        <div className="text-4xl mb-3">🔒</div>
        <h1 className="text-lg font-bold mb-2" style={{ color: 'var(--text)' }}>
          Panelde görebileceğiniz bir ekran yok
        </h1>
        <p className="text-sm mb-4" style={{ color: 'var(--text-s)' }}>
          {user?.fullName ? `${user.fullName}, hesabınız` : 'Hesabınız'} açık ve girişiniz başarılı —
          ancak henüz bir <b>yetki grubuna</b> eklenmemişsiniz. Panel varsayılan olarak her şeyi kapalı
          tutar; yetkiler yönetici tarafından verilir.
        </p>
        <p className="text-sm" style={{ color: 'var(--text-s)' }}>
          Yöneticinizden sizi <b>departman grubunuza</b> eklemesini isteyin. Yetki verildiği anda
          geçerli olur; sayfayı yenilemeniz yeterlidir.
        </p>
      </div>
    </div>
  )
}

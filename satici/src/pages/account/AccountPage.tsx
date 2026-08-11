import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'

/* Hesabım (2026-08-11): firma/kullanıcı bilgisi + sözleşme koşulları (salt okunur) +
 * kargo modu seçimi (K3 — satıcının kararı; mod 3 kargo entegrasyonlarıyla açılacak).
 * Kargo modu değişimi API hesaplarının kargo bildirme yetkisini de eşitler. */

interface Me {
  user: { email: string; fullName: string }
  account: { code: string; title: string; contactName: string | null; email: string | null; phone: string | null }
}
interface Settings { cargoMode: string; settlementDelayDays: number; payoutPeriod: string; hasContract: boolean }

const PERIYOT: Record<string, string> = { weekly: 'Haftalık', monthly: 'Aylık', immediate: 'Uygunlaşınca' }

export function AccountPage() {
  const queryClient = useQueryClient()
  const { data: me } = useQuery<Me>({
    queryKey: ['supplier-me'],
    queryFn: async () => (await api.get('/supplier/me')).data.data,
  })
  const { data: settings } = useQuery<Settings>({
    queryKey: ['supplier-settings'],
    queryFn: async () => (await api.get('/supplier/account/settings')).data.data,
  })

  const [cargoMode, setCargoMode] = useState('platform_contract')
  useEffect(() => { if (settings) setCargoMode(settings.cargoMode) }, [settings])

  const kaydet = useMutation({
    mutationFn: async () => { await api.put('/supplier/account/settings/cargo-mode', { cargoMode }) },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['supplier-settings'] }),
  })

  return (
    <div className="max-w-2xl">
      <h1 className="text-lg font-bold mb-4">Hesabım</h1>

      <div className="card p-5 mb-4">
        <div className="text-xs font-semibold uppercase tracking-wider mb-2 opacity-70">Firma Bilgileri</div>
        <div className="text-sm grid gap-1">
          <div><span className="opacity-60">Cari kod:</span> {me?.account.code}</div>
          <div><span className="opacity-60">Ünvan:</span> {me?.account.title}</div>
          <div><span className="opacity-60">Giriş kullanıcısı:</span> {me?.user.fullName} ({me?.user.email})</div>
        </div>
        <p className="text-xs opacity-60 mt-2">Firma bilgisi değişiklikleri için platform yönetimiyle iletişime geçin.</p>
      </div>

      <div className="card p-5 mb-4">
        <div className="text-xs font-semibold uppercase tracking-wider mb-2 opacity-70">Hakediş Koşulları</div>
        <div className="text-sm grid gap-1">
          <div><span className="opacity-60">Bekleme süresi:</span> teslimden {settings?.settlementDelayDays ?? 14} gün sonra bakiyeye geçer</div>
          <div><span className="opacity-60">Ödeme periyodu:</span> {PERIYOT[settings?.payoutPeriod ?? 'weekly']}</div>
        </div>
        <p className="text-xs opacity-60 mt-2">Komisyon oranlarınız satışlarınızın hakediş satırlarında görünür (Mali Durum sayfası).</p>
      </div>

      <div className="card p-5">
        <div className="text-xs font-semibold uppercase tracking-wider mb-2 opacity-70">Kargo Ayarı</div>
        <div className="grid gap-2">
          <label className="flex items-start gap-2 text-sm cursor-pointer">
            <input type="radio" name="cargo" className="mt-1" checked={cargoMode === 'platform_contract'}
              onChange={() => setCargoMode('platform_contract')} />
            <span><strong>Platform gönderir</strong> — paketleriniz platformun kargo anlaşmasıyla gönderilir; takip bilgisi girmezsiniz.</span>
          </label>
          <label className="flex items-start gap-2 text-sm cursor-pointer">
            <input type="radio" name="cargo" className="mt-1" checked={cargoMode === 'seller_ships'}
              onChange={() => setCargoMode('seller_ships')} />
            <span><strong>Kendim gönderirim</strong> — kendi kargo anlaşmanızla gönderir, sipariş detayından taşıyıcı + takip numarası bildirirsiniz.</span>
          </label>
          <label className="flex items-start gap-2 text-sm opacity-50">
            <input type="radio" name="cargo" className="mt-1" disabled />
            <span><strong>Sözleşmemle platform gönderir</strong> — kargo entegrasyonları tamamlanınca açılacak.</span>
          </label>
        </div>
        <div className="flex items-center gap-3 mt-4">
          <Button onClick={() => kaydet.mutate()} disabled={kaydet.isPending || cargoMode === settings?.cargoMode}>
            {kaydet.isPending ? 'Kaydediliyor…' : 'Kaydet'}
          </Button>
          {kaydet.isSuccess && <span className="text-xs text-green-600">Kaydedildi ✓</span>}
          {kaydet.isError && (
            <span className="text-xs text-red-600">
              {(kaydet.error as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Kaydedilemedi'}
            </span>
          )}
        </div>
      </div>
    </div>
  )
}

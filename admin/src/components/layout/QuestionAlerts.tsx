// Satıcıya Soru Sor — panel anlık bildirim katmanı (2026-09-02, kullanıcı isteği):
// siteden yeni soru gelince personel ANINDA haberdar olur (SignalR toast), soru
// cevaplanana kadar göz önünde kalır (sidebar rozeti + sekme başlığı sayacı — 60 sn
// poll güvence, hub olayı anında tazeler). Panelin İLK SignalR bağlantısı budur;
// hub kopsa bile poll sayesinde haber en geç 1 dakika gecikir, hiç kaybolmaz.
import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import * as signalR from '@microsoft/signalr'
import api from '@/api/client'
import { useQuestionAlertStore } from '@/store/questionAlerts'

interface QuestionToast {
  key: number
  productCode: string
  question: string
  memberName: string
}

const BASLIK = 'ECSPros Admin'

export function QuestionAlerts() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const setPendingCount = useQuestionAlertStore((s) => s.setPendingCount)
  const [toasts, setToasts] = useState<QuestionToast[]>([])
  const toastKey = useRef(0)

  // ── Güvence katmanı: 60 sn'de bir cevap bekleyen sayısı (hub'sız da çalışır) ──
  const { data: pendingCount } = useQuery({
    queryKey: ['product-questions-pending-count'],
    queryFn: async () => {
      const res = await api.get('/product-questions', { params: { status: 'pending', pageSize: 1 } })
      return (res.data.data?.totalCount ?? 0) as number
    },
    refetchInterval: 60_000,
    refetchIntervalInBackground: true,
  })

  // Sayaç → store (Sidebar rozeti + Dashboard kartı) ve sekme başlığı
  useEffect(() => {
    const n = pendingCount ?? 0
    setPendingCount(n)
    document.title = n > 0 ? `(${n}) ${BASLIK}` : BASLIK
  }, [pendingCount, setPendingCount])

  // ── Anlık katman: SignalR /hubs/notifications → topic:questions ──
  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/notifications', {
        accessTokenFactory: () => localStorage.getItem('access_token') ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    const tazele = () => {
      queryClient.invalidateQueries({ queryKey: ['product-questions-pending-count'] })
      queryClient.invalidateQueries({ queryKey: ['product-questions'] })
    }

    connection.on('QuestionCreated', (data: { productCode?: string; question?: string; memberName?: string }) => {
      tazele()
      const toast: QuestionToast = {
        key: ++toastKey.current,
        productCode: data?.productCode ?? '',
        question: data?.question ?? '',
        memberName: data?.memberName ?? '',
      }
      setToasts((t) => [...t.slice(-2), toast]) // en çok 3 toast üst üste
      window.setTimeout(() => setToasts((t) => t.filter((x) => x.key !== toast.key)), 12_000)
    })
    // Başka bir kullanıcı cevapladığında herkesin rozeti/listesi anında düşsün
    connection.on('QuestionAnswered', tazele)

    const abonelik = () => connection.invoke('Subscribe', 'questions').catch(() => {})
    connection.onreconnected(() => { abonelik(); tazele() })
    connection.start().then(abonelik).catch(() => {
      // Bağlantı kurulamadı (eski token, proxy vs.) — poll katmanı devrede, sessiz geç.
    })

    return () => { connection.stop().catch(() => {}) }
  }, [queryClient])

  if (toasts.length === 0) return null

  return (
    <div className="fixed top-4 right-4 z-[100] space-y-2 w-[340px] max-w-[calc(100vw-2rem)]">
      {toasts.map((t) => (
        <button
          key={t.key}
          type="button"
          onClick={() => {
            setToasts((x) => x.filter((y) => y.key !== t.key))
            navigate('/storefront/questions')
          }}
          className="w-full text-left rounded-xl shadow-lg p-3 border cursor-pointer"
          style={{ background: 'var(--surface)', borderColor: 'var(--brand)', color: 'var(--text)' }}
        >
          <div className="flex items-center gap-2 text-xs font-semibold" style={{ color: 'var(--brand)' }}>
            <span>💬 Yeni ürün sorusu</span>
            <span className="ml-auto font-mono" style={{ color: 'var(--text-s)' }}>{t.productCode}</span>
          </div>
          <p className="text-sm mt-1 line-clamp-2">{t.question}</p>
          <p className="text-[11px] mt-1" style={{ color: 'var(--text-s)' }}>
            {t.memberName} · cevaplamak için tıklayın
          </p>
        </button>
      ))}
    </div>
  )
}

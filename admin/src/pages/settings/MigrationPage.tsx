import { useState, useEffect, useRef } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  DatabaseZap, Play, CheckCircle2, XCircle,
  Clock, Loader2, ChevronDown, ChevronUp, AlertTriangle
} from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { cn } from '@/lib/utils'

// ── Types ──────────────────────────────────────────────────────────────────────

interface MigrationStatus {
  status: 'idle' | 'running' | 'completed' | 'failed'
  phase: number
  startedAt: string | null
  finishedAt: string | null
  error: string | null
  output: string
  tableStats: Record<string, number>
}

// ── Constants ──────────────────────────────────────────────────────────────────

const PHASES = [
  { id: 0, label: 'Tüm Fazlar', desc: 'Sıfırdan tam migration (1-7 arası)' },
  { id: 1, label: 'Faz 1 — Image Sets', desc: 'dfresimsetleri → catalog_image_sets (2 kayıt)' },
  { id: 2, label: 'Faz 2 — Attribute Types', desc: 'dfvaryanttipleri → catalog_attribute_types (43 kayıt)' },
  { id: 3, label: 'Faz 3 — Attribute Values', desc: 'dfvaryanttipdegerleri + markalar → catalog_attribute_values (~4K kayıt)' },
  { id: 4, label: 'Faz 4 — Product Groups', desc: 'dfurungruplari → catalog_product_groups (217 kayıt)' },
  { id: 5, label: 'Faz 5 — Products', desc: 'apurunler → catalog_products + catalog_product_attributes (~117K kayıt)' },
  { id: 6, label: 'Faz 6 — Variants', desc: 'apurunvaryantlari → catalog_product_variants + attributes (~1.2M kayıt, ~22dk)' },
  { id: 7, label: 'Faz 7 — Images', desc: 'apurunresimleri → catalog_product_images (~1.4M kayıt, ~11dk)' },
  { id: 8, label: 'Faz 8 — Grup Adı Düzelt', desc: 'Mevcut ürün grubu adlarından cinsiyet prefixini kaldırır (Silme yok — sadece UPDATE, ~1sn)' },
  { id: 9, label: 'Faz 9 — Grup Birleştir', desc: 'Aynı isimli ürün gruplarını tek gruba indirir; ürünleri canonical gruba yönlendirir, duplicate\'leri siler (~1sn)' },
]

const TABLE_LABELS: Record<string, string> = {
  catalog_image_sets: 'Image Sets',
  catalog_attribute_types: 'Attribute Types',
  catalog_attribute_values: 'Attribute Values',
  catalog_product_groups: 'Product Groups',
  catalog_products: 'Products',
  catalog_product_attributes: 'Product Attributes',
  catalog_product_variants: 'Product Variants',
  catalog_product_variant_attributes: 'Variant Attributes',
  catalog_product_images: 'Product Images',
}

// ── API ────────────────────────────────────────────────────────────────────────

const fetchStatus = async (): Promise<MigrationStatus> => {
  const res = await api.get('/migration/status')
  return res.data.data
}

const runMigration = async (phase: number): Promise<void> => {
  await api.post('/migration/run', { phase })
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function fmt(n: number) {
  return n.toLocaleString('tr-TR')
}

function elapsed(start: string | null, end: string | null): string {
  if (!start) return '—'
  const a = new Date(start).getTime()
  const b = end ? new Date(end).getTime() : Date.now()
  const s = Math.round((b - a) / 1000)
  if (s < 60) return `${s}s`
  return `${Math.floor(s / 60)}dk ${s % 60}s`
}

// ── Page ──────────────────────────────────────────────────────────────────────

export function MigrationPage() {
  const qc = useQueryClient()
  const [selectedPhase, setSelectedPhase] = useState(0)
  const [showOutput, setShowOutput] = useState(false)
  const [showConfirm, setShowConfirm] = useState(false)
  const logRef = useRef<HTMLPreElement>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['migration-status'],
    queryFn: fetchStatus,
    refetchInterval: (query) =>
      query.state.data?.status === 'running' ? 2000 : false,
  })

  const runMutation = useMutation({
    mutationFn: runMigration,
    onSuccess: () => {
      setShowConfirm(false)
      setTimeout(() => qc.invalidateQueries({ queryKey: ['migration-status'] }), 500)
    },
  })

  // Log'lar güncellenince en alta scroll
  useEffect(() => {
    if (showOutput && logRef.current) {
      logRef.current.scrollTop = logRef.current.scrollHeight
    }
  }, [data?.output, showOutput])

  const isRunning = data?.status === 'running'
  const stats = data?.tableStats ?? {}
  const totalRows = Object.values(stats).reduce((s, v) => s + v, 0)

  return (
    <div className="p-6 max-w-5xl mx-auto space-y-6">

      {/* Başlık */}
      <div className="flex items-center gap-3">
        <DatabaseZap className="w-6 h-6 text-[var(--brand)]" />
        <div>
          <h1 className="text-lg font-semibold">Eski Veritabanı Migration</h1>
          <p className="text-sm text-[var(--text-s)]">
            MySQL juludedb → PostgreSQL ecommerce_db (catalog şeması)
          </p>
        </div>
      </div>

      {/* Durum kartı */}
      <StatusCard data={data} isLoading={isLoading} />

      {/* Tablo istatistikleri */}
      <div className="border border-[var(--border)] rounded-lg overflow-hidden">
        <div className="px-4 py-3 bg-[var(--surface2)] border-b border-[var(--border)] flex items-center justify-between">
          <span className="text-sm font-medium">Mevcut Tablo Durumu</span>
          <span className="text-xs text-[var(--text-s)]">
            Toplam: <strong>{fmt(totalRows)}</strong> kayıt
          </span>
        </div>
        <div className="divide-y divide-[var(--border)]">
          {Object.entries(TABLE_LABELS).map(([key, label]) => (
            <div key={key} className="flex items-center justify-between px-4 py-2.5">
              <span className="text-sm text-[var(--text)]">{label}</span>
              <span className={cn(
                'text-sm font-mono font-medium',
                (stats[key] ?? 0) > 0 ? 'text-[var(--brand)]' : 'text-[var(--text-s)]'
              )}>
                {fmt(stats[key] ?? 0)}
              </span>
            </div>
          ))}
        </div>
      </div>

      {/* Faz seçimi + çalıştır */}
      <div className="border border-[var(--border)] rounded-lg overflow-hidden">
        <div className="px-4 py-3 bg-[var(--surface2)] border-b border-[var(--border)]">
          <span className="text-sm font-medium">Migration Çalıştır</span>
        </div>
        <div className="p-4 space-y-3">
          <div className="grid gap-2">
            {PHASES.map(p => (
              <label key={p.id} className={cn(
                'flex items-start gap-3 p-3 rounded-lg border cursor-pointer transition-colors',
                selectedPhase === p.id
                  ? 'border-[var(--brand)] bg-[color-mix(in_srgb,var(--brand)_6%,transparent)]'
                  : 'border-[var(--border)] hover:bg-[var(--surface2)]'
              )}>
                <input
                  type="radio"
                  name="phase"
                  value={p.id}
                  checked={selectedPhase === p.id}
                  onChange={() => setSelectedPhase(p.id)}
                  className="mt-0.5 accent-[var(--brand)]"
                  disabled={isRunning}
                />
                <div>
                  <div className="text-sm font-medium">{p.label}</div>
                  <div className="text-xs text-[var(--text-s)] mt-0.5">{p.desc}</div>
                </div>
              </label>
            ))}
          </div>

          {showConfirm ? (
            <div className="flex items-center gap-3 p-3 rounded-lg bg-amber-50 border border-amber-200">
              <AlertTriangle className="w-4 h-4 text-amber-600 shrink-0" />
              <p className="text-sm text-amber-800 flex-1">
                {selectedPhase === 0
                  ? 'Tüm migration fazları çalıştırılacak. Mevcut veriler silinecek. Emin misiniz?'
                  : selectedPhase === 8
                  ? 'Faz 8: Ürün grubu adlarındaki cinsiyet prefixi kaldırılacak. Sadece UPDATE yapılır, veri silinmez. Emin misiniz?'
                  : selectedPhase === 9
                  ? 'Faz 9: Aynı isimli ürün grupları birleştirilecek. Duplicate gruplar silinir, ürünler canonical gruba yönlendirilir. Emin misiniz?'
                  : `Faz ${selectedPhase} çalıştırılacak. İlgili tablolar silinip yeniden yazılacak. Emin misiniz?`}
              </p>
              <div className="flex gap-2 shrink-0">
                <Button
                  size="sm"
                  onClick={() => runMutation.mutate(selectedPhase)}
                  disabled={runMutation.isPending}
                >
                  {runMutation.isPending ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : 'Evet, Başlat'}
                </Button>
                <Button size="sm" variant="ghost" onClick={() => setShowConfirm(false)}>
                  İptal
                </Button>
              </div>
            </div>
          ) : (
            <Button
              onClick={() => setShowConfirm(true)}
              disabled={isRunning}
              className="gap-2"
            >
              {isRunning
                ? <><Loader2 className="w-4 h-4 animate-spin" /> Çalışıyor…</>
                : <><Play className="w-4 h-4" /> Migration Başlat</>}
            </Button>
          )}

          {runMutation.isError && (
            <p className="text-sm text-red-600">
              {(runMutation.error as any)?.response?.data?.error ?? 'Bir hata oluştu'}
            </p>
          )}
        </div>
      </div>

      {/* Log çıktısı */}
      {data?.output && (
        <div className="border border-[var(--border)] rounded-lg overflow-hidden">
          <button
            onClick={() => setShowOutput(v => !v)}
            className="w-full flex items-center justify-between px-4 py-3 bg-[var(--surface2)] border-b border-[var(--border)] text-sm font-medium hover:bg-[var(--surface2)]"
          >
            <span>İşlem Logu</span>
            {showOutput ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
          </button>
          {showOutput && (
            <pre
              ref={logRef}
              className="p-4 text-xs font-mono bg-gray-950 text-green-400 overflow-auto max-h-[400px] leading-relaxed"
            >
              {data.output}
            </pre>
          )}
        </div>
      )}
    </div>
  )
}

// ── Status Card ───────────────────────────────────────────────────────────────

function StatusCard({ data, isLoading }: { data?: MigrationStatus; isLoading: boolean }) {
  if (isLoading) {
    return (
      <div className="flex items-center gap-3 p-4 rounded-lg border border-[var(--border)] bg-[var(--surface2)]">
        <Loader2 className="w-5 h-5 animate-spin text-[var(--text-s)]" />
        <span className="text-sm text-[var(--text-s)]">Durum yükleniyor…</span>
      </div>
    )
  }

  const status = data?.status ?? 'idle'

  const configs = {
    idle: {
      icon: <Clock className="w-5 h-5 text-[var(--text-s)]" />,
      label: 'Bekleniyor',
      sub: 'Henüz migration çalıştırılmadı.',
      cls: 'border-[var(--border)] bg-[var(--surface2)]',
    },
    running: {
      icon: <Loader2 className="w-5 h-5 animate-spin text-blue-500" />,
      label: `Çalışıyor — Faz ${data?.phase === 0 ? 'Tümü' : data?.phase}`,
      sub: `Başlangıç: ${data?.startedAt ? new Date(data.startedAt).toLocaleTimeString('tr-TR') : '—'} • Geçen: ${elapsed(data?.startedAt ?? null, null)}`,
      cls: 'border-blue-200 bg-blue-50',
    },
    completed: {
      icon: <CheckCircle2 className="w-5 h-5 text-emerald-500" />,
      label: 'Tamamlandı',
      sub: `Faz ${data?.phase === 0 ? 'Tümü' : data?.phase} • Süre: ${elapsed(data?.startedAt ?? null, data?.finishedAt ?? null)}`,
      cls: 'border-emerald-200 bg-emerald-50',
    },
    failed: {
      icon: <XCircle className="w-5 h-5 text-red-500" />,
      label: 'Hata',
      sub: data?.error ?? 'Bilinmeyen hata',
      cls: 'border-red-200 bg-red-50',
    },
  }

  const cfg = configs[status]

  return (
    <div className={cn('flex items-center gap-3 p-4 rounded-lg border', cfg.cls)}>
      {cfg.icon}
      <div>
        <div className="text-sm font-medium">{cfg.label}</div>
        <div className="text-xs text-[var(--text-s)] mt-0.5">{cfg.sub}</div>
      </div>
    </div>
  )
}

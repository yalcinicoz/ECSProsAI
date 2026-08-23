/**
 * F1 Kanal Kapsamı sayfası (docs/satis-kanali-ortak-kurgu.md §3.1) — kanalın "hangi ürünler söz konusu"
 * kararı. Günlük operasyon ekranından (Kanal Ürünleri) AYRI tutulur: kapsam kararlarını sorumlu kişiler
 * önceden verir; personel Kanal Ürünleri'nde yalnız kanal kararlarıyla (kanala al / çıkar / durdur) çalışır.
 * Kanal seçimi Kanal Ürünleri ile aynı sessionStorage anahtarını paylaşır.
 */
import { useState, useEffect } from 'react'
import { useQuery, useQueries } from '@tanstack/react-query'
import api from '@/api/client'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { PageSpinner } from '@/components/ui/Spinner'
import { ChannelScopeTab } from './ChannelScopeTab'

interface Firm { id: string; nameI18n: Record<string, string> }
interface Channel { id: string; nameI18n: Record<string, string>; code: string; firmId: string; firmName: string }

const nameOf = (i18n: Record<string, string>) => i18n?.['tr'] ?? i18n?.[Object.keys(i18n ?? {})[0]] ?? '—'
const channelLabel = (ch: Channel) => ch.nameI18n?.['tr'] ?? ch.nameI18n?.[Object.keys(ch.nameI18n ?? {})[0]] ?? ch.code

export function ChannelScopePage() {
  const [channelId, setChannelId] = useState<string>(() => sessionStorage.getItem('channelProducts.channelId') ?? '')
  useEffect(() => { if (channelId) sessionStorage.setItem('channelProducts.channelId', channelId) }, [channelId])

  const { data: firms = [], isLoading: firmsLoading } = useQuery<Firm[]>({
    queryKey: ['firms'],
    queryFn: async () => { const { data } = await api.get('/core/firms'); return data.data ?? [] },
  })
  const platformQueries = useQueries({
    queries: firms.map(firm => ({
      queryKey: ['firm-platforms', firm.id],
      queryFn: async (): Promise<Channel[]> => {
        const { data } = await api.get(`/core/firms/${firm.id}/platforms`)
        const firmName = nameOf(firm.nameI18n)
        return (data.data ?? []).map((ch: Channel) => ({ ...ch, firmId: firm.id, firmName }))
      },
      enabled: firms.length > 0,
    })),
  })
  const channels: Channel[] = platformQueries.flatMap(q => q.data ?? [])
  const chLoading = firmsLoading || platformQueries.some(q => q.isLoading)
  const channelOptions = channels.map(c => ({ value: c.id, label: `${channelLabel(c)} (${c.firmName})` }))

  if (chLoading) return <PageSpinner />

  return (
    <div className="p-6">
      <div className="mb-6">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Kanal Kapsamı</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          Bu kanalda hangi ürünlerin söz konusu olacağını belirleyin: tümü ya da kayıtlı filtre (+ manuel istisnalar).
          Günlük kanala al / çıkar / durdur işlemleri <b>Kanal Ürünleri</b> sayfasındadır.
        </p>
      </div>

      <div className="card mb-4">
        <label className="flbl mb-2">Satış Kanalı</label>
        <SearchableSelect
          value={channelId}
          onChange={(v) => { if (v) setChannelId(v) }}
          options={channelOptions}
          placeholder="Kanal seçin…"
          hasValue={!!channelId}
        />
      </div>

      {channelId && <ChannelScopeTab channelId={channelId} />}
    </div>
  )
}

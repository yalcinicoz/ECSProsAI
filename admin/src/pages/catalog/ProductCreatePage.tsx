import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { ChevronRight } from 'lucide-react'
import api from '@/api/client'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/Button'
import { SearchableSelect } from '@/components/ui/SearchableSelect'

interface ProductGroup {
  id: string
  code: string
  nameI18n: Record<string, string>
}

function getName(g: ProductGroup): string {
  return g.nameI18n['tr'] ?? g.nameI18n[Object.keys(g.nameI18n)[0]] ?? g.code
}

export function ProductCreatePage() {
  const navigate = useNavigate()

  const [groupId, setGroupId] = useState<string | null>(null)
  const [code, setCode]       = useState('')
  const [nameTr, setNameTr]   = useState('')

  const { data: groups = [], isLoading: groupsLoading } = useQuery<ProductGroup[]>({
    queryKey: ['product-groups', false],
    queryFn: async () => {
      const { data } = await api.get('/catalog/product-groups?activeOnly=false')
      return data.data
    },
    staleTime: 5 * 60 * 1000,
  })

  const groupOptions = groups.map((g) => ({ value: g.id, label: getName(g) }))

  const canSubmit = !!groupId && nameTr.trim().length > 0


  const createMutation = useMutation({
    mutationFn: async () => {
      const trimmedCode = code.trim() || undefined
      const { data } = await api.post('/catalog/products', {
        productGroupId: groupId!,
        ...(trimmedCode ? { code: trimmedCode } : {}),
        nameI18n: { tr: nameTr.trim() },
      })
      return data.data?.code as string
    },
    onSuccess: (code) => {
      navigate(`/catalog/products/${code}`)
    },
  })

  return (
    <div className="flex-1 flex flex-col">

      {/* ── Header ── */}
      <div className="vh">
        <div className="flex items-start justify-between gap-3">
          <div>
            <div className="flex items-center gap-1.5 text-xs mb-1" style={{ color: 'var(--text-s)' }}>
              <Link to="/catalog/products" className="hover:underline" style={{ color: 'var(--text-s)' }}>
                Ürünler
              </Link>
              <ChevronRight size={12} />
              <span style={{ color: 'var(--text-m)' }}>Yeni Ürün Kartı</span>
            </div>
            <h1 className="text-lg font-bold" style={{ color: 'var(--text)' }}>Yeni Ürün Kartı</h1>
            <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
              Ürün grubu, kodu ve adı ile hızlıca kart açın
            </p>
          </div>
          <Link
            to="/catalog/products"
            className="px-4 py-2 rounded-xl text-sm font-semibold transition-colors hover:bg-[var(--surface2)]"
            style={{ border: '1px solid var(--border)', color: 'var(--text-m)' }}
          >
            İptal
          </Link>
        </div>
      </div>

      {/* ── Form ── */}
      <div className="vc">
        <div className="card space-y-5" style={{ maxWidth: 560 }}>

          {/* Ürün Grubu */}
          <div>
            <label className="flbl">
              Ürün Grubu <span className="text-amber-500 font-bold">*</span>
            </label>
            <SearchableSelect
              options={groupOptions}
              value={groupId}
              onChange={setGroupId}
              placeholder={groupsLoading ? 'Yükleniyor…' : '— Grup seçin —'}
            />
            <p className="text-[11px] mt-1" style={{ color: 'var(--text-s)' }}>
              Oluşturulduktan sonra değiştirilemez.
            </p>
          </div>

          {/* Ürün Kodu */}
          <div>
            <label className="flbl">
              Ürün Kodu{' '}
              <span className="text-[10px] font-normal" style={{ color: 'var(--text-s)' }}>
                (opsiyonel — boşsa otomatik atanır)
              </span>
            </label>
            <input
              className="inp"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              placeholder="örn: NK-AM270"
            />
          </div>

          {/* Ürün Adı TR */}
          <div>
            <label className="flbl">
              Ürün Adı (TR) <span className="text-amber-500 font-bold">*</span>
            </label>
            <input
              className={cn('inp', nameTr.trim() && 'ok')}
              value={nameTr}
              onChange={(e) => setNameTr(e.target.value)}
              placeholder="örn: Nike Air Max 270"
              autoFocus
            />
          </div>

          {/* Error */}
          {createMutation.isError && (
            <p className="text-sm" style={{ color: 'var(--danger, #ef4444)' }}>
              {(createMutation.error as any)?.response?.data?.error ?? 'Hata oluştu. Tekrar deneyin.'}
            </p>
          )}

          {/* Submit */}
          <div className="pt-1 flex justify-end">
            <Button
              onClick={() => createMutation.mutate()}
              loading={createMutation.isPending}
              disabled={!canSubmit}
            >
              Ürün Kartını Oluştur
            </Button>
          </div>
        </div>
      </div>

    </div>
  )
}

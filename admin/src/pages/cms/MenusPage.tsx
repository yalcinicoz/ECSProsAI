import { useState, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, ChevronRight, LayoutList } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { IntegerInput } from '@/components/ui/IntegerInput'
import { I18nField } from '@/components/ui/I18nField'
import { PageSpinner } from '@/components/ui/Spinner'
import { useLanguages } from '@/hooks/useLanguages'
import { FL } from '@/lib/field-labels'
import { buildI18nValues } from '@/lib/i18n-helper'

// ── Types ─────────────────────────────────────────────────────────────────────

interface MenuDto {
  id: string
  code: string
  nameI18n: Record<string, string>
  menuType: string
  isActive: boolean
  sortOrder: number
}

interface CreateForm {
  code: string
  nameI18n: Record<string, string>
  menuType: string
  sortOrder: number
}

const MENU_TYPE_OPTIONS = [
  { value: 'header',  label: 'Header' },
  { value: 'footer',  label: 'Footer' },
  { value: 'sidebar', label: 'Sidebar' },
  { value: 'custom',  label: 'Özel' },
]

function getName(i18n: Record<string, string>, fallback: string): string {
  return i18n['tr'] ?? i18n[Object.keys(i18n)[0]] ?? fallback
}

// ── Component ─────────────────────────────────────────────────────────────────

export function MenusPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { data: languages = [], isLoading: langsLoading } = useLanguages()

  const [createOpen, setCreateOpen] = useState(false)
  const [form, setForm] = useState<CreateForm>({
    code: '',
    nameI18n: {},
    menuType: 'main',
    sortOrder: 0,
  })

  const { data: menus = [], isLoading } = useQuery<MenuDto[]>({
    queryKey: ['menus'],
    queryFn: async () => {
      const { data } = await api.get('/navigation/menus')
      return data.data
    },
  })

  const sourceLang = languages.find((l) => l.isDefault)?.code ?? languages[0]?.code ?? 'tr'
  const nameFields = useMemo(() => [{ key: 'name', labels: FL.categoryName, required: true }], [])

  const [createError, setCreateError] = useState<string | null>(null)

  const createMutation = useMutation({
    mutationFn: async () => {
      const { data } = await api.post('/navigation/menus', {
        firmPlatformId: '00000000-0000-0000-0000-000000000000', // default; gerekirse platform seçimi eklenebilir
        code: form.code.trim().toLowerCase().replace(/\s+/g, '-'),
        nameI18n: form.nameI18n,
        menuType: form.menuType,
        sortOrder: form.sortOrder,
      })
      return data.data?.id as string
    },
    onSuccess: (newId) => {
      setCreateError(null)
      queryClient.invalidateQueries({ queryKey: ['menus'] })
      setCreateOpen(false)
      navigate(`/navigation/menus/${newId}`)
    },
    onError: (err: unknown) => {
      const msg =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        'Bir hata oluştu, lütfen tekrar deneyin.'
      setCreateError(msg)
    },
  })

  if (isLoading || langsLoading) return <PageSpinner />

  return (
    <div className="p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Menüler</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{menus.length} menü</p>
        </div>
        <Button onClick={() => setCreateOpen(true)}>
          <Plus size={14} /> Yeni Menü
        </Button>
      </div>

      {/* List */}
      <div className="card overflow-hidden p-0">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Menü</th>
              <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Tip</th>
              <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Stil</th>
              <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Öğe</th>
              <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Durum</th>
              <th className="w-10" />
            </tr>
          </thead>
          <tbody>
            {menus.length === 0 && (
              <tr>
                <td colSpan={6} className="py-12 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                  Henüz menü eklenmemiş
                </td>
              </tr>
            )}
            {menus.map((m) => (
              <tr
                key={m.id}
                className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                style={{ borderBottom: '1px solid var(--border)' }}
                onClick={() => navigate(`/navigation/menus/${m.id}`)}
              >
                <td className="px-4 py-3">
                  <div className="flex items-center gap-3">
                    <div className="w-8 h-8 rounded-xl flex-shrink-0 flex items-center justify-center"
                      style={{ background: 'var(--brand-bg)', color: 'var(--brand)' }}>
                      <LayoutList size={13} />
                    </div>
                    <div>
                      <div className="text-sm font-semibold" style={{ color: 'var(--text)' }}>
                        {getName(m.nameI18n, m.code)}
                      </div>
                      <code className="text-xs" style={{ color: 'var(--text-s)' }}>{m.code}</code>
                    </div>
                  </div>
                </td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>
                  {MENU_TYPE_OPTIONS.find((o) => o.value === m.menuType)?.label ?? m.menuType}
                </td>
                <td className="px-4 py-3 text-center">
                  <Badge variant={m.isActive ? 'success' : 'neutral'}>
                    {m.isActive ? 'Aktif' : 'Pasif'}
                  </Badge>
                </td>
                <td className="px-4 py-3 text-center">
                  <ChevronRight size={14} style={{ color: 'var(--text-s)' }} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Create Modal */}
      <Modal
        open={createOpen}
        onClose={() => { setCreateOpen(false); setCreateError(null) }}
        title="Yeni Menü"
        size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button
              onClick={() => createMutation.mutate()}
              loading={createMutation.isPending}
              disabled={!form.code.trim()}
            >
              Oluştur ve Düzenle
            </Button>
          </>
        }
      >
        <div className="space-y-5">
          <div>
            <label className="flbl">Kod *</label>
            <input
              className="inp"
              value={form.code}
              onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))}
              placeholder="main-nav"
              autoFocus
            />
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              Store API'de bu kod ile çağrılır: /api/store/navigation/menus/main-nav
            </p>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="flbl">Menü Tipi</label>
              <SearchableSelect
                value={form.menuType}
                onChange={(v) => v && setForm((f) => ({ ...f, menuType: v }))}
                options={MENU_TYPE_OPTIONS}
                hasValue
              />
            </div>
          </div>

          <div>
            <label className="flbl">Sıra</label>
            <IntegerInput value={form.sortOrder} onChange={(v) => setForm((f) => ({ ...f, sortOrder: v ?? 0 }))} />
          </div>

          {languages.length > 0 && (
            <div>
              <label className="flbl mb-2">Ad</label>
              <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
                <I18nField
                  sourceLang={sourceLang}
                  languages={languages}
                  fields={nameFields}
                  values={buildI18nValues(form.nameI18n, languages)}
                  onChange={(lang, _key, value) =>
                    setForm((f) => ({ ...f, nameI18n: { ...f.nameI18n, [lang]: value } }))
                  }
                />
              </div>
            </div>
          )}

          {createError && (
            <p className="text-sm" style={{ color: '#ef4444' }}>{createError}</p>
          )}
        </div>
      </Modal>
    </div>
  )
}

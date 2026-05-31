import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import type { Language } from '@/components/ui/I18nField'
import { GENDER_AWARE_LANGS } from '@/lib/i18n-helper'

const FLAG_MAP: Record<string, string> = {
  tr: '🇹🇷', en: '🇬🇧', de: '🇩🇪', fr: '🇫🇷',
  ar: '🇸🇦', es: '🇪🇸', it: '🇮🇹', ru: '🇷🇺',
  zh: '🇨🇳', ja: '🇯🇵', ko: '🇰🇷', nl: '🇳🇱',
}

export interface LanguageExt extends Language {
  isDefault?: boolean
}

export function useLanguages() {
  return useQuery<LanguageExt[]>({
    queryKey: ['languages'],
    queryFn: async () => {
      const { data } = await api.get('/core/languages')
      // Sort by default first, then by sortOrder
      const sorted = [...data.data].sort((a: any, b: any) =>
        b.isDefault - a.isDefault || a.sortOrder - b.sortOrder
      )
      return sorted.map((l: any) => ({
        code: l.code,
        name: l.nativeName,
        flag: FLAG_MAP[l.code] ?? '🌐',
        rtl: l.direction === 'rtl',
        isDefault: l.isDefault,
        genderAware: GENDER_AWARE_LANGS.has(l.code),
      }))
    },
    staleTime: 5 * 60 * 1000,  // 5 dakika — yeni dil eklenince max 5 dk bekle
  })
}

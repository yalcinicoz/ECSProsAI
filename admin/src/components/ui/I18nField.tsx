/**
 * I18nField — çok dilli alan bileşeni. Tüm projede standart:
 * - Kaynak dil sekmesi: tek kolon, normal input
 * - Hedef dil sekmeleri: 2 kolon (kaynak readonly | hedef editable)
 * - Eksik çevirisi olan sekme düğmesinde turuncu nokta
 * - Eksik çevirisi olan alanın kaynak verisinin sağında turuncu nokta ikonu
 * - genderAware diller (ar, he) için opsiyonel cinsiyet varyantı bölümü
 */
import { useState } from 'react'
import { cn } from '@/lib/utils'

export interface Language {
  code: string   // 'tr', 'en', 'de'
  name: string   // 'Türkçe', 'İngilizce'
  flag: string   // '🇹🇷', '🇬🇧'
  rtl?: boolean
  /** Arapça, İbranice gibi cinsiyet çekimi olan diller */
  genderAware?: boolean
}

export interface I18nFieldDef {
  key: string
  /** Alan adı her dilde: { tr: 'Ürün Adı', en: 'Product Name' } */
  labels: Record<string, string>
  type?: 'text' | 'textarea'
  required?: boolean
  rows?: number
}

export interface I18nFieldProps {
  sourceLang: string
  languages: Language[]
  fields: I18nFieldDef[]
  /** { tr: { name: 'Renk', description: '...' }, en: { name: 'Color' } } */
  values: Record<string, Record<string, string>>
  onChange?: (lang: string, key: string, value: string) => void
  readOnly?: boolean
  uppercase?: boolean
}

export function I18nField({
  sourceLang,
  languages,
  fields,
  values,
  onChange,
  readOnly = false,
  uppercase = false,
}: I18nFieldProps) {
  const [activeLang, setActiveLang] = useState(sourceLang)
  // Her dil için cinsiyet varyant bölümünün açık/kapalı durumu
  const [showGenderMap, setShowGenderMap] = useState<Record<string, boolean>>({})

  /** Bir dilin zorunlu alanlarından herhangi biri boşsa eksik sayılır */
  function isMissing(langCode: string): boolean {
    if (langCode === sourceLang) return false
    const langVals = values[langCode] ?? {}
    return fields.some((f) => f.required !== false && !langVals[f.key]?.trim())
  }

  const lang = languages.find((l) => l.code === activeLang) ?? languages[0]
  const isSource = activeLang === sourceLang
  const sourceVals = values[sourceLang] ?? {}
  const targetVals = values[activeLang] ?? {}

  function handleChange(key: string, val: string) {
    onChange?.(activeLang, key, uppercase ? val.toLocaleUpperCase('tr') : val)
  }

  const inputStyle = uppercase ? { textTransform: 'uppercase' as const } : undefined

  return (
    <div>
      {/* Tab buttons */}
      <div className="flex border-b overflow-x-auto" style={{ borderColor: 'var(--border)', background: 'var(--surface2)' }}>
        {languages.map((l) => {
          const missing = isMissing(l.code)
          return (
            <button
              key={l.code}
              type="button"
              onClick={() => setActiveLang(l.code)}
              className={cn('lang-tab flex items-center gap-1.5', activeLang === l.code && 'active')}
            >
              <span>{l.flag} {l.name}</span>
              {missing && (
                <span className="w-1.5 h-1.5 rounded-full bg-orange-400 flex-shrink-0" />
              )}
            </button>
          )
        })}
      </div>

      {/* Panel */}
      <div className="p-4 space-y-4">
        {isSource ? (
          /* ── Source language: single-column normal inputs ── */
          fields.map((f) => (
            <div key={f.key}>
              <label className="flbl">
                {f.labels[sourceLang] ?? f.key}
                {f.required !== false && <span className="text-amber-500 font-bold ml-1">*</span>}
              </label>
              {f.type === 'textarea' ? (
                <textarea
                  className={cn('ta', sourceVals[f.key] && 'ok')}
                  rows={f.rows ?? 3}
                  value={sourceVals[f.key] ?? ''}
                  onChange={(e) => handleChange(f.key, e.target.value)}
                  readOnly={readOnly}
                  style={inputStyle}
                  dir={languages.find((l) => l.code === sourceLang)?.rtl ? 'rtl' : undefined}
                />
              ) : (
                <input
                  className={cn('inp', sourceVals[f.key] && 'ok')}
                  value={sourceVals[f.key] ?? ''}
                  onChange={(e) => handleChange(f.key, e.target.value)}
                  readOnly={readOnly}
                  style={inputStyle}
                />
              )}
            </div>
          ))
        ) : (
          /* ── Target language: 2-column (source | target) ── */
          <>
            {fields.map((f) => {
              const srcVal = sourceVals[f.key] ?? ''
              const tgtVal = targetVals[f.key] ?? ''
              const fieldMissing = !tgtVal.trim()
              const srcLabel = f.labels[sourceLang] ?? f.key
              const tgtLabel = f.labels[activeLang] ?? f.key

              return (
                <div key={f.key} className="grid grid-cols-1 md:grid-cols-2 gap-3">
                  {/* Source (read-only) */}
                  <div>
                    <label className="flbl flbl-dim">{srcLabel}</label>
                    <div className="flex items-start gap-1.5">
                      {f.type === 'textarea' ? (
                        <div className="src-block flex-1" style={{ minHeight: `${(f.rows ?? 3) * 24}px` }}>
                          {srcVal}
                        </div>
                      ) : (
                        <div className="src-block flex-1">{srcVal}</div>
                      )}
                      {/* Turuncu nokta — yalnızca bu alan eksikse */}
                      {fieldMissing && (
                        <span
                          className="mt-2 flex-shrink-0 w-1.5 h-1.5 rounded-full bg-orange-400"
                          title="Çeviri eksik"
                        />
                      )}
                    </div>
                  </div>

                  {/* Target (editable) */}
                  <div>
                    <label className="flbl">{tgtLabel}</label>
                    {f.type === 'textarea' ? (
                      <textarea
                        className={cn('ta', tgtVal && 'ok')}
                        rows={f.rows ?? 3}
                        value={tgtVal}
                        onChange={(e) => handleChange(f.key, e.target.value)}
                        readOnly={readOnly}
                        style={inputStyle}
                        dir={lang.rtl ? 'rtl' : undefined}
                      />
                    ) : (
                      <input
                        className={cn('inp', tgtVal && 'ok')}
                        value={tgtVal}
                        onChange={(e) => handleChange(f.key, e.target.value)}
                        readOnly={readOnly}
                        style={inputStyle}
                        dir={lang.rtl ? 'rtl' : undefined}
                      />
                    )}
                  </div>
                </div>
              )
            })}

            {/* ── Cinsiyet varyantları — sadece genderAware diller için ── */}
            {lang.genderAware && (() => {
              const isExpanded = showGenderMap[activeLang] ?? false
              return (
                <div className="mt-1">
                  <button
                    type="button"
                    onClick={() => setShowGenderMap((m) => ({ ...m, [activeLang]: !isExpanded }))}
                    className="flex items-center gap-1.5 text-xs px-2 py-1 rounded transition-colors"
                    style={{
                      color: 'var(--text-m)',
                      background: 'var(--surface2)',
                      border: '1px solid var(--border)',
                    }}
                  >
                    <span style={{ fontSize: '10px' }}>{isExpanded ? '▾' : '▸'}</span>
                    <span>Cinsiyet varyantları (opsiyonel)</span>
                  </button>

                  {isExpanded && (
                    <div
                      className="mt-3 space-y-4 pl-4"
                      style={{ borderLeft: '2px solid var(--border)' }}
                    >
                      {fields.map((f) => {
                        const mKey = `${activeLang}:m`
                        const fKey = `${activeLang}:f`
                        const mVal = values[mKey]?.[f.key] ?? ''
                        const fVal = values[fKey]?.[f.key] ?? ''
                        const fieldLabel = f.labels[activeLang] ?? f.key

                        return (
                          <div key={f.key} className="space-y-2">
                            {/* Erkek varyantı */}
                            <div>
                              <label className="flbl" style={{ color: 'var(--text-m)', fontSize: '11px' }}>
                                ♂ {fieldLabel} — Erkek
                              </label>
                              <input
                                className={cn('inp', mVal && 'ok')}
                                value={mVal}
                                onChange={(e) => onChange?.(mKey, f.key, e.target.value)}
                                readOnly={readOnly}
                                dir={lang.rtl ? 'rtl' : undefined}
                                placeholder="Erkek formu (boş bırakılırsa varsayılan kullanılır)"
                              />
                            </div>
                            {/* Kadın varyantı */}
                            <div>
                              <label className="flbl" style={{ color: 'var(--text-m)', fontSize: '11px' }}>
                                ♀ {fieldLabel} — Kadın
                              </label>
                              <input
                                className={cn('inp', fVal && 'ok')}
                                value={fVal}
                                onChange={(e) => onChange?.(fKey, f.key, e.target.value)}
                                readOnly={readOnly}
                                dir={lang.rtl ? 'rtl' : undefined}
                                placeholder="Kadın formu (boş bırakılırsa varsayılan kullanılır)"
                              />
                            </div>
                          </div>
                        )
                      })}
                    </div>
                  )}
                </div>
              )
            })()}
          </>
        )}
      </div>
    </div>
  )
}

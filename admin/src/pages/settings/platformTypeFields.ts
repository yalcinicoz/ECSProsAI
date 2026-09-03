export interface SchemaField {
  key: string
  labelI18n: Record<string, string>
  type: 'text' | 'password' | 'number' | 'date' | 'boolean'
  section: 'credentials' | 'settings'
  required: boolean
  /** Alan yanındaki info ikonunda gösterilen açıklama — yoksa ikon çıkmaz. */
  helpI18n?: Record<string, string> | null
}

export function getFieldLabel(field: SchemaField, lang = 'tr'): string {
  if (!field.labelI18n) return field.key
  return field.labelI18n[lang]
    ?? field.labelI18n['tr']
    ?? field.labelI18n[Object.keys(field.labelI18n)[0]]
    ?? field.key
}

export function getFieldHelp(field: SchemaField, lang = 'tr'): string | null {
  const help = field.helpI18n
  if (!help) return null
  return help[lang] ?? help['tr'] ?? help[Object.keys(help)[0]] ?? null
}

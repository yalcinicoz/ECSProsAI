/** I18n sözlüğünden görüntülenecek metni seç: önce tr, sonra en, sonra ilk değer. */
export function pickName(dict?: Record<string, string> | null): string {
  if (!dict) return ''
  return dict.tr || dict.en || Object.values(dict)[0] || ''
}

export function formatDate(iso?: string | null): string {
  if (!iso) return '—'
  const d = new Date(iso)
  return d.toLocaleDateString('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric' }) +
    ' ' + d.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })
}

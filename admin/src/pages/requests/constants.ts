import type { BadgeVariant } from '@/components/ui/Badge'

export const STATUS_META: Record<string, { label: string; badge: BadgeVariant }> = {
  new:         { label: 'Yeni',            badge: 'info' },
  evaluation:  { label: 'Değerlendirmede', badge: 'warning' },
  planned:     { label: 'Planlandı',       badge: 'default' },
  in_progress: { label: 'Yapılıyor',       badge: 'warning' },
  testing:     { label: 'Testte',          badge: 'info' },
  done:        { label: 'Tamamlandı',      badge: 'success' },
  rejected:    { label: 'Reddedildi',      badge: 'danger' },
  cancelled:   { label: 'İptal',           badge: 'neutral' },
}

/** Backend ChangeRequestStatusCommand geçiş haritasının aynası. */
export const STATUS_TRANSITIONS: Record<string, string[]> = {
  new: ['evaluation', 'rejected', 'cancelled'],
  evaluation: ['planned', 'rejected', 'cancelled'],
  planned: ['in_progress', 'cancelled'],
  in_progress: ['testing', 'cancelled'],
  testing: ['done', 'in_progress', 'cancelled'],
}

export const CATEGORY_LABELS: Record<string, string> = {
  yeni_ozellik: 'Yeni Özellik',
  hata: 'Hata',
  iyilestirme: 'İyileştirme',
  veri_isi: 'Veri İşi',
  diger: 'Diğer',
}

export const PRIORITY_META: Record<string, { label: string; badge: BadgeVariant }> = {
  low:      { label: 'Düşük',  badge: 'neutral' },
  normal:   { label: 'Normal', badge: 'info' },
  high:     { label: 'Yüksek', badge: 'warning' },
  critical: { label: 'Kritik', badge: 'danger' },
}

export interface RequestListItem {
  id: string
  code: string
  title: string
  category: string
  priority: string
  status: string
  requestedByName: string
  assignedToName?: string
  dueDate?: string
  createdAt: string
  completedAt?: string
  commentCount: number
}

export interface RequestActivity {
  id: string
  activityType: string
  comment?: string
  oldValue?: string
  newValue?: string
  userName: string
  attachments: string[]
  createdAt: string
}

export interface RequestDetail {
  id: string
  code: string
  title: string
  description: string
  category: string
  priority: string
  status: string
  requestedBy: string
  requestedByName: string
  assignedTo?: string
  assignedToName?: string
  dueDate?: string
  createdAt: string
  updatedAt?: string
  completedAt?: string
  activities: RequestActivity[]
}

export const isTerminal = (status: string) =>
  status === 'done' || status === 'rejected' || status === 'cancelled'

export const isOverdue = (r: { dueDate?: string; status: string }) =>
  !!r.dueDate && !isTerminal(r.status) && new Date(r.dueDate) < new Date(new Date().toDateString())

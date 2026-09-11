// Panel DataGrid standardı (docs/datagrid-standardi-plani.md v2, F1 — 2026-09-08).
// Tanım-güdümlü tablo: her sayfa yalnız kolonlarını ve ayarlarını verir; UX davranışı panel genelinde ortaktır.
import type { ReactNode } from 'react'

export type GridBreakpoint = 'mobile' | 'tablet' | 'desktop'

export type GridFilterType = 'text' | 'enum' | 'date' | 'number' | 'boolean'

export interface GridFilterOption { value: string; label: string }

/** Kolonun filtre tanımı (F2 FilterBar bunu kullanır; F1'de yalnız taşınır). */
export interface GridFilterDef {
  type: GridFilterType
  /** Filtre etiketinde/çipte görünen ad (varsayılan: kolon başlığı) */
  label?: string
  options?: GridFilterOption[]
  /** enum: çoklu seçim (in) */
  multiple?: boolean
  /** Hızlı filtre çubuğunda görünsün (aksi halde Gelişmiş panelde) */
  quick?: boolean
  /** text için izin verilen operatörler (varsayılan contains|eq|startswith) */
  ops?: string[]
  /** Sunucu tarafındaki alan adı kolon anahtarından farklıysa */
  field?: string
}

export interface GridColumn<T> {
  /** Sıralama/filtre/export/gizleme anahtarı — sunucu GridSchema alan adıyla aynı */
  key: string
  header: string
  cell: (row: T) => ReactNode
  sortable?: boolean
  /** Başlık filtresi (ikon → açılır pencere); alan adı filter.field ?? key */
  filter?: GridFilterDef
  /** Aynı başlık penceresinde gösterilecek EK alanlar (örn. ÖDEME: ödeme durumu + yöntem + tahsilat) — field ve label zorunlu */
  filters?: (GridFilterDef & { field: string; label: string })[]
  /** 1 her zaman, 2 tablet ve üstü, 3 yalnız masaüstü (varsayılan görünürlük; kullanıcı tercihi ezilmez) */
  priority?: 1 | 2 | 3
  /** Masaüstünde sabit (sticky left) aday — bütçe kuralı (%35 hedef / %40 sınır) uygulanır */
  frozen?: boolean
  /** Sağda sabit (sticky right) aksiyon kolonu — bütçe/kullanıcı tercihi dışında, her zaman görünür (2026-09-11 faturalar "Görüntüle") */
  frozenRight?: boolean
  /** Varsayılan görünür (varsayılan true) */
  defaultVisible?: boolean
  /** Gizlenemez (kritik kolon) */
  lockVisible?: boolean
  /** Excel'e aktarılabilir (varsayılan true) */
  exportable?: boolean
  width?: number
  minWidth?: number
  align?: 'left' | 'right' | 'center'
  className?: string
  /** Hücre içi aksiyon düğmeleri barındırır: satır tıklaması bu hücrede tetiklenmez */
  stopRowClick?: boolean
}

export interface GridFilterValue {
  field: string
  op: string
  value: string
  /** Hızlı seçim etiketi (örn. tarih "last7") — çipte gösterilir, URL'de fq.<field> */
  quick?: string
}

export interface GridState {
  page: number
  pageSize: number
  search: string
  sort: string | null
  dir: 'asc' | 'desc' | null
  filters: GridFilterValue[]
}

/** Kişisel tablo tercihleri — localStorage `ecspros-grid:<gridId>:<userId>` (K3) */
export interface GridPrefs {
  /** Kolon sırası (anahtarlar); listede olmayanlar tanım sırasıyla sona eklenir */
  order?: string[]
  /** Kullanıcının AÇIKÇA açtığı kolonlar — responsive priority bunları gizlemez [E5] */
  manualVisible?: string[]
  /** Kullanıcının AÇIKÇA gizlediği kolonlar */
  manualHidden?: string[]
  pageSize?: number
  /** (eski) Sabit kolonlar: auto | off — geriye uyumluluk; `frozenKeys` tanımlıysa yok sayılır */
  frozen?: 'auto' | 'off'
  /**
   * Kullanıcının seçtiği sabit kolonlar (2026-09-08): tanımsız → kritik kolonlar (`GridColumn.frozen`) varsayılan;
   * tanımlıysa yalnız bu liste sabittir (kritik kolon da çıkarılabilir). Sabit kolonlar sola alınır; %40 genişlik sınırı korunur.
   */
  frozenKeys?: string[]
  /** Mobil görünüm tercihi (compact tanımı olan grid'lerde) */
  mobileView?: 'compact' | 'table'
}

/** Mobil kompakt görünüm (plan §2.7, F5): ana satırda başlık/alt başlık/sağ değer; dokununca diğer görünür kolonlar açılır. Otomatik karta dönüştürme YOK. */
export interface GridCompactConfig<T> {
  title: (row: T) => ReactNode
  subtitle?: (row: T) => ReactNode
  right?: (row: T) => ReactNode
  badge?: (row: T) => ReactNode
}

/** Satır seçimi standardı (F5): seçim kümesi sayfa dışına taşınabilir; toplu aksiyonlar sayfaya aittir. */
export interface GridSelection {
  selected: Set<string>
  onChange: (next: Set<string>) => void
  /** seçim varken araç çubuğu altında gösterilecek aksiyonlar */
  actions?: (selected: Set<string>) => ReactNode
}

export interface GridFrozenConfig { desktop: number; tablet: number; mobile: number }

export const GRID_PAGE_SIZES = [25, 50, 100, 250]
export const GRID_MAX_PAGE_SIZE = 250

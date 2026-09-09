import { useMemo } from 'react'
import type { GridStateApi } from './useGridState'

/**
 * SINIRLI ve SAYFALANMAYAN tablolar için yerel grid değerlendirmesi (2026-09-09, tur 12).
 *
 * Neden var: bazı ekranlarda tablo bir LİSTE değil FORM'dur — satır kümesi kanal başına bir satır
 * (sipariş numarası serileri), entegrasyon başına barkod aralığı ya da eşleme tezgâhının çalışma
 * kümesidir. Bu kümeler sunucuda sayfalanmaz (sayfalamak formu bölerdi) ama kullanıcı yine de her
 * sütunda sıralama/süzgeç ve mobil Kompakt görünümü bekliyor. Bu kanca grid durumunu (arama,
 * f.* süzgeçleri, sort/dir, sayfa) TAM listeye YERELDE uygular ve DataGrid'e sayfa dilimini verir.
 *
 * ⚠ Kural: yeni bir sayfalı uç varsa bu kancayı KULLANMA — sunucu şeması (GridSchema) tek doğru
 * yoldur; yerel değerlendirme yalnız "satır kümesi doğası gereği tam gelir" hâli içindir.
 * Operatörler sunucu sözleşmesiyle aynı adlandırmayı izler: contains/eq/startswith,
 * eq/gt/gte/lt/lte/between, boolean true/false, enum çoklu (virgülle), tarih "a,b" aralığı.
 */
export interface LocalGridConfig<T> {
  /** alan adı → değer (süzgeç + sıralama aynı erişiciyi kullanır) */
  values: Record<string, (row: T) => unknown>
  /** global arama kutusunun taradığı metin */
  search?: (row: T) => string
}

const trLower = (v: unknown) => String(v ?? '').toLocaleLowerCase('tr')

function karsilastir(a: unknown, b: unknown): number {
  if (a == null && b == null) return 0
  if (a == null) return -1
  if (b == null) return 1
  if (typeof a === 'number' && typeof b === 'number') return a - b
  if (typeof a === 'boolean' && typeof b === 'boolean') return a === b ? 0 : a ? 1 : -1
  return String(a).localeCompare(String(b), 'tr')
}

function tarihMi(v: unknown): boolean {
  return v instanceof Date || (typeof v === 'string' && /^\d{4}-\d{2}-\d{2}/.test(v))
}

function gun(v: unknown): string {
  if (v instanceof Date) return v.toISOString().slice(0, 10)
  return String(v ?? '').slice(0, 10)
}

/** Tek süzgeci tek satıra uygular (sunucu operatör sözleşmesiyle aynı anlam). */
function uygunMu(deger: unknown, op: string | undefined, ham: string): boolean {
  // Boolean alan: "true" / "false"
  if (typeof deger === 'boolean') return ham === String(deger)

  // Tarih aralığı: "yyyy-MM-dd,yyyy-MM-dd" (bitiş DAHİL, sunucu gibi gün bazlı)
  if (tarihMi(deger) && ham.includes(',')) {
    const [bas, bit] = ham.split(',')
    const g = gun(deger)
    if (bas && g < bas) return false
    if (bit && g > bit) return false
    return true
  }
  if (tarihMi(deger) && /^\d{4}-\d{2}-\d{2}$/.test(ham)) return gun(deger) === ham

  // Sayı
  if (typeof deger === 'number') {
    if (op === 'between') {
      const [a, b] = ham.split(',').map(x => parseFloat(x))
      if (!isNaN(a) && deger < a) return false
      if (!isNaN(b) && deger > b) return false
      return true
    }
    const n = parseFloat(ham.replace(',', '.'))
    if (isNaN(n)) return true
    switch (op) {
      case 'gt': return deger > n
      case 'gte': return deger >= n
      case 'lt': return deger < n
      case 'lte': return deger <= n
      default: return deger === n
    }
  }

  // Metin / enum (çoklu enum virgülle gelir)
  const d = trLower(deger)
  if (ham.includes(',') && op !== 'contains' && op !== 'startswith') {
    return ham.split(',').some(v => d === trLower(v))
  }
  const t = trLower(ham)
  switch (op) {
    case 'eq': return d === t
    case 'startswith': return d.startsWith(t)
    default: return d.includes(t)
  }
}

/** Grid durumunu tam listeye uygular; DataGrid'e verilecek sayfa dilimini ve toplam sayıyı döner. */
export function useLocalGrid<T>(tumSatirlar: T[], grid: GridStateApi, cfg: LocalGridConfig<T>) {
  const { state } = grid
  return useMemo(() => {
    let liste = tumSatirlar

    if (state.search && cfg.search) {
      const t = trLower(state.search)
      liste = liste.filter(r => trLower(cfg.search!(r)).includes(t))
    }

    for (const f of state.filters) {
      const eris = cfg.values[f.field]
      if (!eris || !f.value) continue          // tanımsız alan sessizce yok sayılır (sunucu 400 verir; burada liste bozulmasın)
      liste = liste.filter(r => uygunMu(eris(r), f.op, f.value))
    }

    if (state.sort && cfg.values[state.sort]) {
      const eris = cfg.values[state.sort]
      const yon = state.dir === 'desc' ? -1 : 1
      liste = [...liste].sort((a, b) => karsilastir(eris(a), eris(b)) * yon)
    }

    const toplam = liste.length
    const bas = (Math.max(1, state.page) - 1) * state.pageSize
    return { rows: liste.slice(bas, bas + state.pageSize), totalCount: toplam }
  }, [tumSatirlar, state, cfg])
}

# Panel DataGrid Standardı — Analiz ve Uygulama Planı (v1, 2026-09-08)

Kaynak istek: `docs/Yeni Panel İçin Gelişmiş Tablo - DataGrid UX İyileştirme Promptu (1).md`.
Bu doküman isteğin §21'deki "önce analiz" adımıdır; kod yazılmadan önce onay bekler.

> Not: İstek "Bootstrap tasarım dili"nden söz ediyor. Yeni panel Bootstrap değil, **React 19 + Tailwind v4 +
> option-h şablonu** (CSS değişkenleri `--brand/--surface/--border`, `.card`, `.inp`, `.stab`, `.flbl`) kullanıyor.
> Plan bu tasarım dilini korur; yeni framework/kütüphane getirmez.

---

## 1. Mevcut durum envanteri

### 1.1 Ortak bileşenler
| Bileşen | Dosya | Durum |
|---|---|---|
| `DataTable<T>` | `admin/src/components/ui/DataTable.tsx` (55 satır) | 5 prop: `columns, rows, loading, empty, onRowClick`. `Column` = `header, cell, className`. Sıralama, seçim, sabit sütun, sütun anahtarı YOK. `div.card.overflow-hidden > div.overflow-x-auto > table`. |
| `Pager` | aynı dosya | önceki/sonraki + "sayfa/toplam"; sayfa boyu seçimi yok |
| `Pagination` | `ui/Pagination.tsx` | "x–y / toplam" + 7 numaralı düğme; sayfa boyu seçimi yok |
| `Badge` | `ui/Badge.tsx` | chip'e en yakın parça (kaldır düğmesi yok) |
| `SearchableSelect` | `ui/SearchableSelect.tsx` | portal'lı tek açılır bileşen |
| `FilterBuilder` | `components/catalog/FilterBuilder.tsx` (896 satır) | ürün seçimine özel `FilterDef`; genel liste filtresi değil |
| `DataTable.utils.ts` | — | `tarih, tarihSaat, para, i18nAd, errText` hücre biçimleyicileri |

**Yok:** Table/Grid, FilterBar, Chip, Dropdown/Popover, Checkbox bileşeni; `useMediaQuery`/`useBreakpoint`
kancası; sütun gizleme; sıralama arayüzü; aktif filtre çipleri; liste durumu kalıcılığı; dışa aktarma.

### 1.2 Kullanım dağılımı
- `DataTable` kullanan sayfa: **14** (ikincil ekranlar: Users, Roles, AuditLogs, GiftCards, Quotes, PosSales…)
- Elle yazılmış `<table>`: **67 dosya** (Orders, Products, Members, Tickets, Stocks, Invoices, Returns, Campaigns dahil)
- Üç ayrı sayfalayıcı yan yana: `Pager` (11), `Pagination` (8), sayfa içi elle yazılmış (Orders, Products, Members…)

### 1.3 Ana liste sayfaları
| Sayfa | Tablo | API parametreleri | Sıralama | Yatay kaydırma |
|---|---|---|---|---|
| Siparişler `orders/OrdersPage` | elle | page, pageSize=20, statuses, search, paymentCollected, from/to | yok | **yok** (`overflow-hidden`) |
| Ürünler `catalog/ProductsPage` | elle | page, pageSize, activeOnly, search, sort=`newest` sabit | sabit | kısmen |
| Üyeler `crm/MembersPage` | elle | page, pageSize=20, activeOnly, search | yok | **yok** |
| Talepler `crm/tickets/TicketsPage` | elle + `Pagination` | URL `useSearchParams`: status, search, customer, orderNumber, trackingNo, page | backend var, UI yok | var (iyi örnek) |
| Stoklar `inventory/StocksPage` | elle + `Pagination` | page, pageSize, search, warehouseId, availableOnly, variantId, sectionId, binId (+facets ucu) | yok | **yok** |
| Faturalar / İadeler | elle | status sekmesi, page | yok | **yok** |
| Kampanyalar | elle | activeOnly | yok | yok, sayfalama yok |
| Kullanıcılar `settings/UsersPage` | `DataTable` + `Pager` | page, pageSize=20, search | yok | var |

### 1.4 Backend kalıpları
- Sayfalama yanıtı her yerde `{items, totalCount, page, pageSize, totalPages}` ama **5+ ayrı record** ile
  (`Shared.Kernel.PagedResult<T>`, Catalog'un gölgeleyen `PagedResult<T>`, `PagedOrderResult`, `PagedUserResult`,
  `PagedMemberResult`, `TicketPageDto`).
- Sıralama: genel `SortBy/SortDir` yok; her handler 2-3 dallı sabit `switch` (`newest`, `created_asc`, `activity`).
- Filtreler: endpoint'e özel adlandırılmış parametreler; genel filtre soyutlaması/specification yok.
- Sayfa boyu sınırı handler bazında tutarsız (`Clamp 1..200` / `1..100` / hiç yok — Orders, Products, Members, Users'ta yok).
- `iam.Users.Preferences` jsonb sütunu var, **hiçbir endpoint okumuyor/yazmıyor**; `GET /auth/me` yalnız JWT claim'lerinden döner.
- Dışa aktarma: hiçbir liste ucu yok; Excel kütüphanesi (ClosedXML/EPPlus/MiniExcel) referanslı değil.
  Admin'de `xlsx@0.18.5` yalnız **içe aktarma** için (kupon kodu dosyası okuma).

### 1.5 Responsive/kaydırma
- `index.css:127` `.tbl-wrap { overflow-x:auto }` tanımlı ama **hiç kullanılmıyor**; option-h şablonundaki
  "her tabloya `min-width`" kuralı React'e taşınmamış.
- `position: sticky; left` hiçbir yerde yok; kaydırma gölgesi yok; tek breakpoint kuralı `.mob-hide` (`max-width:767px`).
- Tailwind v4 varsayılan breakpoint'leri: sm 640 / md 768 / lg 1024 / xl 1280.

### 1.6 İstenen özelliklerin mevcutluk haritası
| # | Özellik | Durum |
|---|---|---|
| 1 | Responsive + yatay kaydırma | DataTable'da var, elle tablolarda çoğunlukla yok |
| 2 | Sürekli erişilebilir yatay scroll (sticky) | yok |
| 3 | Sabit (frozen) sütun | yok |
| 4 | Server-side filtre | var (adlandırılmış parametrelerle, uçtan uca) |
| 5 | Tipli filtreler (metin op., enum çoklu, tarih hızlı seçim, sayı aralığı, boolean) | yok (yalnız düz search/status/tarih) |
| 6 | Global arama + sütun filtresi birlikte | kısmen (Tickets) |
| 7 | Hızlı / gelişmiş filtre ayrımı | kısmen (`.stab` sekmeleri hızlı filtre görevi görüyor) |
| 8 | Aktif filtre çipleri + tümünü temizle | yok |
| 9 | Debounce + yüklenme durumu | debounce yok (Enter/Ara düğmesi), react-query `isLoading` var |
| 10 | Server-side sıralama | UI yok; backend sabit sözlük |
| 11 | Sayfa boyu seçimi + toplam/filtreli sayaç | `Pagination` toplamı gösteriyor; sayfa boyu seçimi yok |
| 12 | Sütun göster/gizle | yok |
| 13 | Sütun sırası | yok |
| 14 | Tercih kalıcılığı | yalnız `ecspros-ui` (sidebar, dark) — liste için yok |
| 15 | Kaydedilmiş görünümler | yok |
| 16 | Mobil kompakt görünüm | yok |
| 17 | Sütun önceliği | yok |
| 18 | Excel export (tüm filtreli veri) | yok |
| 19 | Ortak standart | DataTable iskeleti, %17 kapsama |

---

## 2. Önerilen mimari

### 2.1 Frontend: `DataGrid` (yeni, `admin/src/components/grid/`)
Mevcut `DataTable`'ın tasarım dilini (card, border token'ları, hover, satır tıklama) koruyan, tanım-güdümlü bir
bileşen. Ek kütüphane yok (`@tanstack/react-table` değerlendirildi: headless olsa da sunucu-taraflı durum için
katma değeri düşük, ~40 KB; kendi 600-800 satırlık çekirdeğimiz yeterli).

```ts
interface GridColumn<T> {
  key: string                       // sıralama/filtre/export/gizleme anahtarı (API alan adı)
  header: string
  cell: (row: T) => ReactNode
  sortable?: boolean
  filter?: GridFilterDef            // { type: 'text'|'enum'|'date'|'number'|'boolean', options?, ops?, quick?: boolean }
  priority?: 1 | 2 | 3              // 1 her zaman, 2 alan varsa (≥md), 3 detay (≥lg)
  frozen?: boolean                  // masaüstünde sabit aday
  defaultVisible?: boolean          // varsayılan true
  lockVisible?: boolean             // gizlenemez (kritik)
  exportable?: boolean | ((row: T) => string | number | Date | null)
  width?: number; minWidth?: number; align?: 'left'|'right'|'center'
  className?: string
}

interface DataGridProps<T> {
  gridId: string                    // tercih anahtarı: ecspros-grid:<gridId>:<userId>
  columns: GridColumn<T>[]
  rows: T[]; totalCount: number; loading: boolean
  state: GridState; onStateChange: (s: GridState) => void   // useGridState'ten gelir
  onRowClick?: (row: T) => void
  search?: { placeholder: string }  // global arama kutusu
  quickFilters?: string[]           // hızlı görünen filtre anahtarları (kolon.filter.quick ile eşdeğer)
  frozen?: { desktop: number; tablet: number; mobile: number }  // varsayılan {2,1,0}
  export?: { enabled: boolean; fileName: string; endpoint?: string }
  pageSizes?: number[]              // varsayılan [25, 50, 100, 250]
  selection?: { selected: Set<string>; onChange }             // opsiyonel, mevcut davranışı bozmaz
  empty?: ReactNode
}
```

Yardımcılar:
- `useGridState(gridId, defaults)` — tek durum kaynağı. **Filtre/arama/sıralama/sayfa URL'de** (`useSearchParams`,
  Tickets sayfasındaki mevcut kalıp; geri tuşu ve paylaşılabilir link), **sütun görünürlüğü/sırası/sayfa boyu/
  frozen tercihi localStorage'da** (`ecspros-grid:<gridId>:<userId>`, zustand `persist` kalıbı). Filtreler kalıcı
  tutulmaz (§14 uyarısı: unutulmuş filtre riski); URL'de olduğu için yenilemede korunur, yeni gelişte temiz.
- `toGridQuery(state)` → API parametreleri (aşağıdaki sözleşme).
- `useBreakpoint()` — `matchMedia` ile `mobile | tablet | desktop` (`<768`, `768-1023`, `≥1024`).
- `useStickyHScroll(ref)` — sticky yatay scrollbar (bkz. §2.5).
- `FilterBar` — global arama (debounce 400 ms, Enter anında), hızlı filtreler satırı, "Gelişmiş ▾" paneli,
  aktif filtre çipleri (`Badge` + ×), "Tümünü temizle", sonuç sayacı ("4.268 kayıt · 50 gösteriliyor").
- `ColumnsMenu` — "Kolonlar" düğmesi: onay kutuları + ▲▼ ile sıra değiştirme (sürükle-bırak yerine; kütüphane
  ve dokunmatik uyumu maliyeti gerekçesiyle) + "Varsayılana dön".
- `GridPagination` — mevcut `Pagination` genişletilir: sayfa boyu seçimi + toplam/filtreli sayaç; `Pager` ve
  elle yazılmış sayfalayıcılar zamanla buna göç eder.
- `ExportButton` — "Excel'e aktar ▾": Tüm kolonlar / Yalnız görünür kolonlar.

### 2.2 Filtre tipleri ve davranış
| Tip | Arayüz | Operatörler | URL kodlaması |
|---|---|---|---|
| text | input | `contains` (varsayılan), `eq`, `startsWith` | `f.customer=contains:ahmet` |
| enum | tek/çoklu seçim (`SearchableSelect` çoklu modu eklenir) | `in` | `f.status=in:pending,confirmed` |
| date | hızlı seçenekler (Bugün, Dün, Son 7 gün, Son 30 gün, Bu ay, Geçen ay, Özel aralık) | `between` | `f.createdAt=between:2026-09-01,2026-09-08` (hızlı seçim istemcide aralığa çevrilir; çipte etiketi korunur `q=last7`) |
| number | eşit/büyük/küçük/aralık | `eq, gt, lt, between` | `f.total=gt:1000` |
| boolean | Tümü/Evet/Hayır | `eq` | `f.paid=eq:true` |

Global arama: `search=` (mevcut parametre), backend'de kolon listesi endpoint'e özel kalır (örn. sipariş no,
müşteri, telefon, barkod, kargo takip). Sütun filtreleri ve global arama aynı anda AND ile uygulanır.

### 2.3 Backend sözleşmesi (Shared.Kernel + Api)
Yeni tip eklemeden mevcut adlandırılmış parametreler korunur; üzerine standart bir katman gelir:

```
GET /api/orders?page=1&pageSize=50&search=...&sort=createdAt&dir=desc&f.status=in:pending,confirmed&f.total=gt:1000
```

- `GridRequest` (Shared.Kernel): `Page, PageSize, Search, Sort, Dir, Filters: List<GridFilter{Field, Op, Value}>`;
  controller'da `[FromQuery]` + `f.*` ayrıştırıcı (`GridRequestBinder`).
- `GridQuery<T>` yardımcısı: `.ApplyFilters(map)` ve `.ApplySort(map, defaultSort)` — **beyaz liste**: her handler
  `Dictionary<string, Expression<Func<T, object>>>` (sıralama) ve `Dictionary<string, IFilterTarget>` (filtre)
  verir; listede olmayan alan **400** döner. Dinamik LINQ / keyfi property adı YOK (güvenlik + indeks kontrolü).
  Metin filtreleri `ILIKE` (pg_trgm indeksi olan kolonlarda), i18n jsonb alanlarda `->>'tr'`.
- Sayfa boyu merkezi clamp: `1..250` (istekteki seçenek listesi); export için ayrı yol.
- Sayfalı yanıt: mevcut `{items,totalCount,page,pageSize,totalPages}` şekli **aynen** korunur (mevcut sayfalar bozulmaz).
- Sayaç uçları (`/orders/status-counts` gibi) aynı `GridRequest`'i kabul eder → sekme sayıları filtreyle tutarlı.

### 2.4 Excel export
- Endpoint: listeleme ucunun yanına `GET /api/<liste>/export?format=xlsx&columns=a,b,c` + aynı filtre/arama/sıralama
  parametreleri; handler aynı sorgu üzerinden **sayfalama olmadan** `IAsyncEnumerable` ile akıtır.
- Kütüphane: **MiniExcel** (MIT, akış tabanlı, düşük bellek; 50K satırda ~saniyeler). Alternatif ClosedXML
  (biçimlendirme zengin, bellek yüksek). Tavsiye: MiniExcel.
- Üst sınır: `Grid:ExportMaxRows` (varsayılan 100.000); aşılırsa 400 + "filtreyi daraltın" mesajı.
- Kolon seçimi: istemci görünür kolon anahtarlarını gönderir; sunucu export tanımındaki (`GridExportColumn`)
  başlık/biçim ile üretir. Tarih `dd.MM.yyyy HH:mm`, para sayısal hücre (Excel'de toplanabilir).
- Aynı filtre modeli olduğu için ekran ile Excel farkı olmaz. Dosya adı `siparisler-2026-09-08-1425.xlsx`.
- İndirme: `axios responseType:'blob'` + `URL.createObjectURL` (yetkili istek; nginx'ten geçen aynı yol).
- Denetim: export çağrısı `iam.audit_logs`'a yazılır (kim, hangi liste, kaç satır) — kişisel veri çıkışı izi.

### 2.5 Sticky yatay scrollbar (uzun tablolar)
- Tablo sarmalayıcısı `.grid-scroll` (`overflow-x:auto`). Altında `position: fixed; bottom: 0` bir "ghost scrollbar"
  (`div` + içinde `scrollWidth` genişliğinde boş `div`) render edilir; iki yönlü `scroll` senkronu (`requestAnimationFrame`
  ile geri besleme döngüsü engellenir).
- Görünürlük: `IntersectionObserver` — tablo viewport'ta **ve** gerçek alt scrollbar viewport dışındaysa görünür;
  tablonun altı görünür olunca veya tablo viewport dışına çıkınca gizlenir. `scrollWidth <= clientWidth` ise hiç çıkmaz.
- Kenar ipuçları: sarmalayıcıya `data-scroll="start|middle|end|none"`; CSS ile sağ/sol iç gölge (`box-shadow inset`
  yerine iki `::before/::after` gradient; sabit sütunun üstünde kalır).
- Touchpad/Shift+wheel: tarayıcı davranışı korunur (kaydırmaya müdahale yok, yalnız dinleme).
- Üstte ikinci scrollbar: görsel kalabalık ve dokunmatikte anlamsız → **önerilmez**.

### 2.6 Sabit (frozen) sütunlar
- `position: sticky; left: <hesaplanan px>`; her frozen kolonun `left` değeri önceki frozen kolon genişliklerinin
  toplamı (`ResizeObserver` ile ölçülür). Başlık hücresi de sticky (`top` + `left` birlikte, `z-index` kademeli).
- Responsive: `frozen: {desktop: 2, tablet: 1, mobile: 0}` varsayılanı; ek kural: sabit kolonların toplam genişliği
  viewport'un **%45**'ini aşarsa sabit sayısı otomatik düşer, tek kolon bile aşarsa sabitleme kapanır.
- Kartın `overflow-hidden`'ı sticky'yi bozmaz (sticky, en yakın kaydırma atası `.grid-scroll`'a göre çalışır).

### 2.7 Responsive ve mobil
- **Masaüstü (≥1024):** tüm kolonlar, 2 frozen, sticky h-scroll, tam filtre çubuğu.
- **Tablet (768-1023):** priority 3 kolonlar varsayılan gizli (Kolonlar'dan açılabilir), 1 frozen, gelişmiş filtreler panelde.
- **Mobil (<768):** priority 2-3 gizli (açılabilir), frozen kapalı, `min-width` ile yatay kaydırma **her zaman**
  erişilebilir, filtre çubuğu alt sayfa (bottom sheet) olarak açılır, sayfa boyu 25.
- **Kompakt görünüm (F5):** grid tanımında `compact: { title, subtitle, meta[] }` verilirse mobilde "Kompakt / Tablo"
  geçişi; satıra dokununca alt satır açılır (accordion), satır tıklama → detay davranışı bir "Detay →" düğmesine
  taşınır. Otomatik karta dönüştürme YOK; sayfa isteğe bağlı tanımlar.

### 2.8 Tercihler ve kaydedilmiş görünümler
- F1: localStorage (`ecspros-grid:<gridId>:<userId>`): görünür kolonlar, sıra, sayfa boyu, frozen.
- F5: sunucu tarafı — `GET/PUT /api/iam/me/preferences` (`iam.Users.Preferences` jsonb, şu an ölü sütun);
  `grids.<gridId>.views[]` = `{name, columns, order, filters, sort, pageSize, isDefault}`. Görünüm seçici
  FilterBar'ın solunda ("Görünüm: Depo ▾"). Filtre içeren görünüm yüklendiğinde çipler zorunlu görünür.
  Ekip görünümü (paylaşımlı) bu fazda yok; istenirse `definition` değil `core` şemasında ayrı tablo.

---

## 3. Uygulama fazları

| Faz | Kapsam | Çıktı |
|---|---|---|
| **F0 Backend temeli** | `GridRequest/GridFilter` + `f.*` binder, `ApplyFilters/ApplySort` beyaz liste yardımcıları, merkezi clamp, MiniExcel + `GridExportWriter`, audit; **pilot: Siparişler** (`GetOrdersQuery` sıralama+filtre haritası, `/orders/export`) | izole 5051 testi |
| **F1 DataGrid çekirdeği** | `DataGrid`, `useGridState`, `useBreakpoint`, sticky h-scroll + gölgeler, frozen, `ColumnsMenu`, `GridPagination`; **ilk göç: Kullanıcılar** (küçük, DataTable'da) | panel |
| **F2 FilterBar** | global arama + hızlı/gelişmiş filtreler + çipler + tarih hızlı seçimleri; **Siparişler sayfası tam göç** (durum sekmeleri korunur, sayaçlar filtreyle tutarlı) | panel |
| **F3 Excel** | `ExportButton` (tüm/görünür kolon), Siparişler + Kullanıcılar export | panel + API |
| **F4 Yaygınlaştırma** | Ürünler, Üyeler, Talepler, Stoklar, Faturalar, İadeler, Kampanyalar (her biri: backend harita + export + göç); DataTable'daki 14 sayfa mekanik göç | tek tek commit |
| **F5 İleri** | kaydedilmiş görünümler (sunucu tercihleri), mobil kompakt görünüm, satır seçimi standardı | |

Her faz: izole publish (5051) → kullanıcı testi → canlı. F0+F1+F2 birlikte "pilot" sayılır; onay bu üçü için istenir.

---

## 4. Riskler ve mevcut davranışı bozabilecek noktalar
1. **Satır tıklama → detay** kuralı: hücre içi düğmeler `stopPropagation` ister; `DataGrid` action hücresi için
   `stopRowClick` sarmalayıcısı sunar; göçte her sayfa gözden geçirilir. Klavye erişimi (`tabIndex/Enter`) eklenir.
2. **Sticky + `overflow-hidden` kart:** sticky başlık/kolon `.grid-scroll` içinde kalmalı; kart dışına taşan
   popover'lar (SearchableSelect) portal ile açılmalı (zaten `portal` prop'u var).
3. **URL durumu:** Tickets zaten URL parametreli; diğer sayfalar `useState`. Göçte mevcut derin linkler
   (`/orders?status=` gibi) korunur; yeni `f.*` parametreleri ek olur.
4. **Sıralama performansı:** beyaz listeye yalnız indeksli/uygun kolonlar alınır; i18n jsonb ve hesaplanan
   kolonlarda sıralama kapalı bırakılır (UI'da sortable=false). `EXPLAIN` ile pilot doğrulama.
5. **Export yükü:** 100K satır tavanı + akış; aynı anda çok export'u sınırlamak için `RateLimiter` (`store-sensitive`
   kalıbı) — dakikada 5.
6. **Sayaç uçları:** durum sekmelerindeki sayılar filtre modelini almazsa ekranla çelişir → F2'de aynı istekten beslenir.
7. **Türkçe metin:** `ILIKE` küçük/büyük harf; "İ/ı" için `lower()` yerine `unaccent`/`tr_TR` collation değerlendirilir
   (mevcut `search` davranışıyla aynı kalır, geriye dönük risk yok).
8. **Eski `Pager`/`Pagination`:** kaldırılmaz; yeni sayfalar `GridPagination` kullanır, göç tamamlanınca eskiler silinir.
9. **Mobil filtre bottom sheet:** `body` scroll kilidi; mevcut `Modal` bileşeninin kalıbı kullanılır.
10. **Redis/cache:** liste uçları cache'siz; değişiklik yok. `ICacheService` bağımlılığı eklenmez.

---

## 5. Karar noktaları (onay için)
- **K1** Sıralama/filtre beyaz liste yaklaşımı (keyfi kolon adı reddedilir) — tavsiye: evet.
- **K2** Excel kütüphanesi: MiniExcel (tavsiye) / ClosedXML.
- **K3** Filtreler URL'de tutulur, localStorage'a yazılmaz (yenilemede kalır, yeni gelişte temiz) — tavsiye: evet.
- **K4** Sütun sırası: Kolonlar menüsünde ▲▼ (sürükle-bırak yok) — tavsiye: evet.
- **K5** Pilot sayfa: Siparişler (en çok kullanılan, sekme sayaçları ve tarih filtresi olduğu için en kapsayıcı).
- **K6** Export tavanı 100.000 satır ve dakikada 5 export sınırı.
- **K7** Kaydedilmiş görünümler kişisel (sunucu tercihleri), ekip paylaşımı F5 sonrası ayrı iş.

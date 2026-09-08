# Panel DataGrid Standardı — Nihai Plan (v2, 2026-09-08)

Kaynak istek: `docs/Yeni Panel İçin Gelişmiş Tablo - DataGrid UX İyileştirme Promptu (1).md`.
v1 analizi (envanter) §1'de korunur; v2 = kullanıcı kararları K1-K7 + 15 ek madde işlenmiş **nihai plan**.
Durum: **onay bekliyor** — onaydan sonra F0'dan başlanır.

> Yeni panel Bootstrap değil, **React 19 + Tailwind v4 + option-h şablonu**dur. Plan bu tasarım dilini korur;
> yeni UI framework/kütüphane getirmez (tek istisna: backend'e MiniExcel NuGet'i).

---

## 0. Kararlar (onaylı)
| Karar | Sonuç |
|---|---|
| K1 | Sıralama/filtre **beyaz liste**; listede olmayan/keyfi kolon adı → 400 |
| K2 | Excel: **MiniExcel** (akış, düşük bellek) |
| K3 | Filtre/arama/sıralama/sayfa **URL'de**; kolon görünürlüğü/sırası/sayfa boyu/frozen tercihi **localStorage'da** (kullanıcıya özel anahtar) |
| K4 | Kolon sırası **Kolonlar menüsünde ▲/▼**; sürükle-bırak yok |
| K5 | Gerçek UX pilotu **Siparişler**; Kullanıcılar yalnız teknik smoke-test |
| K6 | Export tavanı **100.000 satır**; **kullanıcı bazlı** dakikada 5 export (global değil) |
| K7 | Kaydedilmiş görünümler **kişisel**; ekip/paylaşımlı görünüm sonraki faz |

Ek maddeler (1-15) ilgili bölümlere işlendi ve `[E#]` ile işaretlendi.

---

## 1. Mevcut durum (v1 envanteri, özet)
- `DataTable` 5 prop'luk iskelet, 14 sayfada; **67 dosya elle `<table>`**; 3 ayrı sayfalayıcı (`Pager`, `Pagination`, sayfa içi).
- Sıralama arayüzü, sütun gizleme, filtre çipleri, tercih kalıcılığı, Excel export, sabit sütun, sticky yatay scroll: **yok**.
  Siparişler/Üyeler/Stoklar/İadeler kartları `overflow-hidden` → yatay kaydırma bile yok. `.tbl-wrap` tanımlı ama kullanılmıyor.
- Backend: filtreler server-side ama endpoint'e özel parametreler; genel sort/filter soyutlaması yok (2-3 dallı sabit `switch`);
  sayfa boyu clamp'i tutarsız; `PagedResult` 5+ ayrı record ama tel şekli aynı `{items,totalCount,page,pageSize,totalPages}`.
- `iam.Users.Preferences` jsonb var, hiçbir endpoint kullanmıyor. Excel kütüphanesi yok; admin'deki `xlsx` yalnız içe aktarma.
- Tailwind v4 varsayılan breakpoint'ler (md 768, lg 1024); `useBreakpoint` yok; tek media kuralı `.mob-hide`.

---

## 2. Mimari

### 2.1 Frontend — `admin/src/components/grid/`
| Parça | Görev |
|---|---|
| `DataGrid<T>` | tanım-güdümlü tablo; card/border token'ları, satır tıklama → detay kuralı, klavye erişimi (`tabIndex`, Enter) |
| `GridColumn<T>` | `key, header, cell, sortable, filter, priority(1-3), frozen, defaultVisible, lockVisible, exportable, width/minWidth, align` |
| `useGridState(gridId, defaults)` | tek durum kaynağı: URL (filtre/arama/sıralama/sayfa) + localStorage (kolon tercihleri) |
| `useBreakpoint()` | `matchMedia`: mobile <768, tablet 768-1023, desktop ≥1024 |
| `useStickyHScroll()` | sticky yatay scrollbar + kenar ipuçları (§2.5) |
| `FilterBar` | global arama, hızlı filtreler, Gelişmiş paneli, aktif çipler, Tümünü temizle, sonuç sayacı (§2.3) |
| `ColumnsMenu` | Kolonlar: göster/gizle onay kutuları, ▲/▼ sıra, "Varsayılana dön"; `lockVisible` kolonlar gri/kapalı |
| `GridPagination` | mevcut `Pagination` genişletilir: sayfa boyu (25/50/100/250), "x–y / toplam", filtreli sayaç |
| `ExportButton` | "Excel'e aktar ▾": Tüm kolonlar / Yalnız görünür kolonlar (§2.6) |
| `stopRowClick(node)` | action hücreleri için `stopPropagation` sarmalayıcı (mevcut 2 kalıp korunur) |

Kişisel tercih anahtarı: `ecspros-grid:<gridId>:<userId>` → `{ visible: string[], order: string[], pageSize, frozenOverride?, manualVisible: string[] }`.
`manualVisible` = kullanıcının Kolonlar'dan **açıkça açtığı** kolonlar (bkz. §2.4 [E5]).

Ek kütüphane yok (`@tanstack/react-table` sunucu-taraflı durum için katma değersiz; çekirdek ~700 satır).

### 2.2 Backend — `Shared.Kernel.Grid` + `Api/Grid`
```
GET /api/orders?page=1&pageSize=50&search=…&sort=createdAt&dir=desc&f.status=in:pending,confirmed&f.total=gt:1000
```
- `GridRequest { Page, PageSize, Search, Sort, Dir, Filters: List<GridFilter{Field, Op, Value}> }`; `f.*` için `GridRequestBinder`.
- `GridQuery` yardımcıları: `ApplyFilters(IQueryable, map)`, `ApplySort(IQueryable, map, defaultSort)` — **beyaz liste** [K1]:
  handler `GridSchema<T>` verir (`Sort: Dictionary<key, Expression>`, `Filter: Dictionary<key, FilterTarget{Type, Expression}>`).
  Bilinmeyen alan/operatör/tip uyuşmazlığı → `400 {error:"Geçersiz filtre alanı: x"}`. Dinamik LINQ / property adı ile erişim YOK.
- Merkezi clamp: `Page ≥ 1`, `PageSize 1..250`. Yanıt şekli **aynen** `{items,totalCount,page,pageSize,totalPages}`.
- Metin: `ILIKE` (trgm indeksli kolonlar), i18n jsonb `->>'tr'`; tarih `between` UTC'ye normalize (`AsUtc` kalıbı).
- Sayaç uçları (`/orders/status-counts`) aynı `GridRequest`'i alır → sekme sayıları filtreyle tutarlı.
- Mevcut adlandırılmış parametreler (`statuses`, `from/to`…) **korunur**; `f.*` bunların üzerine gelir, eski derin linkler çalışır.

### 2.3 Filtre mimarisi [E11 öncelikli]
| Tip | Arayüz | Operatörler | URL |
|---|---|---|---|
| text | input, debounce 400 ms, Enter anında | `contains`(vars.), `eq`, `startsWith` | `f.customer=contains:ahmet` |
| enum | tek/çoklu seçim (`SearchableSelect` çoklu modu) | `in` | `f.status=in:pending,confirmed` |
| date | Bugün, Dün, Son 7 gün, Son 30 gün, Bu ay, Geçen ay, Özel aralık | `between` | `f.createdAt=between:2026-09-01,2026-09-08&fq.createdAt=last7` (etiket çipte korunur) |
| number | eşit / büyük / küçük / aralık | `eq, gt, lt, between` | `f.total=gt:1000` |
| boolean | Tümü / Evet / Hayır | `eq` | `f.paid=eq:true` |

- **Global arama** (`search=`) + **kolon filtreleri** aynı anda, AND ile [E11]. Global aramanın kapsadığı alanlar endpoint'e özel
  (Siparişler: sipariş no, müşteri, telefon, e-posta, barkod, kargo takip).
- **Hızlı / Gelişmiş ayrımı** grid tanımında: `filter.quick=true` olanlar çubukta; kalanı "Gelişmiş ▾" panelinde [E11].
  Siparişler hızlı: Durum (sekme sayaçlı), Tarih, Ödeme durumu, Kargo durumu; gelişmiş: kanal, ödeme yöntemi, tutar aralığı, il, kupon…
- **Aktif çipler**: her filtre `Durum: Hazırlanıyor ×` şeklinde; tek tek kaldırma; "Tüm filtreleri temizle" [E11].
  Sonuç sayacı: "4.268 kayıt (filtreli) · sayfa 1/86 · 50 gösteriliyor".
- **Yüklenme**: react-query `isFetching` ile tablo üstünde ince ilerleme çubuğu + satırlar soluk; tablo kilitlenmez (eski veri kalır, `placeholderData`).
- **URL davranışı** [E14, K3]: filtreler yalnız URL'de. Geri/ileri tarayıcı gezintisi `useSearchParams` ile çalışır; link paylaşılabilir.
  Menüden listeye **ilk giriş** her zaman filtresiz (sidebar linkleri parametresiz). Aktif filtre varken çipler ve
  sayaçtaki "(filtreli)" ibaresi zorunlu görünür; hiçbir filtre gizli kalmaz.
- **Mobil** [E6]: filtre çubuğu yerine "Filtreler (3)" düğmesi (rozet = aktif filtre sayısı, global arama hariç);
  bottom sheet kapalıyken de çip satırı özet olarak görünür (kaydırılabilir).

### 2.4 Kolonlar, öncelik ve kişiselleştirme
- `priority`: 1 her zaman, 2 tablet ve üstü, 3 masaüstü. Responsive **varsayılan** görünürlüğü belirler.
- **Kullanıcı tercihi ezilmez** [E5]: Kolonlar'dan açıkça açılan kolon `manualVisible`'a yazılır; ekran küçülünce
  priority kuralı bu kolonu gizlemez, yatay kaydırma genişler. Açıkça gizlenen kolon da ekran büyüyünce geri gelmez.
- `lockVisible` (kritik: Sipariş No, Durum) gizlenemez; export'ta da her zaman bulunur.
- Sıra ▲/▼ [K4]; "Varsayılana dön" tercihi siler.
- Excel'de kolon seçimi `manualVisible` dahil o anki görünür kümedir.

### 2.5 Sticky yatay scrollbar ve kaydırma ipuçları [E2, E3, E8, E9]
- Yapı: `.grid-scroll` (`overflow-x:auto`, tabloya `min-width`) + `position: fixed; bottom: var(--grid-hscroll-offset, 0)` bir
  **ghost scrollbar** (`div` içinde `scrollWidth` genişliğinde boş `div`), grid'in yatay konumu/genişliğiyle hizalı.
- **Çift yönlü senkron** [E8]: gerçek → ghost ve ghost → gerçek `scroll` olayları; `requestAnimationFrame` + "kaynak" bayrağı ile
  geri besleme döngüsü yok. Touchpad / Shift+wheel tarayıcı davranışı korunur (müdahale yok, yalnız dinleme).
- **Görünürlük** [E8]: `IntersectionObserver` iki gözcü — (a) grid gövdesi viewport'ta mı, (b) tablonun **alt kenarı**
  (gerçek scrollbar) viewport'ta mı. Ghost = (a) ∧ ¬(b) ∧ `scrollWidth > clientWidth`. Gerçek scrollbar görünür olunca ghost
  kaybolur; tablo viewport dışına çıkınca kaybolur.
- **Aynı sayfada birden çok grid** [E2]: tek bir `GridScrollRegistry` (context). Her grid görünürlük oranını bildirir; ghost
  **yalnız en yüksek görünürlüğe sahip / son etkileşimli** grid için render edilir (tek DOM düğümü). Aynı anda iki ghost olamaz.
- **Alt sabit alanlarla çakışma** [E3]: `--grid-hscroll-offset` CSS değişkeni; sayfada `position: fixed/sticky` alt
  action bar/footer varsa `useBottomOffset()` bu alanların yüksekliğini ölçüp (`data-bottom-bar` işaretli öğeler,
  `ResizeObserver`) offset'i yazar. Mobilde sidebar/bottom nav ile aynı kural. Ghost, `z-index` olarak Modal'ın altında.
- **Kenar ipuçları** [E9]: sarmalayıcıya `data-scroll="start|middle|end|none"`; CSS ile sağ/sol iç gradient gölge.
  En sağda sağ gölge, en solda sol gölge kaybolur; kaydırma yoksa hiç görünmez. Gölgeler frozen kolonun üstünde kalır.
- Üstte ikinci scrollbar: **yok** (kalabalık, dokunmatikte anlamsız).

### 2.6 Frozen (sabit) kolon kuralları [E1, E10]
- `position: sticky; left: <px>`; `left` = önceki frozen kolon genişlikleri toplamı (`ResizeObserver`). Başlık hem `top` hem `left` sticky; z-index kademeli.
- Varsayılan adet: **masaüstü 2, tablet 1, mobil 0**; grid tanımında değiştirilebilir (`frozen: {desktop, tablet, mobile}`).
- **Genişlik bütçesi** [E1]: frozen toplamı viewport genişliğinin **%35 hedef**; aşarsa en sağdaki frozen serbest bırakılır,
  tek frozen kolon bile **%40 mutlak sınırı** aşıyorsa sabitleme tamamen kapanır. Hesap her `resize`'da yenilenir.
- **Mobil** [E10]: frozen kapalı (adet 0). İstisna olarak grid tanımı `mobile: 1` derse yalnız `priority 1` + dar (`≤ 120px`)
  bir kolon sabitlenebilir; bütçe kuralı yine geçerlidir. Temel prensip: **tüm görünür kolonlara erişim korunur**.
- Kullanıcı "Kolonlar" menüsünden frozen'ı kapatabilir (`frozenOverride: 'off'`, localStorage).
- Frozen kolonlar Kolonlar menüsünde gizlenebilir (lockVisible değilse); gizlenince bir sonraki aday sabitlenmez, adet düşer.

### 2.7 Responsive ve mobil davranış
| | Masaüstü ≥1024 | Tablet 768-1023 | Mobil <768 |
|---|---|---|---|
| Kolonlar | tümü (priority 1-3) | priority 3 varsayılan gizli | priority 2-3 varsayılan gizli |
| Frozen | 2 (bütçe %35/%40) | 1 (bütçe) | 0 |
| Yatay kaydırma | `min-width` + ghost scrollbar | aynı | `min-width` ile **her zaman** kaydırılabilir; ghost scrollbar dokunmatikte gizli (doğal kaydırma), kenar gölgeleri açık |
| Filtreler | çubuk + Gelişmiş paneli | çubuk daraltılmış, Gelişmiş panelde | "Filtreler (n)" + bottom sheet [E6] |
| Sayfa boyu | tercihe göre (vars. 50) | 50 | 25 |
| Kolonlar menüsü | var | var | var (manuel açılan kolon korunur [E5]) |
| Kompakt görünüm | — | — | F5: grid `compact` tanımlarsa "Kompakt/Tablo" geçişi |

Otomatik karta dönüştürme YOK; operasyonel karşılaştırma için tablo yapısı korunur.

### 2.8 Excel export [E4, E12, E13, K2, K6]
- **Yöntem** [E4]: **`POST /api/<liste>/export`**, gövde = `GridExportRequest { Search, Sort, Dir, Filters[], Columns[] , Named{…mevcut parametreler} }`.
  Listeleme GET kalır; export'ta URL uzunluğu sorunu olmaz (çoklu seçim, 20+ filtre, kolon listesi). Yanıt `application/vnd.openxmlformats…`,
  `Content-Disposition: attachment`; istemci `axios responseType:'blob'` + `createObjectURL` ile indirir.
- **Kapsam** [E12]: aynı `GridSchema` ile aynı sorgu, `Skip/Take` **uygulanmaz**; `AsNoTracking().AsAsyncEnumerable()` → MiniExcel akışı.
  Sayfa/sayfa boyu gövdede kabul edilmez (gönderilse yok sayılır).
- **Kolon seçimi** [E13]: `Columns` boş → tüm exportable kolonlar; dolu → yalnız o kolonlar (+ `lockVisible` olanlar her zaman).
  Başlık/biçim sunucu tanımından (`GridExportColumn`): tarih `dd.MM.yyyy HH:mm`, para sayısal hücre, enum Türkçe etiket.
- **Sınırlar** [K6]: `Grid:ExportMaxRows=100000`; önce `COUNT`, aşarsa `400 "Sonuç 100.000 satırı aşıyor, filtreyi daraltın"`.
  Rate limit **kullanıcı bazlı**: ASP.NET `RateLimiter` politikası `grid-export`, partition key = `sub` claim, 5/dk, sabit pencere;
  aşımda `429` + "Dakikada en fazla 5 dışa aktarma". Diğer kullanıcılar etkilenmez.
- **Denetim**: `iam.audit_logs` — kullanıcı, grid, filtre özeti, satır sayısı, süre.
- Dosya adı: `siparisler-2026-09-08-1425.xlsx`. Düğme export sırasında `loading`; 100K satır ≈ 3-6 sn (akış, bellek sabit).

### 2.9 Tercihler ve kaydedilmiş görünümler [K3, K7]
- F1: localStorage tercih (`ecspros-grid:<gridId>:<userId>`). Kullanıcı değişince anahtar değişir; `clearSessionStoragePreservingFavorites`
  kalıbına `ecspros-grid:*` koruma eklenir.
- F5: `GET/PUT /api/iam/me/preferences` (`iam.Users.Preferences` jsonb): `grids.<gridId>.views[] = {name, columns, order, filters, sort, pageSize, isDefault}`.
  Görünüm seçici FilterBar'ın solunda; filtre içeren görünüm yüklenince çipler ve "(görünüm: Depo)" ibaresi görünür.
  Paylaşımlı/ekip görünümü bu planda yok.

---

## 3. Fazlar ve pilot kapsamı [E7]

| Faz | Kapsam | Doğrulama |
|---|---|---|
| **F0 Backend temeli** | `GridRequest/GridFilter/GridSchema`, `f.*` binder, `ApplyFilters/ApplySort`, merkezi clamp, MiniExcel + `GridExportWriter`, `grid-export` kullanıcı bazlı RateLimiter, audit. **Siparişler**: `GetOrdersQuery` sort/filter haritası, `status-counts` aynı istek, `POST /orders/export`. | izole 5051: 400 (bilinmeyen kolon), 429 (6. export), 100K tavanı, filtre=ekran eşitliği |
| **F1 DataGrid çekirdeği** | `DataGrid`, `useGridState`, `useBreakpoint`, ghost scrollbar + registry + bottom offset + kenar gölgeleri, frozen bütçesi, `ColumnsMenu`, `GridPagination`. **Smoke-test: Kullanıcılar** (teknik, riski düşük). | panel: masaüstü/tablet/mobil ölçüler (headless Chromium) |
| **F2 FilterBar + Siparişler pilotu** | global arama, hızlı/gelişmiş, çipler, tarih hızlı seçimleri, mobil "Filtreler (n)"; **Siparişler tam göç** (durum sekmeleri + sayaçlar korunur, `overflow-hidden` kaldırılır). | kullanıcı testi |
| **F3 Excel** | `ExportButton` (tüm/görünür), Siparişler export uçtan uca. | 4.268-kayıt senaryosu: ekran 50, Excel 4.268 |
| **Pilot kapanışı** | Sticky scroll + frozen + yoğun filtre + sayfalama + kolon kişiselleştirme + Excel **Siparişler'de birlikte** doğrulanmadan F4'e geçilmez [E7]. | onay |
| **F4 Yaygınlaştırma** | Ürünler, Üyeler, Talepler, Stoklar, Faturalar, İadeler, Kampanyalar (her biri: backend harita + export + göç; ayrı commit); DataTable'daki 14 sayfa mekanik göç; eski `Pager`/sayfa içi sayfalayıcılar kaldırılır. | sayfa başına kısa test |
| **F5 İleri** | kişisel kaydedilmiş görünümler (sunucu tercihleri), mobil kompakt görünüm, satır seçimi standardı. | |

Her faz: izole publish (5051) → kullanıcı testi → canlı publish; restart kullanıcıda.

---

## 4. Riskler ve korunacak davranışlar [E15]
1. **Satır tıklama → detay**: action hücreleri `stopRowClick`; göçte her sayfanın düğmeleri tek tek kontrol edilir; klavye erişimi eklenir.
2. **Sticky + `overflow-hidden` kart**: sticky en yakın kaydırma atası `.grid-scroll`'a göre çalışır; kart `overflow-hidden` kalabilir.
   Popover'lar (`SearchableSelect`) portal ile açılır.
3. **URL durumu**: mevcut derin linkler (`/orders?status=`, Tickets parametreleri) korunur; `f.*` ek olur.
4. **Sıralama performansı**: beyaz listeye yalnız indeksli/uygun kolonlar; i18n jsonb ve hesaplanan kolonlar `sortable=false`; pilotta `EXPLAIN`.
5. **Export yükü**: 100K tavan + akış + kullanıcı bazlı 5/dk; export sorgusu `CommandTimeout 120 sn`.
6. **Sayaç uçları**: filtre modelini almazsa sekme sayıları ekranla çelişir → F2'de aynı istekten beslenir.
7. **Türkçe metin**: `ILIKE` + mevcut `search` davranışı korunur; "İ/ı" için `lower()` yerine collation değerlendirmesi ayrı iş.
8. **Birden çok grid / sabit alt alanlar**: registry + offset (§2.5); test sayfası: Sipariş detayı (paket + kalem tabloları) ve mobil.
9. **Tercih anahtarı**: userId'siz eski localStorage anahtarı yok; `localStorage.clear()` çağıran `clearSessionStoragePreservingFavorites` grid tercihlerini de korur.
10. **Mevcut CRUD/modal/yetki/link/backend**: liste yanıt şekli, endpoint yolları, `PermissionGuard`, satır aksiyonları değişmez; yalnız yeni parametreler ve yeni export ucu eklenir.
11. **Redis/cache**: liste uçları cache'siz; `ICacheService` bağımlılığı eklenmez.

---

## 5. Onay
Bu nihai plan (v2) onaylandığında F0 ile başlanır. Pilot = F0+F1+F2+F3 (Siparişler); yaygınlaştırma pilot kapanış onayından sonra.

# AI ile İşletme Raporlama Planı — Ölçü Sözlüğü, Yetki, Yürütme, Kayıt/Paylaşım

> Sürüm: **v1.0 — 2026-09-10** · Durum: **PLAN — K1-K8 kullanıcı onayı bekliyor; kod/migration/yayın YOK.**
> Alan: **Admin panel (pano #2)** + yeni `Reporting` modülü (Order/Inventory/Catalog/Accounts okuma).
> Kaynaklar: `docs/AIRapor/ECSProsAI_AI_Raporlama_Plani.pdf` (v1.0), `docs/AIRapor/ECSProsAI_Rapor_Plani_Konsolide_Degerlendirme.md`
> (Claude+Gemini+ChatGPT), `docs/AIRapor/ECSProsAI_AI_Raporlama_Degerlendirme.md` (değerlendirme + iş planı, §7).
> Bu doküman üç kaynağı **tek uygulanabilir plana** indirger: kararlar (§3), sözlük şeması (§4), mükerrer toplam
> mekanizması (§5), fazlar ve kabul kapıları (§6). Kaynak PDF'in "Aktarım gecikmesi / V3-legacy güncelliği" cümlesi
> kullanıcı kararıyla kapsam DIŞIDIR; yalnız "mükerrer toplamlar" kuralı esas alınır.

---

## 0. İlkeler (kaynak plandan, tartışma dışı)

1. **AI yorumlar, uygulama yetkiyi denetler ve rakamı hesaplar.** Model serbest SQL üretmez/çalıştırmaz; yalnız
   sürümlenmiş, yapılandırılmış **rapor tanımı** çıkarır. Tablo ve grafik aynı doğrulanmış sonuç kümesinden üretilir.
2. **Yetki tamamen sunucuda:** konu yetkisi / satır kapsamı (firma-depo-kanal) / alan yetkisi (maliyet, kâr, kişisel veri) /
   işlem yetkisi (çalıştır, kaydet, paylaş, dışa aktar, arşiv) / **her açılışta yeniden kontrol**. Yetkisiz alanla filtreleme de engellenir.
3. **Kayıtlı rapor = tarif.** Tekrar çalışırken AI'a yeniden yorumlatılmaz; "son 30 gün" açıldığı güne göre yenilenir.
   Arşiv çıktısı (belli tarihte donmuş sonuç) ayrı nesnedir ve v1'de YOKTUR.
4. **Paylaşım yetki devretmez:** alıcı raporu kendi kapsamıyla çalıştırır; anonim/herkese açık bağlantı yok; v1'de yalnız görüntüle/çalıştır.
5. **Ekran genel sohbet, site/SEO/sunucu analizi yapmaz;** "rapor" kelimesi kabul sebebi değildir.
6. **Modele kimlik/şifre/token ve ham müşteri verisi gitmez;** verideki (ürün adı, açıklama, not) talimatlar güvenilmeyen içeriktir.
7. **İlk adım sohbet ekranı değil, ölçü sözlüğü ve zorunlu yetki denetimidir** (kaynak planın son cümlesi → bu planın ilk ilkesi).

## 1. Mevcut durum (2026-09-10 canlı tespiti — kaynak plan "canlıdan doğrulanmamıştır" diyordu, burada doğrulandı)

| Bileşen | Durum |
|---|---|
| Yeni DB sipariş verisi | `order.ord_orders` **171 sipariş** (2025-01-10 → 2026-09-07, 3 kanal), 246 kalem, **0 ödeme satırı**, 1 iade, 1 fatura. Satış geçmişinin büyük gövdesi eski sistemde (juludedb); yeni DB'de "son 1 yıl kanal bazında satış" raporu bugün neredeyse boş çıkar. → **R1 kaynak envanteri bulgusu; kapsam kararı K4'e girdi** |
| Para birimi | Tümü TRY (`CurrencyCode`/`InvoiceCurrencyCode` alanları var). **Kur tablosu YOK.** → K1 |
| Stok | `inventory.inv_stocks` **248.710 satır**; `Quantity` + `ReservedQuantity` + `StockType` + depo/kısım/birim. **Fiziksel / rezerve / kullanılabilir üç ölçü türetilebilir** (§5.2). `inv_stock_movements` **0 satır** → **geçmiş stok raporu v1'de SUNULMAZ** (kaynak plan §5 kuralı doğrulandı) |
| Ödeme/iade fan-out | `ord_order_payments` (sipariş başına çoklu), `ord_returns` + `ord_return_items` + `ord_return_refunds`, `ord_order_discounts/expenses/taxes/gifts`, `ord_invoices`+`ord_invoice_items`. Bugün çoklu ödeme/çoklu iade örneği yok ama şema izin veriyor → §5.1 kuralı şart |
| Yetki modeli | `Shared.Kernel.Authorization.Permissions` 73 sabit; kanal kapsamı `RolePermission.ChannelIds`/`UserPermission.ChannelIds` (Y0-Y8 canlı); **alan izni** (kâr/marj, maliyet) Y6 artığı olarak AÇIK; **rapor izni yok** |
| Dışa aktarma | `GridExportEndpoint`/`GridExportColumn` XLSX altyapısı var (DataGrid standardı) → R4 yeniden kullanır |
| AI istemcisi | Kodda hiçbir LLM istemcisi yok (Anthropic/OpenAI/Gemini) → K5 |
| Mevcut raporlar | `ProcurementReportService` (tedarik raporu, dönemsel mutabakatı kesin değil), Dashboard metrikleri (bazı hatalı istekleri sıfır sayıyor). Yeni raporlama **bu davranışları taşımaz**: sorgu hatası ≠ gerçek sıfır |
| Saat dilimi | Yapılandırma `Europe/Istanbul`; DB UTC → takvim kuralı K-takvim (§3, K7) |
| Çoklu sunucu | FAZ 10/11 (ekip arkadaşı): kuyruk/iş sahiplenme ortak DB'de olmalı; bu plan yalnız gereksinimi yazar, altyapı kararı orada |

## 2. Değerlendirme dokümanına göre düzeltilen noktalar

- Konsolide dokümandaki "19 madde" 21'dir; "mimari değişiklik yok" ile "read replica Faz 2 mimari kararı" çelişkisi
  **hacim kanıtına bağlı karar** olarak çözüldü (K6 değil, R2.3). Tek-AI kaynaklı maddeler (read replica, sahiplik devri,
  zamanlanmış rapor) "öneri" statüsünde, üçlü-uzlaşma maddeleri (kur, netleştirme UX, sözlük şeması) "şart" statüsünde.
- "Mükerrer toplamlar" kuralı iki kaynakta da ilke düzeyindeydi; §5'te **mekanizma** düzeyine indirildi.
- R1 kapısı daraltıldı: iş birimi onayı yalnız **satış + stok** tanımları için (maliyet/kâr R6'ya).

## 3. Kararlar (K1-K8) — kullanıcı onayı bekliyor

| # | Karar | Öneri (gerekçe) |
|---|---|---|
| **K1** | Kur politikası | **v1 = yalnız TRY.** Farklı para birimli satırlar toplanmaz; ayrı satırda "TRY dışı, toplama dahil değil" gösterilir. Kur gerekirse **sipariş tarihindeki kur, siparişe yazılır** (TCMB günlük), rapor anı kuru KULLANILMAZ. Kur tablosu ve siparişe `ExchangeRate` alanı v2. (Canlı: 171/171 TRY) |
| **K2** | Ölçü sözlüğünün tek kaynağı | **Kodda, sürümlü, makine-okunur (§4 şeması), `reporting.metric_catalog` tablosuna seed.** Prompt, validator ve sorgu üreticisi AYNI nesneden beslenir; ikinci kopya yasak. Formül kullanıcıya gösterilir; sürüm değişince kayıtlı rapor açılışında uyarı |
| **K3** | Drill-down | **v1'de ham satır YOK.** Yalnız "zaten izinli boyutta bir kırılım aşağı" (kanal → ürün grubu → ürün). Ham kayıt listesi (`reporting.drilldown` izni) v2, ayrı izin + ayrı denetim kaydı |
| **K4** | Kapsam | **v1 = satış + stok.** Kapsam cümlesi: "hedef tüm izinli işletme raporlarıdır; ilk sürüm satış ve stok ile açılır". Maliyet/kâr/cari/tedarik sonraki sürümler. **Ek bulgu:** yeni DB'de satış geçmişi ince (171 sipariş) — ya (a) satış raporu yalnız go-live sonrası dönemi kapsar ve bu açıkça yazılır, ya da (b) eski sipariş geçmişi FAZ 7 PART B'de aktarılır. Öneri: **(a) ile açıl, (b) FAZ 7'ye bağlansın** |
| **K5** | AI sağlayıcısı + KVKK aktarım modeli | **Anthropic Claude, "yalnız tanım" modu:** modele sözlük + kullanıcı isteği gider, **ham veri gitmez**; özet üretimi (R3.3) yalnız toplulaştırılmış sonuç satırlarıyla, kişisel alan olmadan. Model önerisi: tanım çıkarımı `claude-sonnet-5` (yapılandırılmış çıktı/tool-use), netleştirme kararı için aynı çağrı. KVKK: aydınlatma metni, veri işleyen sözleşmesi, yurt dışı aktarım dayanağı, **prompt log saklama 90 gün + yalnız `reporting.audit` izniyle erişim**. Sağlayıcı kimlik bilgisi `core_firm_platform_integrations`'a (şifreli Credentials, ServiceType=ai) |
| **K6** | Mükerrer toplam mekanizması | §5 kabul edilir: **her olgu tablosu kendi tanesinde toplanır, join'ler toplandıktan sonra `OrderId` üzerinden;** stok üç ölçü. Kabul testi: çoklu ödeme + kısmi iade + iptal senaryosu referans hesapla birebir |
| **K7** | Takvim ve karşılaştırma kuralları | Hafta Pazartesi başlar; "son 30 gün" bugünü **kapsar** (bugün dahil 30 takvim günü); saat dilimi Europe/Istanbul (DB UTC → dönüştürülür); YoY/MoM aynı uzunlukta hizalanır; sıfır paydada % değişim "—" |
| **K8** | Ölçü tanımlarının tek karar vericisi | İsimle belirlenmeli (iş birimi sahibi). Bu kişi olmadan R1 kapanmaz |

## 4. Ölçü sözlüğü şeması (K2 — tek kaynak)

```jsonc
{
  "version": "2026.09.10-1",                 // sözlük sürümü; rapor tanımı bunu taşır
  "subjects": [{ "code": "sales", "permission": "reporting.sales" },
               { "code": "stock", "permission": "reporting.stock" }],
  "metrics": [{
      "code": "net_sales",
      "subject": "sales",
      "label": { "tr": "Net Satış", "en": "Net Sales" },
      "formula": "gross_sales - cancellations - returns",       // kullanıcıya gösterilen formül
      "unit": "currency", "currency": "TRY",
      "sources": ["order.ord_orders", "order.ord_order_items", "order.ord_returns"],
      "grain": "order",                                          // §5: toplama tanesi
      "date_basis": "order_date",                                // sipariş/ödeme/sevk tarihi — K7
      "includes": { "vat": true, "shipping": false, "discounts": "net" },
      "requires_fields": [],                                     // alan izni gerektiren ölçüler burada listeler ("cost", "profit")
      "since": "2026-07-06"                                      // K4(a): go-live öncesi dönem yok
  }, {
      "code": "available_stock", "subject": "stock",
      "formula": "physical_stock - reserved_stock",
      "sources": ["inventory.inv_stocks"], "grain": "stock_row", "point_in_time": true   // dönem filtresiyle KARIŞTIRILMAZ
  }],
  "dimensions": [{ "code": "channel", "source": "core.core_firm_platforms", "scope": "channel" },  // satır kapsamı
                 { "code": "warehouse", "scope": "warehouse" }, { "code": "product_group" }, { "code": "product" },
                 { "code": "color" }, { "code": "size" }, { "code": "date", "granularities": ["day","week","month"] }],
  "filters":    [{ "code": "date_range", "required_for": ["sales"] }, { "code": "channel" }, { "code": "warehouse" },
                 { "code": "status", "values": ["confirmed","processing","shipped","delivered","cancelled"] }]
}
```

Rapor tanımı (modelin çıktısı) bu sözlüğe **referans verir**, kendi alan/tablo adı üretemez:
`{ dictionaryVersion, subject, metrics[], dimensions[], filters{}, dateRange, compareTo?, sort, limit, view: table|bar|line, assumptions[] }`.
Validator: kod sözlükte yok → ret; yetkisiz konu/alan → ret (gizlice daraltılmış rapor gösterilmez); `assumptions[]` netleştirme chip'leri olur.

## 5. Mükerrer toplamlar — mekanizma (K6)

### 5.1 Sipariş ailesinde fan-out kuralı
- **Her olgu tablosu kendi tanesinde toplanır, join'ler TOPLANDIKTAN sonra yapılır.**
  Kalem toplamı `ord_order_items GROUP BY OrderId`, ödeme toplamı `ord_order_payments GROUP BY OrderId`, iade toplamı
  `ord_return_items`/`ord_return_refunds GROUP BY OrderId`, fatura `ord_invoice_items GROUP BY OrderId`; sonra `ord_orders`'a
  `LEFT JOIN` (sipariş × ödeme × kalem çarpımı ASLA). Sorgu üreticisi bu kalıbı **tek şablondan** basar; el yazımı join yok.
- **Sipariş başlığı ölçüleri** (toplam, indirim, kargo, vergi) `ord_orders`'tan; kalem ölçüleri (adet, ürün bazlı tutar) `ord_order_items`'tan.
  Sepet-seviyesi indirim kalemlere ağırlıklı dağıtılmış durumda (`OrderItem.DiscountAmount`, 2026-07-31) → ürün bazlı net tutar kalemden okunur, başlıkla çakışmaz.
- **İptal:** `Status = cancelled` siparişler brütte sayılır, `cancellations` ölçüsüne ayrı düşer; net = brüt − iptal − iade.
  **Kısmi iade:** iade tutarı `ord_return_items` kalem bazında (gerçek ödenen fiyattan), sipariş toplamının oranı DEĞİL.
- **Çoklu ödeme:** ödeme toplamı sipariş tutarıyla mutabık olmak zorunda değil (kısmi ödeme/iade); "tahsilat" ayrı ölçüdür, "satış" ile karıştırılmaz.
- **Referans hesap:** her ölçü için el ile yazılmış tek SQL (`tests/.../ReportingReferenceQueries`) ve sentetik veri seti
  (1 sipariş × 2 ödeme × 1 kısmi iade × 1 iptal); üretilen sorgu = referans sonucu (kuruş farkı 0). Kabul kapısı R2.

### 5.2 Stok üç ölçü
| Ölçü | Tanım | Kaynak |
|---|---|---|
| `physical_stock` | Depodaki fiziksel adet | `inv_stocks.Quantity` (StockType'a göre ayrılır; hasarlı/karantina ayrı) |
| `reserved_stock` | Onaylı siparişlerin rezervasyonu | `inv_stocks.ReservedQuantity` (OrderConfirmed → rezerve, Shipped → düşer) |
| `available_stock` | Satılabilir | `Quantity − ReservedQuantity`, negatif 0'a kırpılmaz — negatif ayrı uyarı satırıdır |
Üçü **anlık** (point-in-time) ölçüdür; dönem filtresiyle birlikte istendiğinde "bugünkü stok" olarak etiketlenir. Geçmiş stok (`inv_stock_movements` boş) v1'de reddedilir: "geçmiş stok hareket kaydı yok — hesaplanamadı" (gerçek sıfır DEĞİL).

## 6. Fazlar ve kabul kapıları

**R1 — Veri ve yetki tasarımı** (kapı: **satış + stok** tanımlarının K8 karar vericisi onayı; K1-K8 kapalı)
- [ ] R1.1 §4 sözlüğü kodda + seed (satış: brüt/iptal/iade/net satış, adet, ortalama sepet; stok: üç ölçü) — sürüm + formül metni.
- [ ] R1.2 Yetki sabitleri: `reporting.sales`, `reporting.stock`, `reporting.run`, `reporting.save`, `reporting.share`, `reporting.export`,
      `reporting.audit` (+ v2: `reporting.cost`, `reporting.profit`, `reporting.customer_fields`, `reporting.drilldown`). Satır kapsamı mevcut `ChannelIds` + depo kapsamı (yeni).
- [ ] R1.3 Kaynak envanteri ve veri boşlukları: yeni DB satış dönemi (K4), stok hareket yokluğu, ödeme satırlarının 0 olması (checkout ödeme kaydı akışı doğrulanmalı).
- [ ] R1.4 KVKK paketi (K5): aydınlatma, veri işleyen sözleşmesi, prompt log saklama/erişim.
- [ ] R1.5 §5 referans hesapları + sentetik veri seti yazılır (henüz sorgu üreticisi yok; referans önce gelir).

**R2 — Rapor yürütme temeli** (kapı: üretilen sorgu = referans; yalnız salt-okunur DB rolü)
- [ ] R2.1 `reporting` şeması: `report_definitions` (+sürüm), `report_shares`, `report_runs` (durum, süre, satır, kapsam özeti, iptal), `metric_catalog`. İş verisi okuma rolü ≠ metadata yazma rolü.
- [ ] R2.2 Validator + sorgu üreticisi (§5.1 şablonu, parametreli, `statement_timeout`, satır limiti) + tablo/grafik çıktısı aynı sonuç kümesinden.
- [ ] R2.3 Lineage: sonuç yanında tarih aralığı/saat dilimi, kapsam, ölçü formülleri, para birimi, **veri güncelliği damgası**, satır sayısı/gösterim sınırı; sorgu hatası ≠ veri yok ≠ gerçek sıfır.
- [ ] R2.4 Limitler: tablo gösterim limiti, grafik veri noktası limiti, **otomatik zaman gruplaması** (gün → hafta → ay), büyük export → kuyruk. **Read replica kararı hacim envanterinden SONRA** (bugün 2,4 GB, ağır tablo inv_stocks; karar kanıtla).

**R3 — AI ekranı** (kapı: eval seti **tanım eşleşmesi ≥ %90, sessiz yanlış varsayım = 0**)
- [ ] R3.1 Netleştirme UX: "Seni şöyle anladım" özeti → düzenlenebilir varsayım chip'leri (`Sipariş tarihi` · `İptaller hariç` · `KDV dahil` · `TRY`) → yalnız gerçek ikili belirsizlikte şıklı soru. Bloklayan soru yalnız maliyeti yüksek belirsizlikte.
- [ ] R3.2 Eval seti: 50-100 istek + beklenen tanım, CI'da (`tests/ECSPros.Api.Tests` içinde deterministik doğrulayıcı + gerçek model için nightly).
- [ ] R3.3 Özet doğrulaması: özetteki her sayı sonuç kümesinde birebir bulunmalı, yoksa özet gösterilmez; özet ayrı, araçsız çağrı, düz metin render.
- [ ] R3.4 Kapsam dışı ret ("site performans raporu" dahil), prompt-injection testleri (kuralları unut / tüm firmaları göster / SQL yaz).
- [ ] R3.5 Deterministik düzenlemeler ("sütun grafiğine çevir", "son 90 güne çıkar", "tedarikçiye göre grupla") modele gitmeden tanım üzerinde işlenir.

**R4 — Kayıt ve paylaşım** (kapı: başka kullanıcı/firma verisi sızmaz; yetki kaldırılınca eski kayıt/cache/dosya yeniden denetlenir)
- [ ] R4.1 Raporlarım: kaydet/sürüm/yeniden çalıştır; "ölçü tanımı güncellendi" uyarısı.
- [ ] R4.2 Paylaşım: görüntüle/çalıştır; alıcı kendi kapsamıyla; **kapsam uyarısı** ("bu rapor senin veri kapsamınla hesaplandı"); kopya kaydetme.
- [ ] R4.3 Sahiplik devri: personel pasifleşince raporları yöneticiye devredilir / kurumsal rapora dönüştürülür; paylaşımlar düşer.
- [ ] R4.4 Export: **XLSX + CSV** (mevcut GridExport altyapısı), satır limiti, ayrı izin + denetim kaydı; PDF v2; dosyalar süreli, yetkili URL.

**R5 — Yük ve yayın kabulü** (kapı: pilot + ölçülmüş kapasite + geri dönüş planı)
- [ ] R5.1 Kuyruk/iptal/cache (anahtar = tanım + firma + etkili yetkiler) — ortak DB'de iş sahiplenme, iki node çift üretmez (FAZ 11 ile).
- [ ] R5.2 Tenant/kullanıcı bazlı token ve maliyet sayacı; davranış telemetrisi (kaydetti/sildi/düzeltti → eval setini besler).
- [ ] R5.3 Degradasyon: AI kesilince kayıtlı raporlar çalışır; yeni istekte açık mesaj. Prompt caching (sözlük sabit).

**R6 (v2, açık kapı) —** maliyet/kâr/cari/tedarik konuları (alan izinleriyle), drill-down ham satır, arşiv çıktısı, PDF, zamanlanmış rapor + eşik alarmı (tanım + zamanlama + iletim kanalı ayrı tasarlanır; v1 engellemez).

## 7. Kontrol listesi (değerlendirme §7.3 — durum)

- [ ] K1 kur politikası kararlaştırıldı
- [ ] K2 sözlük şeması tek kaynak (§4 taslak — onay)
- [ ] K3 drill-down yetki kararı
- [ ] K4 kapsam daraltma v1 satış + stok (+ satış dönemi seçeneği a/b)
- [ ] K5 KVKK aydınlatma + veri işleyen sözleşmesi + sağlayıcı
- [ ] K6 join deduplikasyon kuralı yazıldı (§5.1 — onay)
- [ ] K6 fiziksel/rezerve/kullanılabilir stok tanımı (§5.2 — onay)
- [ ] R3 eval seti CI + eşik
- [ ] R2.4 read replica kararı hacim kanıtıyla
- [ ] R4.3 sahiplik devri politikası
- [ ] K7 takvim kuralları · K8 karar verici ismi

## 8. Zorunlu test örnekleri (kaynak plan §6, R kapılarına eşlendi)
Aynı tanım + aynı veri anı → aynı toplam (R2) · iptal/kısmi iade/çoklu ödeme/para birimi (R2, §5) · başka rapor ID/paylaşım/dosya URL ile yetki aşılamaz (R4) ·
yetki kaldırılınca cache/arşiv yeniden denetlenir (R4) · "kuralları unut / tüm firmaları göster / siteyi analiz et" reddedilir (R3) · veri içi sahte talimat, serbest SQL, yazma istekleri çalışmaz (R3) ·
eksik veri / sorgu hatası / gerçek sıfır ayrılır (R2.3) · uzun sorgu iptal, iki node çift üretmez, kullanıcı limiti (R5).

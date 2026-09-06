# ERP Eşleme Planı — Nebim V3 Sözlüğü, Grup + Özellik Koşullu Kurallar, Yapılandırma Sözlüklerinin Tablolara Taşınması

> Sürüm: **v1.1 — 2026-09-06** · Durum: **EM0 UYGULANDI ⚠️ restart bekliyor** (kullanıcı "Başla"); K1-K6 açık (EM1 bağımsız, EM2+ K1/K2 ile)
> Alan: **Admin panel (pano #2) + Integration/Catalog çekirdeği.** ERP okuma/yazma worker'ı ekip arkadaşında
> (`docs/erp-kaynak-senkron-gecis-plani.md`); bu plan worker'ın config yerine tablodan okuyacağı sözleşmeyi tanımlar.
> İlgili: `docs/pazaryeri-referans-ve-esleme-plani.md` (aynı desen), `docs/stok-karti-ve-urun-yonetimi.md`.

---

## 0. İlkeler (kullanıcı kararları, 2026-09-06)

1. **Gruplamada katman YOK.** `definition.product_groups` tek seviye kalır; "Kot Ceket", "Blazer" birer gruptur.
   Üst ad ("Ceket") `urun_grubu` SEÇİM tipli özelliktedir; grup başına varsayılan değer vardır. *(Uygulandı.)*
2. **Eşleme grup-grup değildir.** Hiçbir entegrasyonda tek başına yetmez (Trendyol "Kot Ceket", başka pazaryeri
   "Kadın Kot Ceket"). Eşleme = **grup + özellik koşulları** (cinsiyet, yaş grubu, kumaş…) → hedef kod.
3. **Pazaryeri ile aynı mekanizma.** Yeni eşleme motoru yazılmaz; pazaryeri eşlemesinin sözlük + eşleme + kural +
   öneri deseni ERP hedefine açılır. Bugün yapılandırmada duran ERP sözlükleri tablolara taşınır ve config'ten kalkar.
4. **Eşlenmemiş ERP kaydı ürün yazmaz** (mevcut fail-closed kural korunur) ama sessiz log yerine panelde görünür kuyruk olur.

## 1. Mevcut durum (2026-09-06)

| Bileşen | Durum |
|---|---|
| ERP → bizim grup | `ErpSourceOptions.ProductGroupCodes` config sözlüğü, **ad** anahtarlı, 4 kayıt (Kot Ceket→grp_46…) |
| ERP varyant ekseni | `VariantAttributeTypeCodes` config: 1→renk, 2→beden |
| ERP ürün özellik tipi | `ProductAttributeTypeCodes` config: 29 kayıt (keywordId → attribute code); `IgnoredProductAttributeTypeCodes` 15 |
| ERP değer takma adı | `ProductAttributeValueAliases` config: 1 kayıt; değerler `AttributeValue.ExtraData` içinde kaynak koduyla otomatik açılır |
| ERP tedarikçi | `SupplierAccountCodes` config: **boş** → tedarikçi hiçbir ürüne yazılmıyor |
| Eşlenmemiş kayıt | Worker log satırı; panelde görünmez |
| Pazaryeri eşleme motoru | ✅ `marketplace_category_mappings` (direct/rules/pool + RulesJson), `marketplace_attribute_mappings` (map_values/pass_literal/fixed_value), `marketplace_value_mappings`; öneri + toplu eşleme + sağlık + readiness canlı |
| Kural modeli | Kural = tek koşul (attributeTypeCode = valueId) → hedef; sıralı; ilk eşleşen kazanır |

## 2. Hedef model

### 2.1 ERP sözlüğü (firma verisi — marketplace_ref'ten AYRI)
`integration.erp_reference_items`: `IntegrationId` (core_firm_platform_integrations, ServiceType=erp — Nebim sözleşmesi),
`Kind` (product_group | variant_axis | attribute_type | attribute_value | supplier | color), `Code`, `Name`, `ParentCode?`,
`IsActive`, `FirstSeenAt`, `LastSeenAt`, `RawJson?`. Tekil (IntegrationId, Kind, Code).
Dolum: worker V3'ten salt okunur çeker (hangi tablo/prosedür: ekip arkadaşı — K1); elle ekleme serbest. marketplace_ref
gibi merkezî DAĞITILMAZ; firmaya özeldir.

### 2.2 Eşleme tabloları = pazaryeri tabloları, hedef sistem anahtarı "erp:<servisKodu>"
- `marketplace_category_mappings.Marketplace = "erp:nebim"`, `ProductGroupId` = bizim grup, `TargetExternalId` = ERP grup kodu,
  `MappingKind` direct | rules, `RulesJson` aynı kural modeli. Hedef seçici pazaryerinde marketplace_ref'ten, ERP'de
  `erp_reference_items(Kind=product_group)`'tan beslenir.
- `marketplace_attribute_mappings` / `marketplace_value_mappings`: bizim özellik tipi/değeri → ERP özellik kodu/değer kodu
  (bugünkü `ProductAttributeTypeCodes` + `ProductAttributeValueAliases` + `VariantAttributeTypeCodes`'un yerine).
  ERP'de özellik kategoriye bağlı olmadığından `MpCategoryExternalId = "*"`.
- Tedarikçi: `erp_reference_items(Kind=supplier)` → `accounts.current_accounts` eşlemesi için aynı tabloya
  `TargetKind=account` satırı (bugünkü `SupplierAccountCodes` yerine); panelde "Tedarikçiler" sekmesi.
- **Kolon adı `Marketplace` "hedef sistem" anlamında kullanılır**; yeniden adlandırma (`TargetSystem`) K5.

### 2.3 Kural modeli genişletmesi (pazaryeri + ERP ortak)
Kural: `conditions: [{attributeTypeCode, valueId}]` (VE bağlı, 1..n) + `target`. Mevcut tek koşullu kayıtlar
`conditions` dizisine dönüştürülür (geriye uyumlu okuma). Panel: kurala "+ koşul" düğmesi. Çözümleme: sıralı kurallar,
tüm koşulları sağlayan ilk kural; hiçbiri sağlamıyorsa varsayılan hedef (direct). Readiness ve gönderim aynı çözücüyü kullanır.

### 2.4 Yönler
- **Dışa (E7 — ekip arkadaşı):** ürün grubu + özellik değerleri → kural → ERP grup kodu; özellik/değer eşlemeleri → ERP kodları.
- **İçe (worker — ekip arkadaşı):** ERP grup kodu → `marketplace_category_mappings` ters bakış (TargetExternalId → ProductGroupId;
  birden çok grup aynı ERP koduna bağlıysa ERP özellik değerleriyle ayrıştıran TERS kural — ihtiyaç çıkınca, K3).
  Grup varsayılanları (ör. Ürün Grubu=Ceket) gruptan gelir; ERP'den ayrıca gelmez.
- **Eşlenmemiş:** sözlük satırı `IsMapped=false` görünümü; panelde "Eşlenmemiş ERP kayıtları" kuyruğu (kırmızı sayaç);
  ürün yazılmaz, worker bugünkü davranışı korur.

### 2.5 Panel
Mevcut `/marketplaces/mapping` sayfası: hedef seçicide pazaryerlerinin yanına **"ERP: Nebim"** (sözleşme başına bir hedef).
Sekmeler aynı: Kategoriler (ERP'de "Ürün Grupları"), Özellikler, Değerler, Gözden Geçir; ERP'ye özel ek sekmeler:
**Varyant Eksenleri**, **Tedarikçiler**, **Eşlenmemiş** (sözlükte olup eşlemesi olmayanlar; "bu ERP grubu için grup aç"
kısayolu — operatör onaylı). Öneri aracı (ad benzerliği) aynen çalışır. Rehber sayfası `92-…` ERP bölümüyle genişler.

## 3. Fazlar

| Faz | İş | Kabul |
|---|---|---|
| **EM0** Sözlük + hedef anahtarı ✅ **UYGULANDI (2026-09-06)** — `integration.erp_reference_items` (anahtar **TargetSystem** "erp:<servis>" — planın IntegrationId anahtarı yerine; IntegrationId yalnız köken; migration `AddErpReferenceItems` dev+demo), MarketplaceMappingService ERP dalları (grup arama/öneri/özellik/değer sözlükten; readiness tetikleme ERP'de atlanır), uçlar `mapping/targets` (pazaryerleri + ERP servisleri), `mapping/erp-items` GET/POST(upsert)/DELETE(pasif), panel: hedef çiplerinde "ERP: Nebim" + ERP Sözlüğü paneli (tür/kod/ad/üst kod, eşli/eşlenmemiş rozeti) + "Ürün Grubu Eşleme" sekme adı | `erp_reference_items` tablosu + migration; eşleme servislerinde hedef sistem çözümü ("erp:*" ise sözlükten); `GET /api/marketplaces/mapping/targets` (pazaryerleri + ERP sözleşmeleri); elle sözlük kaydı ucu | Nebim hedefi seçilebilir; sözlüğe elle eklenen grup kategori seçicide görünür |
| **EM1** Kural genişletmesi | `conditions[]` modeli + geriye uyumlu okuma + çözücü tekleştirme (readiness + send + ERP) + panel "+ koşul" | "cinsiyet=kadın VE yaş=çocuk" kuralı kaydedilir, readiness doğru hedefi seçer; eski tek koşullu kayıtlar bozulmaz |
| **EM2** Config sözlüklerinin taşınması | Tek seferlik aktarım: 4 grup, 2 eksen, 29 tip, 15 yok-sayılan, 1 takma ad → tablolar; worker config yerine tablodan okur (ekip arkadaşı); config anahtarları kaldırılır | Worker dry-run eski/yeni eşleme sonucu birebir; config boş |
| **EM3** Eşlenmemiş kuyruğu + panel sekmeleri | Worker eşlenmemiş kodu sözlüğe yazar (Kind + Code + Name); panelde Eşlenmemiş sekmesi + sayaç + "grup aç" kısayolu; Varyant Eksenleri ve Tedarikçiler sekmeleri | Yeni Nebim grubu geldiğinde panelde kırmızı görünür, eşlenince sonraki turda ürün yazılır |
| **EM4** Tedarikçi eşlemesi | Kind=supplier sözlüğü + cari eşlemesi; worker `SupplierId` yazar (fail-closed korunur) | Eşlenmiş tedarikçili ürünlerde kart tedarikçisi dolar |
| **EM5** Dışa yazım sözleşmesi | E7 için "bizim ürün → ERP kodları" çözücü servisi (`IErpMappingResolver`) — ekip arkadaşı tüketir | E7 kabulüyle |

Sıra: EM0 → EM1 → EM2 → EM3 → EM4; EM5 E7 takvimine bağlı. Bir faz kapanmadan sonrakine geçilmez.

## 4. Karar soruları (K)

| # | Soru | Öneri |
|---|---|---|
| K1 | Nebim V3'te grup/özellik/tedarikçi sözlüklerinin kaynağı (tablo/prosedür) ve dolum kadansı | Ekip arkadaşı belirler; günlük tam tarama yeterli |
| K2 | Sözlük dolumu worker'da mı, ayrı "sözlük senkronu" düğmesi mi? | İkisi: worker günlük + panelde "Şimdi tara" |
| K3 | İçe aktarımda ters kural (ERP grubu bizden kaba ise) şimdi mi? | Hayır — Nebim grupları bizimle aynı incelikte; ihtiyaç çıkınca |
| K4 | Eşlenmemiş ERP grubu için "grup aç" kısayolu grubu ŞEMASIZ mı açar? | Evet, boş şema + uyarı; şema operatörce tamamlanır (K9 tedarik kuralıyla uyumlu: kart operatör işi) |
| K5 | `Marketplace` kolonunun `TargetSystem` olarak yeniden adlandırılması | Şimdi değil; EM0'da yalnız yorum/DTO adı; migration riski için ertelenir |
| K6 | Değer eşlemesinde ERP değerleri otomatik açılmaya devam etsin mi (bugünkü AutoCreate*Values)? | Evet; yalnız eşlenmiş tiplerde, ExtraData kaynak kodu korunur |

## 5. Riskler
- Worker (ekip arkadaşı) config'ten tabloya geçmeden EM2 kapanmaz; iki kaynak dönemi kısa tutulur (dry-run karşılaştırma).
- Kural genişletmesi readiness/send/ERP üç tüketiciyi etkiler — tek çözücü sınıfı şart (EM1 kabul kriteri).
- Sözlük firmaya özel: yedek/aktarım paketine dahil değil (marketplace_ref'ten farklı).

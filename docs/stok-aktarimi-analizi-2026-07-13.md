# Stok Aktarımı Analizi — Eski (juludedb) Stok/Depo Modeli (2026-07-13)

Eski stok/depo modelinin doğru okunuşu — kullanıcı düzeltmeleri: gerçek stok `opproductlocations`'ta (satır = fiziksel adet), `stokAdedi` ve `opmagazadepo` aktarım DIŞI; `dfstorages` operasyon depoları bilinçli tasarım.

Stok aktarımı öncesi kullanıcının verdiği domain bilgisi (2026-07-13) — bunlar tablo adlarından
çıkarılamaz, yanlış varsayımla aktarım yapılmasın:

- **`opmagazadepo` KULLANIM DIŞI** — veri aktarımında dikkate alınmayacak (raf okutma logu gibi
  görünür ama terk edilmiş).
- **`apurunvaryantlari.stokAdedi` operasyonel değer DEĞİL** — hız gereken ama güncellik
  gerekmeyen raporlar/iç operasyonlar içindi. Stok aktarımında KULLANMA.
- **Gerçek stok = `opproductlocations`** (2026-07-13: ~278K satır, CANLI — son yazma aynı gün,
  son 30 günde ~72K satır). Model: **1 satır = 1 fiziksel adet** (productVariantId +
  storageUnitId). Miktar = satır sayısı (variant+raf GROUP BY).
  - `transactionDetailId` NULL değilse ürün **rezerve**dir.
  - `transactionType`: 0=Sistem/Rezervsiz, 1=Sipariş rezervi (detailId → `oporderlines.Id`),
    2=Özel Toplama (detailId → `optransactiondetail.Id`).
  - `sourceType` 4/5/9 değerleri görüldü — anlamı sorulmadı/bilinmiyor.
- **`dfstorages` (38 depo)**: "Satıcıya İade", "Ürün Kabul" gibi operasyon-görünümlü depolar
  **bilinçli tasarım** — transfer mantığını oturtmak için depo olarak modellendi; böyle bir
  depodaki ürün satışta da tutulabiliyor (yeni modeldeki `IsSellableOnline` karşılığı).
  `groupCode` gruplar: WEBDEPO (A/B blok katları, hepsi erpDepoKodu=D012 — ERP tek depo görür),
  MAGAZA/MAGAZAREYONU (M002-1/M002), AYAKKABIREYON (M004), GUNGORENDEPO/GUNGORENREYON
  (M005-1/M005). Eski B1-B7 "Bölüm" + S1 sanal depoları boş (raf tanımlı, stok yok).
  Reyon depolarında raf takibi yok (tek dummy raf).
- `dfstorageunits` (~124K raf, barcode'lu) — yalnız ~13.5K'sı dolu; boş "Bölüm" depoları
  ~63K raf içeriyor.
- Yeni katalog kesişimi: opproductlocations'ın 272.074 satırı (47.676 varyant) taşınan
  katalogda; rezervler: 754 sipariş + 1.099 özel toplama.
**Kullanıcı kararları (2026-07-13, 2. tur):**
- **Üçlü yapı istiyor: Depo (fiziki, YENİ kavram — eski sistemde yoktu) → Kısım (kat/ana bölme
  = eski dfstorages) → Birim/Raf (= eski dfstorageunits).** İnternet satışına açma/kapama
  **KISIM seviyesinde** — depo tümden kapatılamaz. "Web depo" fiziki değil sanal birimdi;
  yeni yapıda BİR depo "Merkez" işaretlenecek (sipariş konsolidasyonu merkez depoda yapılır).
- Mağazalar da depodur; şimdilik tek kısım + tek birim yeter (ileride detaylandırılabilir).
- Boş raflar AKTARILMAYACAK (yalnız stoklu birimler).
- Rezervler TAŞINACAK, şimdilik kaynaksız olabilir; ama kaynak işlem ID takibi önemli
  (kaynak iptal olursa rezerv düzeltilebilmeli) — legacy referans alanı tut.
- **Tüm aktarımlar TEST aktarımı** — go-live'da DB boşaltılıp nihai aktarım yapılacak;
  canlı eski sistemdeki değişim/drift şimdilik dert değil. sourceType yok sayılacak.
- **Satın alma + ürün kabul süreçleri SİSTEME DAHİL EDİLMEYECEK** (evrak-fiziki uyuşmazlığı
  yapısal; ürünler ayrıştırılıp paketlendikten sonra barkod okutmayla depo yetkilisine
  teslimde sisteme girer).
- Sistemin takibi biten çıkışlar: sipariş, toplu satış, tedarikçiye iade, defo, imha, bağış.
  Depolar arası transfer ÇIKIŞ DEĞİL (iç transfer, genelde satışta kalır).
- Girişler: satın alma sonrası, kargolanamayan sipariş iadesi, müşteri iadesi, "depoda
  bulunan" ürün (dışarıdan giriş yok, yerleştirme). **Ad-hoc hafif işlemler şart** — her
  işleme süreç zinciri dayatma (raf okutup +1 gibi nedensiz düzeltmeler operasyon gerçeği).
- Kullanıcı 45 eksik ürün kodunu `yeniurunkodlari`'na ekledi → katalog yeniden yüklenmeli
  ki 278.283 adedin tümü stok aktarımında eşleşsin. DİKKAT: MigrationTool Faz 5/6/7
  DELETE-then-reload — 45 ürün "eklenemez", tam katalog reload gerekir (tüm GUID'ler değişir,
  bağımlı fazlar 11-15 de yeniden koşulmalı).
**Sunulan öneri (2026-07-13, oturum sonu — ONAY BEKLİYOR, karar verilmedi):**
- Üçlü yapı: (1) `inv_warehouses` sadeleşir — `WarehouseType` depo|magaza'ya iner,
  `IsCentral` (tek merkez depo) + `ErpCode` eklenir, satışa-açıklık depodan KALKAR;
  (2) `inv_warehouse_sections` YENİ tablo (WarehouseId, Code, Name, **IsSellableOnline
  — yönetim noktası**, PickingOrder); (3) `inv_warehouse_bins` = mevcut
  `inv_warehouse_locations` sadeleşir (SectionId, Code, Barcode; ParentId/LocationType
  hiyerarşisi kalkar). `inv_stocks`: VariantId+BinId başına miktar, WarehouseId+SectionId
  denormalize; site "stokta var" = aktif depo + IsSellableOnline kısımların serbest toplamı.
  `inv_stock_reservations`: ReferenceType'a legacy_order/legacy_pick + `LegacyReferenceId
  bigint` (eski oporderlines/optransactiondetail Id'leri). `inv_stock_movements`: süreçsiz
  tek satır hareket; neden kodları: giriş satinalma_giris/kargolanamayan_iade/musteri_iade/
  bulunan_urun, çıkış siparis/toplu_satis/tedarikci_iade/defo/imha/bagis, ayrıca transfer
  (çıkış değil) + duzeltme (ad-hoc raf okutma ±1).
- Eşleme önerisi: Merkez Depo (IsCentral, D012; kısımlar: A Blok 1-5. kat + Merdiven,
  B Blok 0/3/4/5. kat, İade Değişim, Defo, Bağış) / Mağaza (M002; tek kısım Mağaza Reyonu) /
  Ayakkabı (M004; tek kısım) / Tekkeköy Depo (tek kısım, 13 dolu raf). Boş depolar
  (B1-B7, S1, Güngören, Sipariş, Çağrı) ve boş raflar taşınmaz; yalnız ~13.5K dolu raf gelir.
- Uygulama sırası önerisi: (1) inventory şema geçişi + sipariş/POS stok event handler'ları
  kısım-duyarlı, (2) admin Depo/Kısım/Birim ekranları (K16), (3) MigrationTool Faz 16
  (depo yapısı + stok GROUP BY variant+raf + rezervler legacy referanslı), (4) TAM test
  reload (katalog fazları 45 ürün dahil + Faz 16) + doğrulama (SUM(Quantity) = eski satır
  sayısı, o günkü değer 278.283; rezerv 754+1099).

**2 sorunun CEVAPLARI (kullanıcı, 2026-07-14 — öneri ONAYLANDI):**
1. **Tekkeköy ayrı bina AMA artık kullanım dışı — yeni tablolarda OLUŞTURULMAYACAK.**
   (Dikkat: eski veride 13 dolu rafı var; bu satırlar aktarım dışı kalır, doğrulama
   toplamı 278.283'ten Tekkeköy satırları düşülerek hesaplanmalı.)
   **Ayakkabı Reyon ayrı mağaza (M004).** **İade/Defo/Bağış merkez binasının kısımları.**
   → Nihai depo listesi: Merkez Depo (IsCentral, D012) / Mağaza (M002) / Ayakkabı (M004).
2. **Uygun**: başlangıç IsSellableOnline katlar+reyonlar açık, İade/Defo/Bağış kapalı;
   panelden değiştirilir.

**Zamanlama notu (kullanıcı, oturum kapanışı):** Ana iş akışı (FAZ P panel senkronizasyonu,
diğer terminaldeki paralel oturum) önceliklidir; kullanıcı stok aktarımına "basit olur"
düşüncesiyle başlamıştı, kapsam büyüdü — devamı ana iş akışı dikkate alınarak planlanacak.
2026-07-14: 2 soru cevaplandı, öneri onaylandı (yukarıda) — uygulama ana iş akışına
(FAZ P) göre planlanacak. Bu iş için kod/şema DEĞİŞİKLİĞİ YAPILMADI — yalnız analiz + öneri.

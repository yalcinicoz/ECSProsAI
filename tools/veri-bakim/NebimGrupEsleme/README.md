# Nebim (V3) ürün grupları → bizim ürün grupları eşleme aracı (2026-09-07)

Salt-okunur V3 (SQL Server) okuması + PostgreSQL'e eşleme/sözlük yazımı. `definition.*` altın kuralı:
yalnız `definition.product_groups`'a **kullanıcı onayıyla** yeni grup açar (--create-missing); özellik şablonunu
adı en yakın mevcut gruptan kopyalar. Eşlemeler `integration.marketplace_category_mappings` (Marketplace `erp:nebim`,
MappingKind `direct`), sözlük `integration.erp_reference_items` (Kind `product_group`).

Ortam: `V3CONN` (SQL Server bağlantı dizesi — chat'e yazma, env ver), `PGCONN` (varsayılan canlı ecommerce_db).

```
dotnet run -- discover                 # V3 özellik tipleri (hangisi "Ürün Grubu"?) + ürün sayıları
dotnet run -- groups --type 2          # o tipin kodları/adları/ürün sayısı (V3)
dotnet run -- groups --procedure       # jld_Appurunler çıktısındaki farklı urunGrubu adları (kod yok)
dotnet run -- plan --type 2            # eşleme önerisi (kuru) — rapor: eşleşen / yeni açılacak
dotnet run -- apply --type 2 [--create-missing]   # yaz (sözlük + eşleme [+ yeni gruplar])
```

Seçenekler: `--skip KOD[,KOD]` → o Nebim grubu eşlenmez ama sözlüğe **eşlenmemiş** satır yazılır (panel Eşlenmemiş kuyruğu;
ürünleri worker'da `gecici` geçici grubuna düşer — 2026-09-07 kararı). Ad birebir eşleşme appsettings `ProductGroupCodes`
sözlüğünün ÖNÜNDEdir; sözlük yalnız yazım farkı için (Büstiyer→grp_9). Şablonsuz açılan gruplara özellik kopyası
2026-09-07'de elle seçilmiş şablon listesiyle SQL ile yapıldı (PROGRESS.md); sonraki tek tük grup için panel "Özellikleri Kopyala".

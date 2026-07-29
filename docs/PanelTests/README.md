# ECSPros Panel Test Merkezi

Admin panel test koşularının kalıcı arşivi. Tarayıcıdan erişim: **http://51.178.208.59:8090**
(sunucu: `cd /opt/ECSProsAI/docs/PanelTests && nohup python3 -m http.server 8090 --bind 0.0.0.0 &`)

## Yapı

```
PanelTests/
├── index.html            # Dashboard — manifest.json'daki tüm koşuları listeler/grafikler
├── manifest.json         # Koşu kayıtları (runs[])
├── _sablon/report.html   # KALICI rapor şablonu — veriye dayalı, koşuya özel içerik yok
└── YYYY-MM-DD_HHMM_<ad>/ # Her koşu tarih damgalı klasörde
    ├── report.html       # _sablon/report.html'in kopyası (değiştirilmez)
    ├── findings.json     # Doğrulanmış bulgular + iyi çalışanlar + DB notları + test verileri
    ├── results.json      # Ham adım sonuçları [{ts,suite,step,status,note,shot}]
    ├── shots.txt         # screenshots/ dosya listesi (satır satır)
    ├── screenshots/      # Ekran görüntüleri
    └── scripts/          # O koşuda kullanılan Playwright scriptleri (tekrar için)
```

## Yeni koşu ekleme

1. `YYYY-MM-DD_HHMM_<ad>/` klasörünü oluştur; `scripts/` içindeki Playwright scriptlerini
   temel al (kurulum: memory `reference-headless-chromium-no-root`).
2. Koşu çıktılarından `results.json`, `findings.json`, `shots.txt`, `screenshots/` üret.
3. `cp _sablon/report.html <koşu>/report.html`
4. `manifest.json` → `runs[]` listesine kaydı ekle (id/title/date/steps/counts/findings/report/results/ozet).
5. Dashboard otomatik güncellenir; rapor modül modül sınıflandırmayı ve bulgu+görsel
   yerleşimini şablondan alır.

## Rapor formatı (kalıcı kurallar)

- Sonuçlar **modül modül** gruplanır (Giriş, Dashboard, Katalog, Sipariş, CRM, …);
  eşleme `_sablon/report.html` içindeki `SUITE_MAP` (suite→modül) ve `SHOT_MAP`
  (görsel adı öneki→modül) tablolarındadır — yeni suite/önek eklenirse oraya satır eklenir.
- **Bulgu kartında görsel sayfa içinde gömülü** gösterilir (tıklayınca tam boyut).
- Her modülde: bulgu kartları → katlanabilir adım tablosu → katlanabilir görsel galerisi.
- findings.json bulgu alanları: `id, onem (kritik|orta|dusuk|bilgi), modul, baslik, detay, kanit(dosya adı)`.

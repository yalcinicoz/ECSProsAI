---
title: Entegrasyon Logları
route: /integrations/logs
group: Sistem
order: 92
summary: Dış servislere yapılan çağrıların (kargo, pazaryeri, e-fatura, takip, eski sistem senkronu…) zaman, süre, sonuç ve hata mesajıyla izlendiği salt okunur ekran.
---

## Ne işe yarar
Mağazanız kargo firması, pazaryeri, e-fatura sağlayıcısı gibi dış servislerle konuştuğunda her çağrının izi burada
tutulur. "Kargo etiketi neden oluşmadı?", "Pazaryerine stok gitti mi?" gibi soruların ilk bakılacak yeri bu ekrandır:
çağrının ne zaman yapıldığı, kaç milisaniye sürdüğü, başarılı mı hatalı mı bittiği ve hata mesajı görünür.
Ekranın alt başlığı: "N kayıt — dış servis çağrılarının (e-posta, kargo, pazaryeri…) izleri".

Sol menüde **Sistem > Entegrasyonlar** bağlantısı bu sayfayı açar. Ayrı izin gerekmez. Servis tanımlarının kendisi
(hangi kargo firması, hangi hesap) **Sistem > Firmalar** altındaki entegrasyon ayarlarından yapılır; bu ekran yalnız
izlemedir.

## Ekran yerleşimi
![Entegrasyon logları — durum sekmeleri ve çağrı tablosu](img/integrations-logs.webp)
1. **Başlık satırı** — "Entegrasyon Logları" ve toplam kayıt sayısı.
2. **Sekmeler** — `Tümü` / `Başarılı` / `Hatalı`.
3. **Tablo** — en yeni çağrı en üstte.
4. **Sayfalama** — `← Önceki  1 / N  Sonraki →`; sayfa boyutu 50.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| TARİH | Çağrının yapıldığı tarih ve saat. |
| SERVİS | Servis türü: `E-posta`, `Kargo`, `Pazaryeri`, `E-Fatura`, `Görsel Arama`, `SMS`. Tanımlı olmayan türler kod adıyla görünür (ör. `legacy` = eski sistem senkronu, `invoice_integrator`, takip/reklam servis kodları). |
| İŞLEM | Çağrının türü (kod): `sync_product` (ürün gönderimi), `update_stock` (stok güncelleme), `fetch_orders` (sipariş çekme), `create_shipment` (kargo kaydı/etiket), `track_shipment` (kargo takibi), `send_invoice` (e-fatura gönderimi), `reconcile` (pazaryeri mutabakatı), `send_event:…` (takip/reklam olayı), `sync_…` (eski sistem senkron dilimi). |
| SÜRE | Çağrının süresi, milisaniye. Uzun süreler dış servisin yavaşlığına işaret eder. |
| DURUM | `Başarılı` (yeşil), `Hata` (kırmızı), `Bekliyor` (sarı). Diğer kodlar (ör. `failure`, `failed`, `synced`) gri rozetle kod adıyla görünür. |
| HATA | Hata mesajının ilk 120 karakteri (kırmızı); fareyle üzerine gelince tam metin ipucu olarak çıkar. Hata yoksa `—`. |

| Filtre | Ne yapar |
|---|---|
| Tümü | Tüm kayıtlar. |
| Başarılı | Yalnız `Başarılı` (success) kayıtlar. |
| Hatalı | Yalnız `Hata` (error) durum koduyla yazılmış kayıtlar. |

- Arama, servis/işlem ya da tarih filtresi yoktur; sekme değişince liste 1. sayfaya döner.
- Satıra tıklamak bir şey yapmaz; istek/yanıt içeriği ekranda gösterilmez.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Sekme (Tümü / Başarılı / Hatalı) | Liste üstü | Listeyi duruma göre süzer. | — |
| ← Önceki / Sonraki → | Tablo altı | Sayfalar arasında gezinir. | Birden çok sayfa varsa |

Ekranda başka buton yoktur; kayıt silinemez, çağrı buradan yeniden denenemez. Yeniden deneme ilgili ekrandan
(örn. sipariş detayında kargo kaydı, pazaryeri ürün gönderimi) yapılır.

## Durumlar ve iş kuralları
| Rozet | Anlamı |
|---|---|
| `Başarılı` (`success`) | Dış servis çağrıyı kabul etti. |
| `Hata` (`error`) | Çağrı hatayla bitti; HATA sütununda mesaj var. |
| `Bekliyor` (`pending`) | Çağrı başlatıldı, sonucu henüz yazılmadı. |
| diğer kodlar | Bazı servisler sonucu `failure`/`failed` (hata) ya da `synced` (eşitlendi) gibi kendi kodlarıyla yazar; bunlar `Hatalı` sekmesinde değil `Tümü` sekmesinde görünür. |

- Her kayıt bir firma entegrasyon tanımına bağlıdır; aynı servisin birden çok hesabı varsa hepsi bu listede karışık
  görünür.
- Liste boşsa henüz dış servis çağrısı yapılmamıştır: "Entegrasyon logu yok — dış servis çağrısı yapıldıkça burada
  listelenir."
- Kayıtlar kalıcıdır; panelden temizlenmez.

## Adım adım
**Başarısız bir kargo kaydının nedenini bulma**
1. **Sistem > Entegrasyonlar** sayfasında `Hatalı` sekmesine geçin (bulamazsanız `Tümü`'nde `failure`/`failed`
   rozetli satırlara bakın).
2. SERVİS = `Kargo`, İŞLEM = `create_shipment` olan, siparişin saatine denk gelen satırı bulun.
3. HATA sütunundaki mesajın üzerine gelip tam metni okuyun (adres eksik, ağırlık yok, hesap bilgisi hatalı vb.).
4. Nedeni ilgili ekranda düzeltip işlemi oradan yeniden deneyin; yeni deneme bu listeye yeni satır olarak düşer.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Aynı anda çok sayıda `Hata` görüyorsanız önce SÜRE sütununa bakın — hepsi zaman aşımı sınırında
> (binlerce ms) ise sorun büyük ihtimalle dış servistedir, tanımlarınızda değil.

> **Dikkat:** `Hatalı` sekmesi yalnız `error` koduyla yazılmış kayıtları gösterir; bazı pazaryeri/kargo işlemleri
> başarısızlığı `failure` ya da `failed` olarak yazar. Eksiksiz hata taraması için `Tümü` sekmesini de gözden geçirin.

> **Not:** Bu ekranda panel içi işlemler (kullanıcı, vitrin düzenleme) yer almaz; onlar için
> [Denetim Logları](/rehber/sistem/denetim-loglari/)'na bakın.

## İlgili sayfalar
- [Denetim Logları](/rehber/sistem/denetim-loglari/)
- [Roller ve Yetkiler](/rehber/sistem/roller-ve-yetkiler/)
- [Giriş ve Panel Yapısı](/rehber/genel/panel-yapisi/)

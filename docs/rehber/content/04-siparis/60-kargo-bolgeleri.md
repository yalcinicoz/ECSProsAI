---
title: Kargo Bölgeleri
route: /orders/cargo-zones
group: Sipariş Yönetimi
order: 60
summary: Firmanın kargo şirketleri için genel öncelik sırasının ve mahalle bazında özel kargo atamalarının tanımlandığı ekran; müşterinin teslimat adımında göreceği kargo seçeneklerini belirler.
---

## Ne işe yarar
Müşteri sitede teslimat adresini seçince hangi kargo şirketlerinin, hangi sırayla sunulacağı bu ekrandan belirlenir. Üstte firmanın
**genel öncelik sırası**, altta il → ilçe → mahalle seçimiyle **mahalleye özel atamalar** tanımlanır. Mahalleye atama yapılırsa o adreste
**yalnız atanan kargolar** buradaki sırayla sunulur; atama yoksa tüm aktif kargolar genel sırayla görünür. Müşterinin seçimi siparişe
"Kargo Tercihi" olarak yazılır ve Kargoya Ver penceresinde varsayılan gelir.

## Ekran yerleşimi
![Kargo Bölgeleri — üstte Genel Öncelik listesi, altta il/ilçe/mahalle seçimi ve Mahalle Atamaları](img/orders-cargo-zones.webp)
1. **Başlık satırı** — "Kargo Bölgeleri"; sağda kayıt durumu mesajı ("Kaydedildi ✓" / hata) ve birden fazla firma varsa **Firma** seçici.
2. **Uyarı kutusu** (sarı) — firmada aktif kargo entegrasyonu yoksa görünür; "firma entegrasyonları" bağlantısı firma detayına gider.
3. **Genel Öncelik (tüm firma)** kartı — sıralı kargo listesi + **Genel Sırayı Kaydet**.
4. **Mahalle Atamaları** kartı — İl / İlçe / Mahalle aramalı seçiciler; mahalle seçilince o mahallenin atama listesi + **Mahalle Atamasını Kaydet**.

## Liste ve filtreler
| Öğe | Ne yapar |
|---|---|
| Firma seçici | Birden fazla firma varsa hangi firmanın kuralları düzenleneceğini seçer; tek firmada gizlidir ve otomatik seçilir. |
| İl / İlçe / Mahalle | Aramalı açılır listeler; ilçe seçilmeden mahalle kapalıdır ("Önce ilçe seçin"). Mahalle listesinde atama olan mahalleler "· N kargo" ekiyle görünür. |

### Sıralı kargo listesi (her iki kartta ortak)
| Öğe | Anlamı |
|---|---|
| 1., 2., … | Öncelik sırası — **üstteki önce sunulur**. |
| Kargo adı | Firma entegrasyonunun adı (sözleşme adı ya da taşıyıcı adı). |
| `Pasif entegrasyon` (sarı rozet) | Listede duran ama entegrasyonu pasife alınmış kargo; müşteriye sunulmaz. |
| ↑ / ↓ | Satırı bir üst/alt sıraya taşır (ilk satırda ↑, son satırda ↓ pasiftir). |
| Kaldır (kırmızı) | Satırı listeden çıkarır. |
| + Kargo Ekle | Aramalı seçici; henüz listede olmayan aktif kargoları ekler. Eklenecek kargo kalmayınca görünmez. |

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Genel Sırayı Kaydet | Genel Öncelik kartı | Listedeki sırayı firmanın genel önceliği olarak kaydeder (üst satır en yüksek öncelik). | Listede değişiklik yapılmış olmalı; aktif kargo entegrasyonu olmalı |
| Mahalle Atamasını Kaydet | Seçili mahalle kutusu | Listeyi o mahallenin özel ataması olarak kaydeder. Liste boş kaydedilirse mahalle ataması kalkar, genel öncelik geçerli olur. | Mahalle seçili, listede değişiklik yapılmış |
| ↑ / ↓ / Kaldır / + Kargo Ekle | Her iki liste | Listeyi düzenler; kaydedilene kadar sunucuya yazılmaz. | — |
| firma entegrasyonları bağlantısı | Sarı uyarı | Firma detay sayfasına gider (kargo servisi eklemek için). | Uyarı görünür olduğunda |

Kayıt sonrası sağ üstte 2,5 sn "Kaydedildi ✓" görünür; hata varsa mesajı aynı yerde çıkar.

## Durumlar ve iş kuralları
- **Önceliklendirme:** Müşteri adresinin mahallesine atama varsa → yalnız atanan kargolar, atama sırasıyla. Atama yoksa → tüm aktif kargolar
  genel sırayla; genel listeye eklenmemiş aktif kargolar listenin sonunda ada göre sıralanır. Genel sıra da tanımlanmadıysa aktif kargolar ada göre sunulur.
- Aynı kargo şirketi bir listede iki kez yer alamaz; listeye yalnız bu firmaya ait kargo tipi entegrasyonlar eklenebilir.
- Pasife alınan entegrasyon listede kalır ama sunulmaz ("Pasif entegrasyon" rozeti); listeyi temizlemek için **Kaldır** kullanın.
- Firmada hiç aktif kargo entegrasyonu yoksa bölge tanımı anlamsızdır; önce Ayarlar → Firmalar → (firma) → Entegrasyonlar'dan kargo servisi ekleyin.
- Müşterinin seçtiği kargo siparişe **Kargo Tercihi** olarak yazılır; operatör Kargoya Ver adımında değiştirebilir (bağlayıcı değildir).

## Adım adım
**Genel sırayı belirleme**
1. (Birden fazla firma varsa) sağ üstten firmayı seçin.
2. **Genel Öncelik** kartında **+ Kargo Ekle** ile kargoları ekleyin; ↑/↓ ile sıralayın.
3. **Genel Sırayı Kaydet**.

**Bir mahalleye özel kargo atama**
1. **Mahalle Atamaları** kartında İl → İlçe → Mahalle seçin.
2. Açılan kutuda **+ Kargo Ekle** ile yalnız o mahalleye gitmesini istediğiniz kargoları ekleyin, sıralayın.
3. **Mahalle Atamasını Kaydet**. Atamayı kaldırmak için tüm satırları **Kaldır** ile silip yeniden kaydedin.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** Mahalle ataması "ek öncelik" değil **kısıtlamadır**: atanmayan kargolar o mahallede hiç sunulmaz.

> **İpucu:** Hangi mahallelere atama yaptığınızı görmek için mahalle listesindeki "· N kargo" ekine bakın.

> **Not:** Kaydet butonları yalnız listede değişiklik yapınca etkinleşir; firma değiştirince kaydedilmemiş değişiklikler sıfırlanır.

## İlgili sayfalar
- [Sipariş Detayı](/rehber/siparis/siparis-detay/) (Kargo Tercihi ve Kargoya Ver)
- [Numara Serileri](/rehber/siparis/numara-serileri/) (kargo barkod aralıkları)

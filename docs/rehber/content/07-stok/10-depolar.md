---
title: Depolar
route: /inventory/warehouses
group: Stok
order: 10
summary: Depoların listelendiği ve oluşturulduğu; depo detayında Kısım ve Birim/Raf yapısının, internete satışa açıklığın yönetildiği ekran.
---

## Ne işe yarar
Depolar ekranı, stokların tutulduğu fiziksel ve sanal depoları tanımlar. Stok **Depo → Kısım → Birim (raf)** yapısında tutulur: her depo kısımlara (kat, reyon, İade, Defo…), her kısım raflara bölünür; stok satırları raf başına izlenir. Sitede "stokta" sayılacak miktar **kısım** seviyesindeki "İnternete satışa açık" işaretiyle belirlenir. Depo sorumluları bu ekranda depo açar, kısım/raf tanımlar ve raf barkodlarını yönetir.

## Ekran yerleşimi
![Depolar listesi](img/inventory-warehouses.webp)
1. **Başlık satırı** — "Depolar" (yetkiniz yoksa yanında **Salt Okunur** rozeti), kayıt sayısı; sağda **Tümü / Aktif** anahtarı ve **+ Yeni Depo**.
2. **Tablo** — depolar; satıra tıklayınca depo detayı açılır; satır sonunda **Düzenle**.
3. **Pencereler** — "Yeni Depo" / "Depo Düzenle".

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| AD | Depo adı (kaynak dilde). |
| KOD | Depo kodu. |
| TİP | `Fiziksel` / `Sanal` / `Dropship` / `Konsinyasyon`. |
| ONLİNE | Depo düzeyindeki "Online satışa açık" işareti (✓ / —). Asıl satışa açıklık kısım seviyesinde yönetilir (aşağıya bakın). |
| DURUM | `Aktif` / `Pasif`. |
| ADRES | Depo adresi (kısaltılmış; yoksa —). |
| Düzenle › | Düzenleme penceresi (yetkiyle) ve detay oku. |

| Filtre | Ne yapar |
|---|---|
| Tümü / Aktif | Aktif seçiliyken pasif depolar gizlenir. |

Sayfalama yoktur. Kayıt yoksa "Depo bulunamadı." yazar.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| + Yeni Depo | Liste başlığı | "Yeni Depo" penceresi açılır. | `inventory.manage` |
| Düzenle | Satır sonu | "Depo Düzenle" penceresi açılır (satırı tıklamadan). | `inventory.manage` |
| Satır tıklama | Tablo | `/inventory/warehouses/:id` depo detayı açılır. | — |
| Oluştur / Kaydet / İptal | Depo pencereleri | Kaydeder / vazgeçer. | Oluştur: Kod ve kaynak dilde Ad dolu |
| ‹ | Detay başlığı | Listeye döner. | — |
| + Yeni Kısım | Detay → Kısımlar başlığı | "Yeni Kısım" penceresi açılır. | `inventory.manage` |
| ✓ Satışa açık / ✗ Satışa kapalı | Detay → kısım satırı | Tek tıkla kısmın internete satışa açıklığı değişir. | — |
| ✎ (kalem) | Kısım satırı sonu | "Kısım Düzenle — KOD" penceresi açılır. | — |
| ▸ / ▾ | Kısım satırı başı | Kısmın raflarını açar/kapatır. | — |
| + Yeni Birim | Açılan kısım bloğu | "Yeni Birim/Raf" penceresi açılır. | — |
| ✎ (kalem) | Raf satırı sonu | "Birim Düzenle — KOD" penceresi açılır. | — |
| Kaydet / İptal | Kısım ve Birim pencereleri | Kaydeder / vazgeçer. | Ad (kısım) ya da Barkod (raf) dolu; oluştururken Kod dolu |

Yetkisiz kullanıcıda yazma butonları görünmez, liste ve detay salt okunur gelir.

## Form alanları
### Yeni Depo / Depo Düzenle
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Kod | Evet (yalnız oluştururken) | Büyük harfe çevrilir. Örn. `WH-001`. Benzersiz: "'WH-001' depo kodu zaten mevcut." |
| Tip | Hayır (varsayılan Fiziksel) | Fiziksel / Sanal / Dropship / Konsinyasyon. |
| Adres | Hayır | Depo adresi. |
| Sıra | Hayır (0) | Tam sayı. |
| Rezervasyon Önceliği | Hayır (0) | Tam sayı; küçük değer önce. Sipariş rezervasyonu depo seçerken bu sırayı kullanır. |
| Online satışa açık | Hayır | Depo düzeyi işaret; listede ONLİNE sütunu. Site stok görünürlüğü kısım düzeyindeki işarete göre hesaplanır. |
| Aktif | — | Yalnız düzenlemede. |
| Ad (çok dilli) | Evet (kaynak dil) | TR/EN sekmeli alan; kaynak dil zorunlu, diğerleri boş kalabilir. |

### Yeni Kısım / Kısım Düzenle
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Kod | Evet (oluştururken) | Büyük harf. Örn. `SATIS-KATI`, `IADE`. Aynı depoda benzersiz: "'IADE' kodlu kısım bu depoda zaten var." |
| Ad | Evet | Örn. `Satış Katı`. |
| Toplama Sırası | Hayır (0) | Rezervasyon/tüketimde kısımların denenme sırası (küçük önce). |
| İnternete satışa açık | Hayır (varsayılan açık) | Bu kısımdaki serbest stok sitede "stokta" sayılır. |
| Aktif | — | Yalnız düzenlemede. |

### Yeni Birim/Raf / Birim Düzenle
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Kod | Evet (oluştururken) | Büyük harf. Örn. `A1-01`. Aynı kısımda benzersiz: "'A1-01' kodlu birim bu kısımda zaten var." |
| Barkod | Evet | Raf barkodu; tüm depolarda benzersiz: "'X' barkodu başka bir birimde kayıtlı." |
| Ad | Hayır | Serbest ad. |
| Aktif | — | Yalnız düzenlemede. |

## Detay sayfası
![Depo detayı — Kısımlar tablosu ve açılmış raf listesi](img/inventory-warehouses-detay.webp)
*(1) Başlık: depo adı, kod, tip rozeti, Aktif/Pasif · (2) Açıklama satırı · (3) Kısımlar (N) tablosu · (4) Açılan kısmın Birimler / Raflar bloğu*

**Kısımlar** tablosu:
| Sütun | Anlamı |
|---|---|
| ▸ | Rafları açar/kapatır. |
| Kod / Ad | Kısım kodu ve adı. |
| İnternete Satış | `✓ Satışa açık` (yeşil) / `✗ Satışa kapalı` (kırmızı); tıklanınca değişir. |
| Toplama Sırası | Sayı. |
| Birim | Kısımdaki raf sayısı. |
| Stok (satır / adet) | Kısımdaki stok satırı sayısı / toplam adet. |
| Durum | `Aktif` / `Pasif`. |
| ✎ | Düzenle. |

**Birimler / Raflar (N)** bloğu (kısım açılınca):
| Sütun | Anlamı |
|---|---|
| Kod | Raf kodu. |
| Barkod | Raf barkodu. |
| Ad | Raf adı (yoksa —). |
| Stok (satır / adet) | Raftaki stok satırı sayısı / toplam adet. |
| Durum | `Aktif` / `Pasif`. |
| ✎ | Düzenle. |

Kısım yoksa: "Bu depoda kısım tanımlı değil. Reyon/mağaza depolarında tek kısım yeterlidir." Raf yoksa: "Birim yok. Raf takibi yapılmayan kısımlarda tek 'dummy' birim yeterlidir."

## Durumlar ve iş kuralları
- **Depo → Kısım → Birim:** stok satırları raf başına tutulur; depo toplamı kısım ve raf toplamlarından oluşur.
- **İnternete satış kısım seviyesindedir.** Satışa kapalı kısımlardaki (İade, Defo, Bağış vb.) stok sitede "stokta" sayılmaz; açık kısımlardaki serbest stok (stok − rezerve) online mevcut sayılır. Değişiklik anında etkilidir.
- **Giriş/çıkış raf seçimi otomatiktir:** stok çıkışı/rezervasyonu satışa açık kısımlardan, toplama sırasına göre yapılır; iade girişleri satışa kapalı kısma tercih edilir; diğer girişlerde varyantın depodaki mevcut rafı, yoksa deponun uygun varsayılan rafı kullanılır.
- Depo, kısım ve raf **silinmez**; Aktif işaretini kaldırarak pasife alınır. Kodlar sonradan değiştirilemez.
- Raf barkodu tüm sistemde benzersizdir; barkod okuyucuyla raf tanımada kullanılır.
- Yazma işlemleri `inventory.manage` yetkisi ister; yetkisiz kullanıcı **Salt Okunur** görür.

## Adım adım
**Yeni depo açma ve tek kısım/raf tanımlama**
1. Stok > Depolar'da **+ Yeni Depo**'ya tıklayın; **Kod** (örn. `MAGAZA-1`), **Tip**, kaynak dilde **Ad** girin; **Oluştur**.
2. Listede depoya tıklayın; **+ Yeni Kısım** ile Kod `SATIS`, Ad "Satış" girin; **İnternete satışa açık** işaretli kalsın; **Kaydet**.
3. Kısım satırındaki ▸ ile açın, **+ Yeni Birim**'e tıklayın; Kod `GENEL`, Barkod (örn. `MAGAZA1-GENEL`) girin; **Kaydet**.

**İade kısmını siteye kapatma**
1. Depo detayında İade kısmının **İnternete Satış** rozetine tıklayın; `✗ Satışa kapalı` olur.
2. Aynı işlemi ✎ ile açılan pencerede kutuyu kaldırarak da yapabilirsiniz.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** "Depo Düzenle" penceresi açıldığında **Rezervasyon Önceliği** alanı 0 olarak gelir; kaydetmeden önce istediğiniz değeri yeniden girin, aksi halde 0 kaydedilir.

> **İpucu:** Mağaza/reyon depolarında tek kısım ve tek raf yeterlidir; raf takibi yapmıyorsanız "dummy" bir raf açıp tüm stoğu orada tutun.

> **Dikkat:** "'X' barkodu başka bir birimde kayıtlı." hatası başka depodaki bir raftan da kaynaklanabilir; barkodlar deponun değil tüm sistemin içinde benzersizdir.

## İlgili sayfalar
- [Stok Takibi](/rehber/stok/stok-takibi/)
- [Stok Hareketleri](/rehber/stok/stok-hareketleri/)

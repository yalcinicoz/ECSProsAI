---
title: Denetim Logları
route: /settings/audit-logs
group: Sistem
order: 89
summary: Panelde yapılan işlemlerin (kim, ne zaman, hangi kayıt üzerinde, hangi IP'den) salt okunur izi.
---

## Ne işe yarar
"Bu değişikliği kim, ne zaman yaptı?" sorusunun cevabı buradadır. Ekran, panelde yapılan kayıtlı işlemleri tarih
sırasıyla listeler; hiçbir şey değiştirilemez ya da silinemez. Bir sorunun kökünü ararken ya da yetki denetimi
yaparken kullanılır. Ekranın alt başlığı: "N kayıt — panelde yapılan işlemlerin izleri (salt okunur)".

Sayfa sol menüde doğrudan listelenmez; `/admin/settings/audit-logs` adresinden açılır. Ayrı izin gerekmez.

## Ekran yerleşimi
![Denetim logları — kayıt tipi süzgeci ve işlem tablosu](img/settings-audit-logs.webp)
1. **Başlık satırı** — "Denetim Logları" ve toplam kayıt sayısı.
2. **Süzgeç şeridi** — "Kayıt tipi süz (ör. User, Page)…" kutusu ve **Süz** butonu.
3. **Tablo** — en yeni işlem en üstte.
4. **Sayfalama** — `← Önceki  1 / N  Sonraki →`; sayfa boyutu 30.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| TARİH | İşlemin yapıldığı tarih ve saat. |
| İŞLEM | İşlem türü rozeti: `Oluşturma` (yeşil), `Güncelleme` (mavi), `Silme` (kırmızı), `Giriş` (gri). Bu dördü dışındaki işlemler kodlarıyla gri rozet olarak görünür (ör. `Published`, `Rollback`, `Activated`, `Deactivated`, `Previewed`). |
| KAYIT TİPİ | İşlem yapılan kaydın türü (kod biçiminde, ör. `SliderBlock`, `Slide`, `Rule`, `PublishedSnapshot`). |
| KAYIT | İlgili kaydın kimliğinin ilk 8 karakteri (`a1b2c3d4…`); aynı kayda ait satırları gözle eşleştirmeye yarar. |
| IP | İşlemi yapanın IP adresi; yoksa `—`. |

| Filtre | Ne yapar |
|---|---|
| Kayıt tipi süzgeci | Yazılan kayıt tipiyle **tam eşleşen** satırları getirir (büyük/küçük harf duyarlı, örn. `Slide`). **Enter** ya da **Süz** ile uygulanır; liste 1. sayfaya döner. Kutuyu boşaltıp Süz deyince tüm kayıtlar gelir. |

- Sıralama: tarihe göre yeniden eskiye, sabit.
- Satıra tıklamak bir şey yapmaz; detay penceresi yoktur.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Süz | Süzgeç şeridi | Kayıt tipi filtresini uygular. | — |
| ← Önceki / Sonraki → | Tablo altı | Sayfalar arasında gezinir. | Birden çok sayfa varsa |

Ekranda başka buton yoktur; kayıtlar silinemez ve dışa aktarılamaz.

## Durumlar ve iş kuralları
| Rozet | Anlamı |
|---|---|
| `Oluşturma` | Yeni kayıt eklendi. |
| `Güncelleme` | Var olan kayıt değiştirildi. |
| `Silme` | Kayıt silindi. |
| `Giriş` | Kullanıcı girişi. |
| diğer kodlar | Yayınlama (`Published`), geri alma (`Rollback`), etkinleştirme/pasifleştirme (`Activated`/`Deactivated`), önizleme (`Previewed`) gibi özel işlemler kod adıyla gösterilir. |

- Şu an denetim izi üreten işlemler ağırlıklı olarak **vitrin / sayfa düzenleyicisi** işlemleridir: blok
  (`BannerBlock`, `SliderBlock`, `StoryBannerBlock`, `CarouselProductBlock`, `InfinityProductBlock`, `TabsBlock`,
  `CollectionBlock`, `CategoriesBlock`, `BrandsBlock`, `InstagramBlock`, `AnnouncementBlock`), öğe (`Slide`,
  `StoryItem`, `TabItem`, `BlockItem`), kural (`Rule`), yayın (`PublishedSnapshot`) ve yerleşim (`PagePlacement`)
  kayıtları. Kural değişikliklerinde eski/yeni değer de saklanır.
- Denetim kaydı yazılamazsa asıl işlem yine tamamlanır; log eksikliği işlemi engellemez.
- Kayıtlar kalıcıdır; panelden temizlenmez.

## Adım adım
**Vitrindeki bir değişikliği kimin yaptığını bulma**
1. Süzgeç kutusuna değişen öğenin tipini yazın (örn. slayt için `Slide`, kural için `Rule`) ve **Süz**'e basın.
2. TARİH sütununda ilgili zamana denk gelen satırı bulun; İŞLEM rozetinden ne yapıldığını görün.
3. KAYIT sütunundaki kısa kimlikle aynı kayda ait diğer satırları (oluşturma → güncellemeler → yayın) izleyin.
4. IP sütunu hangi bağlantıdan yapıldığını gösterir.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Süzgeç tam ad ister; `slide` ya da `Slid` yazmak sonuç getirmez, `Slide` yazın. Kayıt tipi adlarını
> yukarıdaki listeden alabilirsiniz.

> **Not:** Ürün, sipariş, üye gibi diğer modüllerdeki değişiklikler için bu ekranda henüz kayıt oluşmayabilir;
> o kayıtların kendi detay sayfalarındaki geçmiş/not alanlarına bakın. Dış servis çağrılarının izi ise ayrı ekranda,
> [Entegrasyon Logları](/rehber/sistem/entegrasyon-loglari/)'ndadır.

> **Dikkat:** Bu ekran yalnız izleme içindir; bir işlemi buradan geri alamazsınız. Geri alma ilgili ekrandan
> (örn. vitrin düzenleyicisindeki yayın geçmişi) yapılır.

## İlgili sayfalar
- [Kullanıcılar](/rehber/sistem/kullanicilar/)
- [Roller ve Yetkiler](/rehber/sistem/roller-ve-yetkiler/)
- [Entegrasyon Logları](/rehber/sistem/entegrasyon-loglari/)

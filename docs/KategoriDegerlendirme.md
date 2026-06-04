❯ Filtreler ve Kategoriler tanımlanırken satış kanalı dikkate alınmıyor. Özellikle kategoriler bir satış kanalında kullanılmak istendiğinde satış kanalına
  nasıl özelleştireceğiz? Her satış kanalının sorumlusu ve satış stratejileri farklı olacak, pek çok yeni kategori eklemek isteyecektir. Yani ortalık
  kategori çöplüğüne dönecektir.
  Bir şekilde hem filtreleri hem de kategorileri satış kanalına özel yapmamız gerekiyor. Bunu nasıl sağlayabiliriz? Sadece tartışalım. Uygulamaya geçme.



● Bu klasik bir e-ticaret mimari sorusu — "catalog taxonomy" ile "commerce presentation" arasındaki gerilim. İki temel yaklaşım var:

  ---
  Yaklaşım A — İki Katmanlı Mimari (Önerim)

  Katman 1 — Global Katalog Kategorisi (mevcut Category entity)
  - Ürün organizasyonu için: satın alma, depo, raporlama
  - Katalog ekibi yönetir, sayısı az ve disiplinli
  - Ürünler buraya atanır (ürün kayıt formunda seçilen kategori)

  Katman 2 — Kanal Ticaret Katmanı (Storefront modülünde ChannelCategory)
  - Her kanal kendi kategori ağacını kurar
  - Global katalog kategorisiyle 0..N ilişkisi: "Bu kanal kategorisini şu global kategorilerden besle"
  - Kanal müdürü kendi kanalında istediği kadar kategori açar — çöplük kendi sandığında
  - Filtreler, SEO, görsel, sıralama kuralları — hepsi bu katmanda, kanala özel

  Varolan NavNode yapısı aslında buna çok yakın; onu biraz güçlendirmek yeterli olabilir (şu an navigation odaklı, commerce odaklı değil).

  ---
  Yaklaşım B — Global Kategori + Kanal Override

  Tek kategori ağacı kalır, ama her kategoriye ChannelCategoryConfig(channelId, categoryId) tablosu eklenir:
  - Görünürlük (bu kanalda aktif mi?)
  - Kanal-özel isim, SEO, filtreler
  - Ek kanal-özel sub-kategoriler

  Daha basit ama "kanallar arası kategori yönetimi" giderek karmaşıklaşır.

  ---
  Asıl Sorun: Filtreler Nereye Ait?

  Filtreler doğası gereği kanala özgü olmalı:
  - Moda kanalı → beden, renk, kumaş
  - Teknik kanalı → voltaj, garanti, uyumluluk
  - Aynı ürün, farklı kanallarda farklı filtrelerle listelenmeli

  Bu da filtrelerin global kategoriye değil, kanal kategori katmanına bağlanması gerektiğini gösteriyor — A yaklaşımını destekliyor.

  ---
  Öneri

  NavNode → ChannelCategory'e dönüştür (veya genişlet):
  - channelId (FirmPlatformId)
  - sourceType: global katalog kategorisi mi, elle tanımlı mı?
  - catalogCategoryIds[]: hangi global kategorilerden ürün çeksin
  - filterPreset: bu kanala özgü filtre tanımı
  - sortRule, seoSlug, displayNameI18n

  Bu şekilde global katalog temiz kalır, kanal müdürleri kendi alanında özgür olur, filtreler de kanala özgü hale gelir.

  Ne düşünüyorsun — iki katmanlı mı, yoksa override modeli mi daha çok oturur senin senaryona?
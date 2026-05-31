 Çoklu site yapısını şimdiden kurmak gerekiyor. Hangi sitede hangi ürünlerin satılacağı kapsamlı bir konu. Satılacak ürünlerin belirlenmesi sadece siteler
  için değil tüm satış kanalları (pazaryerleri, bayiler vb) için yönetilmesi gerekiyor.
  İÇERİK ana başlığı altında Menüler var. Menü kavramı ile kategori kavramı iyiden iyiye karıştı bir birine. Aynı şeyler olduğunu düşünüyorum.
  Yönetim kolaylığı ve kafa karışıklığını ortadan kaldırmak için kategori ve menü kavramlarını nasıl netleştirelim?
  Siteden bağımsız olarak kategorileri tanımlayıp menüleri siteye özel yapmak bir seçenek gibi duruyor. Yeni bir sitede menüler eklenirken bu menüye
  önceden tanımlı kategoriler eklenebilir, sonrasında da siteye özel sanal kategorler eklenebilir menüye. Kategori başlıkları, görselleri, SEO, paylaşım
  unsurları vb siteye özelleştirilebilir. Bu durumda temel kategori tanımları olabilidiğince basit tutulabilir; kategori adı, filtresi gibi. Özelliştirme
  siteye eklenirken yapılabilir.
  Çözüm önerin nedir?

● Kullanıcının sezgisi çok doğru. Şunu net görmek lazım:

  Menü ve kategori aynı şeyin iki farklı adı değil — biri diğerinin görünümü.

  ---
  Temel Çelişki

  Şu an iki kavram şunu yapıyor:

  ┌────────────────────────────────┬──────────┬────────┐
  │                                │ Kategori │  Menü  │
  ├────────────────────────────────┼──────────┼────────┤
  │ "Hangi ürünler?"               │ ✓        │ ✗      │
  ├────────────────────────────────┼──────────┼────────┤
  │ "Navigasyonda nerede?"         │ kısmen   │ ✓      │
  ├────────────────────────────────┼──────────┼────────┤
  │ "Nasıl görünür?" (SEO, görsel) │ kısmen   │ kısmen │
  ├────────────────────────────────┼──────────┼────────┤
  │ Siteye özel mi?                │ hayır    │ evet   │
  └────────────────────────────────┴──────────┴────────┘

  İkisi arasındaki çizgi bulanık çünkü aslında iki ayrı sorunu tek kavramla çözmeye çalışıyoruz.

  ---
  Önerim: İki Katmanlı Model

  Katman 1 — Category (Global, site-bağımsız)

  Sadece "hangi ürünler" sorusuna cevap verir. Mümkün olduğunca yalın:

  Category
  - code          # "erkek-gomlek", "yaz-koleksiyonu"
  - nameI18n      # varsayılan ad (düzenleme ekranında görünür)
  - type          # dynamic | manual | hybrid
  - filterDef     # JSONB — ürün grubu, fiyat, tarih, özellik vb.
  - parentId      # opsiyonel hafif hiyerarşi (organizasyon için)

  Bu katman katalog bilgisi — pazarlamadan bağımsız, yönetim ekranında merkezi olarak tanımlanır. Yeni bir site eklerken sıfırdan filtre yazmak yerine
  buradan seçilir.

  ---
  Katman 2 — NavNode (Kanal-spesifik)

  "Bu kategori, bu kanalda nerede ve nasıl görünür?" sorusunu cevaplar:

  NavNode
  - channelId
  - parentNavNodeId       # navigasyon ağacı burada kurulur
  - categoryId            # nullable — global kategoriye bağlı olabilir
  - sortOrder

  # Kimlik (override veya bağımsız)
  - nameOverrideI18n      # boşsa Category.nameI18n kullanılır
  - slug

  # Sunum
  - imageUrl
  - badgeLabel            # "Yeni", "İndirim" vb.

  # SEO / Paylaşım
  - seoTitleI18n
  - seoDescriptionI18n
  - canonicalUrl
  - ogImageUrl
  - ogTitleI18n

  # Tip
  - nodeType: category | link | label
    # category → categoryId üzerinden ürün listesi
    # link     → harici/dahili URL (hakkımızda, iletişim)
    # label    → kendi sayfası olmayan grup başlığı (dropdown header)

  ---
  CMS Menüsü Bu Modele Nasıl Oturuyor?

  CMS'teki "Menüler" (header, footer, yan navigasyon) da NavNode ağaçlarından başka bir şey değil. Fark sadece amaç:

  NavigationMenu
  - channelId
  - code         # "header", "footer", "sidebar"
  - nameI18n
  - rootNodes[]  # NavNode listesi

  Yani CMS menüsü, NavNode ağacının adlandırılmış bir giriş noktası. Header menüsünde "Erkek" düğümü bir label node, altındaki "Gömlekler" bir category node
  — ikisi de aynı yapı.

  Kafa karışıklığı burada çözülüyor: Artık "kategori mi menü mü" sorusu yok. Her şey NavNode; kanalda nereye bağlandığı ve ne tür olduğu nodeType ile
  ayrışıyor.

  ---
  Satış Kanalları (Marketplace, Bayi vb.)

  Bu kanallar için navigasyon ağacı gerekmeyebilir — ama "hangi ürünler bu kanalda satılır" sorusu hâlâ geçerli.

  İki mekanizma:

  ┌────────────────────────────┬────────────────────────────────────────────────────────────────────────────────┐
  │           Yöntem           │                                    Açıklama                                    │
  ├────────────────────────────┼────────────────────────────────────────────────────────────────────────────────┤
  │ NavNode tabanlı (implicit) │ Kanalda tanımlı kategorilerde görünen ürünler satılabilir — site gibi davranır │
  ├────────────────────────────┼────────────────────────────────────────────────────────────────────────────────┤
  │ ChannelProduct (explicit)  │ Navigasyondan bağımsız, ürün bazlı açık/kapalı kontrolü                        │
  └────────────────────────────┴────────────────────────────────────────────────────────────────────────────────┘

  Marketplace'ler genellikle kendi kategori yapılarına sahip olduğundan, onlar için NavNode yerine ChannelProduct daha anlamlı. Bayiler için de benzer
  şekilde.

  Channel.type enum'u bu ayrımı yönetir:

  ChannelType: site | marketplace | dealer | pos | b2b

  Site tipli kanallar tam navigasyon ağacına sahip; diğerleri sadece ürün havuzu tanımlar.

  ---
  Modül Yerleşimi

  ┌────────────────────────────────────────┬───────────────────────────────────┐
  │                 Kavram                 │               Modül               │
  ├────────────────────────────────────────┼───────────────────────────────────┤
  │ Category (global)                      │ Catalog — ürün koleksiyonu tanımı │
  ├────────────────────────────────────────┼───────────────────────────────────┤
  │ Channel                                │ Yeni Storefront                   │
  ├────────────────────────────────────────┼───────────────────────────────────┤
  │ NavNode, NavigationMenu                │ Yeni Storefront                   │
  ├────────────────────────────────────────┼───────────────────────────────────┤
  │ ChannelProduct (explicit availability) │ Yeni Storefront                   │
  └────────────────────────────────────────┴───────────────────────────────────┘

  CMS'teki mevcut "Menü" entity'si Storefront'taki NavigationMenu'ya taşınır; CMS'te sadece sayfa/şablon/içerik kalır.

  ---
  Özet

  Category (global)          →  "Erkek Gömlekler" filtresi bir kez tanımlanır
  NavNode (site A)           →  Site A'da /erkek/giyim/gomlek altında, TR SEO ile
  NavNode (site B)           →  Site B'de /men/shirts altında, EN SEO ile
  ChannelProduct (Trendyol)  →  Navigasyon yok, sadece "bu ürünler Trendyol'da aktif"

  Bu yaklaşımı beğendin mi? Onaylarsan bir sonraki adım olarak Storefront modülünü entity düzeyinde planlar ve mevcut Category ile CMS Menu yapısını buraya
  taşırız.
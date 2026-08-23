---
title: Bülten Aboneleri
route: /storefront/newsletter
group: Pazarlama
order: 50
summary: Sitedeki alt bilgi (footer) bülten formundan gelen e-posta aboneliklerinin kanal bazlı listelendiği ve arandığı salt-okunur ekran.
---

## Ne işe yarar
Müşteriler sitenin alt bilgisindeki bülten formuna e-postalarını yazarak abone olur. Bu ekran pazarlama ekibinin
abone listesini görmesi, kanala göre süzmesi ve bir adresin abone olup olmadığını kontrol etmesi içindir. Abone
ekleme, silme ya da toplu e-posta gönderme burada yapılmaz.

## Ekran yerleşimi
![Bülten Aboneleri listesi](img/storefront-newsletter.webp)
1. **Başlık ve sayaç** — "Bülten Aboneleri" ve "N kayıt — footer bülten formundan gelen abonelikler"; sağda platform seçici.
2. **Sekmeler** — `Aktif` / `Tümü`.
3. **Arama şeridi** — "E-posta ara…" + **Ara**.
4. **Tablo** ve sayfalama (20 kayıt/sayfa). Satırlar tıklanmaz.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| E-POSTA | Abone adresi. |
| PLATFORM | Aboneliğin alındığı kanal. |
| ÜYE | Abone kayıtlı üyeyse üye kimliğinin başı; değilse `Misafir`. |
| DURUM | `Aktif` (yeşil) / `Pasif` (gri) — abonelikten çıkmış kayıtlar pasiftir. |
| KAYIT TARİHİ | Abone olunan zaman. |

| Sekme / Filtre | Ne yapar |
|---|---|
| `Aktif` | Yalnız aktif abonelikler (varsayılan). |
| `Tümü` | Pasifler dahil. |
| Platform seçici | `Tüm platformlar` ya da tek kanal. |
| E-posta ara… + Ara | Adresin bir bölümüyle arar (Enter da çalışır). |

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Ara | Arama şeridi | Yazılan metni uygular ve 1. sayfaya döner. | Panele giriş yeterli. |
| Platform seçici | Sağ üst | Listeyi kanala göre süzer. | — |
| ← Önceki / Sonraki → | Liste altı | Sayfalar arasında geçiş. | — |

Bu ekranda kayıt oluşturma/düzenleme/silme yoktur.

## Durumlar ve iş kuralları
- Kayıtlar yalnız sitedeki alt bilgi bülten formundan oluşur; aynı kanalda aynı e-posta tek kayıttır.
- Abone olan kişi sitede oturum açmışsa ÜYE sütununda üye kimliği görünür; aksi hâlde `Misafir`.
- Abonelikten çıkan kayıt silinmez, `Pasif` olur ve `Tümü` sekmesinde görünür.
- Bülten aboneliği, sitede izin verilmişse pazarlama takip platformlarına "kayıt/lead" olayı olarak da iletilebilir
  (bkz. Takip & Reklam).

## Adım adım
**Bir adresin abone olup olmadığını kontrol etmek**
1. `Tümü` sekmesine geçin, e-postayı yazıp **Ara**.
2. DURUM `Aktif` ise abone; `Pasif` ise çıkmış; kayıt yoksa hiç abone olmamış.

**Kanal bazlı abone sayısı**
1. Platform seçiciden kanalı seçin; başlıktaki "N kayıt" o kanalın aktif abone sayısıdır (sekme `Aktif` iken).

## İpuçları ve sık karşılaşılan durumlar
> **Not:** Liste dışa aktarma (Excel) butonu yoktur; ihtiyaç hâlinde sayfa sayfa kopyalanır ya da geliştirme
> ekibinden rapor istenir.

## İlgili sayfalar
- [Bildirimler](/rehber/pazarlama/bildirimler/)
- [Takip & Reklam](/rehber/pazarlama/takip-ve-reklam/)

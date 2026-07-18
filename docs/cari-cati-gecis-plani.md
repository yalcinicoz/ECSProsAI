# Değer Hesapları — Cari Çatı Geçiş Planı (Seçenek B)

**Tarih:** 2026-07-18 · **Durum:** B0–B4 + B6 UYGULANDI (2026-07-18; restart bekliyor) · B5 ertelendi
**Amaç:** Üye cüzdanı, depozito, avans gibi parasal kavramları her kavram için ayrı tablo açmadan,
mevcut **Accounts modülü** (`accounts.current_accounts` + `current_account_ledgers`) çatısı altında
tek "parti + defter + hareket" yapısında yönetmek. Panel testi bulgusu **B-03** (üye detayında
Cüzdan/Puan 404) bu planın Faz B3'ünde kalıcı olarak kapanır.

---

## 0. Mevcut Durum (tespitler — 2026-07-18 incelemesi)

- **Accounts modülü** (`accounts` şeması): `current_accounts` (Code, Title,
  AccountType=customer|supplier|both, GroupId, vergi/iletişim alanları, CreditLimit, Currency,
  IsActive), `current_account_ledgers` (CurrentAccountId, Currency, Description, IsDefault,
  Balance), `current_account_groups`. API: `/api/accounts` (hesap+grup CRUD, ledger ekleme).
  Admin: Cari Kartlar + Cari Grupları sayfaları. **Hareket tablosu Accounts modülünde YOK.**
- **Finance:** Ayrı Supplier entity'si YOK — tedarikçi = current_account. Tüm belge/hareket
  kayıtları (`SupplierInvoice/Delivery/Payment/Return/Transaction`) `CurrentAccountId`'ye bağlı.
  `SupplierTransaction` (fin şeması): TransactionType, Debit, Credit, BalanceAfter,
  ReferenceType/Id, TransactionDate. ⚠️ `CreateSupplierPayment` BalanceAfter'ı "son hareket −
  tutar" diye elle hesaplıyor ve `ledger.Balance` HİÇ güncellenmiyor — yarış koşuluna açık kalıp;
  ortak çekirdek bunu da düzeltecek.
- **CRM:** `crm_wallets`, `crm_wallet_transactions`, `crm_loyalty_accounts`,
  `crm_loyalty_transactions`, `crm_member_credits` tabloları migration'la mevcut ama **hep boş**
  (hesap açan/hareket yazan kod yok). Okuma uçları çalışıyor: admin
  `GET /api/crm/members/{id}/wallet|loyalty` (satır yoksa 404), store `GET wallet` +
  Hesabım SSR özeti (puan 0 fallback).
- **Order:** `ReturnRefund.RefundMethod` admin'de 'wallet' seçilebiliyor ama cüzdana yazan akış yok.
- **Veri aktarım yükü:** Cüzdan/puan tarafında SIFIR (tablolar boş). `fin.supplier_transactions`'ta
  canlı veri olabilir → Faz B5 öncesi satır sayısı kontrol edilecek.

## 1. Hedef Mimari

| Katman | Tablo | Rol |
|---|---|---|
| **Parti** | `current_accounts` (+`OwnerType`, +`OwnerId`) | Kişi/kurum kimliği: harici cari, üye, firma. Üye cüzdanı = üyenin cari hesabı |
| **Kavram** | `current_account_ledgers` (+`ConceptCode`) | Hesap altındaki her parasal kavram bir defter: `cari`, `wallet`, `deposit`, `advance`… |
| **Hareket** | **YENİ** `accounts.current_account_transactions` | Tüm kavramların ortak hareket defteri (LedgerId, TransactionType, Debit, Credit, BalanceAfter, ReferenceType, ReferenceId, Description, TransactionDate) |

Kurallar:
- `ledger.Balance` tek doğruluk kaynağı; hareket ekleme **atomik tek kapıdan**
  (`PostAccountTransactionCommand`): satır kilidi (SELECT … FOR UPDATE) veya EF concurrency token
  ile Balance güncelle + BalanceAfter yaz. Elle bakiye hesabı hiçbir modülde kalmaz.
- Hareketler silinmez/düzeltilmez; düzeltme = ters kayıt (storno).
- Kavram sözlüğü Core lookup'ı olarak tutulur (`account_concept`) → panelden görünür/yönetilir (K16).
- Unique'ler: `(OwnerType, OwnerId)` (OwnerId dolu iken), `(CurrentAccountId, ConceptCode, Currency)`.
- **Kapsam dışı:** Loyalty/puan (vade+birim farklı, kendi tablosunda kalır), GiftCard (kod bazlı ürün),
  MemberCredit emekliliği (ayrı karar), çoklu para birimi kur dönüşümü.

## 2. Fazlar

### Faz B0 — Kararlar ✅ (2026-07-18, kullanıcı onayı)
- [x] Kavram listesi v1: `cari`, `wallet` (+ ufukta: `deposit`, `advance` — yalnız lookup'a kod eklenerek)
- [x] Üye hesabı açılma anı: **ilk harekette lazy** (PostAccountTransaction içinde EnsureAccount)
- [x] Üye hesap kodu üretimi: **`M-{6 haneli sıra}`** (otomatik; advisory lock ile serileşir; ilk hesap M-000001 açıldı)
- [x] Yön sözleşmesi: cüzdanda **Credit = bakiye artar**, **Debit = azalır**; negatif bakiye
      varsayılan reddedilir (`AllowNegativeBalance` yalnız storno gibi özel durumlarda)
- [x] Modüller arası çağrı: **MediatR command, doğrudan proje referansı** (Crm/Order.Application → Accounts.Application)

### Faz B1 — Şema ✅ (2026-07-18)
- [x] `current_accounts` += `OwnerType` (text, default 'external') + `OwnerId` (uuid null)
      + partial unique index `(OwnerType, OwnerId) WHERE OwnerId IS NOT NULL`
- [x] `current_account_ledgers` += `ConceptCode` (text, default 'cari')
      + unique `(CurrentAccountId, ConceptCode, Currency)`
- [x] YENİ `current_account_transactions` (yukarıdaki kolonlar + index: LedgerId+CreatedAt)
- [ ] Core seed: `account_concept` lookup değerleri — ERTELENDİ (kavram kodları şimdilik kodda sabit;
      panelde filtre seçenekleri elle tanımlı — yeni kavram eklerken lookup'la birlikte yapılacak)
- [x] `dotnet ef database update` (kural: migration yetmez, DB update şart) — additive olduğundan
      çalışan binary etkilenmez

### Faz B2 — Accounts uygulama çekirdeği ✅ (2026-07-18)
- [x] Hesap/defter lazy açılışı `PostAccountTransactionCommand` İÇİNDE (ayrı Ensure command'ine
      gerek kalmadı; M-kod üretimi advisory lock ile serileşiyor)
- [x] `PostAccountTransactionCommand` (atomik bakiye — pg_advisory_xact_lock; storno ters kayıt desteği)
- [x] `GetOwnerLedgerQuery` (bakiye + son hareketler) + `GetAccountTransactionsQuery` (sayfalı döküm)
- [x] İzole API testleri (5051 staging) + 10 paralel yazım yarış koşulu testi: kayıpsız, zincir 61→70 sıralı

### Faz B3 — CRM cüzdan geçişi = B-03 kapanışı ✅ (2026-07-18)
- [x] `GetMemberWalletQueryHandler` → Accounts'tan okur (OwnerType='member', Concept='wallet').
      **`WalletDto` şekli AYNEN korunur** → admin MemberDetailPage, `StoreAccountController.GetWallet`,
      Hesabım SSR **hiç değişmez**
- [x] Hesap/ledger yoksa **404 yerine 0 bakiyeli başarı DTO'su** (boş hareket listesi)
- [x] `crm_wallets` + `crm_wallet_transactions` DEPRECATED işaretlenir (kod referansı kalmayınca
      ayrı temizlik migration'ı ile drop — acele değil; tablolar zaten boş, veri aktarımı YOK)

### Faz B4 — İlk yazma akışları ✅ (2026-07-18)
- [x] İade: complete-refund'da `RefundMethod='wallet'` ise `PostAccountTransaction`
      (Credit, ReferenceType='return_refund', ReferenceId=refundId) — ilk gerçek cüzdan hareketi
- [x] Admin manuel düzeltme: `POST /api/crm/members/{id}/wallet/adjust` (veya accounts ucu) —
      TransactionType='manual_adjustment', açıklama zorunlu
- [x] 🔭 İşaretlenen gelecek işler (bu planda YAPILMAZ): checkout'ta cüzdanla ödeme, depozito akışı

### Faz B5 — Finance hareket birleşmesi (1 gün; ERTELENEBİLİR — B1-B4'ten bağımsız)
- [ ] `fin.supplier_transactions` satır sayısı/veri kontrolü (canlı)
- [ ] Yeni tedarikçi hareket yazımları (`CreateSupplierPayment` vb.) → `PostAccountTransaction`
      (elle BalanceAfter hesabı kalkar, yarış koşulu kapanır)
- [ ] Mevcut veri tek seferlik kopya (LegacyReferenceId sakla) + `GetSupplierTransactions` ortak
      tablodan okur; eski tablo DEPRECATED
- [ ] Tedarikçi cari hesaplarında OwnerType='external' kalır (değişiklik gerekmez)

### Faz B6 — Panel senkronu ✅ (2026-07-18)
- [x] Cari Kartlar listesi: OwnerType/kavram filtresi (Üye cüzdanları ayrı görünüm; üye carisi
      üye detayına link)
- [x] Cari detay (hareket dökümü + defter rozetleri; manuel hareket üye tarafında MemberDetail üzerinden): ledger listesi + hareket dökümü + manuel hareket ekleme
- [x] MemberDetailPage cüzdan kartı: bakiye + son hareketler + hareket ekleme (mevcut '—' yerine)
- [ ] PanelTests: bir sonraki koşuya B-03 regresyon adımı (üye detayında Cüzdan 404 DEĞİL, 0 ₺ veya
      bakiye görünmeli)

## 3. Etkilenen Başlıca Dosyalar

| Alan | Dosyalar |
|---|---|
| Şema | `Accounts.Domain/Entities/*` (+OwnerType/OwnerId/ConceptCode, yeni `CurrentAccountTransaction`), `AccountConfigurations.cs`, yeni migration |
| Çekirdek | `Accounts.Application/Commands|Queries/*` (yeni Ensure/Post/Get'ler), `IAccountsDbContext` |
| CRM | `GetMemberWalletQuery.cs` (handler değişir, DTO aynı), `Crm.Application` → `Accounts.Application` proje referansı |
| Order | `CompleteRefund` command handler'ı (wallet dalı), `Order.Application` → `Accounts.Application` referansı |
| Finance (B5) | `CreateSupplierPaymentCommand.cs`, `GetSupplierTransactionsQuery.cs` |
| API | `CrmController` (davranış: 404→200), `AccountsController` (hareket uçları), `Program.cs` değişmez (Accounts kayıtlı) |
| Admin | `AccountsPage/AccountDetail` (hareketler), `crm/MemberDetailPage.tsx` (cüzdan kartı) |

## 4. Riskler / Dikkat

- **Yarış koşulu:** finance'daki "son hareket − tutar" kalıbı KOPYALANMAZ; tek kapı
  `PostAccountTransaction` + satır kilidi. Eşzamanlılık testi şart.
- **Modül bağımlılığı:** Accounts → (hiçbir modül). Crm/Order/Finance → Accounts.Application tek yön;
  döngü oluşmaz. Domain event alternatifi (OrderRefundCompletedEvent → Accounts handler) B0'da
  tartışılabilir; mevcut in-process event kalıbıyla uyumlu.
- **Deploy sırası:** B1 migration additive → publish → restart (restart'ı kullanıcı çalıştırır).
  Dev'de izole test; canlıda deneme-yanılma yok.
- **Soft delete/IsDeleted:** hareket tablosunda soft delete kullanılmaz varsayımıyla raporlama
  basit kalır; BaseEntity gereği kolonlar var ama silme akışı tanımlanmaz (storno kuralı).
- **Currency:** ledger başına tek para birimi; TRY dışı ihtiyaç çıkarsa yeni ledger açılır.
- **B-02 (dashboard sayaçları) bu plandan bağımsızdır** — ayrı küçük iş.

## 5. Kapanış Kriterleri

1. Üye detayında Cüzdan alanı 404 almadan bakiye gösteriyor (0 dahil) — B-03 kapalı.
2. İade "cüzdana" tamamlandığında üyenin cüzdan bakiyesi artıyor; hareket dökümünde
   `return_refund` referansıyla görünüyor (sitede Hesabım > Cüzdan'da da aynı bakiye).
3. Panelden manuel cüzdan düzeltmesi yapılabiliyor ve hareket olarak izleniyor (K16 sağlandı).
4. Yeni kavram eklemek = lookup'a kod + `EnsureOwnerAccount(concept)` çağrısı; yeni tablo/migration
   GEREKMİYOR (hedefin kanıtı).

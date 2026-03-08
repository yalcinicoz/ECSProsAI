# E-Ticaret Altyapı Projesi - Teknoloji Stack Dokümanı

**Doküman Versiyonu:** 1.0  
**Son Güncelleme:** Ocak 2025  
**Durum:** Onaylandı

---

## 1. Genel Bakış

Bu doküman, e-ticaret altyapı projesi için seçilen teknolojileri ve seçim gerekçelerini içermektedir.

### 1.1 Ekip Yapısı
- 3 kişilik fullstack geliştirme ekibi
- Mevcut deneyim: .NET 8, MVC, Bootstrap
- Yeni öğrenilecek: React, TypeScript

### 1.2 Altyapı
- Tamamen bulut tabanlı sunucular
- S3 uyumlu object storage (sağlayıcı bağımsız)

---

## 2. Backend

### 2.1 Ana Framework: .NET 8 (LTS)

**Seçim Gerekçeleri:**
- Ekibin mevcut deneyimi
- Long Term Support (LTS) - uzun vadeli destek garantisi
- Yüksek performans
- Güçlü tip sistemi ve derleme zamanı hata kontrolü
- Kapsamlı ekosistem ve topluluk desteği

### 2.2 Mimari Yaklaşım: Modüler Monolith

**Seçim Gerekçeleri:**
- 3 kişilik ekip için microservices başlangıçta fazla karmaşık
- Modüller arası net sınırlar tanımlanabilir
- İleride gerekirse microservices'e evrilebilir
- Deployment ve debugging daha basit
- Tek veritabanı ile başlanabilir, sonra ayrılabilir

**Modül Önerileri (ön görüş):**
- Core (ortak yapılar, utilities)
- Identity (kullanıcı, yetkilendirme)
- Catalog (ürün, kategori, özellikler)
- Inventory (stok, depo, transfer)
- Pricing (fiyatlandırma, kampanyalar)
- Order (sipariş, ödeme)
- Fulfillment (toplama, paketleme, kargo)
- Integration (pazaryeri, kargo, entegratör adaptörleri)
- Reporting (raporlama, analytics)
- Finance (cari, fatura, ödeme takibi)

---

## 3. Frontend

### 3.1 Ana Framework: React + TypeScript

**Seçim Gerekçeleri:**
- Reaktif arayüzler için güçlü (depo operasyonları, anlık güncellemeler)
- Component bazlı mimari - yeniden kullanılabilirlik
- Çok geniş ekosistem ve topluluk
- TypeScript ile tip güvenliği, refactoring kolaylığı
- Çok dilli yapı için olgun i18n kütüphaneleri
- İş ilanlarında yaygınlık - gelecekte ekip genişletme kolaylığı

### 3.2 Önerilen Kütüphaneler (değerlendirme aşamasında)

| İhtiyaç | Aday Kütüphaneler |
|---------|-------------------|
| State Management | Redux Toolkit, Zustand, veya React Query |
| UI Components | Ant Design, MUI, veya Tailwind + Headless UI |
| Routing | React Router |
| Forms | React Hook Form |
| i18n | react-i18next |
| HTTP Client | Axios veya fetch + React Query |
| Tablolar | TanStack Table (eski adı React Table) |
| Grafikler | Recharts, Chart.js |

*Not: Kütüphane seçimleri tasarım aşamasında netleştirilecek.*

---

## 4. Veritabanı

### 4.1 Ana Veritabanı: PostgreSQL

**Seçim Gerekçeleri:**
- Açık kaynak, lisans maliyeti yok
- Bulut maliyetleri SQL Server'a göre düşük
- Güçlü JSON/JSONB desteği (yarı-yapılandırılmış veriler için)
- Partitioning desteği (büyük tablolar için)
- Full-text search (başlangıç için yeterli)
- .NET entegrasyonu sorunsuz (Npgsql, EF Core)
- Enterprise seviyede güvenilirlik ve performans

### 4.2 ORM: Entity Framework Core

**Seçim Gerekçeleri:**
- .NET ekosisteminin standart ORM'i
- Code-first veya database-first yaklaşım
- Migration desteği
- LINQ ile güçlü sorgulama
- PostgreSQL için tam destek (Npgsql.EntityFrameworkCore.PostgreSQL)

---

## 5. Gerçek Zamanlı İletişim

### 5.1 Teknoloji: SignalR

**Seçim Gerekçeleri:**
- .NET ekosisteminin parçası, entegrasyon kolay
- WebSocket, Server-Sent Events, Long Polling fallback desteği
- React ile kullanımı basit (@microsoft/signalr paketi)
- Ölçeklendirme için Redis backplane desteği

**Kullanım Alanları:**
- Depo operasyonlarında sesli yönlendirmeler
- Anlık stok güncellemeleri
- Sipariş durumu değişiklikleri
- Dashboard'larda canlı veriler
- Bildirim sistemi

---

## 6. Kuyruk Sistemi

### 6.1 Başlangıç: Redis Streams

**Seçim Gerekçeleri:**
- Redis zaten cache için kullanılacak, ek bileşen yok
- Basit kuyruk senaryoları için yeterli
- Düşük latency
- Öğrenme eğrisi düşük

**Kullanım Alanları:**
- Pazaryeri stok senkronizasyonu
- Fatura oluşturma işlemleri
- E-posta/SMS gönderimi
- Toplu veri işlemleri

### 6.2 Gelecek Opsiyon: RabbitMQ

Eğer kuyruk ihtiyaçları karmaşıklaşırsa (routing, dead letter queues, delayed messages) RabbitMQ'ya geçiş değerlendirilebilir.

---

## 7. Önbellek

### 7.1 Teknoloji: Redis

**Seçim Gerekçeleri:**
- Endüstri standardı
- Çok hızlı (in-memory)
- Çeşitli veri yapıları desteği
- .NET entegrasyonu kolay (StackExchange.Redis)
- Kuyruk (Streams) ve cache tek araçta

**Kullanım Alanları:**
- Oturum yönetimi
- Sık erişilen veriler (ürün bilgileri, fiyatlar)
- Kampanya kuralları cache
- API rate limiting
- SignalR backplane (ölçeklendirme için)

---

## 8. Dosya Depolama

### 8.1 Yaklaşım: S3 Uyumlu Object Storage

**Seçim Gerekçeleri:**
- S3 API endüstri standardı
- Sağlayıcı bağımsızlığı - kod değişmeden sağlayıcı değiştirilebilir
- Ölçeklenebilir, dayanıklı
- CDN entegrasyonu kolay

**Olası Sağlayıcılar:**
- AWS S3
- Google Cloud Storage
- Azure Blob Storage (S3 uyumlu erişim)
- DigitalOcean Spaces
- Cloudflare R2
- MinIO (self-hosted opsiyon)
- Hetzner Object Storage

*Not: Sağlayıcı seçimi maliyet, lokasyon (KVKK) ve mevcut altyapıya göre yapılacak.*

**Kullanım Alanları:**
- Ürün fotoğrafları
- Fatura PDF'leri
- Rapor çıktıları
- Import/export dosyaları
- Yedeklemeler

---

## 9. Arama

### 9.1 Teknoloji: Elasticsearch

**Seçim Gerekçeleri:**
- Çok hızlı full-text search
- Fuzzy search (yazım hatalarını tolere etme)
- Faceted search (filtreli arama, kategori sayıları)
- Autocomplete / suggest önerileri
- Çok dilli arama desteği (Türkçe stemming, analyzer)
- Ürün sayısı arttıkça performans avantajı
- Synonym desteği (eş anlamlı kelimelerle arama)
- Boosting (belirli alanları ön plana çıkarma)

**Kullanım Alanları:**
- Ürün araması (site ve panel)
- Sipariş arama
- Müşteri arama
- Stok kartı arama
- Log analizi (opsiyonel)

**Dikkat Edilecekler:**
- Elasticsearch ve PostgreSQL arasında veri senkronizasyonu
- Index güncelleme stratejisi (anlık mı, periyodik mi)
- Türkçe için doğru analyzer konfigürasyonu

---

## 10. Yapay Zeka Entegrasyonu (Gelecek Faz)

### 10.1 Raporlama için AI

**Amaç:** Kullanıcının doğal dilde rapor talep edebilmesi.

**Olası Çözümler:**
- OpenAI API (GPT-4)
- Azure OpenAI Service
- Self-hosted açık kaynak modeller (Llama, Mistral)

**Dikkat Edilecekler:**
- Veri güvenliği (kullanıcı yetkisi dışına çıkılmamalı)
- SQL injection riski
- Performans (kötü sorguların kontrolü)
- Maliyet yönetimi

*Not: Detaylar raporlama modülü tasarımında ele alınacak.*

---

## 11. Geliştirme ve DevOps Araçları

### 11.1 Versiyon Kontrolü
- Git
- GitHub veya GitLab veya Azure DevOps (ekip tercihine göre)

### 11.2 CI/CD
- GitHub Actions veya GitLab CI veya Azure Pipelines
- Docker container'ları ile deployment

### 11.3 Konteynerizasyon
- Docker
- Docker Compose (geliştirme ortamı için)
- Kubernetes (production ölçeklendirme için, opsiyonel)

### 11.4 Monitoring ve Logging
- Serilog (.NET logging)
- Seq veya Elasticsearch + Kibana (log aggregation)
- Prometheus + Grafana veya Application Insights (metrics)

*Not: DevOps araçları ekip deneyimi ve bulut sağlayıcıya göre netleştirilecek.*

---

## 12. Özet Tablo

| Katman | Teknoloji | Durum |
|--------|-----------|-------|
| Backend Framework | .NET 8 | Onaylandı |
| Backend Mimari | Modüler Monolith | Onaylandı |
| Frontend Framework | React + TypeScript | Onaylandı |
| Veritabanı | PostgreSQL | Onaylandı |
| ORM | Entity Framework Core | Onaylandı |
| Gerçek Zamanlı | SignalR | Onaylandı |
| Kuyruk | Redis Streams | Onaylandı |
| Önbellek | Redis | Onaylandı |
| Dosya Depolama | S3 Uyumlu Storage | Onaylandı |
| Arama | Elasticsearch | Onaylandı |
| AI Raporlama | OpenAI / Azure OpenAI | Gelecek faz |

---

## 13. Sonraki Adımlar

1. Veritabanı tasarımı
2. Backend modül yapısının detaylandırılması
3. API tasarımı
4. Frontend component mimarisi
5. Entegrasyon katmanı tasarımı
6. Geliştirme ortamının kurulumu

---

## Revizyon Geçmişi

| Versiyon | Tarih | Değişiklik |
|----------|-------|------------|
| 1.0 | Ocak 2025 | İlk versiyon - Teknoloji seçimleri onaylandı |
| 1.1 | Ocak 2025 | Arama için doğrudan Elasticsearch kullanılacak şekilde güncellendi |

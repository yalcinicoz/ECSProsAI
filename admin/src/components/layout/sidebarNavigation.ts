import { matchPath } from 'react-router-dom'

export interface NavItem {
  label: string
  to: string
  icon: string
  badge?: number
  permission?: string
  /** Mevcut liste URL'sini paylaşmayan detay/yardımcı sayfalar. Yeni route oluşturmaz. */
  activePatterns?: string[]
}
export interface NavSection {
  id: string
  label: string
  icon: string
  direct?: boolean
  items: NavItem[]
}

// Mevcut NAV_SECTIONS envanteri; yalnız iş amacına göre yeniden gruplandı.
export const NAV_SECTIONS: NavSection[] = [
  { id: 'general', label: 'Genel', icon: 'gauge', direct: true, items: [
    { label: 'Dashboard', to: '/', icon: 'gauge' },
    { label: 'Proje Talepleri', to: '/requests', icon: 'inbox' },
  ]},
  { id: 'products', label: 'Ürün Yönetimi', icon: 'box', items: [
    { label: 'Ürün Kartları',      to: '/catalog/products',        icon: 'box' },
    { label: 'Tedarikçi Gönderimleri', to: '/catalog/product-submissions', icon: 'inbox', permission: 'catalog.products.manage' },
    { label: 'Toplu Resim Yükleme',to: '/catalog/bulk-images',     icon: 'images' },
  ]},
  { id: 'channels', label: 'Satış Kanalları', icon: 'store', items: [
    { label: 'Kanal Ürünleri', to: '/storefront/channel-products', icon: 'layout' },
    { label: 'Kanal Kategorileri', to: '/storefront/channel-categories', icon: 'layout' },
    { label: 'Menü Yerleşimi', to: '/storefront/menu-placement', icon: 'layout' },
    { label: 'Kanal Kapsamı', to: '/storefront/channel-scope', icon: 'filter' },
    { label: 'Pazaryerleri', to: '/marketplaces',   icon: 'store' },
  ]},
  { id: 'orders', label: 'Sipariş', icon: 'shoppingbag', items: [
    { label: 'Siparişler', to: '/orders',           icon: 'shoppingbag' },
    { label: 'İadeler',    to: '/orders/returns',   icon: 'rotateccw' },
    { label: 'Faturalar',  to: '/orders/invoices',  icon: 'filetext' },
    { label: 'Teklifler',  to: '/orders/quotes',    icon: 'handshake' },
    { label: 'POS',           to: '/pos/sales',             icon: 'creditcard', activePatterns: ["/pos/registers"] },
  ]},
  { id: 'operations', label: 'Depo & Operasyon', icon: 'boxes', items: [
    { label: 'Toplama Planlama', to: '/fulfillment/picking-plans', icon: 'boxes', activePatterns: ["/fulfillment/tasks/:id","/fulfillment/fast-lane/:planId","/fulfillment/sorting/:planId","/fulfillment/sorting-wall/:planId"] },
    { label: 'Ürün Toplama',  to: '/fulfillment/my-picking',    icon: 'scan' },
    { label: 'Masa İzleme',   to: '/fulfillment/desks',         icon: 'monitor', activePatterns: ["/fulfillment/desk/:deskId","/fulfillment/packing-stations"] },
    { label: 'Kargo Yönlendirme', to: '/fulfillment/cargo-reroute', icon: 'truck' },
    { label: 'Stok Takibi',      to: '/inventory/stocks',     icon: 'boxes' },
    { label: 'Stok Hareketleri', to: '/inventory/transfers',  icon: 'refreshcw' },
  ]},
  { id: 'procurement', label: 'Tedarik', icon: 'inbox', items: [
    { label: 'Satın Almalar', to: '/procurement/purchase-orders', icon: 'inbox', permission: 'procurement.manage' },
    { label: 'Mal Kabul', to: '/procurement/receipts', icon: 'box', permission: 'procurement.manage' },
    { label: 'Etiket Basımı', to: '/procurement/labels', icon: 'filetext', permission: 'procurement.manage' },
    { label: 'Sayım / Teslim', to: '/procurement/sorting', icon: 'layers', permission: 'procurement.sort' },
    { label: 'Tedarik Raporu', to: '/procurement/report', icon: 'gauge', permission: 'procurement.manage' },
  ]},
  { id: 'crm', label: 'Müşteriler / CRM', icon: 'users', items: [
    { label: 'Üyeler', to: '/crm/members',       icon: 'users' },
    { label: 'Müşteri İlişkileri', to: '/crm/tickets', icon: 'clipboard' },
    { label: 'İletişim Mesajları', to: '/storefront/contact-messages', icon: 'mail' },
    { label: 'Ürün Soruları', to: '/storefront/questions', icon: 'inbox' },
    { label: 'Yorum Moderasyonu', to: '/storefront/reviews', icon: 'layout' },
  ]},
  { id: 'finance', label: 'Cari & Finans', icon: 'creditcard', items: [
    { label: 'Cari Kartlar', to: '/accounts',        icon: 'users' },
    { label: 'Komisyon Yönetimi', to: '/commission',  icon: 'percent' },
    { label: 'Finans',        to: '/finance/supplier-invoices', icon: 'clipboard' },
  ]},
  { id: 'marketing', label: 'Pazarlama', icon: 'percent', items: [
    { label: 'Kampanyalar',  to: '/promotion/campaigns',  icon: 'percent' },
    { label: 'Kuponlar',     to: '/promotion/coupons',    icon: 'ticket' },
    { label: 'Hediye Kartı', to: '/orders/gift-cards',    icon: 'gift' },
    { label: 'Bildirimler',      to: '/storefront/notifications', icon: 'bell' },
    { label: 'Bülten Aboneleri', to: '/storefront/newsletter',    icon: 'mail' },
    { label: 'Takip & Reklam',   to: '/marketing/tracking',       icon: 'plug' },
  ]},
  { id: 'content', label: 'Vitrin & İçerik', icon: 'layout', items: [
    { label: 'Vitrin Yönetimi', to: '/storefront/pages', icon: 'layout' },
    { label: 'Koleksiyon Moderasyonu', to: '/storefront/collections', icon: 'layout' },
    { label: 'Sayfalar',  to: '/cms/pages', icon: 'filetext' },
    { label: 'Ürün Kartı', to: '/storefront/product-card', icon: 'layout' },
    { label: 'Takip & Çerez', to: '/storefront/tracking-consent', icon: 'layout' },
  ]},
  { id: 'definitions', label: 'Tanımlar', icon: 'sliders', items: [
    { label: 'Ürün Grupları',      to: '/catalog/product-groups',        icon: 'layers' },
    { label: 'Özellik Tipleri',    to: '/catalog/attribute-types', icon: 'sliders' },
    { label: 'Katalog Ayarları',   to: '/catalog/settings',                  icon: 'settings', permission: 'catalog.settings.manage' },
    { label: 'Depolar',          to: '/inventory/warehouses', icon: 'warehouse' },
    { label: 'Cari Grupları', to: '/accounts/groups', icon: 'usersround' },
    { label: 'Üye Grupları', to: '/crm/member-groups', icon: 'usersround' },
    { label: 'Fatura Serileri', to: '/orders/invoice-series', icon: 'sliders' },
    { label: 'Numara Serileri', to: '/orders/number-series', icon: 'sliders' },
    { label: 'Kargo Bölgeleri', to: '/orders/cargo-zones', icon: 'truck' },
    { label: 'Kampanya Tipleri', to: '/promotion/campaign-types', icon: 'layers' },
    { label: 'Etiket Şablonları', to: '/settings/label-templates', icon: 'scan', permission: 'procurement.manage' },
    { label: 'Platform Tipleri',to: '/settings/platform-types', icon: 'globe' },
    { label: 'Bildirim Şablonları', to: '/settings/notification-templates', icon: 'bell' },
    { label: 'Entegrasyonlar', to: '/settings/integration-services', icon: 'plug', permission: 'definition.manage' },
    { label: 'Satış Kanalı Tanımları', to: '/settings/channels',       icon: 'shoppingbag' },
  ]},
  { id: 'system', label: 'Sistem', icon: 'settings', items: [
    { label: 'Entegrasyon Logları',to: '/integrations/logs',     icon: 'plug' },
    { label: 'Firmalar',        to: '/settings/firms',          icon: 'building2' },
    { label: 'Çeviriler',       to: '/settings/translations',   icon: 'languages' },
    { label: 'Migration',       to: '/settings/migration',      icon: 'databasezap' },
    { label: 'Kullanıcılar',         to: '/settings/users',          icon: 'settings', activePatterns: ["/settings/roles","/settings/audit-logs","/settings/languages","/settings/lookup-types"] },
  ]},
]

/** React Router basename'i useLocation tarafından kaldırılır; bu fonksiyon admin-relative yol alır. */
export function findActiveItem(pathname: string, sections: NavSection[] = NAV_SECTIONS): NavItem | undefined {
  const items = sections.flatMap((section) => section.items)
  const exact = items.find((item) => matchPath({ path: item.to, end: true }, pathname))
  if (exact) return exact
  // Segment sınırı React Router tarafından kontrol edilir; /orders-extra, /orders değildir.
  const prefix = items
    .filter((item) => item.to !== '/' && matchPath({ path: item.to, end: false }, pathname))
    .sort((a, b) => b.to.length - a.to.length)[0]
  if (prefix) return prefix
  return items.find((item) => item.activePatterns?.some((path) => matchPath({ path, end: true }, pathname)))
}

export function permittedSections(sections: NavSection[], hasPermission: (permission: string) => boolean): NavSection[] {
  return sections.map((section) => ({
    ...section, items: section.items.filter((item) => !item.permission || hasPermission(item.permission)),
  })).filter((section) => section.items.length > 0)
}

function normalizeSearch(value: string): string {
  return value.trim().toLocaleLowerCase('tr').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/ı/g, 'i').replace(/\s+/g, ' ')
}

/** Yalnız yetki filtresinden geçmiş gruplar verilmelidir. */
export function searchSections(sections: NavSection[], search: string): NavSection[] {
  const query = normalizeSearch(search)
  if (!query) return sections
  return sections.map((section) => ({
    ...section,
    items: section.items.filter((item) => normalizeSearch(item.label).includes(query) || normalizeSearch(section.label).includes(query)),
  })).filter((section) => section.items.length > 0)
}

/** Yalnız görünür öğelerde çağrılır; kapalı gruplar sayı toplamı değil boolean indicator kullanır. */
export function itemBadge(item: NavItem, questions: number, tickets: number): number | undefined {
  if (item.to === '/storefront/questions' && questions > 0) return questions
  if (item.to === '/crm/tickets' && tickets > 0) return tickets
  return item.badge
}

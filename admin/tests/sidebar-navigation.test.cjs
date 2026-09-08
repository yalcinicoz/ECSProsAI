const { test } = require('node:test')
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const vm = require('node:vm')
const ts = require('typescript')

function load(file, mocks = {}) {
  const source = fs.readFileSync(path.join(__dirname, '../src/components/layout', file), 'utf8')
  const output = ts.transpileModule(source, { compilerOptions: {
    module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText
  const exports = {}
  vm.runInNewContext(output, { exports, require: (name) => mocks[name] ?? require(name) })
  return exports
}
const nav = load('sidebarNavigation.ts')
// Refactor öncesi envanter: URL, icon, permission ve statik badge kaybını yakalar.
const baseline = [
  { label: 'Dashboard', to: '/', icon: 'gauge' },
  { label: 'Proje Talepleri', to: '/requests', icon: 'inbox' },
  { label: 'Ürün Kartları',      to: '/catalog/products',        icon: 'box' },
  { label: 'Tedarikçi Gönderimleri', to: '/catalog/product-submissions', icon: 'inbox', permission: 'catalog.products.manage' },
  { label: 'Toplu Resim Yükleme',to: '/catalog/bulk-images',     icon: 'images' },
  { label: 'Özellik Tipleri',    to: '/catalog/attribute-types', icon: 'sliders' },
  { label: 'Ürün Grupları',      to: '/catalog/product-groups',        icon: 'layers' },
  { label: 'Kanal Kategorileri', to: '/storefront/channel-categories', icon: 'layout' },
  { label: 'Menü Yerleşimi', to: '/storefront/menu-placement', icon: 'layout' },
  { label: 'Ürün Kartı', to: '/storefront/product-card', icon: 'layout' },
  { label: 'Takip & Çerez', to: '/storefront/tracking-consent', icon: 'layout' },
  { label: 'Kanal Ürünleri', to: '/storefront/channel-products', icon: 'layout' },
  { label: 'Kanal Kapsamı', to: '/storefront/channel-scope', icon: 'filter' },
  { label: 'Koleksiyon Moderasyonu', to: '/storefront/collections', icon: 'layout' },
  { label: 'Yorum Moderasyonu', to: '/storefront/reviews', icon: 'layout' },
  { label: 'Ürün Soruları', to: '/storefront/questions', icon: 'inbox' },
  { label: 'Vitrin Yönetimi', to: '/storefront/pages', icon: 'layout' },
  { label: 'Katalog Ayarları',   to: '/catalog/settings',                  icon: 'settings', permission: 'catalog.settings.manage' },
  { label: 'Siparişler', to: '/orders',           icon: 'shoppingbag' },
  { label: 'Toplama Planlama', to: '/fulfillment/picking-plans', icon: 'boxes' },
  { label: 'Ürün Toplama',  to: '/fulfillment/my-picking',    icon: 'scan' },
  { label: 'Masa İzleme',   to: '/fulfillment/desks',         icon: 'monitor' },
  { label: 'Kargo Yönlendirme', to: '/fulfillment/cargo-reroute', icon: 'truck' },
  { label: 'İadeler',    to: '/orders/returns',   icon: 'rotateccw' },
  { label: 'Faturalar',  to: '/orders/invoices',  icon: 'filetext' },
  { label: 'Fatura Serileri', to: '/orders/invoice-series', icon: 'sliders' },
  { label: 'Teklifler',  to: '/orders/quotes',    icon: 'handshake' },
  { label: 'Pazaryerleri', to: '/marketplaces',   icon: 'store' },
  { label: 'Numara Serileri', to: '/orders/number-series', icon: 'sliders' },
  { label: 'Kargo Bölgeleri', to: '/orders/cargo-zones', icon: 'truck' },
  { label: 'Cari Kartlar', to: '/accounts',        icon: 'users' },
  { label: 'Komisyon Yönetimi', to: '/commission',  icon: 'percent' },
  { label: 'Cari Grupları', to: '/accounts/groups', icon: 'usersround' },
  { label: 'Üyeler', to: '/crm/members',       icon: 'users' },
  { label: 'Müşteri İlişkileri', to: '/crm/tickets', icon: 'clipboard' },
  { label: 'Gruplar', to: '/crm/member-groups', icon: 'usersround' },
  { label: 'İletişim Mesajları', to: '/storefront/contact-messages', icon: 'mail' },
  { label: 'Depolar',          to: '/inventory/warehouses', icon: 'warehouse' },
  { label: 'Stok Takibi',      to: '/inventory/stocks',     icon: 'boxes' },
  { label: 'Stok Hareketleri', to: '/inventory/transfers',  icon: 'refreshcw' },
  { label: 'Satın Almalar', to: '/procurement/purchase-orders', icon: 'inbox', permission: 'procurement.manage' },
  { label: 'Mal Kabul', to: '/procurement/receipts', icon: 'box', permission: 'procurement.manage' },
  { label: 'Etiket Basımı', to: '/procurement/labels', icon: 'filetext', permission: 'procurement.manage' },
  { label: 'Sayım / Teslim', to: '/procurement/sorting', icon: 'layers', permission: 'procurement.sort' },
  { label: 'Tedarik Raporu', to: '/procurement/report', icon: 'gauge', permission: 'procurement.manage' },
  { label: 'Etiket Şablonları', to: '/settings/label-templates', icon: 'scan', permission: 'procurement.manage' },
  { label: 'Kampanyalar',  to: '/promotion/campaigns',  icon: 'percent' },
  { label: 'Kampanya Tipleri', to: '/promotion/campaign-types', icon: 'layers' },
  { label: 'Kuponlar',     to: '/promotion/coupons',    icon: 'ticket' },
  { label: 'Hediye Kartı', to: '/orders/gift-cards',    icon: 'gift' },
  { label: 'Bildirimler',      to: '/storefront/notifications', icon: 'bell' },
  { label: 'Bülten Aboneleri', to: '/storefront/newsletter',    icon: 'mail' },
  { label: 'Takip & Reklam',   to: '/marketing/tracking',       icon: 'plug' },
  { label: 'Sayfalar',  to: '/cms/pages', icon: 'filetext' },
  { label: 'POS',           to: '/pos/sales',             icon: 'creditcard' },
  { label: 'Entegrasyonlar',to: '/integrations/logs',     icon: 'plug' },
  { label: 'Finans',        to: '/finance/supplier-invoices', icon: 'clipboard' },
  { label: 'Firmalar',        to: '/settings/firms',          icon: 'building2' },
  { label: 'Platform Tipleri',to: '/settings/platform-types', icon: 'globe' },
  { label: 'Bildirim Şablonları', to: '/settings/notification-templates', icon: 'bell' },
  { label: 'Servis Kataloğu', to: '/settings/integration-services', icon: 'plug', permission: 'definition.manage' },
  { label: 'Satış Kanalları', to: '/settings/channels',       icon: 'shoppingbag' },
  { label: 'Çeviriler',       to: '/settings/translations',   icon: 'languages' },
  { label: 'Migration',       to: '/settings/migration',      icon: 'databasezap' },
  { label: 'Ayarlar',         to: '/settings/users',          icon: 'settings' }
]
const flat = (sections) => Array.from(sections.flatMap((section) => section.items))
test('all original routes, icons, permissions and static badges are retained exactly once', () => {
  const items = flat(nav.NAV_SECTIONS)
  assert.equal(items.length, baseline.length)
  assert.equal(new Set(items.map((item) => item.to)).size, baseline.length)
  for (const previous of baseline) {
    const item = items.find((candidate) => candidate.to === previous.to)
    assert.ok(item, previous.to)
    for (const key of ['icon', 'permission', 'badge']) assert.equal(item[key], previous[key], previous.to + ':' + key)
    const label = previous.to === '/crm/member-groups' ? 'Üye Grupları'
      : previous.to === '/settings/channels' ? 'Satış Kanalı Tanımları'
      : previous.to === '/settings/users' ? 'Kullanıcılar'
      : previous.to === '/integrations/logs' ? 'Entegrasyon Logları'
      : previous.to === '/settings/integration-services' ? 'Entegrasyonlar' : previous.label
    assert.equal(item.label, label)
  }
  assert.equal(nav.NAV_SECTIONS.find((section) => section.items.some((item) => item.to === '/crm/member-groups')).id, 'definitions')
})
test('central matching selects one exact/most specific item, including details and moved definitions', () => {
  for (const [route, expected] of [
    ['/', '/'], ['/orders', '/orders'], ['/orders/123', '/orders'],
    ['/orders/returns', '/orders/returns'], ['/orders/returns/123', '/orders/returns'],
    ['/orders/invoices', '/orders/invoices'], ['/orders/invoice-series', '/orders/invoice-series'],
    ['/orders/number-series', '/orders/number-series'],
    ['/accounts/groups', '/accounts/groups'], ['/accounts/123', '/accounts'],
    ['/catalog/products/P-00023146', '/catalog/products'],
    ['/catalog/products/', '/catalog/products'], ['/ORDERS/RETURNS', '/orders/returns'],
    ['/marketplaces/eslestirme', '/marketplaces'],
    ['/fulfillment/tasks/123', '/fulfillment/picking-plans'],
    ['/fulfillment/desk/123', '/fulfillment/desks'],
    ['/settings/roles', '/settings/users'], ['/pos/registers', '/pos/sales'],
    ['/orders-extra', undefined], ['/not-a-menu', undefined],
  ]) assert.equal(nav.findActiveItem(route)?.to, expected, route)
})
test('permission filtering precedes search and badge access; empty groups are removed', () => {
  const hidden = { label: 'Secret', to: '/secret', icon: 'box', permission: 'hidden' }
  Object.defineProperty(hidden, 'badge', { get: () => { throw new Error('Unauthorized badge read') } })
  const sections = nav.permittedSections([{ id: 'secret', label: 'Secret', items: [hidden] }], () => false)
  assert.equal(nav.searchSections(sections, 'secret').length, 0)
  assert.equal(flat(sections).some((item) => nav.itemBadge(item, 4, 5) > 0), false)
  assert.ok(!flat(nav.permittedSections(nav.NAV_SECTIONS, () => false)).some((item) => item.permission))
})
test('Turkish/ASCII search includes all matching groups and matches group labels', () => {
  const visible = nav.permittedSections(nav.NAV_SECTIONS, () => true)
  assert.ok(nav.searchSections(visible, ' urun ').length > 1)
  assert.equal(nav.searchSections(visible, ' TANIMLAR ')[0].items.length, visible.find((section) => section.id === 'definitions').items.length)
  assert.equal(nav.searchSections(visible, 'musteri iliskileri')[0].items[0].to, '/crm/tickets')
  assert.equal(nav.searchSections(visible, 'not-found').length, 0)
  assert.equal(nav.searchSections(visible, '   ').length, visible.length)
})
test('both live badge sources and future static badges retain their values', () => {
  assert.equal(nav.itemBadge({ to: '/storefront/questions' }, 7, 8), 7)
  assert.equal(nav.itemBadge({ to: '/crm/tickets' }, 7, 8), 8)
  assert.equal(nav.itemBadge({ to: '/other', badge: 3 }, 7, 8), 3)
  assert.equal(nav.itemBadge({ to: '/crm/tickets' }, 0, 0), undefined)
})

function harness({ pathname = '/catalog/products/P-1', collapsed = false, mobile = false, permission = () => true } = {}) {
  let cursor = 0, dirty = false, closeCount = 0
  let location = { pathname, search: '', hash: '' }
  const states = []
  const counts = { questions: 7, tickets: 8 }
  const ui = { sidebarCollapsed: collapsed, toggleSidebar: () => { ui.sidebarCollapsed = !ui.sidebarCollapsed } }
  const auth = { user: { fullName: 'Test User', email: 'test@example.invalid' }, logout: () => {}, hasPermission: permission }
  const { Sidebar } = load('Sidebar.tsx', {
    './sidebarNavigation': nav,
    react: {
      useId: () => 'test-sidebar',
      useState: (initial) => {
        const i = cursor++
        if (!(i in states)) states[i] = typeof initial === 'function' ? initial() : initial
        return [states[i], (value) => { states[i] = typeof value === 'function' ? value(states[i]) : value; dirty = true }]
      },
    },
    'react-router-dom': { Link: 'Link', useLocation: () => location },
    '@/lib/utils': { cn: (...values) => values.filter(Boolean).join(' ') },
    '@/store/ui': { useUIStore: () => ui },
    '@/store/auth': { useAuthStore: (select) => select ? select(auth) : auth },
    '@/store/questionAlerts': { useQuestionAlertStore: (select) => select({ pendingCount: counts.questions }) },
    '@/store/ticketAlerts': { useTicketAlertStore: (select) => select({ pendingCount: counts.tickets }) },
  })
  return {
    render: () => {
      let tree, attempts = 0
      do {
        dirty = false; cursor = 0
        tree = Sidebar({ onMobileClose: mobile ? () => { closeCount++ } : undefined })
        if (++attempts > 5) throw new Error('Render loop')
      } while (dirty)
      return tree
    },
    navigate: (pathname, search = '', hash = '') => { location = { pathname, search, hash } },
    ui, counts, closeCount: () => closeCount,
  }
}
function all(node, predicate, visibleOnly = false) {
  if (Array.isArray(node)) return node.flatMap((child) => all(child, predicate, visibleOnly))
  if (!node || typeof node !== 'object' || (visibleOnly && node.props?.hidden)) return []
  return [...(predicate(node) ? [node] : []), ...all(node.props?.children, predicate, visibleOnly)]
}
const group = (tree, label) => all(tree, (node) => node.type === 'button' && node.props['aria-label'] === label)[0]
const expanded = (tree) => all(tree, (node) => node.type === 'button' && node.props['aria-expanded'] === true)
const visibleLinks = (tree) => all(tree, (node) => node.type === 'Link', true)
const search = (page, value) => {
  all(page.render(), (node) => node.type === 'input')[0].props.onChange({ target: { value } })
  return page.render()
}
test('accordion initially opens the active group and manual toggles do not get reset', () => {
  const page = harness()
  assert.equal(expanded(page.render())[0].props['aria-label'], 'Ürün Yönetimi')
  group(page.render(), 'Tanımlar').props.onClick()
  for (let i = 0; i < 3; i++) {
    assert.equal(expanded(page.render()).length, 1)
    assert.equal(expanded(page.render())[0].props['aria-label'], 'Tanımlar')
  }
  group(page.render(), 'Tanımlar').props.onClick()
  assert.equal(expanded(page.render()).length, 0)
})
test('route changes from any source reopen the right group and only the specific link is active', () => {
  const page = harness()
  for (const route of ['/orders/returns/123', '/orders/invoice-series', '/catalog/products/P-2', '/orders/returns/123']) {
    page.navigate(route)
    const tree = page.render()
    const active = visibleLinks(tree).filter((node) => node.props['aria-current'] === 'page')
    assert.equal(active.length, 1)
    assert.equal(active[0].props.to, nav.findActiveItem(route).to)
    assert.equal(expanded(tree).length, 1)
  }
  group(page.render(), 'Tanımlar').props.onClick()
  page.navigate('/orders/returns/123', '?page=2')
  assert.equal(expanded(page.render())[0].props['aria-label'], 'Sipariş')
})
test('search temporarily shows multiple groups, then restores the manually opened accordion', () => {
  const page = harness()
  group(page.render(), 'Tanımlar').props.onClick()
  const tree = search(page, 'urun')
  assert.equal(expanded(tree).length, 0)
  assert.ok(visibleLinks(tree).some((node) => node.props.to === '/catalog/products'))
  assert.ok(visibleLinks(tree).some((node) => node.props.to === '/storefront/questions'))
  assert.equal(all(tree, (node) => node.props.className === 'nav-section-lbl').length > 1, true)
  all(tree, (node) => node.props['aria-label'] === 'Menü aramasını temizle')[0].props.onClick()
  assert.equal(expanded(page.render())[0].props['aria-label'], 'Tanımlar')
  search(page, 'missing')
  assert.equal(all(page.render(), (node) => node.props.role === 'status').length, 1)
  all(page.render(), (node) => node.type === 'input')[0].props.onKeyDown({ key: 'Escape' })
  assert.equal(expanded(page.render())[0].props['aria-label'], 'Tanımlar')
})
test('collapsed rail preserves direct links; group click expands it and opens the selected group', () => {
  const page = harness({ collapsed: true })
  assert.ok(page.render().props.className.includes('w-[60px]'))
  assert.equal(expanded(page.render()).length, 0)
  assert.equal(visibleLinks(page.render()).filter((node) => node.props.to === '/requests').length, 1)
  assert.equal(visibleLinks(page.render()).some((node) => node.props.to === '/catalog/products'), false)
  group(page.render(), 'Tanımlar').props.onClick()
  assert.equal(page.ui.sidebarCollapsed, false)
  assert.equal(expanded(page.render())[0].props['aria-label'], 'Tanımlar')
})
test('group buttons have native keyboard semantics and controls; hidden panels are not visible navigation', () => {
  const tree = harness().render()
  for (const button of all(tree, (node) => node.props['aria-controls'])) {
    assert.equal(button.type, 'button')
    assert.equal(button.props.type, 'button')
    assert.ok(button.props.className.includes('focus-visible:'))
    const panel = all(tree, (node) => node.props.id === button.props['aria-controls'])[0]
    assert.ok(panel)
    assert.equal(Boolean(panel.props.hidden), !button.props['aria-expanded'])
  }
})
test('mobile stays expanded, toggling does not close, real navigation closes the overlay', () => {
  const page = harness({ mobile: true, collapsed: true })
  assert.ok(page.render().props.className.includes('w-[248px]'))
  group(page.render(), 'Sipariş').props.onClick()
  assert.equal(page.closeCount(), 0)
  visibleLinks(page.render()).find((node) => node.props.to === '/orders/returns').props.onClick()
  assert.equal(page.closeCount(), 1)
  assert.equal(page.ui.sidebarCollapsed, true)
})
test('closed CRM group shows a dot, not a sum; opening preserves both numeric badges', () => {
  const page = harness()
  const indicator = all(group(page.render(), 'Müşteriler / CRM'), (node) => node.props['aria-label'] === 'Yeni bildirim var')
  assert.equal(indicator.length, 1)
  assert.equal(indicator[0].props.children, undefined)
  group(page.render(), 'Müşteriler / CRM').props.onClick()
  assert.equal(all(group(page.render(), 'Müşteriler / CRM'), (node) => node.props['aria-label'] === 'Yeni bildirim var').length, 0)
  for (const [url, count] of [['/storefront/questions', 7], ['/crm/tickets', 8]]) {
    const link = visibleLinks(page.render()).find((node) => node.props.to === url)
    assert.equal(all(link, (node) => node.type === 'span' && node.props.children === count).length, 1)
  }
})

test('every group and item has an icon; guide and user helper areas remain intact', () => {
  const tree = harness().render()
  for (const icon of all(tree, (node) => node.props.className === 'ni flex-shrink-0 relative')) {
    assert.ok(icon.props.children[0], 'Missing icon')
  }
  const guide = all(tree, (node) => node.type === 'a' && node.props.href === '/rehber/')[0]
  assert.equal(guide.props.target, '_blank')
  assert.equal(guide.props.rel, 'noopener')
  assert.equal(all(tree, (node) => node.props.title === 'Çıkış').length, 1)
})

test('restricted users do not render restricted menu items or empty procurement group', () => {
  const page = harness({ permission: () => false })
  assert.equal(group(page.render(), 'Tedarik'), undefined)
  for (const tree of [page.render(), search(page, 'katalog')]) {
    const links = all(tree, (node) => node.type === 'Link')
    assert.equal(links.some((node) => node.props.to === '/catalog/settings'), false)
    assert.equal(links.some((node) => node.props.to === '/settings/integration-services'), false)
  }
})

test('navigation during search is restored to the new active group when search clears', () => {
  const page = harness()
  search(page, 'urun')
  page.navigate('/orders/returns')
  page.render()
  const tree = search(page, '')
  assert.equal(expanded(tree)[0].props['aria-label'], 'Sipariş')
})

test('question badge reacts to 0 -> 1 -> 0 and works in search and collapsed group indicators', () => {
  const page = harness()
  page.counts.questions = 0
  page.counts.tickets = 0
  group(page.render(), 'Müşteriler / CRM').props.onClick()
  const question = () => visibleLinks(page.render()).find((node) => node.props.to === '/storefront/questions')
  const badges = () => all(question(), (node) => node.type === 'span' && node.props.className?.includes('font-bold'))
  assert.equal(badges().length, 0)
  page.counts.questions = 1
  assert.equal(badges().length, 1)
  assert.equal(badges()[0].props.children, 1)
  assert.equal(badges()[0].props.style.background, '#ef4444')
  search(page, 'urun sorulari')
  assert.equal(badges()[0].props.children, 1)
  search(page, '')
  group(page.render(), 'Müşteriler / CRM').props.onClick()
  const dots = () => all(group(page.render(), 'Müşteriler / CRM'), (node) => node.props['aria-label'] === 'Yeni bildirim var')
  assert.equal(dots().length, 1)
  page.ui.sidebarCollapsed = true
  assert.equal(dots().length, 1)
  page.counts.questions = 0
  assert.equal(dots().length, 0)
  page.ui.sidebarCollapsed = false
  group(page.render(), 'Müşteriler / CRM').props.onClick()
  assert.equal(badges().length, 0)
})

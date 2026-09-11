const { test } = require('node:test')
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const vm = require('node:vm')
const ts = require('typescript')

function storage() {
  const data = new Map()
  return { getItem: (key) => data.get(key) ?? null, setItem: (key, value) => data.set(key, value), clear: () => data.clear() }
}
function load(file, mocks = {}, localStorage = storage(), globals = {}) {
  const source = fs.readFileSync(path.join(__dirname, '../src', file), 'utf8')
  const output = ts.transpileModule(source, { compilerOptions: {
    module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText
  const exports = {}
  vm.runInNewContext(output, { exports, localStorage, URL, ...globals, require: (name) => mocks[name] ?? require(name) })
  return exports
}
function setup() {
  const disk = storage()
  const nav = load('components/layout/sidebarNavigation.ts')
  const helpers = load('lib/adminFavorites.ts', { '@/components/layout/sidebarNavigation': nav }, disk)
  const makeStore = () => load('store/favorites.ts', { '@/lib/adminFavorites': helpers }, disk).useFavoritesStore
  return { disk, nav, helpers, makeStore, store: makeStore() }
}
test('favorites preserve exact detail/query/hash routes, reject unsafe or unknown destinations', () => {
  const { helpers } = setup()
  const favorite = helpers.favoriteForRoute('/catalog/products/P-123?tab=features#detail')
  assert.equal(favorite.to, '/catalog/products/P-123?tab=features#detail')
  assert.equal(favorite.label, 'Ürün Kartları · P-123')
  for (const url of ['https://other.invalid', '//other.invalid', '/\\other.invalid', 'javascript:alert(1)', '/unknown', '/orders\n', '/orders?' + 'x'.repeat(2050)]) {
    assert.equal(helpers.favoriteForRoute(url), null, url)
  }
})
test('add is idempotent, persists after reload, isolates users and removes only the selected favorite', () => {
  const { store, makeStore } = setup()
  store.getState().add('user-a', '/orders')
  store.getState().add('user-a', '/orders')
  store.getState().add('user-b', '/catalog/products')
  assert.equal(store.getState().byUser['user-a'].length, 1)
  const reloaded = makeStore()
  assert.equal(reloaded.getState().byUser['user-a'][0].to, '/orders')
  reloaded.getState().remove('user-a', '/orders')
  assert.equal(reloaded.getState().byUser['user-a'].length, 0)
  assert.equal(reloaded.getState().byUser['user-b'].length, 1)
})
test('logout/session expiration preserve only validated favorites; all tokens/auth entries are cleared', () => {
  const { store, helpers, disk } = setup()
  store.getState().add('user-a', '/orders')
  for (const key of ['access_token', 'refresh_token', 'ecspros-auth', 'unrelated']) disk.setItem(key, 'secret')
  helpers.clearSessionStoragePreservingFavorites()
  for (const key of ['access_token', 'refresh_token', 'ecspros-auth', 'unrelated']) assert.equal(disk.getItem(key), null)
  assert.equal(helpers.readFavorites()['user-a'][0].to, '/orders')
  for (const file of ['store/auth.ts', 'api/client.ts']) {
    const source = fs.readFileSync(path.join(__dirname, '../src', file), 'utf8')
    assert.ok(source.includes('clearSessionStoragePreservingFavorites()'))
    assert.ok(!source.includes('localStorage.clear()'))
  }
})
test('corrupt stored values cannot inject labels or external shortcuts; current labels are used', () => {
  const { helpers, disk } = setup()
  disk.setItem(helpers.FAVORITES_KEY, '{broken')
  assert.equal(Object.keys(helpers.readFavorites()).length, 0)
  disk.setItem(helpers.FAVORITES_KEY, JSON.stringify({ a: [null, { to: '//evil' }, { to: '/settings/users', label: '<script>bad</script>' }] }))
  assert.equal(helpers.readFavorites().a.length, 1)
  assert.equal(helpers.readFavorites().a[0].label, 'Kullanıcılar')
})
test('storage write failure is visible and must not claim a successful favorite', () => {
  const { store, disk } = setup()
  disk.setItem = () => { throw new Error('Quota denied') }
  assert.equal(store.getState().add('user-a', '/orders'), false)
  assert.ok(store.getState().error)
  assert.equal(store.getState().byUser['user-a'], undefined)
})
test('Turkish/ASCII search supports names and detail codes', () => {
  const { helpers } = setup()
  assert.equal(helpers.matchesFavorite({ label: 'Ürün Grupları', to: '/catalog/product-groups' }, ' urun gruplari '), true)
  assert.equal(helpers.matchesFavorite(helpers.favoriteForRoute('/catalog/products/P-123'), 'p-123'), true)
})

function all(node, predicate) {
  if (Array.isArray(node)) return node.flatMap((child) => all(child, predicate))
  if (!node || typeof node !== 'object') return []
  return [...(predicate(node) ? [node] : []), ...all(node.props?.children, predicate)]
}
function text(node) {
  if (Array.isArray(node)) return node.map(text).join('')
  if (node == null || typeof node === 'boolean') return ''
  return typeof node === 'object' ? text(node.props?.children) : String(node)
}
function harness() {
  const env = setup(), state = []
  let cursor = 0, dirty = false, userId = 'user-a'
  const permissions = new Set(['catalog.products.view', 'definitions.view', 'orders.view', 'storefront.notifications.view'])
  const navigations = [], effects = [], queries = []
  const counts = { 'fav-pending-orders': 0, 'fav-stock-alerts': 0 }
  const location = { pathname: '/catalog/products/P-123', search: '?tab=features', hash: '#details' }
  const ui = { favsPanelOpen: true, setFavsPanelOpen: (open) => { ui.favsPanelOpen = open } }
  const mocks = {
    '@tanstack/react-query': { useQuery: (options) => { queries.push(options); return { data: counts[options.queryKey[0]] } } },
    '@/api/client': { default: {} },
    './sidebarNavigation': env.nav,
    './TicketBell': { TicketBell: 'TicketBell' },
    '@/lib/adminFavorites': env.helpers,
    '@/lib/utils': { cn: (...values) => values.filter(Boolean).join(' ') },
    '@/store/favorites': { useFavoritesStore: () => env.store.getState() },
    '@/store/ui': { useUIStore: () => ui },
    '@/store/auth': { useAuthStore: (select) => select({ user: { id: userId }, hasPermission: (permission) => permissions.has(permission) }) },
    'react-router-dom': { useLocation: () => location, useNavigate: () => (to) => navigations.push(to) },
    react: {
      useState: (initial) => {
        const i = cursor++
        if (!(i in state)) state[i] = initial
        return [state[i], (value) => { state[i] = typeof value === 'function' ? value(state[i]) : value; dirty = true }]
      },
      useRef: () => ({ current: { focus: () => {} } }),
      useEffect: (fn) => effects.push(fn),
    },
  }
  const { FavoritesPanel } = load('components/layout/FavoritesPanel.tsx', mocks, env.disk)
  const { Header } = load('components/layout/Header.tsx', mocks, env.disk)
  return { ...env, ui, navigations, location, effects, counts, queries, permissions,
    switchUser: (id) => { userId = id },
    header: () => Header({ onMobileMenuOpen: () => {} }),
    render: () => {
      let tree, count = 0
      do { cursor = 0; dirty = false; tree = FavoritesPanel(); if (++count > 5) throw new Error('Render loop') } while (dirty)
      return tree
    },
  }
}
const button = (tree, label) => all(tree, (node) => node.type === 'button' && text(node).trim() === label)[0]
test('Header adds the actual page and opens the right panel; repeat clicks do not duplicate', () => {
  const page = harness()
  page.ui.favsPanelOpen = false
  button(page.header(), 'Favorilere Ekle').props.onClick()
  assert.equal(page.ui.favsPanelOpen, true)
  assert.equal(page.store.getState().byUser['user-a'][0].to, '/catalog/products/P-123?tab=features#details')
  button(page.header(), 'Favorilerde').props.onClick()
  assert.equal(page.store.getState().byUser['user-a'].length, 1)
  button(page.render(), 'Ürün Kartları · P-123').props.onClick()
  assert.equal(page.navigations[0], '/catalog/products/P-123?tab=features#details')
  assert.equal(page.ui.favsPanelOpen, false)
})
test('shortcut picker adds a permitted menu page; search, remove and user isolation work', () => {
  const page = harness()
  button(page.render(), 'Kısayol Ekle').props.onClick()
  assert.equal(button(page.render(), 'Servis Kataloğu'), undefined)
  assert.equal(button(page.render(), 'Entegrasyonlar'), undefined)
  button(page.render(), 'Ürün Grupları').props.onClick()
  assert.equal(page.store.getState().byUser['user-a'][0].to, '/catalog/product-groups')
  all(page.render(), (node) => node.type === 'input')[0].props.onChange({ target: { value: 'urun gruplari' } })
  assert.ok(button(page.render(), 'Ürün Grupları'))
  all(page.render(), (node) => node.type === 'input')[0].props.onChange({ target: { value: 'bulunmayan' } })
  assert.equal(button(page.render(), 'Ürün Grupları'), undefined)
  assert.ok(text(page.render()).includes('Aramanızla eşleşen favori bulunamadı.'))
  button(page.header(), 'Favorilere Ekle').props.onClick()
  assert.ok(button(page.render(), 'Ürün Kartları · P-123'), 'Header add clears stale search')
  page.switchUser('user-b')
  assert.equal(button(page.render(), 'Ürün Grupları'), undefined)
  page.switchUser('user-a')
  all(page.render(), (node) => node.props['aria-label'] === 'Ürün Grupları favorisini kaldır')[0].props.onClick()
  assert.equal(page.store.getState().byUser['user-a'].length, 1)
})
test('hidden panel is inert, and stored restricted routes do not render', () => {
  const page = harness()
  page.store.getState().add('user-a', '/settings/integration-services')
  assert.equal(button(page.render(), 'Entegrasyonlar'), undefined)
  page.ui.favsPanelOpen = false
  assert.equal(all(page.render(), (node) => node.props.id === 'favorites-panel')[0].props.inert, true)
})

test('revoked page permission hides saved shortcuts and disables their badge queries', () => {
  const page = harness()
  page.store.getState().add('user-a', '/orders')
  assert.ok(button(page.render(), 'Siparişler'))
  page.permissions.delete('orders.view')
  assert.equal(button(page.render(), 'Siparişler'), undefined)
  assert.equal(page.queries.filter(q => q.queryKey[0] === 'fav-pending-orders').at(-1).enabled, false)
  assert.equal(page.store.getState().byUser['user-a'].length, 1, 'Revocation does not delete user data')
})

test('existing order/stock badges retain live counts, queries are user-scoped and disabled when panel closes', () => {
  const page = harness()
  page.store.getState().add('user-a', '/orders')
  page.store.getState().add('user-a', '/storefront/notifications')
  page.counts['fav-pending-orders'] = 4
  page.counts['fav-stock-alerts'] = 2
  const tree = page.render()
  assert.equal(all(tree, (node) => node.props.title === 'Bekleyen siparişler')[0].props.children, 4)
  assert.equal(all(tree, (node) => node.props.title === 'Stok uyarıları')[0].props.children, 2)
  assert.ok(page.queries.slice(-2).every((query) => query.enabled && query.queryKey[1] === 'user-a'))
  page.ui.favsPanelOpen = false
  page.render()
  assert.ok(page.queries.slice(-2).every((query) => query.enabled === false))
})

for (const hasRefresh of [false, true]) test('401 session cleanup retains favorites and removes tokens: refresh present=' + hasRefresh, async () => {
  const { store, helpers, disk } = setup()
  store.getState().add('user-a', '/orders')
  disk.setItem('access_token', 'expired-test-token')
  disk.setItem('ecspros-auth', 'test-auth')
  if (hasRefresh) disk.setItem('refresh_token', 'expired-refresh')
  let rejectHandler
  const window = { location: { pathname: '/admin/orders', href: '' } }
  const instance = { interceptors: { request: { use: () => {} }, response: { use: (_, reject) => { rejectHandler = reject } } } }
  load('api/client.ts', {
    axios: { default: { create: () => instance, post: async () => { throw new Error('Refresh rejected') } } },
    '@/lib/adminFavorites': helpers,
  }, disk, { window })
  const error = { config: { url: '/orders' }, response: { status: 401 } }
  await assert.rejects(rejectHandler(error))
  assert.equal(window.location.href, '/admin/login')
  for (const key of ['access_token', 'refresh_token', 'ecspros-auth']) assert.equal(disk.getItem(key), null)
  assert.equal(helpers.readFavorites()['user-a'][0].to, '/orders')
})

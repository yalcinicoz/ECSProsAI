const { test } = require('node:test')
const assert = require('node:assert/strict')
const fs = require('node:fs'), path = require('node:path'), vm = require('node:vm'), ts = require('typescript')

// Real URL-backed hook and page; HTTP results are supplied, never client-filtered by the harness.
function harness() {
  const states = [], queries = [], requests = [], navigations = []
  let cursor = 0, sp = new URLSearchParams(), data = { items: [], totalCount: 0 }, error = null
  const mocks = {
    react: { useState: initial => { const i = cursor++; if (!(i in states)) states[i] = typeof initial === 'function' ? initial() : initial
      return [states[i], v => { states[i] = typeof v === 'function' ? v(states[i]) : v }] }, useMemo: f => f(), useCallback: f => f },
    'react-router-dom': { useSearchParams: () => [sp, f => { sp = f(sp) }], useNavigate: () => v => navigations.push(v) },
    '@/store/auth': { useAuthStore: f => f({ user: { id: 'u1' } }) },
    './types': { GRID_MAX_PAGE_SIZE: 250 },
    '@tanstack/react-query': { useQuery: o => { queries.push(o); return { data, error, isLoading: false } } },
    '@/api/client': { default: { get: async url => { requests.push(url); return { data: { data } } } } },
    '@/lib/utils': { cn: (...v) => v.filter(Boolean).join(' ') },
    '@/components/ui/DataTable.utils': { errText: e => e.message },
    './CreateProductGroupModal': { CreateProductGroupModal: 'CreateProductGroupModal' },
  }
  function load(file) {
    const source = fs.readFileSync(path.join(__dirname, '../src', file), 'utf8')
    const output = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX } }).outputText
    const exports = {}
    vm.runInNewContext(output, { exports, URLSearchParams, localStorage: { getItem: () => null, setItem: () => {} }, require: n => {
      if (mocks[n]) return mocks[n]
      if (n.startsWith('@/components/ui/')) return new Proxy({}, { get: (_, key) => key })
      return require(n)
    } })
    return exports
  }
  mocks['@/components/grid'] = { ...load('components/grid/useGridState.ts'), DataGrid: 'DataGrid' }
  const { ProductGroupsPage } = load('pages/catalog/ProductGroupsPage.tsx')
  return { render: () => { cursor = 0; return ProductGroupsPage() }, queries, requests, navigations,
    data: v => { data = v }, error: v => { error = v } }
}
function all(n, pred) {
  if (Array.isArray(n)) return n.flatMap(v => all(v, pred))
  if (!n || typeof n !== 'object') return []
  return [...(pred(n) ? [n] : []), ...all(n.props?.children, pred)]
}
function text(n) { return Array.isArray(n) ? n.map(text).join('') : n && typeof n === 'object' ? text(n.props?.children) : n == null ? '' : String(n) }
const grid = h => all(h.render(), n => n.type === 'DataGrid')[0].props
const button = (h, label) => all(h.render(), n => ['button', 'Button'].includes(n.type) && text(n).includes(label))[0]
const params = h => new URL(h.requests.at(-1), 'https://test.invalid').searchParams

test('product groups use paged endpoint and stable server ordering', async () => {
  const h = harness(), g = grid(h)
  assert.equal(g.grid.state.sort, 'sortOrder'); assert.equal(g.grid.state.dir, 'asc')
  await h.queries.at(-1).queryFn()
  assert.ok(h.requests[0].startsWith('/catalog/product-groups/grid?'))
  assert.equal(params(h).get('pageSize'), '20')
})
test('search text reaches the server and resets page without ASCII/language loss', async () => {
  const h = harness()
  for (const term of ['GÖMLEK', 'gomlek', 'Shirt', 'GRP_83', 'İÇ GİYİM']) {
    grid(h).grid.setPage(3); grid(h).grid.setSearch('  ' + term + '  ')
    assert.equal(grid(h).grid.state.page, 1); await h.queries.at(-1).queryFn()
    assert.equal(params(h).get('search'), term)
  }
})
test('server page order and total count are not replaced by client filtering', () => {
  const h = harness(), rows = [{ id: 'b' }, { id: 'a' }]
  h.data({ items: rows, totalCount: 75 })
  assert.equal(grid(h).rows, rows); assert.equal(grid(h).totalCount, 75)
  grid(h).grid.setSearch('different'); assert.equal(grid(h).rows, rows)
})
test('empty data and server failure are different grid states', () => {
  const h = harness(); assert.equal(grid(h).totalCount, 0); assert.equal(grid(h).error, null)
  h.error(new Error('Test API failure')); assert.equal(grid(h).error, 'Test API failure')
})
test('clearing search preserves active-only and export filter', async () => {
  const h = harness(); grid(h).grid.setSearch('ceket'); button(h, 'Aktif').props.onClick()
  grid(h).grid.setSearch(''); const g = grid(h); await h.queries.at(-1).queryFn()
  assert.equal(params(h).get('activeOnly'), 'true'); assert.equal(g.export.named().activeOnly, 'true')
  assert.equal(g.grid.state.search, ''); assert.equal(g.export.endpoint, '/catalog/product-groups/export')
})
test('row navigation preserves group id after filtering', () => {
  const h = harness(); grid(h).grid.setSearch('grp_83'); grid(h).onRowClick({ id: 'bag' })
  assert.deepEqual(h.navigations, ['/catalog/product-groups/bag'])
})
test('create is guarded and does not use truncated grid page as copy source', () => {
  const h = harness(); grid(h).grid.setSearch('ceket')
  assert.equal(all(h.render(), n => n.type === 'PermissionGuard' && n.props.children)[0].props.permission, 'catalog.platform.manage')
  button(h, 'Yeni Grup').props.onClick()
  const modal = all(h.render(), n => n.type === 'CreateProductGroupModal')[0]
  assert.equal(modal.props.groups, undefined); modal.props.onCreated({ id: 'new' })
  assert.deepEqual(h.navigations, ['/catalog/product-groups/new'])
  assert.equal(all(h.render(), n => n.type === 'CreateProductGroupModal').length, 0)
})

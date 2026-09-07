const { test } = require('node:test')
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const vm = require('node:vm')
const ts = require('typescript')

const groups = Object.freeze([
  { id: 'shirt', code: 'grp_10', nameI18n: { tr: 'Gömlek', en: 'Shirt' }, sortOrder: 3, isActive: true, attributes: [] },
  { id: 'bag', code: 'grp_83', nameI18n: { tr: 'Çanta' }, sortOrder: 1, isActive: true, attributes: [] },
  { id: 'underwear', code: 'grp_20', nameI18n: { tr: 'İç Giyim' }, sortOrder: 2, isActive: true, attributes: [] },
  { id: 'accessory', code: 'grp_99', nameI18n: { tr: 'IŞIKLI Aksesuar' }, sortOrder: 4, isActive: false, attributes: [] },
])

// Exercise the actual page and its event handlers in memory, without API/DB calls or build output.
function pageHarness() {
  const state = [], navigations = [], queryKeys = []
  let cursor = 0, focused = 0
  const ref = { current: { focus: () => { focused++ } } }
  const source = fs.readFileSync(path.join(__dirname, '../src/pages/catalog/ProductGroupsPage.tsx'), 'utf8')
  const output = ts.transpileModule(source, { compilerOptions: {
    module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText
  const mocks = {
    './CreateProductGroupModal': { CreateProductGroupModal: 'CreateProductGroupModal' },
    react: {
      useState: (initial) => {
        const index = cursor++
        if (!(index in state)) state[index] = initial
        return [state[index], (value) => { state[index] = typeof value === 'function' ? value(state[index]) : value }]
      },
      useMemo: (factory) => factory(),
      useRef: () => ref,
    },
    '@tanstack/react-query': {
      useQuery: ({ queryKey }) => {
        queryKeys.push(Array.from(queryKey))
        return { data: queryKey[1] ? groups.filter((g) => g.isActive) : groups, isLoading: false }
      },
      useMutation: () => ({}),
      useQueryClient: () => ({ invalidateQueries: () => {} }),
    },
    'react-router-dom': { useNavigate: () => (url) => navigations.push(url) },
    '@/hooks/useLanguages': { useLanguages: () => ({ data: [], isLoading: false }) },
    '@/lib/utils': { cn: (...values) => values.filter(Boolean).join(' '), toSnakeCase: () => '' },
    '@/lib/field-labels': { FL: { name: {} } },
    '@/lib/i18n-helper': { buildI18nValues: () => [] },
    '@/api/client': { default: { get: () => { throw new Error('Unexpected API request') }, post: () => { throw new Error('Unexpected write') } } },
  }
  const exports = {}
  vm.runInNewContext(output, { exports, require: (name) => {
    if (mocks[name]) return mocks[name]
    if (name.startsWith('@/components/ui/')) {
      return new Proxy({}, { get: (_, key) => key })
    }
    return require(name)
  } })
  return {
    render: () => { cursor = 0; return exports.ProductGroupsPage() },
    navigations, queryKeys, focused: () => focused,
  }
}

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
const input = (tree) => all(tree, (node) => node.props.role === 'searchbox')[0]
const rows = (tree) => all(tree, (node) => node.type === 'tr' && typeof node.props.onClick === 'function')
const ids = (tree) => rows(tree).map((row) => row.key)
const status = (tree) => text(all(tree, (node) => node.props.role === 'status')[0])
function search(page, value) {
  input(page.render()).props.onChange({ target: { value } })
  return page.render()
}

test('initial and whitespace search retain every group and the original ordering', () => {
  const page = pageHarness()
  assert.deepEqual(ids(page.render()), ['bag', 'underwear', 'shirt', 'accessory'])
  assert.deepEqual(ids(search(page, '   ')), ['bag', 'underwear', 'shirt', 'accessory'])
  assert.equal(status(page.render()), '4 / 4 grup')
  assert.deepEqual(groups.map((g) => g.id), ['shirt', 'bag', 'underwear', 'accessory'])
})

test('search matches names, translations and codes with Turkish/ASCII case normalization', () => {
  const page = pageHarness()
  for (const [query, expected] of [
    ['gomlek', 'shirt'], ['GÖMLEK', 'shirt'], ['ShIrT', 'shirt'],
    ['canta', 'bag'], ['GRP_83', 'bag'], ['  İÇ   GİYİM  ', 'underwear'],
    ['ic giyim', 'underwear'], ['ISIKLI', 'accessory'], ['ışıklı', 'accessory'],
  ]) {
    assert.deepEqual(ids(search(page, query)), [expected], query)
  }
  assert.deepEqual(ids(search(page, 'grp_')), ['bag', 'underwear', 'shirt', 'accessory'])
  assert.ok(page.queryKeys.every((key) => key.length === 2 && typeof key[1] === 'boolean'))
})

test('no match displays an explanatory empty state and zero result count', () => {
  const tree = search(pageHarness(), 'bulunmayan-grup')
  assert.equal(rows(tree).length, 0)
  assert.equal(status(tree), '0 / 4 grup')
  assert.ok(text(tree).includes('Aramanızla eşleşen ürün grubu bulunamadı'))
})

test('clear button and Escape restore all rows and focus the search input', () => {
  const page = pageHarness()
  const tree = search(page, 'gomlek')
  all(tree, (node) => node.props['aria-label'] === 'Aramayı temizle')[0].props.onClick()
  assert.equal(input(page.render()).props.value, '')
  assert.equal(rows(page.render()).length, 4)
  assert.equal(page.focused(), 1)
  input(search(page, 'canta')).props.onKeyDown({ key: 'Escape' })
  assert.equal(input(page.render()).props.value, '')
  assert.equal(rows(page.render()).length, 4)
  assert.equal(page.focused(), 2)
})

test('search works with active-only filtering and clearing does not reset that filter', () => {
  const page = pageHarness()
  search(page, 'ISIKLI')
  all(page.render(), (node) => node.type === 'button' && text(node) === 'Aktif')[0].props.onClick()
  assert.equal(input(page.render()).props.value, 'ISIKLI')
  assert.equal(status(page.render()), '0 / 3 grup')
  input(page.render()).props.onKeyDown({ key: 'Escape' })
  assert.equal(status(page.render()), '3 / 3 grup')
  assert.ok(page.queryKeys.some((key) => key[1] === true))
  assert.ok(!ids(page.render()).includes('accessory'))
})

test('filtered rows still navigate to the correct product group', () => {
  const page = pageHarness()
  rows(search(page, 'grp_83'))[0].props.onClick()
  assert.deepEqual(page.navigations, ['/catalog/product-groups/bag'])
})

test('table search opens the shared create form without passing a filtered group list', () => {
  const page = pageHarness()
  search(page, 'gomlek')
  all(page.render(), (node) => node.type === 'Button' && text(node).includes('Yeni Grup'))[0].props.onClick()
  const tree = page.render()
  const modal = all(tree, (node) => node.type === 'CreateProductGroupModal')[0]
  assert.equal(modal.props.groups, undefined)
  assert.deepEqual(ids(tree), ['shirt'])
  modal.props.onCreated({ id: 'new-group' })
  assert.deepEqual(page.navigations, ['/catalog/product-groups/new-group'])
  assert.equal(all(page.render(), (node) => node.type === 'CreateProductGroupModal').length, 0)
})

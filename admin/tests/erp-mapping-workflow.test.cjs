const { test } = require('node:test')
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const vm = require('node:vm')
const ts = require('typescript')

function load(relative, mocks = {}, appendix = '') {
  const source = fs.readFileSync(path.join(__dirname, '..', relative), 'utf8')
  const output = ts.transpileModule(source, { compilerOptions: {
    module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText
  const exports = {}
  vm.runInNewContext(output + appendix, { exports, URLSearchParams, require: (name) => {
    if (mocks[name]) return mocks[name]
    if (name.startsWith('@/components/ui/')) return new Proxy({}, { get: (_, key) => key })
    return require(name)
  } })
  return exports
}
const helpers = load('src/pages/marketplaces/erpMappingForm.ts')
const items = [
  { id: 'mapped', code: '01', name: 'Ceket', kind: 'product_group', isActive: true, isMapped: true, mappingId: 'm1' },
  { id: 'unmapped', code: '02', name: 'Kot Ceket', kind: 'product_group', isActive: true, isMapped: false },
  { id: 'conflict', code: '03', name: 'Blazer', kind: 'product_group', isActive: true, isMapped: true, mappingConflict: true },
  { id: 'inactive', code: '04', name: 'Pasif', kind: 'product_group', isActive: false, isMapped: false },
  { id: 'supplier', code: '05', name: 'Tedarikçi', kind: 'supplier', isActive: true, isMapped: false },
]
const overview = { groups: [{ productGroupId: 'old', name: 'Ceket', code: 'ceket', productCount: 0, mapping: null }], mappedCount: 0, unmappedCount: 1, reviewCount: 0 }
const catalogGroups = [{ id: 'old', nameI18n: { tr: 'Ceket' }, isActive: false, attributes: [{ id: 'attr', attributeTypeCode: 'renk', attributeTypeNameI18n: { tr: 'Renk' }, isVariant: true, isPrimaryAxis: true, sortOrder: 1 }], axisSubAttributes: [{}] }]

// Hook harness runs real JSX/event handlers, but never executes queryFn or contacts a database.
function harness({ shared = false, failPost = false, queryError = false, pending = false, erpItems = items, loading = false } = {}) {
  const states = [], mutations = [], writes = [], invalidations = [], queries = []
  let cursor = 0, mutationCursor = 0, params = new URLSearchParams('mp=erp:nebim&tab=eslenmemis')
  const mocks = {
    // These tests render the dictionary/modals, not the separate DataGrid tabs.
    '@/components/grid': { DataGrid: 'DataGrid', useGridState: () => { throw new Error('Unexpected grid tab render') }, useLocalGrid: () => { throw new Error('Unexpected local grid render') } },
    react: {
      useState: (initial) => {
        const index = cursor++
        if (!(index in states)) states[index] = initial
        return [states[index], (value) => { states[index] = typeof value === 'function' ? value(states[index]) : value }]
      }, useMemo: (factory) => factory(),
    },
    '@tanstack/react-query': {
      useQuery: (options) => {
        queries.push(options)
        const key = options.queryKey
        let data
        if (key[0] === 'erp-items') data = loading ? undefined : erpItems.filter((i) => (!key[2] || i.kind === key[2]) && (!key[3] || i.name.includes(key[3]) || i.code.includes(key[3])))
        else if (key[0] === 'mapping-overview') data = overview
        else if (key[0] === 'mapping-targets') data = { marketplaces: [{ marketplace: 'trendyol', categoryCount: 1 }], erp: [{ key: 'erp:nebim', name: 'Nebim', groupCount: 4 }] }
        else if (key[0] === 'product-groups') data = catalogGroups
        else data = []
        return { data, isLoading: loading, isError: queryError, refetch: () => {} }
      },
      useQueryClient: () => ({ invalidateQueries: (q) => invalidations.push(q.queryKey.join('/')) }),
      useMutation: (options) => {
        const index = mutationCursor++
        mutations[index] ??= { isPending: pending, isError: false }
        const result = mutations[index]
        result.mutate = async (arg) => {
          try { const data = await options.mutationFn(arg); await options.onSuccess?.(data) }
          catch (error) { result.error = error; result.isError = true; options.onError?.(error) }
        }
        return result
      },
    },
    'react-router-dom': { useSearchParams: () => [params, (updater) => { params = updater(params) }] },
    '@/api/client': { default: {
      get: () => { throw new Error('Unexpected real query') },
      post: async (url, body) => { writes.push({ method: 'POST', url, body }); if (failPost) throw { response: { data: { error: 'Test oluşturma hatası' } } }; return { data: { data: { id: 'new' } } } },
      put: async (url, body) => { writes.push({ method: 'PUT', url, body }) },
    } },
    '@/lib/utils': { cn: (...values) => values.filter(Boolean).join(' '), toSnakeCase: (value) => value.trim().toLowerCase().replaceAll(' ', '_') },
    '@/hooks/useLanguages': { useLanguages: () => ({ data: [{ code: 'tr', name: 'Türkçe', isDefault: true }], isLoading: false }) },
    '@/lib/field-labels': { FL: { name: {} } },
    '@/lib/i18n-helper': { buildI18nValues: (value) => value },
    '../catalog/CreateProductGroupModal': { CreateProductGroupModal: 'CreateProductGroupModal' },
    './MarketplacesPage': { StoreLogo: 'StoreLogo' },
    './marketplaceOverview': { pickTr: (value, fallback = '') => value.tr ?? Object.values(value)[0] ?? fallback },
    './erpMappingForm': helpers,
  }
  const module = shared ? load('src/pages/catalog/CreateProductGroupModal.tsx', mocks)
    : load('src/pages/marketplaces/MappingPage.tsx', mocks, '\nexports.ErpDictionaryPanel = ErpDictionaryPanel; exports.ErpGroupMapModal = ErpGroupMapModal;')
  return {
    render: (name, props = {}) => { cursor = 0; mutationCursor = 0; return module[name](props) },
    writes, invalidations, queries, params: () => params,
  }
}
function all(node, predicate) {
  if (Array.isArray(node)) return node.flatMap((n) => all(n, predicate))
  if (!node || typeof node !== 'object') return []
  return [...(predicate(node) ? [node] : []), ...all(node.props?.children, predicate), ...all(node.props?.footer, predicate)]
}
function text(node) {
  if (Array.isArray(node)) return node.map(text).join('')
  if (node == null || typeof node === 'boolean') return ''
  return typeof node === 'object' ? text(node.props?.children) : String(node)
}
const button = (tree, label) => all(tree, (n) => (n.type === 'button' || n.type === 'Button') && text(n).trim() === label)[0]
const component = (tree, name) => all(tree, (n) => n.type === name || n.type?.name === name)[0]
const baseProps = { target: 'erp:nebim', overview, ownTypes: [], onClose: () => {}, onSaved: () => {} }

test('ERP status counts codes, keeps conflicts unresolved and excludes inactive/non-group items', () => {
  assert.equal(items.filter((i) => helpers.erpGroupMappingState(i) === 'mapped').length, 1)
  assert.equal(items.filter((i) => helpers.erpGroupMappingState(i) === 'unmapped').length, 2)
  assert.equal(helpers.erpGroupMappingState(items[2]), 'unmapped')
  assert.equal(helpers.erpGroupMappingState(items[3]), null)
  assert.equal(helpers.erpGroupMappingState(items[4]), null)
})

test('ERP tabs select filtered dictionary views and switching to marketplace resets the ERP-only tab', () => {
  const h = harness()
  let tree = h.render('MappingPage')
  assert.ok(button(tree, 'Eşlenenler (1)'))
  assert.ok(button(tree, 'Eşlenmemişler (2)'))
  assert.equal(component(tree, 'ErpDictionaryPanel').props.mappingState, 'unmapped')
  assert.equal(component(tree, 'AttributesTab'), undefined)
  button(tree, 'Eşlenenler (1)').props.onClick()
  tree = h.render('MappingPage')
  assert.equal(component(tree, 'ErpDictionaryPanel').props.mappingState, 'mapped')
  button(tree, 'Trendyol').props.onClick()
  assert.equal(h.params().get('tab'), 'kategoriler')
  assert.equal(component(h.render('MappingPage'), 'ErpDictionaryPanel'), undefined)
})

test('dictionary filters rows, keeps conflicts visible but prevents mapping them, and supports search', () => {
  const h = harness()
  const props = { ...baseProps, mappingState: 'unmapped' }
  let tree = h.render('ErpDictionaryPanel', props)
  assert.deepEqual(all(tree, (n) => n.type === 'tr' && n.key).map((n) => n.key), ['unmapped', 'conflict'])
  assert.equal(all(tree, (n) => n.type === 'Button' && text(n) === 'Eşle').length, 1)
  assert.equal(all(tree, (n) => n.type === 'select')[0].props.disabled, true)
  all(tree, (n) => n.props.placeholder === 'Sözlükte ara')[0].props.onChange({ target: { value: 'Kot' } })
  tree = h.render('ErpDictionaryPanel', props)
  assert.deepEqual(all(tree, (n) => n.type === 'tr' && n.key).map((n) => n.key), ['unmapped'])
  button(tree, 'Eşle').props.onClick()
  assert.equal(component(h.render('ErpDictionaryPanel', props), 'ErpGroupMapModal').props.item.id, 'unmapped')
})

test('query failures show an error instead of a successful empty dictionary or zero counters', () => {
  const h = harness({ queryError: true })
  assert.ok(button(h.render('MappingPage'), 'Eşlenenler (?)'))
  const tree = harness({ queryError: true }).render('ErpDictionaryPanel', { ...baseProps, mappingState: 'mapped' })
  assert.ok(text(tree).includes('ERP sözlüğü yüklenemedi.'))
  assert.ok(!text(tree).includes('Seçili filtreye ve aramaya uyan ERP kaydı yok.'))
})

test('mapped view lists only healthy active ERP group codes', () => {
  const tree = harness().render('ErpDictionaryPanel', { ...baseProps, mappingState: 'mapped' })
  assert.deepEqual(all(tree, (n) => n.type === 'tr' && n.key).map((n) => n.key), ['mapped'])
  assert.equal(button(tree, 'Eşle'), undefined)
})

test('dashboard unmapped filter and explicit status tabs coexist after merge', () => {
  const props = { ...baseProps, onlyUnmappedInitial: true, placeholderProductCount: 7 }
  const tree = harness().render('ErpDictionaryPanel', props)
  const keys = all(tree, (n) => n.type === 'tr' && n.key).map((n) => n.key)
  assert.ok(!keys.includes('mapped'))
  assert.ok(keys.includes('unmapped') && keys.includes('conflict'))
  assert.ok(text(tree).includes('Geçici Grup'))
  const mapped = harness().render('ErpDictionaryPanel', { ...props, mappingState: 'mapped' })
  assert.deepEqual(all(mapped, (n) => n.type === 'tr' && n.key).map((n) => n.key), ['mapped'])
  assert.equal(all(mapped, (n) => n.type === 'input' && n.props.type === 'checkbox').length, 0)
})

test('loading and the API row limit never appear as exact zero/full-inventory counters', () => {
  assert.ok(button(harness({ loading: true }).render('MappingPage'), 'Eşlenenler (…)'))
  const capped = Array.from({ length: 2000 }, (_, i) => ({ ...items[0], id: `id-${i}`, code: String(i) }))
  assert.ok(button(harness({ erpItems: capped }).render('MappingPage'), 'Eşlenenler (2000+)'))
  const tree = harness({ erpItems: capped }).render('ErpDictionaryPanel', { ...baseProps, mappingState: 'mapped' })
  assert.ok(text(tree).includes('İlk 2000 kayıt gösteriliyor'))
})

test('new group permission and cancel preserve the mapping draft without writing anything', () => {
  const h = harness(), props = { ...baseProps, item: items[1] }
  let tree = h.render('ErpGroupMapModal', props)
  assert.equal(component(tree, 'PermissionGuard').props.permission, 'catalog.platform.manage')
  component(tree, 'SearchableSelect').props.onChange('old')
  button(h.render('ErpGroupMapModal', props), 'Yeni Grup').props.onClick()
  tree = h.render('ErpGroupMapModal', props)
  assert.equal(tree.type, 'CreateProductGroupModal')
  assert.equal(tree.props.initialName, 'Kot Ceket')
  tree.props.onClose()
  assert.equal(component(h.render('ErpGroupMapModal', props), 'SearchableSelect').props.value, 'old')
  assert.equal(h.writes.length, 0)
})

test('newly created group is immediately selected, but mapping writes only on explicit Eşle', async () => {
  const h = harness(), props = { ...baseProps, item: items[1] }
  button(h.render('ErpGroupMapModal', props), 'Yeni Grup').props.onClick()
  h.render('ErpGroupMapModal', props).props.onCreated({ id: 'new', nameI18n: { tr: 'Yeni Kot Ceket' } })
  const tree = h.render('ErpGroupMapModal', props)
  const picker = component(tree, 'SearchableSelect')
  assert.equal(picker.props.value, 'new')
  assert.ok(picker.props.options.some((o) => o.value === 'new'))
  assert.equal(h.writes.length, 0)
  assert.equal(button(tree, 'Eşle').props.disabled, false)
  await button(tree, 'Eşle').props.onClick()
  assert.equal(h.writes.length, 1)
  assert.equal(h.writes[0].body.productGroupId, 'new')
  assert.equal(h.writes[0].body.targetExternalId, '02')
})

test('shared create form preserves copy preview/payload and refreshes group/mapping queries on success', async () => {
  const h = harness({ shared: true }), created = []
  const props = { initialName: 'Kot Ceket', onClose: () => {}, onCreated: (g) => created.push(g) }
  let tree = h.render('CreateProductGroupModal', props)
  const picker = component(tree, 'SearchableSelect')
  assert.ok(picker.props.options.some((o) => o.value === 'old' && o.label.includes('(pasif)')))
  picker.props.onChange('old')
  component(tree, 'IntegerInput').props.onChange(30)
  tree = h.render('CreateProductGroupModal', props)
  assert.ok(all(tree, (n) => n.props['data-testid'] === 'copy-preview').length)
  assert.equal(button(tree, 'Kaydet').props.disabled, false)
  await button(tree, 'Kaydet').props.onClick()
  assert.equal(h.writes.length, 1)
  assert.equal(h.writes[0].url, '/catalog/product-groups')
  assert.equal(h.writes[0].body.copyAttributesFromGroupId, 'old')
  assert.equal(h.writes[0].body.nameI18n.tr, 'Kot Ceket')
  assert.equal(h.writes[0].body.sortOrder, 30)
  assert.equal(created[0].id, 'new')
  assert.ok(h.invalidations.includes('product-groups'))
  assert.ok(h.invalidations.includes('mapping-overview'))
})

test('failed creation retains the form and does not select a group or perform mapping', async () => {
  const h = harness({ shared: true, failPost: true }), created = []
  const props = { initialName: 'Kot Ceket', onClose: () => {}, onCreated: (g) => created.push(g) }
  await button(h.render('CreateProductGroupModal', props), 'Kaydet').props.onClick()
  assert.equal(created.length, 0)
  assert.equal(h.writes.length, 1)
  assert.ok(text(h.render('CreateProductGroupModal', props)).includes('Test oluşturma hatası'))
})

test('pending creation cannot close the dialog and empty new names cannot be saved', () => {
  const h = harness({ shared: true, pending: true })
  let closed = 0
  const tree = h.render('CreateProductGroupModal', { onClose: () => { closed++ }, onCreated: () => {} })
  tree.props.onClose()
  assert.equal(closed, 0)
  assert.equal(button(tree, 'İptal').props.disabled, true)
  assert.equal(button(tree, 'Kaydet').props.disabled, true)
})

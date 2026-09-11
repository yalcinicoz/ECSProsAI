const test = require('node:test')
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const ts = require('typescript')
const root = path.join(__dirname, '../src/pages/reports')
const out = ts.transpileModule(fs.readFileSync(path.join(root, 'savedReportValidation.ts'), 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText
const target = {}
new Function('exports', out)(target)
const valid = target.validSavedPlan
test('saved recipes guard corrupted preferences before rendering', () => {
  const plan = { version: 1, subject: 'stock', dimensions: ['productCode'], metrics: ['stock.available'], filters: [] }
  assert.equal(valid(plan), true)
  for (const value of [null, {}, { ...plan, filters: null }, { ...plan, dimensions: [null] }, { ...plan, filters: [{ field: 'x', operator: 'eq', values: null }] }]) assert.equal(valid(value), false)
  const dynamic = { version: 2, source: 'orders', from: '2026-09-01T00:00:00Z', to: '2026-10-01T00:00:00Z', predicate: null, detail: { columns: ['orders.orderNumber'], direction: 'asc', sort: null } }
  assert.equal(valid(dynamic), true)
  assert.equal(valid({ ...dynamic, period: 'lastMonth' }), true)
  assert.equal(valid({ ...dynamic, period: 'allHistory' }), false)
  assert.equal(valid({ ...dynamic, period: ['lastMonth'] }), false)
  assert.equal(valid({ ...dynamic, source: 'stockMovements', detail: { columns: ['movements.type'], direction: 'asc', sort: null } }), true)
  assert.equal(valid({ ...dynamic, source: 'returns', detail: { columns: ['returns.orderNumber'], direction: 'asc', sort: null } }), true)
  assert.equal(valid({ ...dynamic, source: 'customers', detail: { columns: ['customers.id', 'customers.returnCount'], direction: 'desc', sort: 'customers.returnCount' } }), true)
  assert.equal(valid({ ...dynamic, source: 'stock', from: null, to: null, stockGrain: 'variant', detail: { columns: ['barcode', 'stock.quantity', 'stockType'], direction: 'asc' } }), true)
  assert.equal(valid({ ...dynamic, source: 'stock', from: null, to: null, stockGrain: 'guess' }), false)
  assert.equal(valid({ ...dynamic, source: 'arbitrarySql' }), false)
  assert.equal(valid({ ...dynamic, predicate: { kind: 'all', children: {} } }), false)
  assert.equal(valid({ ...dynamic, aggregate: {} }), false)
})
test('initial card window survives saved recipe and rejects wrong source or bounds', () => {
  const plan = { version: 2, source: 'productCards', from: '2025-01-01T00:00:00Z', to: '2025-02-01T00:00:00Z', cardWindowMonths: 6, predicate: null, detail: { columns: ['cards.code'], direction: 'asc' } }
  assert.equal(valid(JSON.parse(JSON.stringify(plan))), true)
  for (const months of [0, -1, 13, 1.5, '6']) assert.equal(valid({ ...plan, cardWindowMonths: months }), false)
  assert.equal(valid({ ...plan, source: 'orders' }), false)
})

test('saved report state is private and loading does not execute a query', () => {
  const component = fs.readFileSync(path.join(root, 'SavedReports.tsx'), 'utf8')
  assert.match(component, /queryKey: \['ai-saved-reports', userId\]/)
  assert.match(component, /gcTime: 0/)
  assert.doesNotMatch(component, /localStorage|\/interpret|\/grid|\/run/)
  const page = fs.readFileSync(path.join(root, 'AiReportsPage.tsx'), 'utf8')
  const load = page.slice(page.indexOf('onLoad={candidate'), page.indexOf('{savedError &&'))
  assert.match(load, /validSavedPlan/)
  assert.match(load, /run.reset\(\)/)
  assert.doesNotMatch(load, /run.mutate/)
})
test('record changes require confirmation and expected snapshot; sharing never uses a query token', () => {
  const component = fs.readFileSync(path.join(root, 'SavedReports.tsx'), 'utf8')
  assert.match(component, /expected: action.item.value/)
  assert.match(component, /Onaylıyor musunuz/)
  assert.match(component, /setConfirmation\(\{ item, kind: 'remove' \}\)/)
  assert.match(component, /reports.ai.share/)
  assert.match(component, /#ai-report=/)
  assert.match(component, /api.post\('\/reports\/ai\/saved\/shared'/)
  assert.doesNotMatch(component, /\?token=/)
})

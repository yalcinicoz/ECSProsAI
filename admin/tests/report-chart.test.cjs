const test = require('node:test')
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const ts = require('typescript')
const compiled = ts.transpileModule(fs.readFileSync(path.join(__dirname, '../src/pages/reports/reportChartData.ts'), 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText
const exportsObject = {}
new Function('exports', compiled)(exportsObject)
const prepare = exportsObject.prepareReportChart
const input = { columns: ['status', 'count'], rows: [['new', 3], ['returned', -2]], totalCount: 2, dimensions: ['status'], metric: 'count', detail: false }

test('chart preserves complete ordered results, negative and zero values', () => {
  assert.deepEqual(prepare(input).points, [{ label: 'new', value: 3 }, { label: 'returned', value: -2 }])
  assert.equal(prepare({ ...input, rows: [[null, 0]], totalCount: 1 }).points[0].label, 'Belirtilmemiş')
  assert.deepEqual(prepare({ ...input, dimensions: [], rows: [['x', 3]], totalCount: 1 }).points, [{ label: 'Genel toplam', value: 3 }])
})
test('chart refuses incomplete pages, excessive groups and detail rows', () => {
  for (const patch of [{ totalCount: 3 }, { detail: true }, { rows: [], totalCount: 0 }, { rows: Array(51).fill(['x', 1]), totalCount: 51 }])
    assert.ok(prepare({ ...input, ...patch }).error)
})
test('chart refuses mixed currencies, invalid columns and nonfinite values', () => {
  const money = { ...input, columns: ['orders.currencyCode', 'count'], dimensions: ['orders.currencyCode'], rows: [['TRY', 1], ['EUR', 2]] }
  assert.ok(prepare(money).error)
  assert.ok(prepare({ ...money, columns: ['returns.currencyCode', 'count'], dimensions: ['returns.currencyCode'] }).error)
  assert.equal(prepare({ ...money, rows: [['TRY', 1], ['TRY', 2]] }).error, null)
  for (const patch of [{ metric: 'hidden' }, { dimensions: ['hidden'] }, { rows: [['x', Infinity]], totalCount: 1 }, { rows: [['x', '3']], totalCount: 1 }, { rows: [['x']], totalCount: 1 }])
    assert.ok(prepare({ ...input, ...patch }).error)
})

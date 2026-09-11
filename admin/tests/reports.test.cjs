const test = require('node:test')
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const read = name => fs.readFileSync(path.join(__dirname, '..', 'src', name), 'utf8')
const page = read('pages/reports/ReportsPage.tsx')
test('reports reuse server paging and shared filters/export, without client financial aggregates', () => {
  assert.match(page, /<DataGrid<Row>/)
  assert.match(page, /grid\.toParams\(\)/)
  assert.match(page, /advancedFilters/)
  assert.match(page, /endpoint: report\.export/)
  assert.doesNotMatch(page, /\.reduce\(/)
  assert.doesNotMatch(page, /api\.(put|delete|patch|post)\(/)
})
test('report permission guard runs before data component mounts; cache is user scoped', () => {
  assert.ok(page.indexOf('if (!hasPermission(report.permission))') < page.indexOf('return <ReportContent'))
  assert.match(page, /\['fixed-report', userId, reportId/)
  assert.match(page, /filter\(\(\[, r\]\) => hasPermission\(r.permission\)\)/)
})
test('unfiltered stock report does not fetch or export all stocks', () => {
  assert.match(page, /const enabled = !!grid.state.search.trim\(\)/)
  assert.match(page, /enabled,/)
  assert.match(page, /export=\{enabled && !error/)
})
test('only stock report remains; operational order and return pages are preserved', () => {
  const nav = read('components/layout/sidebarNavigation.ts')
  const router = read('router/index.tsx')
  for (const id of ['stocks']) {
    assert.ok(nav.includes(`to: '/reports/${id}'`))
    assert.ok(router.includes(`path: 'reports/${id}'`))
  }
  for (const removed of ['reports/orders', 'reports/returns', 'procurement/report']) {
    assert.ok(!nav.includes(removed))
    assert.ok(!router.includes(removed))
  }
  assert.ok(router.includes("path: 'orders'"))
  assert.ok(router.includes("path: 'orders/returns'"))
  assert.match(nav, /id: 'reports', label: 'Raporlar'/)
})
test('removed procurement report has no endpoint or service registration', () => {
  const apiRoot = path.join(__dirname, '../../src/ECSPros.Api')
  assert.ok(!fs.existsSync(path.join(apiRoot, 'Services/ProcurementReportService.cs')))
  assert.doesNotMatch(fs.readFileSync(path.join(apiRoot, 'Program.cs'), 'utf8'), /ProcurementReportService/)
  assert.doesNotMatch(fs.readFileSync(path.join(apiRoot, 'Controllers/ProcurementController.cs'), 'utf8'), /HttpGet\("report"\)/)
})

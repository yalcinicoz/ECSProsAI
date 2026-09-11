const test = require('node:test')
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const vm = require('node:vm')
const ts = require('typescript')

function setup() {
  let me = { userId: 'test', permissions: [], isSuperAdmin: true }
  const exports = {}
  const source = fs.readFileSync(path.join(__dirname, '../src/store/auth.ts'), 'utf8')
  vm.runInNewContext(ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText, {
    exports, localStorage: { setItem() {} }, require: name => {
      if (name === '@/api/client') return { default: { get: async () => ({ data: { data: me } }), post: async () => ({ data: { data: { accessToken: 'test', refreshToken: 'test' } } }) } }
      if (name === '@/lib/adminFavorites') return { clearSessionStoragePreservingFavorites() {} }
      if (name === 'zustand/middleware') return { persist: creator => creator }
      return require(name)
    },
  })
  return { store: exports.useAuthStore, respond: value => { me = value } }
}
test('login and fetchMe preserve API super admin, revoke on false or missing flag', async () => {
  const { store, respond } = setup()
  await store.getState().login('test', 'test')
  assert.equal(store.getState().hasPermission('orders.view'), true)
  await store.getState().fetchMe()
  assert.equal(store.getState().hasPermission('orders.view'), true)
  for (const isSuperAdmin of [false, undefined, 'true']) {
    respond({ userId: 'test', isSuperAdmin, permissions: ['procurement.manage'] })
    await store.getState().fetchMe()
    assert.equal(store.getState().hasPermission('orders.view'), false)
    assert.equal(store.getState().hasPermission('procurement.manage'), true)
  }
})

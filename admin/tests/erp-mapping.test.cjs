const { test } = require('node:test')
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const vm = require('node:vm')
const ts = require('typescript')

// Compile in memory: no build output, API request, database or extra dependency.
function loadSource(relative, overrides = {}, globals = {}) {
  const source = fs.readFileSync(path.join(__dirname, '..', relative), 'utf8')
  const output = ts.transpileModule(source, { compilerOptions: {
    module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText
  const exports = {}
  vm.runInNewContext(output, { exports, require: (name) => overrides[name] ?? require(name), ...globals })
  return exports
}

const { hasCompleteConditions, mergePoolCandidates } = loadSource('src/pages/marketplaces/erpMappingForm.ts')
test('all AND conditions must be complete; incomplete conditions cannot silently disappear', () => {
  const valid = { attributeTypeCode: 'cinsiyet', valueId: 'kadin' }
  assert.equal(hasCompleteConditions([valid]), true)
  assert.equal(hasCompleteConditions([]), false)
  assert.equal(hasCompleteConditions([valid, { attributeTypeCode: 'beden', valueId: '' }]), false)
  assert.equal(hasCompleteConditions([valid, { attributeTypeCode: '', valueId: 'm' }]), false)
  assert.equal(hasCompleteConditions([valid, { attributeTypeCode: 'beden', valueId: ' ' }]), false)
  assert.equal(hasCompleteConditions([valid, { attributeTypeCode: 'beden', valueId: 'm' }]), true)
})
test('new pools support two distinct candidates without counting duplicates', () => {
  const first = { externalId: '1', name: 'Ceket', path: 'Ceket [1]' }
  const second = { externalId: '2', name: 'Kot Ceket', path: 'Kot Ceket [2]' }
  assert.equal(mergePoolCandidates([], [first]).length, 1)
  assert.equal(mergePoolCandidates([], [first, first]).length, 1)
  assert.equal(mergePoolCandidates([], [first, second]).length, 2)
})
test('existing pool candidates are retained and input arrays are not mutated', () => {
  const original = [{ externalId: '1', name: 'Ceket' }, { externalId: '2', name: 'Kot Ceket' }]
  const added = [{ externalId: '3', name: 'Blazer' }]
  const result = mergePoolCandidates(original, added)
  assert.equal(JSON.stringify(result), JSON.stringify([...original, ...added]))
  assert.equal(original.length, 2)
  assert.equal(added.length, 1)
})
test('portal list scroll stays open, outside scroll/resize close it, listeners clean up', () => {
  class Node {}
  const list = new Node()
  const outside = new Node()
  const effects = [], closed = [], listeners = new Map(), windowListeners = new Map()
  let stateIndex = 0, refIndex = 0
  const react = {
    useState: (initial) => [stateIndex++ === 0 ? true : initial, (value) => closed.push(value)],
    useRef: () => ({ current: refIndex++ === 0 ? { contains: (target) => target === list } : null }),
    useEffect: (effect) => effects.push(effect), useId: () => 'test-select',
  }
  const eventHost = (map) => ({
    addEventListener: (name, handler) => map.set(name, handler),
    removeEventListener: (name, handler) => { assert.equal(map.get(name), handler); map.delete(name) },
  })
  const { SearchableSelect } = loadSource('src/components/ui/SearchableSelect.tsx', {
    react, 'react-dom': { createPortal: (element) => element }, '@/lib/utils': { cn: () => '' },
    'lucide-react': { ChevronDown: () => null, X: () => null, Search: () => null },
  }, { Node, document: eventHost(listeners), window: eventHost(windowListeners) })
  SearchableSelect({ value: null, onChange: () => {}, options: [], portal: true })
  const cleanup = effects[1]()
  listeners.get('scroll')({ target: list })
  assert.equal(closed.length, 0)
  listeners.get('scroll')({ target: outside })
  assert.deepEqual(closed, [false])
  windowListeners.get('resize')()
  assert.deepEqual(closed, [false, false])
  cleanup()
  assert.equal(listeners.size, 0)
  assert.equal(windowListeners.size, 0)
})

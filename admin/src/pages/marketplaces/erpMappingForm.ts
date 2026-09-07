export function hasCompleteConditions(conditions: { attributeTypeCode: string; valueId: string }[]) {
  return conditions.length > 0 && conditions.every((c) => c.attributeTypeCode.trim() && c.valueId.trim())
}

export function mergePoolCandidates<T extends { externalId: string }>(existing: T[], added: T[]): T[] {
  const candidates = new Map(existing.map((candidate) => [candidate.externalId, candidate]))
  for (const candidate of added) candidates.set(candidate.externalId, candidate)
  return [...candidates.values()]
}

export type ErpGroupMappingState = 'mapped' | 'unmapped'

// Count ERP codes, not our groups: direct/rules/pool status is resolved by the API.
export function erpGroupMappingState(item: {
  kind: string; isActive: boolean; isMapped: boolean; mappingConflict?: boolean
}): ErpGroupMappingState | null {
  if (item.kind !== 'product_group' || !item.isActive) return null
  return item.isMapped && !item.mappingConflict ? 'mapped' : 'unmapped'
}

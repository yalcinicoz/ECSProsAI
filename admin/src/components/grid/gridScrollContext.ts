import { createContext, useContext } from 'react'

export interface GridScrollEntry {
  id: string
  el: HTMLDivElement
  /** viewport'ta görünen oran (0..1) */
  visibility: number
  /** tablonun alt kenarı (gerçek scrollbar) viewport'ta mı */
  bottomVisible: boolean
  lastInteraction: number
}

export interface GridScrollRegistryApi {
  register: (entry: GridScrollEntry) => () => void
  report: (id: string, patch: Partial<Pick<GridScrollEntry, 'visibility' | 'bottomVisible' | 'lastInteraction'>>) => void
}

export const GridScrollContext = createContext<GridScrollRegistryApi | null>(null)

export function useGridScrollRegistry(): GridScrollRegistryApi | null { return useContext(GridScrollContext) }

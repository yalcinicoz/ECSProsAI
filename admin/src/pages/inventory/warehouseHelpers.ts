import type { Warehouse } from './WarehousesPage'

export function getWarehouseName(warehouse: Warehouse): string {
  return warehouse.nameI18n['tr']
    ?? warehouse.nameI18n[Object.keys(warehouse.nameI18n)[0]]
    ?? warehouse.code
}

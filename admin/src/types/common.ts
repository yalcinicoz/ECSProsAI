export interface ApiResponse<T> {
  success: boolean
  data: T
  error?: string
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
}

export interface SelectOption {
  value: string
  label: string
}

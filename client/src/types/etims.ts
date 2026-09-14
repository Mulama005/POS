/** A single row from KRA's synced product taxonomy. */
export interface EtimsItemClassOption {
  itemClsCd: string
  itemClsNm: string
  itemClsLvl: number
  taxTyCd: string | null
  mjrTgYn: boolean | null
  useYn: boolean

  /** True when this classification has deeper taxonomy nodes below it. */
  hasChildren: boolean

  /** True when this classification can be assigned to a product. */
  selectable: boolean
}

export interface EtimsItemClassSearchResponse {
  total: number
  page: number
  pageSize: number
  items: EtimsItemClassOption[]
}

/** Response from GET /api/admin/etims/item-classes/children. */
export interface EtimsItemClassChildrenResponse {
  parent: EtimsItemClassOption | null
  items: EtimsItemClassOption[]
}

/** Matches the response shape of POST /api/products/assign-etims-classification. */
export interface AssignEtimsClassificationResult {
  updatedProductIds: string[]
  itemClsCd: string
  itemClsNm: string
  taxTyCd: string | null
  reclassifiedCount: number
}
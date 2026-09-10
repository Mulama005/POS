export type TaxClass = 'Standard' | 'ZeroRated' | 'Exempt'

/** Matches ProductSummaryDto from ProductsController — the read-only lookup
 * contract Step 18's full product module extends rather than replaces. */
export interface ProductSummary {
  id: string
  sku: string
  barcode: string
  name: string
  categoryId: string
  categoryName: string
  salePrice: number
  taxClass: TaxClass
  stockQuantity: number
  imageUrl: string | null
}

/** Matches CategoryDto from CategoriesController. */
export interface Category {
  id: string
  name: string
  description: string | null
  isActive: boolean
  /** True for serialized categories (phones — tracked one StockUnit per
   * physical item); false for bulk categories (cables, chargers — tracked
   * as a plain on-hand quantity). See Product.StockQuantity server-side. */
  requiresSerialTracking: boolean
  productCount: number
}

/** Matches Pos.Application.Features.Products.ProductDto — the full
 * management shape (list/get), distinct from the leaner ProductSummary used
 * by checkout's search/lookup. */
export interface Product {
  id: string
  sku: string
  barcode: string | null
  name: string
  description: string | null
  categoryId: string
  categoryName: string
  costPrice: number
  salePrice: number
  taxClass: TaxClass
  imageUrl: string | null
  reorderThreshold: number
  warrantyMonths: number
  isActive: boolean
  /** Combined bulk + serialized on-hand quantity. */
  stockCount: number
}

export interface ProductListResponse {
  items: Product[]
  total: number
  page: number
  pageSize: number
}

/** Fields the create/edit form collects — posted as multipart/form-data
 * (ProductController's [FromForm] binding) since an image file can ride
 * along. The image itself is passed separately to productsService, not as
 * part of this object, so the same shape works for both create and update. */
export interface ProductFormValues {
  sku: string
  barcode: string
  name: string
  description: string
  categoryId: string
  costPrice: string
  salePrice: string
  taxClass: TaxClass
  reorderThreshold: string
  warrantyMonths: string
}
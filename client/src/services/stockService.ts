import { apiClient } from './apiClient'

export interface ReceiveSerialStockRequest {
  productId: string
  serialNumbers: string[]
  imei?: string[]
  purchaseDate?: string
}

export async function receiveSerialStock(request: ReceiveSerialStockRequest): Promise<{ added: number }> {
  const { data } = await apiClient.post<{ added: number }>('/api/stock/receive', request)
  return data
}

export interface ReceiveBulkStockRequest {
  productId: string
  quantity: number
}

export async function receiveBulkStock(
  request: ReceiveBulkStockRequest,
): Promise<{ productId: string; newQuantity: number }> {
  const { data } = await apiClient.post<{ productId: string; newQuantity: number }>(
    '/api/stock/receive-bulk',
    request,
  )
  return data
}

export interface SourceStockRequest {
  clientTransactionId: string
  productId: string
  supplierName: string
  quantity: number
  unitCost: number
  serialNumbers?: string[]
  reference?: string | null
}

export interface SourceStockResult {
  purchaseId: string
  productId: string
  productName: string
  quantity: number
  totalCost: number
  newQuantity: number
  averageCost: number
  replayed: boolean
}

export async function sourceStock(request: SourceStockRequest): Promise<SourceStockResult> {
  const { data } = await apiClient.post<SourceStockResult>('/api/stock/source', request)
  return data
}

export interface WarrantyInfo {
  name: string
  serial: string
  saleDate: string | null
  warrantyMonths: number
  expiryDate: string | null
  status: string
  isUnderWarranty: boolean
}

export async function warrantyLookup(serialOrImei: string): Promise<WarrantyInfo> {
  const { data } = await apiClient.get<WarrantyInfo>(
    `/api/stock/warranty/${encodeURIComponent(serialOrImei)}`,
  )
  return data
}

import { apiClient } from './apiClient'
import { DiscountApprovalRequiredError, type CompleteSaleRequest, type CompleteSaleResult } from '../types/sale'

/** A Manager/Admin re-enters their own credentials to approve a discount above the
 * threshold, without switching the active (cashier's) session. Returns a short-lived
 * token to include in the completeSale call. */
export async function approveDiscount(email: string, password: string): Promise<string> {
  const { data } = await apiClient.post<{ approvalToken: string }>('/api/sales/approve-discount', {
    email,
    password,
  })
  return data.approvalToken
}

/** Throws DiscountApprovalRequiredError (HTTP 428) if the cart's discount exceeds the
 * configured threshold and no valid approval token was supplied — callers should catch
 * that specifically to prompt for Manager/Admin approval and retry. */
export async function completeSale(request: CompleteSaleRequest): Promise<CompleteSaleResult> {
  try {
    const { data } = await apiClient.post<CompleteSaleResult>('/api/sales', request)
    return data
  } catch (error: unknown) {
    if (isPreconditionRequired(error)) {
      const message =
        (error as { response?: { data?: { message?: string } } }).response?.data?.message ??
        'This discount needs Manager/Admin approval.'
      const totalDiscount = request.items.reduce((sum, i) => sum + i.discountAmount, 0) + request.cartDiscountAmount
      throw new DiscountApprovalRequiredError(totalDiscount, message)
    }
    throw error
  }
}

function isPreconditionRequired(error: unknown): boolean {
  return (
    typeof error === 'object' &&
    error !== null &&
    'response' in error &&
    (error as { response?: { status?: number } }).response?.status === 428
  )
}
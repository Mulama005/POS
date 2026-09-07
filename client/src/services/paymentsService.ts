import { apiClient } from './apiClient'

export interface MpesaPaymentStatus {
  paymentId: string
  method: string
  status: 'Pending' | 'Success' | 'Failed' | 'TimedOut' | string
  externalReference: string | null
  processedAt: string | null
}

/** Triggers (or re-triggers, on retry) the STK Push prompt on the customer's phone. */
export async function initiateMpesaPayment(
  saleId: string,
  paymentId: string,
  phoneNumber: string,
): Promise<{ checkoutRequestId: string }> {
  const { data } = await apiClient.post<{ checkoutRequestId: string }>(
    `/api/sales/${saleId}/payments/${paymentId}/mpesa/initiate`,
    { phoneNumber },
  )
  return data
}

/** Polled while waiting for the customer to respond to the STK Push prompt. */
export async function getPaymentStatus(saleId: string, paymentId: string): Promise<MpesaPaymentStatus> {
  const { data } = await apiClient.get<MpesaPaymentStatus>(`/api/sales/${saleId}/payments/${paymentId}`)
  return data
}
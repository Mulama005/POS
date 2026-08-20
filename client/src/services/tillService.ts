import { apiClient } from './apiClient'
import type { TillReconciliation, TillSession } from '../types/till'

/** Null means the till is currently closed for this register — not an error. */
export async function getCurrentTillSession(registerId: string): Promise<TillSession | null> {
  const { data, status } = await apiClient.get<TillSession | ''>('/api/till/current', {
    params: { registerId },
    validateStatus: (s) => s === 200 || s === 204,
  })
  return status === 204 ? null : (data as TillSession)
}

export async function openTill(registerId: string, openingFloat: number): Promise<TillSession> {
  const { data } = await apiClient.post<TillSession>('/api/till/open', { registerId, openingFloat })
  return data
}

export async function closeTill(registerId: string, countedCashAtClose: number): Promise<TillReconciliation> {
  const { data } = await apiClient.post<TillReconciliation>('/api/till/close', { registerId, countedCashAtClose })
  return data
}
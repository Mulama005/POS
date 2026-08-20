import { apiClient } from './apiClient'

export interface RegisterSummary {
  id: string
  name: string
  location: string | null
  isTillOpen: boolean
}

export async function listRegisters(): Promise<RegisterSummary[]> {
  const { data } = await apiClient.get<RegisterSummary[]>('/api/registers')
  return data
}
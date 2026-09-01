import { apiClient } from './apiClient'
import type { Category } from '../types/product'

export async function listCategories(): Promise<Category[]> {
  const { data } = await apiClient.get<Category[]>('/api/categories')
  return data
}
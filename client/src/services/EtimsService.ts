import { apiClient } from './apiClient'
import type {
  AssignEtimsClassificationResult,
  EtimsItemClassChildrenResponse,
  EtimsItemClassOption,
  EtimsItemClassSearchResponse,
} from '../types/etims'

/**
 * Search the synced KRA product taxonomy.
 *
 * Search is intentionally leaf-only because its purpose is to provide
 * a fast shortcut to an assignable classification.
 */
export async function searchEtimsItemClasses(
  query: string,
  page = 1,
): Promise<EtimsItemClassSearchResponse> {
  const { data } = await apiClient.get<EtimsItemClassSearchResponse>(
    '/api/admin/etims/item-classes',
    {
      params: {
        q: query || undefined,
        leafOnly: true,
        page,
        pageSize: 20,
      },
    },
  )

  return data
}

/**
 * Load the direct children of a KRA taxonomy node.
 *
 * Omit parentCode for the root level. The backend then returns
 * the level-1 Segment nodes.
 */
export async function getEtimsItemClassChildren(
  parentCode?: string,
): Promise<EtimsItemClassChildrenResponse> {
  const { data } = await apiClient.get<EtimsItemClassChildrenResponse>(
    '/api/admin/etims/item-classes/children',
    {
      params: {
        parentCode: parentCode || undefined,
      },
    },
  )

  return data
}

/**
 * Assign one classification to a batch of products.
 *
 * The server re-validates the classification against the synced
 * KRA taxonomy before making any changes.
 */
export async function assignEtimsClassification(
  productIds: string[],
  itemClsCd: string,
): Promise<AssignEtimsClassificationResult> {
  const { data } = await apiClient.post<AssignEtimsClassificationResult>(
    '/api/products/assign-etims-classification',
    {
      productIds,
      itemClsCd,
    },
  )

  return data
}

export type { EtimsItemClassOption }
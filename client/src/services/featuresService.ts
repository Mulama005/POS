import { apiClient } from './apiClient'

export interface Features {
  etimsEnabled: boolean
}

let cached: Promise<Features> | null = null

/** Fetched once per page load; the flags only change when the server config changes. */
export function getFeatures(): Promise<Features> {
  if (!cached) {
    cached = apiClient
      .get<Features>('/api/features')
      .then((r) => r.data)
      .catch((err) => {
        cached = null // allow a retry on the next call
        throw err
      })
  }
  return cached
}
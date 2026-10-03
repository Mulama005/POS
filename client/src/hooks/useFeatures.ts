import { useEffect, useState } from 'react'
import { getFeatures, type Features } from '../services/featuresService'

const OFF: Features = { etimsEnabled: false }

/** Optional-integration flags. Defaults to everything off until the server answers. */
export function useFeatures(): Features {
  const [features, setFeatures] = useState<Features>(OFF)

  useEffect(() => {
    let cancelled = false
    getFeatures()
      .then((f) => { if (!cancelled) setFeatures(f) })
      .catch(() => { /* keep defaults - eTIMS UI stays hidden */ })
    return () => { cancelled = true }
  }, [])

  return features
}
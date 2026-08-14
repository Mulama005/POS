import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import * as Sentry from '@sentry/react'
import './index.css'
import App from './App.tsx'
import { AuthProvider } from './store/AuthContext'

Sentry.init({
  dsn: import.meta.env.VITE_SENTRY_DSN,
  environment: import.meta.env.VITE_SENTRY_ENVIRONMENT,
  integrations: [Sentry.browserTracingIntegration()],
  tracesSampleRate: import.meta.env.VITE_SENTRY_ENVIRONMENT === 'production' ? 0.1 : 1.0,
})

const queryClient = new QueryClient()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
      <BrowserRouter>
          <QueryClientProvider client={queryClient}>
              <Sentry.ErrorBoundary fallback={<p>Something went wrong. Please refresh.</p>}>
                  <AuthProvider>
                      <App />
                  </AuthProvider>
              </Sentry.ErrorBoundary>
          </QueryClientProvider>
      </BrowserRouter>
  </StrictMode>,
)
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig({
    server: {
        proxy: {
            '/api': {
                target: 'http://localhost:5179',  // Change 5000 to your ASP.NET Core port
                changeOrigin: true,
                rewrite: (path) => path,
            },
        },
    },
  plugins: [
      react(),
      VitePWA({
          registerType: 'autoUpdate',
          manifest: {
              name: 'My App',
              short_name: 'App',
              start_url: '/',
              display: 'standalone',
              background_color: '#ffffff',
              theme_color: '#ffffff',
              icons: [
                  {
                      src: 'pwa-192x192.png',
                      sizes: '192x192',
                      type: 'image/png',
                  },
                  {
                      src: 'pwa-512x512.png',
                      sizes: '512x512',
                      type: 'image/png',
                  },
              ],
          },
      }),
  ],
})

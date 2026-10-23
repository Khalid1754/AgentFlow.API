import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Under utveckling skickas /api vidare till .NET-backenden.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': process.env.API_URL ?? 'http://localhost:5098',
    },
  },
})

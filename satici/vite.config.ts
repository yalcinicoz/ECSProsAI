import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

// Dev proxy hedefi: VITE_API_TARGET ile ezilebilir (örn. izole test instance'ı 5051).
const apiTarget = process.env.VITE_API_TARGET || 'http://localhost:5050'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  base: '/',  // 2026-08-11: satici.* subdomain kökünden servis edilir
  server: {
    port: 3001,
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
      },
    },
  },
})

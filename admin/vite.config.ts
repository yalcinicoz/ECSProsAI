import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

export default defineConfig(({ mode }) => {
  // Yalnız geliştirme proxy'si; VITE_ öneki yok, istemci paketine aktarılmaz.
  const target = loadEnv(mode, __dirname, 'ADMIN_DEV_').ADMIN_DEV_API_TARGET || 'http://localhost:5050'
  return {
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  base: '/admin',
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          'vendor-react': ['react', 'react-dom', 'react-router-dom'],
          'vendor-query': ['@tanstack/react-query'],
          'vendor-form': ['react-hook-form', '@hookform/resolvers', 'zod'],
          'vendor-ui': ['lucide-react', 'clsx', 'tailwind-merge'],
        },
      },
    },
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target,
        changeOrigin: true,
      },
      // Ürün Kartı sayfasının SSR önizleme iframe'i (prod'da nginx "/" proxy'si karşılar)
      '/onizleme': {
        target,
        changeOrigin: true,
      },
      '/hubs': {
        target: target.replace(/^http/, 'ws'),
        ws: true,
        changeOrigin: true,
      },
    },
  },
  }
})

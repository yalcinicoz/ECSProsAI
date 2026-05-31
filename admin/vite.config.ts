import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

export default defineConfig({
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
        target: 'http://localhost:5050',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'ws://localhost:5050',
        ws: true,
        changeOrigin: true,
      },
    },
  },
})

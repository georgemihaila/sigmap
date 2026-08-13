import { fileURLToPath, URL } from 'node:url';

import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vitest/config';

// REST and the live gRPC-Web stream are proxied to the backend. Swap the target
// to point at another backend without touching app code.
const proxyTarget = process.env.VITE_PROXY_TARGET ?? 'http://localhost:5090';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    host: '127.0.0.1',
    port: 5173,
    proxy: {
      '/api': { target: proxyTarget, changeOrigin: true },
      // gRPC-Web live stream (route is the lowercase package-qualified method).
      '/sigmap.live.v1.LiveStream': { target: proxyTarget, changeOrigin: true },
    },
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          'maplibre': ['maplibre-gl'],
          'charts': ['recharts'],
        },
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    css: true,
  },
});

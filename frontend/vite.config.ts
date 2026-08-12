import { fileURLToPath, URL } from 'node:url';

import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vitest/config';

// When not running against the MSW mock layer (VITE_ENABLE_MOCKS=false), REST
// and the live gRPC-Web stream are proxied to the BFF. Swap the target to point
// at the real backend without touching app code.
const proxyTarget = process.env.VITE_PROXY_TARGET ?? 'http://localhost:5090';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    host: true,
    port: 5173,
    proxy: {
      '/api': { target: proxyTarget, changeOrigin: true },
      '/LiveStream': { target: proxyTarget, changeOrigin: true },
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

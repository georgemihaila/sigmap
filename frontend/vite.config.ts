import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// In-container dev the BFF is reached by service name; on a host it's localhost.
const proxyTarget = process.env.VITE_PROXY_TARGET ?? 'http://localhost:5090';

export default defineConfig({
  plugins: [react()],
  server: {
    host: true,
    port: 5173,
    proxy: {
      // REST: frontend only talks to the BFF (same-origin in prod).
      '/api': { target: proxyTarget, changeOrigin: true },
      // gRPC-Web streaming.
      '/sigmap.live.LiveStream': { target: proxyTarget, changeOrigin: true },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
  },
});

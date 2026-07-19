import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// In development /api is proxied to the local EMS.API, so no CORS setup is
// needed. In production set VITE_API_URL and configure Cors:AllowedOrigins
// on the API instead.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5102',
        changeOrigin: true,
      },
    },
  },
});

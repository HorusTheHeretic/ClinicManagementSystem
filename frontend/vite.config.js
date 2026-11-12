import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// In development the React app calls /api/... and Vite forwards it to the ASP.NET Core API.
// For a production build, serve the dist/ folder and set VITE_API_URL to the API address.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: 'http://localhost:5080', changeOrigin: true },
    },
  },
});

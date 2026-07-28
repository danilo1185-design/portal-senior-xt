import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// O backend .NET roda em http://localhost:5203 (perfil "http" do launchSettings).
// Em dev, o proxy encaminha /api para lá — evita CORS e mantém as URLs relativas,
// exatamente como quando o app for servido pelo wwwroot da API em produção.
const API_TARGET = process.env.VITE_API_TARGET ?? 'http://localhost:5203'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: API_TARGET,
        changeOrigin: true,
      },
    },
  },
})

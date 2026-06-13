import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  base: '/ELMAH_ROOT/',
  build: {
    outDir: '../../ElmahCore.Mvc/wwwroot',
    emptyOutDir: true,
    rollupOptions: {
      output: {
        entryFileNames: 'js/[name].[hash].js',
        chunkFileNames: 'js/[name].[hash].js',
        assetFileNames: (assetInfo) => {
          const name = assetInfo.name || '';
          if (/\.(css)$/.test(name)) {
            return 'css/[name].[hash].[ext]';
          }
          if (/\.(png|jpe?g|gif|svg|webp|ico)$/.test(name)) {
            return 'img/[name].[hash].[ext]';
          }
          if (/\.(woff2?|eot|ttf|otf)$/.test(name)) {
            return 'fonts/[name].[hash].[ext]';
          }
          return '[name].[hash].[ext]';
        }
      }
    }
  },
  server: {
    port: 3000,
    proxy: {
      '/elmah/api': {
        target: 'http://localhost:5000',
        changeOrigin: true
      }
    }
  }
})

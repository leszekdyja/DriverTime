import { defineConfig, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'
import { createAppIcon } from './scripts/appIcons'

const iconSizes = new Map([["/app-icon-192.png", 192], ["/app-icon-512.png", 512]]);
const appIcons: Plugin = {
  name: 'drivertime-app-icons',
  configureServer(server) {
    server.middlewares.use((request, response, next) => {
      const size = iconSizes.get((request.url ?? '').split('?')[0]);
      if (!size) return next();
      response.setHeader('Content-Type', 'image/png');
      response.end(createAppIcon(size));
    });
  },
  generateBundle() {
    for (const [path, size] of iconSizes) this.emitFile({ type: 'asset', fileName: path.slice(1), source: createAppIcon(size) });
  },
};

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), appIcons],
})

import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';
export default defineConfig({
    plugins: [
        react(),
        VitePWA({
            registerType: 'autoUpdate',
            manifest: {
                name: 'Billares POS',
                short_name: 'Billares POS',
                description: 'Operacion de bares y mesas de billar',
                theme_color: '#111827',
                background_color: '#f8fafc',
                display: 'standalone',
                start_url: '/'
            }
        })
    ],
    server: { port: 5173 }
});

import {fileURLToPath, URL} from 'node:url'
import {defineConfig} from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
    plugins: [vue(), tailwindcss()],
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url)),
        },
    },
    build: {
        rollupOptions: {
            output: {
                manualChunks: {
                    // 对较大的第三方库进行单独打包
                    'naive-ui': ['naive-ui'],
                    'echarts-core': ['echarts/core'],
                    'echarts-charts': ['echarts/charts'],
                    'echarts-components': ['echarts/components'],
                    'markdown-it': ['markdown-it'],
                    'prismjs': ['prismjs']
                    // 移除了 'client-application' 的手动分块，避免循环依赖问题
                }
            },
            // 调整块大小警告限制
            chunkSizeWarningLimit: 1500
        }
    },
    test: {
        environment: 'jsdom',
        globals: true,
        setupFiles: ['./src/tests/setup.ts'],
        deps: {
            // 禁用依赖预构建，避免@vue/devtools-kit在导入时访问localStorage
            prebundle: false
        },
        // 忽略某些文件和目录
        exclude: ['node_modules/**', 'dist/**', '**.config.*']
    }
});
import {defineConfig, devices} from '@playwright/test'

// 这些 E2E 测试默认连接本地已运行的前端（:5173）与后端（:5052），
// 使用后端工作目录下的 SQLite Data.db。可通过环境变量覆盖。
export default defineConfig({
    testDir: './e2e',
    globalSetup: './e2e/global-setup.mjs',
    globalTeardown: './e2e/global-teardown.mjs',
    timeout: 90_000,
    expect: {timeout: 15_000},
    fullyParallel: false,
    workers: 1,
    retries: 0,
    reporter: [['list']],
    use: {
        baseURL: process.env.E2E_BASE_URL || 'http://localhost:5173',
        acceptDownloads: true,
        headless: true,
        ignoreHTTPSErrors: true,
        actionTimeout: 15_000,
        navigationTimeout: 30_000,
        trace: 'retain-on-failure',
    },
    projects: [
        {name: 'chromium', use: {...devices['Desktop Chrome']}},
    ],
})

import {defineConfig, devices} from '@playwright/test'

/**
 * Mocked-API UI suite.
 *
 * The default `playwright.config.ts` boots the real backend + SQLite database.
 * This config instead starts only the Vite dev server and lets every spec mock
 * `**\/api/**` with `page.route`, so the suite needs no backend.
 */
export default defineConfig({
    testDir: './e2e-ui',
    timeout: 60_000,
    expect: {timeout: 10_000},
    fullyParallel: false,
    workers: 1,
    retries: 0,
    reporter: [['list']],
    use: {
        baseURL: 'http://127.0.0.1:4174',
        acceptDownloads: true,
        headless: true,
        actionTimeout: 10_000,
        navigationTimeout: 30_000,
        trace: 'retain-on-failure',
    },
    projects: [
        {
            name: 'chrome',
            use: {
                ...devices['Desktop Chrome'],
                channel: 'chrome',
            },
        },
    ],
    webServer: {
        command: 'pnpm exec vite --host 127.0.0.1 --port 4174 --strictPort',
        url: 'http://127.0.0.1:4174',
        reuseExistingServer: true,
        timeout: 120_000,
    },
})

import {expect, test} from '@playwright/test'
import {injectSession, makeJwt, mockApi} from './helpers'

/**
 * Mocked-API UI regression suite for C11/C12/C13/C14/C15/C16.
 * No backend or database is required: all `/api/**` calls are mocked.
 *
 * Run with:
 *   pnpm exec playwright test -c playwright.ui.config.ts
 */

test.describe('C11/C12 数据导出', () => {
    test('干净 localStorage 下导出可下载，且提示为部分数据导出', async ({page}) => {
        await injectSession(page, {role: 'Founder'})
        await mockApi(page)
        await page.goto('/Centre/Admin')

        await expect(page.getByRole('heading', {name: '概览'})).toBeVisible()
        // 正常登录只会写入 accessToken / refreshToken，不存在旧的 Authorization 键。
        expect(await page.evaluate(() => localStorage.getItem('Authorization'))).toBeNull()

        await page.locator('.ios-dropdown-trigger > button').click()
        const exportButton = page.locator('.ios-dropdown-trigger .apple-dropdown button').first()
        await expect(exportButton).toBeVisible()

        const downloadPromise = page.waitForEvent('download', {timeout: 10_000})
        await exportButton.click()
        const download = await downloadPromise

        expect(download.suggestedFilename()).toMatch(/partial-export/)
        await expect(exportButton).toContainText('部分数据')
        await expect(page.getByText('未授权')).toHaveCount(0)
    })
})

test.describe('C13 会话状态', () => {
    test('退出接口失败时仍清除本地凭证，并单独提示撤销失败', async ({page}) => {
        await injectSession(page, {role: 'Founder'})
        await mockApi(page, {logoutFails: true})

        await page.goto('/Centre')
        await expect(page.getByRole('heading', {name: '控制中心'})).toBeVisible()

        await page.getByRole('button', {name: '退出登录'}).click()

        await expect.poll(
            async () => page.evaluate(() => ({
                accessToken: localStorage.getItem('accessToken'),
                refreshToken: localStorage.getItem('refreshToken'),
            })),
            {timeout: 10_000},
        ).toEqual({accessToken: null, refreshToken: null})

        // 服务端撤销失败需要单独反馈，不能静默。
        await expect(page.getByText(/服务器撤销会话失败/)).toBeVisible()

        // 重新加载后不能从 localStorage 读回旧登录态。
        await page.goto('/Centre')
        await expect(page).toHaveURL(/\/login$/)
    })

    test('自动刷新后路由守卫认可新令牌，不跳回登录页', async ({page}) => {
        const refreshedToken = makeJwt('Founder', {expSecondsFromNow: 7200})
        await injectSession(page, {role: 'Founder', expired: true})
        await mockApi(page, {refreshedToken})

        await page.goto('/')
        await expect(page.getByRole('button', {name: '个人中心'})).toBeVisible()

        // 通过应用自身的 ApiService 触发 ensureValidToken 自动刷新。
        const tokenAfterRefresh = await page.evaluate(async () => {
            const {apiRequest} = await import('/src/services/ApiService.ts')
            await apiRequest({url: '/api/User/data', method: 'GET'})
            return localStorage.getItem('accessToken')
        })
        expect(tokenAfterRefresh).toBe(refreshedToken)

        // 此时 Pinia 仍持有旧令牌；使用客户端路由跳转（而非整页刷新）验证守卫。
        await page.getByRole('button', {name: '个人中心'}).click()
        await expect(page).toHaveURL(/\/Centre$/)
        await expect(page.getByRole('heading', {name: '控制中心'})).toBeVisible()
    })
})

test.describe('C14 统计不可用', () => {
    test('统计请求失败时显示不可用与重试，而不是示例数字', async ({page}) => {
        await injectSession(page, {role: 'Founder'})
        await mockApi(page, {dataCentreFails: true})

        await page.goto('/Centre')

        await expect(page.getByText('统计数据暂不可用')).toBeVisible()
        await expect(page.getByRole('button', {name: '重试'})).toBeVisible()

        // 128/42/24/4 是旧代码里的示例值，不能被当作真实查询结果展示。
        for (const sample of ['128', '42', '24', '4']) {
            await expect(page.getByText(sample, {exact: true})).toHaveCount(0)
        }
    })
})

test.describe('C15 权限入口对齐', () => {
    test('部员可见社团资源与公告文章入口', async ({page}) => {
        await injectSession(page, {role: 'Department'})
        await mockApi(page)
        await page.goto('/Centre')

        await expect(page.getByRole('link', {name: '社团资源'})).toBeVisible()
        await expect(page.getByRole('link', {name: '公告文章'})).toBeVisible()
    })

    test('部长看不到分类管理，直接访问分类路由被拒绝', async ({page}) => {
        await injectSession(page, {role: 'Minister'})
        await mockApi(page)
        await page.goto('/Centre/Article')

        await expect(page.getByText('UI 测试文章')).toBeVisible()
        await expect(page.getByText('分类管理')).toHaveCount(0)

        // 菜单隐藏不是权限边界：直接导航必须被路由守卫拦下。
        await page.goto('/Centre/Category')
        await expect(page).toHaveURL(/\/access-denied$/)
        await expect(page.getByText('访问被拒绝')).toBeVisible()
    })
})

test.describe('C16 删除与抹除入口', () => {
    test('文章删除反馈与永久删除一致', async ({page}) => {
        await injectSession(page, {role: 'Minister'})
        await mockApi(page)
        await page.goto('/Centre/Article')

        await expect(page.getByText('UI 测试文章')).toBeVisible()
        const card = page.locator('.ios-card').filter({hasText: 'UI 测试文章'}).first()
        await card.hover()
        await card.locator('.action-btn-icon[title="删除"]').click()

        await page.locator('.n-dialog__action').getByRole('button', {name: '删除'}).click()

        await expect(page.getByText('已永久删除')).toBeVisible()
        await expect(page.getByText(/废纸篓/)).toHaveCount(0)
    })

    test('概览页不再提供清空数据入口', async ({page}) => {
        await injectSession(page, {role: 'Founder'})
        await mockApi(page)
        await page.goto('/Centre/Admin')

        await page.locator('.ios-dropdown-trigger > button').click()
        await expect(page.getByText('清空数据')).toHaveCount(0)
    })
})

test.describe('C18 账号模型', () => {
    test('只有 Staff 档案的账号在主站被拒绝并清理本地会话', async ({page}) => {
        await injectSession(page, {role: 'Founder'})
        await mockApi(page, {userDataNotFound: true})

        await page.goto('/Centre')

        await expect(page).toHaveURL(/\/login$/)
        await expect.poll(
            async () => page.evaluate(() => ({
                accessToken: localStorage.getItem('accessToken'),
                refreshToken: localStorage.getItem('refreshToken'),
            })),
            {timeout: 10_000},
        ).toEqual({accessToken: null, refreshToken: null})
    })
})

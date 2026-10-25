import {expect, test, type Page} from '@playwright/test'
import {injectSession, makeJwt, mockApi} from './helpers'

/**
 * Mocked-API UI regression suite for C28/C29/C30.
 * No backend or database is required: all `/api/**` calls are mocked.
 *
 * Run with:
 *   pnpm exec playwright test -c playwright.ui.config.ts
 */

const MEMBER_UPDATE_ROW = {
    userId: '9000000001',
    userName: 'UI 测试用户',
    academy: '测试学院',
    politicalLandscape: '群众',
    gender: '男',
    className: '测试班',
    phoneNum: '13800000000',
}

/** 通过隐藏的文件输入触发成员 JSON 导入（等价于点击“导入”选择文件）。 */
async function importMembersJson(page: Page, data: unknown) {
    await page.locator('input[type="file"][accept=".json"]').setInputFiles({
        name: 'members.json',
        mimeType: 'application/json',
        buffer: Buffer.from(JSON.stringify(data), 'utf8'),
    })
}

/** OAuth 授权页的账号密码登录流程。 */
async function loginOnOAuthPage(page: Page) {
    await page.getByPlaceholder('学号').fill('9000000001')
    await page.getByPlaceholder(/密码/).fill('Password1')
    await page.getByRole('button', {name: '授权登录'}).click()
}

const OAUTH_LOGIN_URL =
    '/oauth-login?state=ui-state-1&client_id=ui-client&redirect_uri=https%3A%2F%2Fexample.com%2Fcb&response_type=code'

test.describe('C28 成员 JSON 导入反馈', () => {
    test('导入成功时展示新增/更新/跳过/失败数量与逐行原因', async ({page}) => {
        await injectSession(page, {role: 'Founder'})
        await mockApi(page, {
            bulkUpdate: {
                result: {
                    created: 1,
                    updated: 2,
                    skipped: 1,
                    failed: 1,
                    issues: [
                        {userId: '9000000002', reason: '学号不存在', kind: 'failed'},
                        {userId: '9000000003', reason: '记录未变化', kind: 'skipped'},
                    ],
                },
            },
        })

        await page.goto('/Centre/MemberData')
        await expect(page.getByRole('heading', {name: '成员中心'})).toBeVisible()

        await importMembersJson(page, [MEMBER_UPDATE_ROW])

        const toast = page.locator('.n-message--success-type').first()
        await expect(toast).toBeVisible()
        const text = await toast.innerText()

        expect(text).toContain('新增 1')
        expect(text).toContain('更新 2')
        expect(text).toContain('跳过 1')
        expect(text).toContain('失败 1')
        // 逐行原因必须可见，不能只给一个总数。
        expect(text).toContain('9000000002')
        expect(text).toContain('学号不存在')
        expect(text).toContain('记录未变化')
    })

    test('零写入（全部跳过）时提示失败而不是数据更新成功', async ({page}) => {
        await injectSession(page, {role: 'Founder'})
        await mockApi(page, {
            bulkUpdate: {
                zeroWriteFailure: true,
                result: {
                    created: 0,
                    updated: 0,
                    skipped: 3,
                    failed: 0,
                    issues: [
                        {userId: '9000000002', reason: '学号不存在', kind: 'skipped'},
                        {userId: '9000000003', reason: '记录未变化', kind: 'skipped'},
                    ],
                },
            },
        })

        await page.goto('/Centre/MemberData')
        await expect(page.getByRole('heading', {name: '成员中心'})).toBeVisible()

        await importMembersJson(page, [MEMBER_UPDATE_ROW])

        const toast = page.locator('.n-message--error-type').first()
        await expect(toast).toBeVisible()
        const text = await toast.innerText()

        expect(text).toContain('跳过 3')
        expect(text).toContain('学号不存在')
        // 零写入必须明确告知失败，不能出现“数据更新成功”。
        expect(text).not.toContain('数据更新成功')
        await expect(page.getByText('数据更新成功')).toHaveCount(0)
        await expect(page.locator('.n-message--success-type')).toHaveCount(0)
    })
})

test.describe('C29 SSO 会话存储失败', () => {
    test('store-session 返回 500 时停留在授权登录页且不跳转 SSO/callback', async ({page}) => {
        const callbackRequests: string[] = []
        page.on('request', request => {
            if (request.url().includes('/SSO/callback')) callbackRequests.push(request.url())
        })

        await mockApi(page, {
            loginToken: makeJwt('Founder'),
            storeSession: 'http500',
        })
        // 若旧代码仍然跳转，这里会渲染一个可识别的回调页，便于断言 URL。
        await page.route('**/SSO/callback**', route => route.fulfill({
            status: 200,
            contentType: 'text/html',
            body: '<html><body>SSO callback landed</body></html>',
        }))

        await page.goto(OAUTH_LOGIN_URL)
        await expect(page.getByRole('heading', {name: 'iMember 登录授权'})).toBeVisible()

        await loginOnOAuthPage(page)

        await expect(page.getByText('会话创建失败，请稍后重试')).toBeVisible()
        await expect(page).toHaveURL(/\/oauth-login/)
        await page.waitForTimeout(300)
        expect(callbackRequests).toEqual([])
        await expect(page).toHaveURL(/\/oauth-login/)
    })

    test('store-session 返回 200 但 success=false 时同样按失败处理', async ({page}) => {
        const callbackRequests: string[] = []
        page.on('request', request => {
            if (request.url().includes('/SSO/callback')) callbackRequests.push(request.url())
        })

        await mockApi(page, {
            loginToken: makeJwt('Founder'),
            storeSession: 'successFalse',
        })
        await page.route('**/SSO/callback**', route => route.fulfill({
            status: 200,
            contentType: 'text/html',
            body: '<html><body>SSO callback landed</body></html>',
        }))

        await page.goto(OAUTH_LOGIN_URL)
        await expect(page.getByRole('heading', {name: 'iMember 登录授权'})).toBeVisible()

        await loginOnOAuthPage(page)

        await expect(page.getByText('会话创建失败，请稍后重试')).toBeVisible()
        await expect(page).toHaveURL(/\/oauth-login/)
        await page.waitForTimeout(300)
        expect(callbackRequests).toEqual([])
    })
})

test.describe('C30 监控指标标签', () => {
    test('固定窗口平均每秒请求数使用窗口标签与 averageRequestsPerSecond', async ({page}) => {
        await injectSession(page, {role: 'Founder'})
        await mockApi(page, {
            performance: {
                http: {
                    totalRequests: 12345,
                    windowSeconds: 3600,
                    averageRequestsPerSecond: 3.43,
                    errorRate: 1.5,
                    averageResponseTime: 42.5,
                },
                system: {cpuSecondsTotal: 98765},
                timestamp: '2026-01-02T03:04:05Z',
            },
        })

        await page.goto('/Centre/Admin')
        await expect(page.getByRole('heading', {name: '概览'})).toBeVisible()

        // 标签必须说明固定窗口，避免把 3600 秒累计平均误解为瞬时 QPS。
        await expect(page.getByText('近 1 小时平均每秒请求数')).toBeVisible()
        await expect(page.getByText('3.43', {exact: true})).toBeVisible()
        await expect(page.getByText('12,345', {exact: true})).toBeVisible()

        // 不能只显示旧的“每秒请求数”标签，也不再读取 requestsPerSecond。
        await expect(page.getByText('每秒请求数', {exact: true})).toHaveCount(0)
    })
})

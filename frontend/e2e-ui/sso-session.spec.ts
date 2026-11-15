import {test, expect} from '@playwright/test'

test('SSO password login keeps the main site session and uses its own token for authorization', async ({page}) => {
    const payload = Buffer.from(JSON.stringify({sub: 'main-user', exp: Math.floor(Date.now() / 1000) + 3600})).toString('base64url')
    const mainToken = `header.${payload}.signature`
    await page.addInitScript(token => {
        localStorage.setItem('accessToken', token)
        localStorage.setItem('refreshToken', 'main-refresh')
        localStorage.setItem('UserId', 'main-user')
    }, mainToken)
    await page.route('**/SSO/client-info?*', route => route.fulfill({json: {name: 'Demo'}}))
    await page.route('**/Auth/login?*', route => route.fulfill({
        json: {code: 200, errorCode: 0, message: 'ok', data: 'third-party-token'},
        headers: {'X-Refresh-Token': 'third-party-refresh'},
    }))
    await page.route('**/SSO/store-session', route => route.fulfill({status: 500, json: {success: false}}))
    await page.goto('/oauth-login?client_id=demo&state=test&scope=openid')
    await expect(page.getByRole('button', {name: '使用当前用户进行登录'})).toBeVisible()
    await page.getByPlaceholder('学号').fill('1234567890')
    await page.getByPlaceholder('密码 (未设置时为手机号码)').fill('password123')
    const sessionRequest = page.waitForRequest('**/SSO/store-session')
    await page.getByRole('button', {name: '授权登录'}).click()
    expect((await sessionRequest).headers().authorization).toBe('Bearer third-party-token')
    await expect(page.getByText('会话创建失败，请稍后重试')).toBeVisible()
    const session = await page.evaluate(() => ({
        accessToken: localStorage.getItem('accessToken'),
        refreshToken: localStorage.getItem('refreshToken'),
        userId: localStorage.getItem('UserId'),
    }))
    expect(session).toEqual({accessToken: mainToken, refreshToken: 'main-refresh', userId: 'main-user'})
})

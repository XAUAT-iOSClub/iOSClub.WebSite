import {test, expect} from '@playwright/test'

test('services use DELETE and JSON bodies for sensitive operations', async ({page}) => {
    await page.goto('/ForgotPassword')
    const observed: {method: string; query: string; body: unknown}[] = []
    await page.route(/\/(Auth\/(change-password|refresh-token)|SSO\/from_main_jwt|Staff\/Delete\/|Department\/Delete\/|Category\/Delete\/)/, async route => {
        const request = route.request()
        const url = new URL(request.url())
        observed.push({method: request.method(), query: url.search, body: request.postData() ? request.postDataJSON() : null})
        if (url.pathname.endsWith('/from_main_jwt')) await route.fulfill({body: 'third-party-token'})
        else await route.fulfill({json: {code: 200, errorCode: 0, message: 'ok', data: url.pathname.endsWith('/refresh-token') ? 'new-access' : true}, headers: {'X-Refresh-Token': 'new-refresh', 'Access-Control-Expose-Headers': 'X-Refresh-Token'}})
    })
    const result = await page.evaluate(async () => {
        const paths = ['Auth', 'Staff', 'Department', 'Category'].map(name => `/src/services/${name}Service.ts`)
        const [{AuthService}, {StaffService}, {DepartmentService}, {CategoryService}] = await Promise.all(paths.map(path => import(/* @vite-ignore */ path)))
        const payload = btoa(JSON.stringify({sub: '2024000001', exp: Math.floor(Date.now() / 1000) + 3600, client_id: 'demo', scope: 'profile'}))
        localStorage.setItem('accessToken', `header.${payload}.signature`)
        localStorage.setItem('refreshToken', 'refresh&+#?')
        await StaffService.deleteStaff('2024000002')
        await DepartmentService.deleteDepartment('测试部门')
        await CategoryService.deleteCategory('测试分类')
        await AuthService.changePassword('2024000001', 'Old&+#?', 'New&+#?')
        const session = await AuthService.loginFromMainJwt('demo', 'profile')
        await AuthService.refreshToken()
        return {session, refreshToken: localStorage.getItem('refreshToken')}
    })
    expect(observed.map(r => r.method)).toEqual(['DELETE', 'DELETE', 'DELETE', 'PUT', 'POST', 'POST'])
    expect(observed.every(r => r.query === '')).toBe(true)
    expect(observed[3].body).toEqual({userId: '2024000001', oldPassword: 'Old&+#?', newPassword: 'New&+#?'})
    expect(observed[4].body).toEqual({clientId: 'demo', scope: 'profile'})
    expect(observed[5].body).toEqual({userId: '2024000001', refreshToken: 'refresh&+#?', clientId: 'demo', scope: 'profile'})
    expect(result).toEqual({session: 'third-party-token', refreshToken: 'new-refresh'})
})

import {expect, test} from '@playwright/test'
import {createDepartment, deleteDepartment, injectAuth, loginToken} from './helpers'

const DEPT_A = 'E2E同键甲'
const DEPT_B = 'E2E同键乙'

test.describe('部门 Tab：选中项与内容一致', () => {
    let token: string

    test.beforeAll(async ({request}) => {
        token = await loginToken(request, '9900000001', 'e2epass123')
    })

    test.beforeEach(async ({request}) => {
        await deleteDepartment(request, token, DEPT_A)
        await deleteDepartment(request, token, DEPT_B)
        // 故意使用相同的（空）key：历史实现用 key 作为 tab name，会导致选中错位
        await createDepartment(request, token, DEPT_A, '')
        await createDepartment(request, token, DEPT_B, '')
    })

    test.afterEach(async ({request}) => {
        await deleteDepartment(request, token, DEPT_A)
        await deleteDepartment(request, token, DEPT_B)
    })

    test('key 相同/为空时，切换 Tab 显示的部门标题仍然正确', async ({page}) => {
        await injectAuth(page, token, '9900000001')
        await page.goto('/Centre/Department')

        await page.getByText(DEPT_B, {exact: true}).first().click()
        await expect(page.getByRole('heading', {name: DEPT_B})).toBeVisible()

        await page.getByText(DEPT_A, {exact: true}).first().click()
        await expect(page.getByRole('heading', {name: DEPT_A})).toBeVisible()

        await page.getByText(DEPT_B, {exact: true}).first().click()
        await expect(page.getByRole('heading', {name: DEPT_B})).toBeVisible()
    })
})

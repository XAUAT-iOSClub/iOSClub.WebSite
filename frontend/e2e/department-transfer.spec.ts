import {expect, test} from '@playwright/test'
import {
    assignRole,
    createDepartment,
    deleteDepartment,
    getAllMembers,
    getDepartment,
    importRoster,
    injectAuth,
    loginToken,
} from './helpers'

const DEPT_A = 'E2E人事部'
const DEPT_B = 'E2E外部部'
const KEY_A = 'e2e-hr'
const KEY_B = 'e2e-out'

async function openTab(page: any, name: string) {
    await page.getByText(name, {exact: true}).first().click()
    await expect(page.getByRole('button', {name: '导入名单'})).toBeVisible()
}

test.describe('部门管理系统：人员调动优化', () => {
    let token: string

    test.beforeAll(async ({request}) => {
        token = await loginToken(request, '9900000001', 'e2epass123')
    })

    test.beforeEach(async ({request}) => {
        await deleteDepartment(request, token, DEPT_A)
        await deleteDepartment(request, token, DEPT_B)
        await createDepartment(request, token, DEPT_A, KEY_A)
        await createDepartment(request, token, DEPT_B, KEY_B)
        await importRoster(request, token, DEPT_A, [
            {userId: '9900001001', name: '甲', identity: 'Department'},
            {userId: '9900001002', name: '乙', identity: 'Department'},
        ])
        await importRoster(request, token, DEPT_B, [
            {userId: '9900001004', name: '丁', identity: 'Department'},
        ])
    })

    test.afterEach(async ({request}) => {
        await deleteDepartment(request, token, DEPT_A)
        await deleteDepartment(request, token, DEPT_B)
    })

    test('添加部长只能从本部门部员中选择，并支持搜索', async ({page, request}) => {
        await injectAuth(page, token, '9900000001')
        await page.goto('/Centre/Department')
        await openTab(page, DEPT_A)

        await page.getByTestId('minister-add').click()
        await expect(page.getByText('提拔部长 / 副部长')).toBeVisible()

        // 搜索关键词只命中本部门部员
        const search = page.getByPlaceholder('搜索姓名 / 学号 / 部门...')
        const candidates = page.getByTestId('candidate-list')
        await search.fill('甲')
        await expect(candidates.locator('tr', {hasText: '9900001001'})).toBeVisible()
        await expect(candidates.locator('tr', {hasText: '9900001002'})).toHaveCount(0)

        // 外部门的人不应出现
        await search.fill('丁')
        await expect(candidates.getByText('未找到匹配成员')).toBeVisible()

        // 回到甲并提拔
        await search.fill('甲')
        await candidates.locator('tr', {hasText: '9900001001'}).getByRole('button', {name: '添加'}).click()
        await expect(page.getByText(/已将 甲 提拔为/)).toBeVisible()

        const department = await getDepartment(request, token, DEPT_A)
        const jia = department!.staffs.find(s => s.userId === '9900001001')!
        expect(jia.identity).toBe('Minister')
        expect(jia.departmentName).toBe(DEPT_A)
    })

    test('社长/团支书可从全部部员和部长中选择，并支持搜索', async ({page, request}) => {
        // 先把甲提为部长，验证部长与部员都能作为领导候选
        await assignRole(request, token, '9900001001', 'Minister', DEPT_A)

        await injectAuth(page, token, '9900000001')
        await page.goto('/Centre/Department')
        await expect(page.getByTestId('leader-add')).toBeVisible()

        await page.getByTestId('leader-add').click()
        await expect(page.getByText(/添加领导/)).toBeVisible()

        const search = page.getByPlaceholder('搜索姓名 / 学号 / 部门...')
        const candidates = page.getByTestId('candidate-list')
        // 部长（甲）可被搜索到
        await search.fill('甲')
        await expect(candidates.locator('tr', {hasText: '9900001001'})).toBeVisible()
        // 其他部门部员（丁）也可被搜索到
        await search.fill('丁')
        await expect(candidates.locator('tr', {hasText: '9900001004'})).toBeVisible()
        // 本部门部员（乙）也可被搜索到
        await search.fill('乙')
        await expect(candidates.locator('tr', {hasText: '9900001002'})).toBeVisible()

        await search.fill('甲')
        await candidates.locator('tr', {hasText: '9900001001'}).getByRole('button', {name: '添加'}).click()
        await expect(page.getByText(/已将 甲 添加至领导层/)).toBeVisible()

        const members = await getAllMembers(request, token)
        const jia = members.find(m => m.userId === '9900001001')!
        expect(jia.identity).toBe('President')
        // 领导身份不隶属于任何部门
        const department = await getDepartment(request, token, DEPT_A)
        expect(department!.staffs.find(s => s.userId === '9900001001')).toBeUndefined()
    })
})

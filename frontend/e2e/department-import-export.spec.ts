import {expect, test} from '@playwright/test'
import * as fs from 'node:fs'
import * as XLSX from 'xlsx'
import {
    createDepartment,
    deleteDepartment,
    getAllMembers,
    getDepartment,
    injectAuth,
    loginToken,
    RosterMember,
} from './helpers'

const DEPT = 'E2E导入部'
const KEY = 'e2e-import'

const ROSTER: RosterMember[] = [
    {
        userId: '9900001001', name: '甲', identity: 'Department', academy: '信息学院',
        className: '软工2401', phoneNum: '13900001001', politicalLandscape: '共青团员',
        gender: '男', eMail: 'e2e-a@example.com',
    },
    {
        userId: '9900001002', name: '乙', identity: 'Department', academy: '信息学院',
        className: '软工2402', phoneNum: '13900001002', politicalLandscape: '群众',
        gender: '女', eMail: 'e2e-b@example.com',
    },
    {
        userId: '9900001003', name: '丙', identity: 'Minister', academy: '信息学院',
        className: '软工2403', phoneNum: '13900001003', politicalLandscape: '中共党员',
        gender: '男', eMail: 'e2e-c@example.com',
    },
]

// 把导入名单转成前端期望的中文表头（与 Department.vue 的 pickField 对齐）
function toChineseRows() {
    return ROSTER.map(m => ({
        姓名: m.name,
        学号: m.userId,
        职位: m.identity === 'Minister' ? '部长' : '部员',
        学院: m.academy,
        专业班级: m.className,
        手机号: m.phoneNum,
        政治面貌: m.politicalLandscape,
        性别: m.gender,
        邮箱: m.eMail,
    }))
}

async function openDepartmentTab(page: any, name: string) {
    await page.getByText(name, {exact: true}).first().click()
    await expect(page.getByRole('button', {name: '导入名单'})).toBeVisible()
}

async function downloadJson(page: any): Promise<any[]> {
    await page.getByRole('button', {name: /导出/}).first().click()
    const [download] = await Promise.all([
        page.waitForEvent('download'),
        page.getByText('JSON (.json)', {exact: true}).click(),
    ])
    const filePath = await download.path()
    return JSON.parse(fs.readFileSync(filePath!, 'utf8'))
}

test.describe('部门名单：导入 / 数据库同步 / 导出内容正确性', () => {
    let token: string

    test.beforeAll(async ({request}) => {
        token = await loginToken(request, '9900000001', 'e2epass123')
    })

    test.beforeEach(async ({request}) => {
        await deleteDepartment(request, token, DEPT)
        await createDepartment(request, token, DEPT, KEY)
    })

    test.afterEach(async ({request}) => {
        await deleteDepartment(request, token, DEPT)
    })

    test('导入 XLSX 后数据库同步，且导出的文件内容与导入一致', async ({page, request}) => {
        await injectAuth(page, token, '9900000001')
        await page.goto('/Centre/Department')
        await openDepartmentTab(page, DEPT)
        await page.getByRole('button', {name: '导入名单'}).click()

        // 通过隐藏的 file input 上传真实 xlsx
        const worksheet = XLSX.utils.json_to_sheet(toChineseRows())
        const workbook = XLSX.utils.book_new()
        XLSX.utils.book_append_sheet(workbook, worksheet, '名单')
        const buffer = XLSX.write(workbook, {type: 'buffer', bookType: 'xlsx'})
        await page.locator('input[type="file"]').setInputFiles({
            name: 'e2e-roster.xlsx',
            mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
            buffer,
        })

        // 进入预检查
        await expect(page.getByText('新成员：3 人')).toBeVisible()
        await expect(page.getByText('数据格式正确')).toBeVisible()
        await page.getByRole('button', {name: '确认并覆盖'}).click()
        await expect(page.getByText(/名单导入成功/)).toBeVisible()

        // 1) 数据库同步：Staff 记录（部门/身份）已写入
        const department = await getDepartment(request, token, DEPT)
        expect(department).not.toBeNull()
        expect(department!.staffs).toHaveLength(3)
        for (const expected of ROSTER) {
            const staff = department!.staffs.find(s => s.userId === expected.userId)
            expect(staff, `缺少 ${expected.userId}`).toBeTruthy()
            expect(staff!.name).toBe(expected.name)
            expect(staff!.identity).toBe(expected.identity)
            expect(staff!.departmentName).toBe(DEPT)
        }

        // 2) 数据库同步：Student 档案字段一并补齐
        const members = await getAllMembers(request, token)
        const jia = members.find(m => m.userId === '9900001001')!
        expect(jia.userName).toBe('甲')
        expect(jia.academy).toBe('信息学院')
        expect(jia.className).toBe('软工2401')
        expect(jia.phoneNum).toBe('13900001001')
        expect(jia.gender).toBe('男')
        expect(jia.politicalLandscape).toBe('共青团员')
        expect(jia.eMail).toBe('e2e-a@example.com')

        // 3) 导出文件内容正确
        await openDepartmentTab(page, DEPT)
        const rows = await downloadJson(page)
        expect(rows).toHaveLength(3)
        const jiaRow = rows.find((r: any) => r['学号'] === '9900001001')!
        expect(jiaRow['姓名']).toBe('甲')
        expect(jiaRow['职位']).toBe('部员')
        expect(jiaRow['学院']).toBe('信息学院')
        expect(jiaRow['专业班级']).toBe('软工2401')
        expect(jiaRow['手机号']).toBe('13900001001')
        expect(jiaRow['政治面貌']).toBe('共青团员')
        expect(jiaRow['性别']).toBe('男')
        expect(jiaRow['邮箱']).toBe('e2e-a@example.com')

        const bingRow = rows.find((r: any) => r['学号'] === '9900001003')!
        expect(bingRow['职位']).toBe('部长')
    })

    test('再次导入新名单会覆盖旧名单并同步数据库', async ({page, request}) => {
        // 先直接 API 导入一版
        await request.post(`http://localhost:5052/Department/${encodeURIComponent(DEPT)}/import`, {
            headers: {Authorization: `Bearer ${token}`},
            data: {fileName: 'v1.xlsx', members: ROSTER},
        })

        await injectAuth(page, token, '9900000001')
        await page.goto('/Centre/Department')
        await openDepartmentTab(page, DEPT)
        await page.getByRole('button', {name: '导入名单'}).click()

        // 用只含 1 人的 xlsx 覆盖
        const worksheet = XLSX.utils.json_to_sheet([
            {姓名: '丁', 学号: '9900001004', 职位: '部员', 学院: '机电学院', 性别: '女'},
        ])
        const workbook = XLSX.utils.book_new()
        XLSX.utils.book_append_sheet(workbook, worksheet, '名单')
        await page.locator('input[type="file"]').setInputFiles({
            name: 'v2.xlsx',
            mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
            buffer: XLSX.write(workbook, {type: 'buffer', bookType: 'xlsx'}),
        })
        await expect(page.getByText('新成员：1 人')).toBeVisible()
        await page.getByRole('button', {name: '确认并覆盖'}).click()
        await expect(page.getByText(/名单导入成功/)).toBeVisible()

        const department = await getDepartment(request, token, DEPT)
        expect(department!.staffs).toHaveLength(1)
        expect(department!.staffs[0].userId).toBe('9900001004')
    })

    test('在校成员导出会排除学号前两位 <= 本年-4 的往届成员', async ({page, request}) => {
        // 两位在校成员（前两位 99） + 一位往届成员（前两位 22）
        await request.post(`http://localhost:5052/Department/${encodeURIComponent(DEPT)}/import`, {
            headers: {Authorization: `Bearer ${token}`},
            data: {
                fileName: 'school.xlsx',
                members: [
                    {userId: '9900001011', name: '在校甲', identity: 'Department'},
                    {userId: '9900001012', name: '在校乙', identity: 'Department'},
                    {userId: '2200001001', name: '往届丙', identity: 'Department'},
                ],
            },
        })

        await injectAuth(page, token, '9900000001')
        await page.goto('/Centre/Department')
        await openDepartmentTab(page, DEPT)

        // 正常导出：3 人
        const fullRows = await downloadJson(page)
        expect(fullRows).toHaveLength(3)

        // 在校成员导出：只保留 2 位在校成员
        await openDepartmentTab(page, DEPT)
        await page.getByRole('button', {name: /导出/}).first().click()
        const [download] = await Promise.all([
            page.waitForEvent('download'),
            page.getByText('在校 - JSON (.json)', {exact: true}).click(),
        ])
        const schoolRows = JSON.parse(fs.readFileSync((await download.path())!, 'utf8'))
        expect(schoolRows.map((r: any) => r['学号']).sort()).toEqual(['9900001011', '9900001012'])
    })

    test('成员导出（成员中心）同样支持在校成员模式', async ({page, request}) => {
        // 确保数据库中存在一个往届学号
        await request.post(`http://localhost:5052/Department/${encodeURIComponent(DEPT)}/import`, {
            headers: {Authorization: `Bearer ${token}`},
            data: {
                fileName: 'legacy.xlsx',
                members: [
                    {userId: '9900001011', name: '在校甲', identity: 'Department'},
                    {userId: '2200001001', name: '往届丙', identity: 'Department'},
                ],
            },
        })

        await injectAuth(page, token, '9900000001')
        await page.goto('/Centre/MemberData')
        await page.getByRole('button', {name: '导出'}).click()
        const [download] = await Promise.all([
            page.waitForEvent('download'),
            page.getByText('在校成员 JSON', {exact: true}).click(),
        ])
        const rows = JSON.parse(fs.readFileSync((await download.path())!, 'utf8'))
        // 往届学号不导出，在校成员保留
        expect(rows.some((r: any) => r.userId === '2200001001')).toBe(false)
        expect(rows.some((r: any) => r.userId === '9900001011')).toBe(true)
    })

    test('部门汇总导出（总览面板）同样支持在校成员模式', async ({page, request}) => {
        await request.post(`http://localhost:5052/Department/${encodeURIComponent(DEPT)}/import`, {
            headers: {Authorization: `Bearer ${token}`},
            data: {
                fileName: 'summary.xlsx',
                members: [
                    {userId: '9900001011', name: '在校甲', identity: 'Department'},
                    {userId: '2200001001', name: '往届丙', identity: 'Department'},
                ],
            },
        })

        await injectAuth(page, token, '9900000001')
        await page.goto('/Centre/Department')
        // 总览面板的“导出数据”按钮
        await page.getByTitle('导出数据').click()
        const [download] = await Promise.all([
            page.waitForEvent('download'),
            page.getByText('在校 - JSON (.json)', {exact: true}).click(),
        ])
        const rows = JSON.parse(fs.readFileSync((await download.path())!, 'utf8'))
        expect(rows.some((r: any) => r['学号'] === '2200001001')).toBe(false)
        expect(rows.some((r: any) => r['学号'] === '9900001011')).toBe(true)
    })
})

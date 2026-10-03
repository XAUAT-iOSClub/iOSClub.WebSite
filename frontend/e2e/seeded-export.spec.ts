import {expect, test} from '@playwright/test'
import * as fs from 'node:fs'
import {injectAuth, loginToken} from './helpers'
import {dbAllExportableMembers, dbDepartmentsWithStaff, dbRoster, DbMember, isInSchool} from './db'

const IDENTITY_LABELS: Record<string, string> = {
    President: '社长/副社长/团支书',
    Minister: '部长',
    Department: '部员',
    Founder: '创始人'
}

function expectedRows(rows: DbMember[]) {
    return rows.map(r => ({
        姓名: r.userName || r.name,
        学号: r.userId,
        职位: IDENTITY_LABELS[r.identity] || r.identity,
        学院: r.academy || '',
        专业班级: r.className || '',
        手机号: r.phoneNum || '',
        政治面貌: r.politicalLandscape || '',
        性别: r.gender || '',
        邮箱: r.eMail || ''
    })).sort((a, b) => a.学号.localeCompare(b.学号))
}

function normalize(rows: any[]) {
    return [...rows].sort((a, b) => String(a['学号']).localeCompare(String(b['学号'])))
}

function activePane(page: any, name: string) {
    return page.locator('.n-tab-pane:visible').filter({has: page.getByRole('heading', {name, level: 2})})
}

async function openTab(page: any, name: string) {
    await page.getByText(name, {exact: true}).first().click()
    // 等待动画结束，确保当前只有一个可见面板（避免切换动画期间匹配到两个）
    await expect(activePane(page, name)).toHaveCount(1)
}

async function downloadDeptJson(page: any, name: string, optionLabel: string): Promise<any[]> {
    await activePane(page, name).getByRole('button', {name: '导出', exact: true}).click()
    const [download] = await Promise.all([
        page.waitForEvent('download'),
        page.getByText(optionLabel, {exact: true}).click(),
    ])
    return JSON.parse(fs.readFileSync((await download.path())!, 'utf8'))
}

async function downloadOverviewJson(page: any, optionLabel: string): Promise<any[]> {
    await page.getByTitle('导出数据').click()
    const [download] = await Promise.all([
        page.waitForEvent('download'),
        page.getByText(optionLabel, {exact: true}).click(),
    ])
    return JSON.parse(fs.readFileSync((await download.path())!, 'utf8'))
}

test.describe('已播种数据：导出文件与数据库一致性', () => {
    let token: string

    test.beforeAll(async ({request}) => {
        token = await loginToken(request, '9900000001', 'e2epass123')
    })

    test('每个部门的「全部 / 在校」导出都与数据库逐字段一致', async ({page}) => {
        const departments = dbDepartmentsWithStaff()
        expect(departments.length).toBeGreaterThan(0)

        await injectAuth(page, token, '9900000001')
        await page.goto('/Centre/Department')

        for (const name of departments) {
            const dbRows = dbRoster(name)
            const inschoolDb = dbRows.filter(r => isInSchool(r.userId))

            await openTab(page, name)

            // 正常导出 == 数据库该部门全部成员
            const full = await downloadDeptJson(page, name, 'JSON (.json)')
            expect(normalize(full), `${name} 正常导出`).toEqual(expectedRows(dbRows))

            // 在校导出 == 数据库该部门在校成员
            await openTab(page, name)
            const school = await downloadDeptJson(page, name, '在校 - JSON (.json)')
            expect(normalize(school), `${name} 在校导出`).toEqual(expectedRows(inschoolDb))

            // 在校导出中不应出现任何往届学号
            expect(school.every((r: any) => isInSchool(r['学号'])), `${name} 在校导出含往届`).toBe(true)
        }
    })

    test('总览汇总导出（全部 / 在校）与数据库一致', async ({page}) => {
        const allDb = dbAllExportableMembers()
        const inschoolDb = allDb.filter(r => isInSchool(r.userId))

        await injectAuth(page, token, '9900000001')
        await page.goto('/Centre/Department')

        const full = await downloadOverviewJson(page, 'JSON (.json)')
        expect(normalize(full)).toEqual(expectedRows(allDb))

        const school = await downloadOverviewJson(page, '在校 - JSON (.json)')
        expect(normalize(school)).toEqual(expectedRows(inschoolDb))
        expect(school.every((r: any) => isInSchool(r['学号']))).toBe(true)
    })

    test('成员中心导出在校模式会排除往届学号', async ({page}) => {
        const inschoolIds = dbAllExportableMembers().filter(r => isInSchool(r.userId)).map(r => r.userId)
        const graduatedIds = dbAllExportableMembers().filter(r => !isInSchool(r.userId)).map(r => r.userId)

        await injectAuth(page, token, '9900000001')
        await page.goto('/Centre/MemberData')
        await page.getByRole('button', {name: '导出'}).click()
        const [download] = await Promise.all([
            page.waitForEvent('download'),
            page.getByText('在校成员 JSON', {exact: true}).click(),
        ])
        const rows = JSON.parse(fs.readFileSync((await download.path())!, 'utf8'))
        const exportedIds = rows.map((r: any) => r.userId)

        expect(rows.every((r: any) => isInSchool(r.userId))).toBe(true)
        expect(exportedIds).toEqual(expect.arrayContaining(inschoolIds))
        for (const id of graduatedIds) {
            expect(exportedIds, `往届 ${id} 不应出现在在校导出中`).not.toContain(id)
        }
    })
})

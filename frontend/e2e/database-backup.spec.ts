import {expect, test} from '@playwright/test'
import {DatabaseSync} from 'node:sqlite'
import {createHash} from 'node:crypto'
import {FOUNDER, injectAuth, loginToken} from './helpers'
import {DB_PATH} from './db'

const MEMBER = {userId: '9900000002', password: 'e2epass123', name: 'E2E备份部员'}
const BACKUP_FILE = 'iosclub_2026-10-26T033000Z.dump'
const DOWNLOAD_BODY = 'e2e-backup-payload'

// scripts/e2e-local.sh 会设置该变量：连接真实后端 + R2/S3 mock，不再拦截接口。
const REAL_BACKEND = process.env.E2E_REAL_BACKUP === '1'

function seedDepartmentMember() {
    const db = new DatabaseSync(DB_PATH)
    try {
        const hash = createHash('md5').update(MEMBER.password).digest('hex')
        db.prepare(
            `INSERT OR REPLACE INTO Students
             (UserId, UserName, Academy, PoliticalLandscape, Gender, ClassName, PhoneNum, JoinTime, PasswordHash, EMail)
             VALUES (?, ?, '信息学院', '共青团员', '男', '软工2401', '13900000002', datetime('now'), ?, ?)`
        ).run(MEMBER.userId, MEMBER.name, hash, `${MEMBER.userId}@e2e.example.com`)
        db.prepare(
            `INSERT OR REPLACE INTO Staffs (UserId, Name, Identity, DepartmentName)
             VALUES (?, ?, 'Department', NULL)`
        ).run(MEMBER.userId, MEMBER.name)
    } finally {
        db.close()
    }
}

test.describe('数据库备份按钮', () => {
    let founderToken: string
    let memberToken: string

    test.beforeAll(async ({request}) => {
        // 默认真实后端环境（scripts/e2e-local.sh）会预先在 PostgreSQL 里建好账号。
        if (!REAL_BACKEND) seedDepartmentMember()
        founderToken = await loginToken(request, FOUNDER.userId, FOUNDER.password)
        memberToken = await loginToken(request, MEMBER.userId, MEMBER.password)
    })

    test('部长及以上可见，且点击后按最新备份文件名下载', async ({page}) => {
        await injectAuth(page, founderToken, FOUNDER.userId)

        if (!REAL_BACKEND) {
            await page.route('**/AdminBackups/latest/download-url', route => route.fulfill({
                status: 200,
                contentType: 'application/json',
                body: JSON.stringify({
                    code: 200,
                    errorCode: 0,
                    message: 'Success',
                    data: {
                        fileName: BACKUP_FILE,
                        sizeBytes: DOWNLOAD_BODY.length,
                        createdAt: '2026-10-26T03:30:00Z',
                        downloadUrl: 'http://localhost:5173/__e2e_backup__.dump'
                    }
                })
            }))
            await page.route('**/__e2e_backup__.dump', route => route.fulfill({
                status: 200,
                contentType: 'application/octet-stream',
                headers: {'Content-Disposition': `attachment; filename="${BACKUP_FILE}"`},
                body: DOWNLOAD_BODY
            }))
        }

        await page.goto('/Centre/MemberData')

        const backup = page.getByRole('button', {name: '数据库备份'})
        const exportButton = page.getByRole('button', {name: '导出'})
        await expect(backup).toBeVisible()
        await expect(exportButton).toBeVisible()

        const [backupBox, exportBox] = await Promise.all([backup.boundingBox(), exportButton.boundingBox()])
        expect(backupBox).not.toBeNull()
        expect(exportBox).not.toBeNull()
        expect(backupBox!.x).toBeLessThan(exportBox!.x)
        expect(Math.abs(backupBox!.y - exportBox!.y)).toBeLessThan(4)

        const downloadPromise = page.waitForEvent('download')
        await backup.click()
        const download = await downloadPromise
        if (REAL_BACKEND) {
            expect(download.suggestedFilename()).toMatch(/^iosclub_.*\.dump$/)
        } else {
            expect(download.suggestedFilename()).toBe(BACKUP_FILE)
        }
    })

    test('副部长及以下看不到按钮', async ({page}) => {
        await injectAuth(page, memberToken, MEMBER.userId)
        await page.goto('/Centre/MemberData')

        await expect(page.getByRole('button', {name: '导出'})).toBeVisible()
        await expect(page.getByRole('button', {name: '数据库备份'})).toHaveCount(0)
    })
})

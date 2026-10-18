import {DatabaseSync} from 'node:sqlite'
import {createHash} from 'node:crypto'
import {fileURLToPath} from 'node:url'
import path from 'node:path'

const here = path.dirname(fileURLToPath(import.meta.url))

// 后端工作目录下的 SQLite 数据库（Program.cs 在未配置 SQL 时使用 Data Source=Data.db）
export const DB_PATH = process.env.E2E_DB_PATH
    || path.resolve(here, '../../rearend/iOSClub.WebAPI/iOSClub.WebAPI/Data.db')

// 测试账号与固定前缀，便于清理
export const E2E_PREFIX = '990'
// 用于“在校成员导出”测试的往届学号（前两位 22，早于在校线）
export const OLD_MEMBER_IDS = ['2200001001']
export const FOUNDER = {userId: '9900000001', name: 'E2E创始人', password: 'e2epass123'}

export function md5(text) {
    return createHash('md5').update(text).digest('hex')
}

// 清理所有以 990 开头的测试账号、指定往届学号与其部门/导入历史
export function cleanup(db) {
    db.exec('PRAGMA foreign_keys=OFF')
    const oldIds = OLD_MEMBER_IDS.map(id => `'${id}'`).join(',')
    db.exec(`DELETE FROM ImportHistories WHERE DepartmentName LIKE 'E2E%'`)
    db.exec(`DELETE FROM Staffs WHERE UserId LIKE '${E2E_PREFIX}%' OR UserId IN (${oldIds})`)
    db.exec(`DELETE FROM Students WHERE UserId LIKE '${E2E_PREFIX}%' OR UserId IN (${oldIds})`)
    db.exec(`DELETE FROM Departments WHERE Name LIKE 'E2E%'`)
}

function seedAccount(db, {userId, name, password}, identity, departmentName) {
    const hash = md5(password)
    db.prepare(
        `INSERT INTO Students (UserId, UserName, Academy, PoliticalLandscape, Gender, ClassName, PhoneNum, JoinTime, PasswordHash, EMail)
         VALUES (?, ?, '信息学院', '共青团员', '男', '软工2401', '13900000000', datetime('now'), ?, ?)`
    ).run(userId, name, hash, `${userId}@e2e.example.com`)
    db.prepare(
        `INSERT INTO Staffs (UserId, Name, Identity, DepartmentName) VALUES (?, ?, ?, ?)`
    ).run(userId, name, identity, departmentName)
}

export default async function globalSetup() {
    const db = new DatabaseSync(DB_PATH)
    try {
        cleanup(db)
        seedAccount(db, FOUNDER, 'Founder', null)
    } finally {
        db.close()
    }
}

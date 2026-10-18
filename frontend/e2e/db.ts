import {DatabaseSync} from 'node:sqlite'
import path from 'node:path'
import {fileURLToPath} from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))

// 后端工作目录下的 SQLite 数据库
export const DB_PATH = process.env.E2E_DB_PATH
    || path.resolve(here, '../../rearend/iOSClub.WebAPI/iOSClub.WebAPI/Data.db')

export interface DbMember {
    userId: string
    name: string
    identity: string
    departmentName: string | null
    userName: string | null
    academy: string | null
    className: string | null
    phoneNum: string | null
    politicalLandscape: string | null
    gender: string | null
    eMail: string | null
}

// 学号前两位 > (本年-4) 的两位 → 在校；否则往届
export function isInSchool(userId: string, now: Date = new Date()): boolean {
    const prefix = Number.parseInt((userId || '').slice(0, 2), 10)
    if (Number.isNaN(prefix)) return true
    const cutoff = (((now.getFullYear() - 4) % 100) + 100) % 100
    return prefix > cutoff
}

function withDb<T>(fn: (db: DatabaseSync) => T): T {
    const db = new DatabaseSync(DB_PATH)
    try {
        return fn(db)
    } finally {
        db.close()
    }
}

const ROSTER_SQL = `
    SELECT s.UserId AS userId, s.Name AS name, s.Identity AS identity, s.DepartmentName AS departmentName,
           st.UserName AS userName, st.Academy AS academy, st.ClassName AS className,
           st.PhoneNum AS phoneNum, st.PoliticalLandscape AS politicalLandscape,
           st.Gender AS gender, st.EMail AS eMail
    FROM Staffs s LEFT JOIN Students st ON st.UserId = s.UserId`

export function dbDepartmentsWithStaff(): string[] {
    return withDb(db => (db.prepare(
        `SELECT DISTINCT DepartmentName AS name FROM Staffs
         WHERE DepartmentName IS NOT NULL AND DepartmentName <> ''
         ORDER BY DepartmentName`).all() as any[]).map(r => r.name))
}

export function dbRoster(departmentName: string): DbMember[] {
    return withDb(db => db.prepare(`${ROSTER_SQL} WHERE s.DepartmentName = ?`).all(departmentName)) as DbMember[]
}

// 汇总导出对应的数据源：存在学生档案、且非 Founder 的所有 Staff
export function dbAllExportableMembers(): DbMember[] {
    return withDb(db => db.prepare(
        `${ROSTER_SQL.replace('LEFT JOIN', 'INNER JOIN')} WHERE s.Identity <> 'Founder'`).all()) as DbMember[]
}

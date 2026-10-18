import {APIRequestContext, Page} from '@playwright/test'

export const API_URL = process.env.E2E_API_URL || 'http://localhost:5052'
export const FOUNDER = {userId: '9900000001', password: 'e2epass123'}

export interface RosterMember {
    userId: string
    name: string
    identity?: string
    academy?: string
    className?: string
    phoneNum?: string
    politicalLandscape?: string
    gender?: string
    eMail?: string | null
}

export interface ApiEnvelope<T> {
    code: number
    errorCode: number
    message: string
    data: T
}

export const authHeaders = (token: string) => ({Authorization: `Bearer ${token}`})

export async function loginToken(request: APIRequestContext, userId: string, password: string): Promise<string> {
    const res = await request.post(`${API_URL}/Auth/login`, {
        data: {userId, password, rememberMe: false},
    })
    const body = await res.json()
    if (!body?.data) throw new Error(`登录失败: ${JSON.stringify(body)}`)
    return body.data as string
}

// 通过 localStorage 注入令牌，避免每个用例都走一遍登录 UI
export async function injectAuth(page: Page, token: string, userId: string) {
    await page.addInitScript(([t, u]) => {
        localStorage.setItem('accessToken', t)
        localStorage.setItem('UserId', u)
    }, [token, userId])
}

export async function apiGet<T>(request: APIRequestContext, token: string, path: string): Promise<T> {
    const res = await request.get(`${API_URL}${path}`, {headers: authHeaders(token)})
    const body = (await res.json()) as ApiEnvelope<T>
    return body.data
}

export async function createDepartment(
    request: APIRequestContext, token: string, name: string, key: string, description = 'E2E 测试部门'
): Promise<void> {
    await request.post(`${API_URL}/Department/Create`, {
        headers: authHeaders(token),
        data: {key, name, description},
    })
}

export async function deleteDepartment(request: APIRequestContext, token: string, name: string): Promise<void> {
    // 后端拒绝删除仍含成员的部门：先用空名单覆盖清空成员，再删除，保证用例之间互不残留。
    await request.post(`${API_URL}/Department/${encodeURIComponent(name)}/import`, {
        headers: authHeaders(token),
        data: {members: []},
    })
    await request.get(`${API_URL}/Department/Delete/${encodeURIComponent(name)}`, {
        headers: authHeaders(token),
    })
}

export async function importRoster(
    request: APIRequestContext, token: string, name: string, members: RosterMember[], fileName = 'e2e.xlsx'
) {
    const res = await request.post(`${API_URL}/Department/${encodeURIComponent(name)}/import`, {
        headers: authHeaders(token),
        data: {fileName, members},
    })
    return await res.json()
}

export async function assignRole(
    request: APIRequestContext, token: string, userId: string, identity: string, departmentName: string | null
) {
    const res = await request.post(`${API_URL}/Staff/assign-role`, {
        headers: authHeaders(token),
        data: {userId, identity, departmentName},
    })
    return await res.json()
}

export interface DepartmentVO {
    name: string
    key: string
    description?: string
    staffs: {userId: string; name: string; identity: string; departmentName: string | null}[]
}

export async function getDepartment(
    request: APIRequestContext, token: string, name: string
): Promise<DepartmentVO | null> {
    const res = await request.get(`${API_URL}/Department/${encodeURIComponent(name)}`, {headers: authHeaders(token)})
    const body = await res.json()
    return body.data ?? null
}

export interface MemberVO {
    userId: string
    userName: string
    academy: string
    className: string
    phoneNum: string
    gender: string
    politicalLandscape: string
    eMail: string | null
    identity: string
}

export async function getAllMembers(request: APIRequestContext, token: string): Promise<MemberVO[]> {
    return await apiGet<MemberVO[]>(request, token, '/Staff/members')
}

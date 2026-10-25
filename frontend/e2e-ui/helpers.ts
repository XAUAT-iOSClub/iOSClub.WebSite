import type {Page} from '@playwright/test'

/**
 * Role claim used by rearend JWT tokens and by the frontend store
 * (`stores/Authorization.ts#getRole`).
 */
export const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'

export type Role = 'Member' | 'Department' | 'Minister' | 'President' | 'Founder'

const base64url = (value: unknown) =>
    Buffer.from(JSON.stringify(value)).toString('base64url')

/**
 * Build a structurally valid JWT (header.payload.signature) with `exp` and role
 * claims. The signature is intentionally meaningless: the app only decodes the
 * payload, and this suite never talks to a real backend.
 */
export function makeJwt(
    role: Role,
    options: {expSecondsFromNow?: number; sub?: string; userId?: string} = {}
): string {
    const now = Math.floor(Date.now() / 1000)
    const exp = now + (options.expSecondsFromNow ?? 3600)
    const sub = options.sub ?? options.userId ?? '9000000001'
    const payload = {
        sub,
        unique_name: 'UI 测试用户',
        exp,
        [ROLE_CLAIM]: role,
        // Some older decoders use the plain `role` claim; keep both in sync.
        role,
        client_id: 'ui-test-client',
        scope: 'profile',
    }
    return `${base64url({alg: 'none', typ: 'JWT'})}.${base64url(payload)}.ui-test-signature`
}

export function decodeJwt(token: string | null | undefined): Record<string, any> | null {
    if (!token) return null
    const parts = token.split('.')
    if (parts.length !== 3) return null
    try {
        return JSON.parse(Buffer.from(parts[1], 'base64url').toString('utf8'))
    } catch {
        return null
    }
}

function roleFromAuthorization(header: string | undefined): Role {
    const token = (header || '').replace(/^Bearer\s+/i, '')
    const role = decodeJwt(token)?.[ROLE_CLAIM]
    return (role as Role) || 'Member'
}

/**
 * Seed the same localStorage keys a normal login writes (`accessToken`,
 * `refreshToken`, `UserId`) and nothing else. A sessionStorage marker prevents
 * re-seeding on reload, so logout tests can observe the real persisted state.
 */
export async function injectSession(
    page: Page,
    options: {
        role?: Role
        expired?: boolean
        token?: string
        refreshToken?: string
        userId?: string
    } = {}
): Promise<string> {
    const token = options.token ?? makeJwt(options.role ?? 'Founder', {
        expSecondsFromNow: options.expired ? -120 : 3600,
    })
    const refreshToken = options.refreshToken ?? 'mock-refresh-1'
    const userId = options.userId ?? '9000000001'

    await page.addInitScript(([t, r, u]) => {
        if (sessionStorage.getItem('__ui_test_session_seeded') === '1') return
        sessionStorage.setItem('__ui_test_session_seeded', '1')
        localStorage.clear()
        localStorage.setItem('accessToken', t)
        localStorage.setItem('refreshToken', r)
        localStorage.setItem('UserId', u)
    }, [token, refreshToken, userId])

    return token
}

const DEFAULT_ARTICLE = {
    path: 'ui-test-article',
    title: 'UI 测试文章',
    content: '# UI 测试文章',
    lastWriteTime: '2026-01-02T03:04:05Z',
    identity: 'Member',
    categoryName: '测试分类',
    categoryId: 'category-1',
    articleOrder: 1,
}

const memberFor = (identity: Role) => ({
    userId: '9000000001',
    userName: 'UI 测试用户',
    academy: '测试学院',
    politicalLandscape: '群众',
    gender: '男',
    className: '测试班',
    phoneNum: '13800000000',
    joinTime: '2024-09-01T00:00:00Z',
    eMail: null,
    identity,
})

/**
 * `.env` points VITE_API_URL at a backend on another origin, so mocked
 * responses must also satisfy CORS (including preflight and exposed headers).
 */
const CORS_HEADERS: Record<string, string> = {
    'access-control-allow-origin': '*',
    'access-control-allow-methods': 'GET,POST,PUT,DELETE,OPTIONS',
    'access-control-allow-headers': 'authorization,content-type',
    'access-control-expose-headers': 'X-Refresh-Token,Content-Disposition',
}

export interface MockApiOptions {
    articles?: Array<Record<string, any>>
    /** Make POST /Auth/logout fail with a 500 response. */
    logoutFails?: boolean
    /** Make GET /DataCentre fail with a 500 response. */
    dataCentreFails?: boolean
    /** Make GET /User/data fail as if the account has no Student profile. */
    userDataNotFound?: boolean
    /** Token returned by POST /Auth/refresh-token. */
    refreshedToken?: string
}

/**
 * Mock every API call the admin centre makes, plus the two third-party URLs the
 * shell loads. Unknown `/api/**` calls resolve to a successful empty envelope.
 */
export async function mockApi(page: Page, options: MockApiOptions = {}): Promise<void> {
    const articles = options.articles ?? [DEFAULT_ARTICLE]

    await page.route('https://link.xauat.site/**', route =>
        route.fulfill({status: 200, json: {links: []}}))
    await page.route('https://at.alicdn.com/**', route => route.abort())
    await page.route('https://assets.codepen.io/**', route => route.abort())

    const handler = async (route: any) => {
        const request = route.request()
        // `.env` may point VITE_API_URL at `http://localhost:5052` (no /api
        // prefix); normalise both that origin and an `/api` base to paths like
        // `/Article` so one dispatcher covers both shapes.
        const path = new URL(request.url()).pathname.replace(/^\/api(?=\/|$)/, '') || '/'
        const method = request.method()
        const identity = roleFromAuthorization(request.headers()['authorization'])

        if (method === 'OPTIONS') {
            return route.fulfill({status: 204, headers: CORS_HEADERS})
        }

        const json = (
            status: number,
            body: unknown,
            headers: Record<string, string> = {},
        ) => route.fulfill({
            status,
            headers: {...CORS_HEADERS, ...headers},
            json: body,
        })
        const ok = (data: unknown, headers: Record<string, string> = {}) =>
            json(200, {code: 200, errorCode: 0, message: 'ok', data}, headers)
        const fail = (status: number, errorCode: number, message: string) =>
            json(status, {code: status, errorCode, message, data: null})

        if (path === '/User/data') {
            if (options.userDataNotFound) return fail(404, 4003, '用户不存在或没有学生档案')
            return ok(memberFor(identity))
        }

        if (path === '/DataCentre/export-json') {
            return ok(
                {students: [], departments: [], presidents: [], resources: []},
                {'content-disposition': 'attachment; filename="partial-export.json"'},
            )
        }

        if (path === '/DataCentre' && method === 'GET') {
            if (options.dataCentreFails) {
                return fail(500, 5000, '统计服务不可用')
            }
            return ok({members: 10, staffs: 5, resources: 3, departments: 2})
        }

        if (path === '/Article' && method === 'GET') return ok(articles)
        if (path.startsWith('/Article/delete/')) return ok(true)

        if (path === '/Auth/logout') {
            if (options.logoutFails) {
                return fail(500, 5000, '服务端不可用')
            }
            return ok(true)
        }

        if (path === '/Auth/refresh-token') {
            const refreshedToken = options.refreshedToken
                || makeJwt('Founder', {expSecondsFromNow: 7200})
            return ok(refreshedToken, {'X-Refresh-Token': 'mock-refresh-2'})
        }

        if (path === '/Category/all') return ok([])
        if (path === '/Auth/validate') return ok(true)

        return ok(null)
    }

    // The repo's `.env` uses an absolute backend URL, while deployments may use
    // a same-origin `/api` base. Register the dispatcher for both shapes.
    await page.route('**/api/**', handler)
    await page.route('http://localhost:5052/**', handler)
    await page.route('http://127.0.0.1:5052/**', handler)
}

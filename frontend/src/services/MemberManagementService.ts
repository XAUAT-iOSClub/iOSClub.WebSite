import {url} from './Url';
import {apiRequest, readApiResponse} from './ApiService';
import {AuthService} from './AuthService';
import {ErrorCode} from '../constants/ErrorCode';
import {StudentCreateDTO, StudentUpdateDTO, StudentVO} from "../models";

/**
 * 批量更新（JSON 导入）的逐行问题说明 —— 对应后端的 issues 数组。
 * kind=skipped 表示记录被规则过滤，kind=failed 表示该行处理出错。
 */
export interface BulkUpdateIssue {
    userId: string;
    reason: string;
    kind: 'skipped' | 'failed';
}

/**
 * 批量更新结果 —— 对应后端 BulkUpdateResultVO。
 * 后端在零写入时返回失败业务码（ApiResponseResultFilter 使 HTTP 状态等于业务码），
 * 但 data 里仍保留这些计数。
 */
export interface BulkUpdateResult {
    created: number;
    updated: number;
    skipped: number;
    failed: number;
    issues: BulkUpdateIssue[];
}

/**
 * 带成败标记的批量更新结果。
 *
 * 不能像旧实现那样在 `apiRequest` 抛错时丢掉 data：零写入的失败响应同样带
 * 计数与原因，页面需要把它们展示出来，而不是只弹一句“导入失败”。
 */
export interface BulkUpdateOutcome extends BulkUpdateResult {
    /** 后端是否以成功业务码返回。 */
    success: boolean;
    /** 后端响应里的 message，供页面兜底显示。 */
    message: string;
}

const toCount = (value: unknown): number =>
    typeof value === 'number' && Number.isFinite(value) && value > 0 ? Math.trunc(value) : 0;

/** 兼容后端可能采用的 camelCase / PascalCase 序列化，并丢弃无效字段。 */
function normalizeIssue(raw: any): BulkUpdateIssue | null {
    if (!raw || typeof raw !== 'object') return null;
    const userId = String(raw.userId ?? raw.UserId ?? '').trim();
    const reason = String(raw.reason ?? raw.Reason ?? '').trim();
    if (!userId && !reason) return null;
    return {
        userId,
        reason,
        kind: (raw.kind ?? raw.Kind) === 'failed' ? 'failed' : 'skipped',
    };
}

function normalizeResult(data: any): BulkUpdateResult {
    if (!data || typeof data !== 'object') {
        return {created: 0, updated: 0, skipped: 0, failed: 0, issues: []};
    }
    const rawIssues = data.issues ?? data.Issues;
    const issues = Array.isArray(rawIssues)
        ? rawIssues.map(normalizeIssue).filter((issue): issue is BulkUpdateIssue => issue !== null)
        : [];
    return {
        created: toCount(data.created ?? data.Created),
        updated: toCount(data.updated ?? data.Updated),
        skipped: toCount(data.skipped ?? data.Skipped),
        failed: toCount(data.failed ?? data.Failed),
        issues,
    };
}

/**
 * 成员管理服务类 - 处理成员的删除、新增、更新等管理功能
 */
export class MemberManagementService {
    /**
     * 删除学生成员
     * @param id 学生ID
     * @returns Promise<void>
     */
    static async deleteMember(id: string): Promise<void> {
        await apiRequest<void>({
            url: `${url}/MemberManagement/delete/${id}`,
            method: 'POST'
        });
    }

    /**
     * 新增学生成员
     * 与 update 分开：update 以学号为定位键，学号不存在时必然失败。
     * @param model 学生创建请求（含管理员设置的初始密码）
     * @returns Promise<StudentVO> 新建的学生信息（不含密码字段）
     */
    static async createMember(model: StudentCreateDTO): Promise<StudentVO> {
        return await apiRequest<StudentVO>({
            url: `${url}/MemberManagement/create`,
            method: 'POST',
            body: model
        });
    }

    /**
     * 批量更新或添加学生成员
     *
     * 直接读取统一响应信封而不是走 `apiRequest`：零写入时后端用失败业务码返回
     * （HTTP 400），`apiRequest` 会抛错并丢弃 data，导致页面拿不到跳过/失败原因。
     * 这里保留计数并返回 success 标记，由页面决定提示成功还是失败。
     *
     * @param list 学生更新请求列表
     * @returns Promise<BulkUpdateOutcome> 逐行计数、原因与成败标记
     */
    static async updateManyMembers(list: StudentUpdateDTO[]): Promise<BulkUpdateOutcome> {
        const send = (token: string | null) => fetch(`${url}/MemberManagement/update-many`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                ...(token ? {'Authorization': `Bearer ${token}`} : {})
            },
            body: JSON.stringify(list)
        });

        let response = await send(AuthService.getToken());
        let apiResponse = await readApiResponse<Partial<BulkUpdateResult> | null>(response);

        // 凭证失效时刷新一次并重试，保持与 apiRequest 一致的自动续期行为。
        if (response.status === 401 || apiResponse.errorCode === ErrorCode.LoginExpired) {
            try {
                const refreshedToken = await AuthService.refreshToken();
                response = await send(refreshedToken);
                apiResponse = await readApiResponse<Partial<BulkUpdateResult> | null>(response);
            } catch {
                AuthService.clearToken();
                throw new Error(apiResponse.message || '登录已过期，请重新登录');
            }
        }

        if (response.status === 401
            || apiResponse.errorCode === ErrorCode.LoginExpired
            || apiResponse.errorCode === ErrorCode.InvalidToken) {
            AuthService.clearToken();
            throw new Error(apiResponse.message || '登录已过期，请重新登录');
        }

        return {
            ...normalizeResult(apiResponse.data),
            success: response.ok && apiResponse.errorCode === ErrorCode.Success,
            message: apiResponse.message || ''
        };
    }

    /**
     * 更新单个成员信息
     * @param model 学生更新请求
     * @returns Promise<void>
     */
    static async updateMember(model: StudentUpdateDTO): Promise<void> {
        await apiRequest<void>({
            url: `${url}/MemberManagement/update`,
            method: 'POST',
            body: model
        });
    }

    /**
     * 管理员重置成员密码
     * @param userId 成员ID
     * @param newPassword 新密码
     * @returns Promise<void>
     */
    static async resetMemberPassword(userId: string, newPassword: string): Promise<void> {
        await apiRequest<void>({
            url: `${url}/MemberManagement/reset-password`,
            method: 'POST',
            body: {userId, newPassword}
        });
    }
}

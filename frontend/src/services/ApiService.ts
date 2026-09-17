import {AuthService} from './AuthService';
import MessageService from './MessageService';
import {ErrorCode} from '../constants/ErrorCode';

/**
 * 后端统一响应信封 —— 对应 iOSClub.WebAPI/Common/ApiResponse.cs。
 *
 * 契约：HTTP 状态码恒等于 body 里的 `code`，`errorCode === 0` 表示成功。
 * 所有接口（包括失败）都返回这个形状。
 */
export interface ApiResponse<T> {
    code: number;
    errorCode: number;
    message: string;
    detail?: string | null;
    data: T;
    requestId?: string | null;
    timestamp?: string | null;
}

/**
 * 带业务元数据的请求错误。
 *
 * 调用方（如页面）需要区分"认证失败要登出"与"业务数据不存在只需提示"时，
 * 不能只看 message 文本，必须能拿到 HTTP 状态码与业务错误码。
 */
export class ApiError extends Error {
    /** HTTP 状态码（与响应体 code 恒等） */
    readonly code: number;
    /** 业务错误码（0 表示成功） */
    readonly errorCode: number;
    readonly detail?: string | null;
    readonly requestId?: string | null;

    constructor(message: string, code: number, errorCode: number, detail?: string | null, requestId?: string | null) {
        super(message);
        this.name = 'ApiError';
        this.code = code;
        this.errorCode = errorCode;
        this.detail = detail;
        this.requestId = requestId;
    }
}

/**
 * API请求配置
 */
export interface ApiRequestConfig extends Omit<RequestInit, 'body'> {
    url: string;
    requiresAuth?: boolean;
    /**
     * Attach the current identity when possible, but fall back to an anonymous
     * request if the access/refresh token can no longer be used.
     */
    optionalAuth?: boolean;
    body?: any;

    showMessage?: boolean;
    showSuccess?: boolean;
    showError?: boolean;
}

// 正在进行的刷新 Promise，用于并发请求去重
let refreshPromise: Promise<string> | null = null;

/**
 * 把 HTTP 状态码翻译成业务错误码，仅在响应体不是统一信封时兜底使用。
 */
function errorCodeFromHttpStatus(status: number): number {
    switch (status) {
        case 401:
            return ErrorCode.InvalidToken;
        case 403:
            return ErrorCode.InsufficientPermission;
        case 404:
            return ErrorCode.ResourceNotFound;
        case 429:
            return ErrorCode.TooManyRequests;
        default:
            return status >= 500 ? ErrorCode.InternalServerError : ErrorCode.ParameterFormatError;
    }
}

/**
 * 解析后端响应。
 *
 * 关键点：**无论 HTTP 状态是否为 2xx，都先尝试解析响应体**。
 * 以前这里在 `!response.ok` 时直接丢弃 body、把 errorCode 伪造成 5000，
 * 导致后端辛苦返回的 4003/1001/7000 全部丢失，前端只能看到"服务器内部错误"。
 *
 * 只有响应体不是统一信封时（代理错误页、OAuth 协议端点、裸字符串响应等）
 * 才退回按 HTTP 状态合成。
 */
export async function readApiResponse<T>(response: Response): Promise<ApiResponse<T>> {
    let payload: any = null;
    try {
        payload = await response.json();
    } catch {
        payload = null;
    }

    if (payload && typeof payload === 'object' && typeof payload.code === 'number') {
        return payload as ApiResponse<T>;
    }

    // 不是统一信封：尽量从常见字段里取一句可读的错误说明
    const message = payload?.message
        || payload?.error_description
        || payload?.error
        || response.statusText
        || `请求失败 (${response.status})`;

    return {
        code: response.status,
        errorCode: errorCodeFromHttpStatus(response.status),
        message,
        detail: typeof payload?.error === 'string' ? payload.error : null,
        data: null as T,
        requestId: null,
        timestamp: null
    };
}

/**
 * 把失败响应转成 Error。按错误码区间分类，避免为每个错误码单独写分支。
 */
function toError(apiResponse: ApiResponse<unknown>): Error {
    const {errorCode, message, code, detail, requestId} = apiResponse;
    const make = (text: string) => new ApiError(text, code, errorCode, detail, requestId);

    // 3000-3999：认证/权限类。除"权限不足"外都意味着当前凭证已不可用，清掉本地令牌。
    // 注意 3001(InsufficientPermission) 只是权限不够——以前把它和 401 混为一谈，
    // 结果用户撞到权限墙就被登出。
    if (errorCode >= 3000 && errorCode < 4000) {
        if (errorCode === ErrorCode.InsufficientPermission) {
            return make(message || '权限不足');
        }
        AuthService.clearToken();
        return make(message || '登录已过期，请重新登录');
    }

    if (errorCode >= 5000 && errorCode < 6000) {
        return make(message || '服务器内部错误');
    }

    if (errorCode >= 7000 && errorCode < 8000) {
        return make(message || '请求频率过高，请稍后再试');
    }

    return make(message || '请求失败');
}

/**
 * 该响应是否值得刷新令牌后重试。
 * 只有"凭证不可用"才值得：HTTP 401、登录已过期(3002)、无效令牌(3003)。
 */
function shouldRefreshToken(apiResponse: ApiResponse<unknown>): boolean {
    return apiResponse.code === 401
        || apiResponse.errorCode === ErrorCode.LoginExpired
        || apiResponse.errorCode === ErrorCode.InvalidToken;
}

/**
 * 按配置弹出提示。集中一处调用，避免同一响应同时命中 showMessage 与 showSuccess 时弹两次。
 */
function notify<T>(apiResponse: ApiResponse<T>, config: ApiRequestConfig): void {
    if (config.showMessage || config.showSuccess || config.showError) {
        MessageService.handleResponse(apiResponse, config);
    }
}

/**
 * 确保 Token 有效——如果即将过期则主动刷新。
 * 同时通过共享 Promise 实现并发刷新去重。
 */
async function ensureValidToken(): Promise<void> {
    const token = AuthService.getToken();
    if (!token) return;

    const userInfo = AuthService.parseJwtToken(token);
    if (!userInfo?.exp) return;

    const nowSeconds = Math.floor(Date.now() / 1000);
    // Token 在未来 60 秒内过期，主动刷新
    if (userInfo.exp - nowSeconds > 60) return;

    if (!refreshPromise) {
        refreshPromise = AuthService.refreshToken().finally(() => {
            refreshPromise = null;
        });
    }

    await refreshPromise;
}

/**
 * 通用API请求处理函数
 * @param config 请求配置
 * @returns Promise<T> 响应数据
 */
export async function apiRequest<T>(config: ApiRequestConfig): Promise<T> {
    const {url, headers = {}, body, ...rest} = config;
    const isFormData = typeof FormData !== 'undefined' && body instanceof FormData;

    // 添加默认请求头
    const requestHeaders: Record<string, string> = {
        // FormData 的 multipart boundary 必须由浏览器生成；手动设置会导致后端无法绑定文件。
        ...(isFormData ? {} : {'Content-Type': 'application/json'}),
        ...((headers as Record<string, string>) || {})
    };

    // 需要认证的请求携带令牌；可选认证请求在令牌失效时降级为匿名请求。
    if (config.requiresAuth !== false) {
        const token = AuthService.getToken();
        if (token) {
            try {
                await ensureValidToken();
            } catch (error) {
                if (!config.optionalAuth) {
                    throw error;
                }

                AuthService.clearTokens();
            }

            const validToken = AuthService.getToken();
            if (validToken) {
                requestHeaders['Authorization'] = `Bearer ${validToken}`;
            }
        }
    }

    // 处理请求体：如果是对象且Content-Type为application/json，则转换为JSON字符串
    let requestBody: BodyInit | null | undefined = body;
    const contentType = requestHeaders['Content-Type'];
    if (!isFormData && body && typeof body === 'object' && contentType === 'application/json') {
        requestBody = JSON.stringify(body);
    }

    let apiResponse: ApiResponse<T>;

    try {
        const response = await fetch(url, {
            headers: requestHeaders,
            body: requestBody,
            ...rest
        });
        apiResponse = await readApiResponse<T>(response);
    } catch (reason: any) {
        // 网络层失败（连接被拒、DNS、CORS 等），此时没有响应体可解析
        apiResponse = {
            code: 0,
            errorCode: ErrorCode.NetworkError,
            message: reason?.message || '网络连接异常',
            data: null as T
        };
    }

    if (apiResponse.errorCode === ErrorCode.Success) {
        notify(apiResponse, config);
        return apiResponse.data;
    }

    // 凭证失效：刷新令牌后重试一次
    if (config.requiresAuth !== false && shouldRefreshToken(apiResponse)) {
        try {
            // 使用共享 Promise 刷新令牌，避免并发 401 触发多次刷新
            if (!refreshPromise) {
                refreshPromise = AuthService.refreshToken().finally(() => {
                    refreshPromise = null;
                });
            }
            await refreshPromise;

            const newToken = AuthService.getToken();
            if (newToken) {
                requestHeaders['Authorization'] = `Bearer ${newToken}`;

                const retryResponse = await fetch(url, {
                    headers: requestHeaders,
                    body: requestBody,
                    ...rest
                });
                apiResponse = await readApiResponse<T>(retryResponse);

                if (apiResponse.errorCode === ErrorCode.Success) {
                    notify(apiResponse, config);
                    return apiResponse.data;
                }
            }
        } catch (refreshError) {
            // 公开但按身份过滤的接口可以在刷新失败后匿名重试。
            if (config.optionalAuth) {
                AuthService.clearTokens();
                delete requestHeaders['Authorization'];

                const anonymousResponse = await fetch(url, {
                    headers: requestHeaders,
                    body: requestBody,
                    ...rest
                });
                apiResponse = await readApiResponse<T>(anonymousResponse);

                if (apiResponse.errorCode === ErrorCode.Success) {
                    return apiResponse.data;
                }
            } else {
                // 刷新令牌失败，清除令牌并抛出错误
                AuthService.clearToken();
                throw new Error('登录已过期，请重新登录');
            }
        }
    }

    notify(apiResponse, config);
    throw toError(apiResponse);
}

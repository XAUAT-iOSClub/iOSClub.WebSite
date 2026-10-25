import {url} from './Url';
import type {StudentCreateDTO, LoginModel} from '../models';
import {apiRequest, readApiResponse} from './ApiService';

/**
 * 密码哈希函数
 * @param password 原始密码
 * @returns Promise<string> 哈希后的密码
 */
export const hashPassword = async (password: string) => {
    const encoder = new TextEncoder()
    const data = encoder.encode(password)
    const hashBuffer = await crypto.subtle.digest('SHA-256', data)
    const hashArray = Array.from(new Uint8Array(hashBuffer))
    return hashArray.map(b => b.toString(16).padStart(2, '0')).join('')
}

/**
 * 认证服务类 - 处理登录注册相关的API调用
 */
export class AuthService {
    /**
     * 用户登录
     * @param loginModel 登录信息
     * @param clientId 客户端ID
     * @param scope 授权范围
     * @returns Promise<{ accessToken: string, refreshToken: string }> 访问令牌和刷新令牌
     */
    static async login(loginModel: LoginModel, clientId: string | null | undefined = '', scope: string | null | undefined = ''): Promise<{ accessToken: string, refreshToken: string }> {
        // 直接使用fetch获取完整响应，以便获取响应头中的刷新令牌；
        // 响应体解析复用 readApiResponse，与 apiRequest 保持同一套判据。
        const response = await fetch(`${url}/Auth/login?clientId=${clientId}&scope=${scope}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(loginModel)
        });

        const data = await readApiResponse<string>(response);

        if (data.errorCode !== 0 || !data.data) {
            throw new Error(data.message || '登录失败');
        }

        const accessToken = data.data;

        // 从响应头中获取刷新令牌
        const refreshToken = response.headers.get('X-Refresh-Token') || '';

        this.saveTokens(accessToken, refreshToken);
        return { accessToken, refreshToken };
    }

    /**
     * 学生注册
     * @param model 学生注册信息
     * @returns Promise<{ accessToken: string, refreshToken: string }> 访问令牌和刷新令牌
     */
    static async signup(model: StudentCreateDTO): Promise<{ accessToken: string, refreshToken: string }> {
        // 直接使用fetch获取完整响应，以便获取响应头中的刷新令牌。
        // 注意：字段名必须与后端 StudentCreateDTO 一致（密码字段是 password）。
        const response = await fetch(`${url}/Auth/signup`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(model)
        });

        const data = await readApiResponse<string>(response);

        if (data.errorCode !== 0 || !data.data) {
            // 后端校验失败时 message 只有“请求参数验证失败”，具体字段原因在 detail 里
            // （形如 "userName: 姓名应为2-20个汉字或字母…"），直接展示给用户。
            const reason = data.detail ? `${data.message}：${data.detail}` : data.message;
            throw new Error(reason || '注册失败');
        }

        const accessToken = data.data;

        // 从响应头中获取刷新令牌
        const refreshToken = response.headers.get('X-Refresh-Token') || '';

        this.saveTokens(accessToken, refreshToken);
        return { accessToken, refreshToken };
    }

    static async logout(userId: string, clientId: string | null | undefined = ''): Promise<boolean> {
        try {
            await apiRequest<boolean>({
                url: `${url}/Auth/logout?userId=${userId}&clientId=${clientId}`,
                method: 'POST'
            });
            this.clearTokens();
            return true;
        } catch (error) {
            return false;
        }
    }

    // 后端 AuthController.ValidateToken 只接受 userId，没有 clientId 参数
    static async validate(userId: string, token: string): Promise<boolean> {
        try {
            await apiRequest<boolean>({
                url: `${url}/Auth/validate?userId=${userId}`,
                method: 'GET',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });
            return true;
        } catch (e) {
            console.error(e);
            return false;
        }
    }

    /**
     * 保存访问令牌和刷新令牌到本地存储
     * @param accessToken 访问令牌
     * @param refreshToken 刷新令牌
     */
    static saveTokens(accessToken: string, refreshToken: string): void {
        localStorage.setItem('accessToken', accessToken);
        localStorage.setItem('refreshToken', refreshToken);
    }

    /**
     * 从本地存储获取访问令牌
     * @returns string | null 访问令牌或null
     */
    static getAccessToken(): string | null {
        return localStorage.getItem('accessToken');
    }

    /**
     * 从本地存储获取刷新令牌
     * @returns string | null 刷新令牌或null
     */
    static getRefreshToken(): string | null {
        return localStorage.getItem('refreshToken');
    }

    /**
     * 使用刷新令牌获取新的访问令牌
     * @returns Promise<string> 新的访问令牌
     */
    static async refreshToken(): Promise<string> {
        const refreshToken = this.getRefreshToken();
        const userInfo = this.getCurrentUserInfo();
        
        if (!refreshToken || !userInfo?.sub) {
            throw new Error('刷新令牌不存在或用户信息无效');
        }
        
        // 刷新令牌按 client_id 分区存储；沿用当前访问令牌的客户端和授权范围。
        const query = new URLSearchParams({
            userId: userInfo.sub,
            refreshToken,
            clientId: userInfo.client_id || '',
            scope: userInfo.scope || ''
        });
        const res = await fetch(`${url}/Auth/refresh-token?${query.toString()}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        })

        // 先解析响应体再判成败：以前这里先看 res.ok，会把 body 里的具体原因丢掉。
        const data = await readApiResponse<string>(res);
        const newAccessToken = data.data;
        const newRefreshToken = res.headers.get('X-Refresh-Token');

        // 后端轮换了刷新令牌，两个令牌必须同时更新；缺任何一个都不能写入本地存储。
        if (data.errorCode !== 0 || !newAccessToken || !newRefreshToken) {
            throw new Error(data.message || '刷新令牌失败');
        }

        this.saveTokens(newAccessToken, newRefreshToken);
        return newAccessToken;
    }

    /**
     * 清除本地存储的令牌
     */
    static clearTokens(): void {
        localStorage.removeItem('accessToken');
        localStorage.removeItem('refreshToken');
    }

    /**
     * 获取当前令牌（兼容旧代码）
     * @returns string | null 访问令牌或null
     */
    static getToken(): string | null {
        return this.getAccessToken();
    }

    /**
     * 保存令牌（兼容旧代码）
     * @param token 访问令牌
     */
    static saveToken(token: string): void {
        const refreshToken = this.getRefreshToken() || '';
        this.saveTokens(token, refreshToken);
    }

    /**
     * 清除令牌（兼容旧代码）
     */
    static clearToken(): void {
        this.clearTokens();
    }

    /**
     * 检查用户是否已登录
     * @returns boolean 是否已登录
     */
    static isLoggedIn(): boolean {
        return this.getToken() !== null;
    }

    /**
     * 解析JWT令牌并提取用户信息
     * @param token JWT令牌
     * @returns any 解析出的用户信息或null
     */
    static parseJwtToken(token: string | null): any {
        if (!token) return null;

        try {
            // JWT格式: header.payload.signature
            const base64Url = token.split('.')[1];
            const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
            const jsonPayload = decodeURIComponent(
                window.atob(base64)
                    .split('')
                    .map(function (c) {
                        return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
                    })
                    .join('')
            );

            return JSON.parse(jsonPayload);
        } catch (e) {
            console.error('解析JWT令牌失败:', e);
            return null;
        }
    }

    /**
     * 获取当前登录用户信息
     * @returns any 用户信息或null
     */
    static getCurrentUserInfo(): any {
        const token = this.getToken();
        return this.parseJwtToken(token);
    }

    /**
     * 修改用户密码
     * @param userId 用户ID
     * @param oldPassword 旧密码
     * @param newPassword 新密码
     * @returns Promise<boolean> 是否修改成功
     */
    static async changePassword(userId: string, oldPassword: string, newPassword: string): Promise<boolean> {
        return await apiRequest<boolean>({
            url: `${url}/Auth/change-password?userId=${userId}&oldPassword=${oldPassword}&newPassword=${newPassword}`,
            method: 'PUT'
        });
    }

    /**
     * 请求重置密码的验证码
     * @param userId 用户ID
     * @returns Promise<boolean> 是否发送成功
     */
    static async requestPasswordReset(userId: string): Promise<boolean> {
        return await apiRequest<boolean>({
            url: `${url}/Auth/request-password-reset?userId=${userId}`,
            method: 'POST',
            requiresAuth: false
        });
    }

    /**
     * 通过验证码重置密码
     * @param userId 用户ID
     * @param code 验证码
     * @param newPassword 新密码
     * @returns Promise<boolean> 是否重置成功
     */
    static async resetPassword(userId: string, code: string, newPassword: string): Promise<boolean> {
        return await apiRequest<boolean>({
            url: `${url}/Auth/reset-password?userId=${userId}&code=${code}&newPassword=${newPassword}`,
            method: 'POST',
            requiresAuth: false
        });
    }

    /**
     * 从主站JWT获取SSO会话
     * @param clientId 客户端ID
     * @param scope 权限范围
     * @returns Promise<string> SSO令牌
     */
    static async loginFromMainJwt(clientId: string, scope: string = 'profile openid role'): Promise<string> {
        const token = this.getToken();
        if (!token) {
            throw new Error('未登录或令牌已过期');
        }

        const response = await fetch(`${url}/SSO/from_main_jwt?scope=${encodeURIComponent(scope)}&client_id=${encodeURIComponent(clientId)}`, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`,
            },
        });

        if (!response.ok) {
            throw new Error('从主站JWT获取SSO会话失败');
        }

        return await response.text();
    }
}

// 组织注册记录类
export class OrgSignRecord {
    constructor(
        public readonly url1: string,
        public readonly url2: string
    ) {
    }
}

// iOS 俱乐部注册信息
export const ios = new OrgSignRecord(
    "mqqapi://card/show_pslcard?src_type=internal&version=1&uin=952954710&card_type=group&source=external",
    "https://qm.qq.com/cgi-bin/qm/qr?authKey=MUNgIj%2B1gnkiI175qAQla6EcR44Fa0APCv%2FLo1a7YIlYgpeg76Q%2BGYMoedb8gGHU&k=HvhhArSc7tzuySOhXsnmZ6RgLcWkzXgu&noverify=0"
);

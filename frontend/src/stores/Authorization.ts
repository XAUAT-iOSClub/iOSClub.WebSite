import {defineStore} from 'pinia';
import {AuthService} from '../services/AuthService';
import {decodeJwtPayload} from '../utils/jwt';
import type {LoginModel, StudentCreateDTO} from '../models';

export const useAuthorizationStore = defineStore('AuthorizationId', {
    state: () => ({
        Authorization: localStorage.getItem('accessToken') || ''
    }),
    getters: {
        getAuthorization: (state) => state.Authorization,
        isAuthenticated: (state) => !!state.Authorization && state.Authorization.length > 0,
        // 检查 Token 是否存在且未过期（路由守卫应优先使用此 getter）
        isTokenValid: (state): boolean => {
            const payload = decodeJwtPayload(state.Authorization);
            const exp = payload?.exp;
            return typeof exp === 'number' && exp > Math.floor(Date.now() / 1000);
        },
        getAuthorizationInfo: (state): any => decodeJwtPayload(state.Authorization),
        getRole: (state): string | null =>
            decodeJwtPayload(state.Authorization)?.['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ?? null,
    },
    actions: {
        /**
         * localStorage is the canonical session source: login and every
         * automatic refresh write there, while this store is only a reactive
         * mirror. Call this before reads that must observe a refreshed token
         * (notably the router guard), otherwise Pinia keeps serving a stale one.
         */
        syncFromStorage(): void {
            this.Authorization = AuthService.getAccessToken() || '';
        },
        async signup(stu: StudentCreateDTO): Promise<boolean> {
            const { accessToken } = await AuthService.signup(stu);
            if (!accessToken) return false;
            this.Authorization = accessToken;
            localStorage.setItem('UserId', stu.userId);
            return true;
        },
        async logout(clientId: string | null | undefined = ''): Promise<boolean> {
            this.Authorization = '';
            const id = localStorage.getItem('UserId');
            let revoked = false;
            try {
                // AuthService.logout always clears local credentials, even when
                // the server-side revoke fails.
                revoked = id ? await AuthService.logout(id, clientId) : false;
                if (!id) AuthService.clearTokens();
            } finally {
                this.syncFromStorage();
                localStorage.removeItem('UserId');
            }
            return revoked;
        },
        async login(user: LoginModel, clientId: string | null | undefined = '', scope: string | null | undefined = ''): Promise<boolean> {
            const { accessToken } = await AuthService.login(user, clientId, scope);
            if (!accessToken) {
                return false;
            }
            this.Authorization = accessToken;
            localStorage.setItem('UserId', user.userId);
            return true;
        },
        async oauthLogin(user: LoginModel, clientId: string | null | undefined = '', scope: string | null | undefined = ''): Promise<string> {
            const { accessToken } = await AuthService.login(user, clientId, scope);
            this.Authorization = accessToken;
            localStorage.setItem('UserId', user.userId);
            return accessToken;
        },
        async validate(): Promise<boolean> {
            const id = localStorage.getItem('UserId');
            if (id === null || id === '') return false;
            return await AuthService.validate(id, this.Authorization);
        },
        async refreshToken(): Promise<boolean> {
            try {
                const newAccessToken = await AuthService.refreshToken();
                if (newAccessToken) {
                    this.Authorization = newAccessToken;
                    return true;
                }
                return false;
            } catch (e) {
                console.error('刷新令牌失败:', e);
                return false;
            }
        },
        isAdmin() {
            return this.getRole === 'Founder' || this.getRole === 'President' || this.getRole === 'Minister';
        }
    }
});
import {url} from './Url';

export class OAuthService {
    static async loadClientAppInfo(clientId: string): Promise<any> {
        if (!clientId) return null;

        try {
            const response = await fetch(`${url}/SSO/client-info?clientId=${clientId}`);
            if (response.ok) {
                return await response.json();
            }
            return null;
        } catch (error) {
            console.error('加载客户端应用信息失败:', error);
            return null;
        }
    }

    /**
     * 把主站登录态写入 SSO 会话。
     *
     * Redis 故障时后端返回 HTTP 500 或 `{success:false}`。两者都必须按失败处理：
     * 旧实现只看 `response.ok`，HTTP 200 + success:false 会被误判为成功，
     * 页面随即跳转 SSO/callback，把一次失败伪装成完成。
     */
    static async storeSession(state: string, clientId: string, authorization: string): Promise<boolean> {
        try {
            const sessionResponse = await fetch(`${url}/SSO/store-session`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${authorization}`
                },
                body: JSON.stringify({
                    state,
                    client_id: clientId
                })
            });

            let payload: {success?: boolean; message?: string} | null = null;
            try {
                payload = await sessionResponse.json();
            } catch {
                // 非 JSON 响应（代理错误页等）交给 HTTP 状态判断。
                payload = null;
            }

            if (!sessionResponse.ok) {
                return false;
            }
            return payload?.success !== false;
        } catch (error) {
            console.error('会话存储失败:', error);
            return false;
        }
    }
}
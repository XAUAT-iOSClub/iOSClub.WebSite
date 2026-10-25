/**
 * 解析 JWT 的载荷（payload）。
 *
 * JWT 三段都用 base64url 编码：`+` 变成 `-`、`/` 变成 `_`、末尾的 `=` 被去掉。
 * 直接把它交给 atob 会因非法字符抛 InvalidCharacterError，而载荷里是否出现
 * `-`/`_` 取决于具体内容，所以「有的 token 能解析、有的不能」。
 *
 * @param token JWT 字符串
 * @returns 载荷对象；格式不合法或解码失败时返回 null
 */
export function decodeJwtPayload(token: string | null | undefined): Record<string, any> | null {
    if (!token) return null;

    const parts = token.split('.');
    if (parts.length !== 3) return null;

    try {
        // 还原标准 base64 并补回 atob 所需的填充
        const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
        const padded = base64.padEnd(base64.length + (4 - (base64.length % 4)) % 4, '=');

        // 载荷里有中文等非 ASCII 内容，按 UTF-8 解码，不能直接用 atob 的结果
        const bytes = Uint8Array.from(atob(padded), (c) => c.charCodeAt(0));
        const payload = JSON.parse(new TextDecoder().decode(bytes));

        return payload && typeof payload === 'object' ? payload : null;
    } catch (e) {
        console.error('解析 JWT 载荷失败:', e);
        return null;
    }
}

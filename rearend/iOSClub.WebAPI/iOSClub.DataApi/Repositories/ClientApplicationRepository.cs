using iOSClub.Data;
using iOSClub.Data.DataObjects;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.DataApi.Repositories;

public interface IClientApplicationRepository
{
    /// <summary>
    /// 获取所有客户端应用
    /// </summary>
    /// <returns>客户端应用列表</returns>
    Task<IEnumerable<ClientApplicationDO>> GetAllAsync();

    /// <summary>
    /// 根据客户端ID获取应用
    /// </summary>
    /// <param name="clientId">客户端ID</param>
    /// <returns>客户端应用</returns>
    Task<ClientApplicationDO?> GetByClientIdAsync(string clientId);

    /// <summary>
    /// 根据重定向URI获取应用
    /// </summary>
    /// <param name="redirectUri">重定向Url</param>
    /// <returns></returns>
    Task<ClientApplicationDO?> GetByRedirectUriAsync(string redirectUri);

    /// <summary>
    /// 创建新的客户端应用
    /// </summary>
    /// <param name="clientApplication">客户端应用</param>
    /// <returns>是否创建成功</returns>
    Task<bool> CreateAsync(ClientApplicationDO clientApplication);

    /// <summary>
    /// 更新客户端应用
    /// </summary>
    /// <param name="clientApplication">客户端应用</param>
    /// <returns>是否更新成功</returns>
    Task<bool> UpdateAsync(ClientApplicationDO clientApplication);

    /// <summary>
    /// 删除客户端应用
    /// </summary>
    /// <param name="clientId">客户端ID</param>
    /// <returns>是否删除成功</returns>
    Task<bool> DeleteAsync(string clientId);

    /// <summary>
    /// 验证客户端凭据
    /// </summary>
    /// <param name="clientId">客户端ID</param>
    /// <param name="clientSecret">客户端密钥</param>
    /// <returns>客户端应用</returns>
    Task<ClientApplicationDO?> ValidateCredentialsAsync(string clientId, string clientSecret);
}

public class ClientApplicationRepository(IDbContextFactory<ClubContext> contextFactory) : IClientApplicationRepository
{
    public async Task<IEnumerable<ClientApplicationDO>> GetAllAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.ClientApplications.ToListAsync();
    }

    public async Task<ClientApplicationDO?> GetByClientIdAsync(string clientId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.ClientApplications.FirstOrDefaultAsync(c => c.ClientId == clientId);
    }

    public async Task<ClientApplicationDO?> GetByRedirectUriAsync(string redirectUri)
    {
        if (string.IsNullOrWhiteSpace(redirectUri)) return null;

        await using var context = await contextFactory.CreateDbContextAsync();
        var applications = await context.ClientApplications.ToListAsync();

        // 与 IsRedirectUriValid 保持一致：按分号拆开后精确比较，
        // 不能用字符串 Contains —— 那会允许 https://evil.example 命中
        // 已注册的 https://app.example/callback 片段。
        return applications.FirstOrDefault(c =>
            c.RedirectUris.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Any(uri => string.Equals(uri.Trim(), redirectUri.Trim(), StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<bool> CreateAsync(ClientApplicationDO clientApplication)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        // 对客户端密钥进行哈希处理后再存储
        if (!IsBcryptHash(clientApplication.ClientSecret))
            clientApplication.ClientSecret = BCrypt.Net.BCrypt.HashPassword(clientApplication.ClientSecret);
        context.ClientApplications.Add(clientApplication);
        return await context.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateAsync(ClientApplicationDO clientApplication)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        // 检查客户端密钥是否需要重新哈希（如果提供了新密钥）
        if (!string.IsNullOrEmpty(clientApplication.ClientSecret) &&
            !IsBcryptHash(clientApplication.ClientSecret))
        {
            clientApplication.ClientSecret = BCrypt.Net.BCrypt.HashPassword(clientApplication.ClientSecret);
        }

        context.ClientApplications.Update(clientApplication);
        return await context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteAsync(string clientId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var clientApplication = await context.ClientApplications.FirstOrDefaultAsync(c => c.ClientId == clientId);
        if (clientApplication == null) return false;

        context.ClientApplications.Remove(clientApplication);
        return await context.SaveChangesAsync() > 0;
    }

    public async Task<ClientApplicationDO?> ValidateCredentialsAsync(string clientId, string clientSecret)
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return null;

        await using var context = await contextFactory.CreateDbContextAsync();
        var clientApplication =
            await context.ClientApplications.FirstOrDefaultAsync(c =>
                c.ClientId == clientId);

        if (clientApplication is not { IsActive: true })
            return null;

        var storedSecret = clientApplication.ClientSecret;
        if (string.IsNullOrEmpty(storedSecret))
            return null;

        // 【临时回退 H1】恢复历史上的明文相等分支，兼容把库里存的值直接当
        // client_secret 使用的旧客户端（包括历史上存明文、以及把 BCrypt 哈希
        // 当密钥使用的客户端）。
        // 安全影响：这会让"库内哈希本身"重新变成可用口令。目前 ClientAppVO
        // 仍不回显密钥，暴露面比 H1 之前小；但这是一次有意的安全回退，
        // 待所有客户端迁移到真实密钥后应删除此分支并恢复 BCrypt-only 校验。
        if (storedSecret == clientSecret)
            return clientApplication;

        try
        {
            return BCrypt.Net.BCrypt.Verify(clientSecret, storedSecret)
                ? clientApplication
                : null;
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return null;
        }
    }

    /// <summary>
    /// BCrypt 哈希格式：$2a$/$2b$/$2x$/$2y$ + 成本 + 22 位盐 + 31 位哈希，共 60 字符。
    /// 只按前缀 "$2" 判断会把"恰好以 $2 开头的明文密钥"误判成哈希。
    /// </summary>
    private static bool IsBcryptHash(string value)
        => value.Length == 60
           && value[0] == '$'
           && value[1] == '2'
           && value[2] is 'a' or 'b' or 'x' or 'y'
           && value[3] == '$';
}
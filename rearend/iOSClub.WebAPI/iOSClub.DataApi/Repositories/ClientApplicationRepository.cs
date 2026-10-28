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
        clientApplication.ClientSecret = BCrypt.Net.BCrypt.HashPassword(clientApplication.ClientSecret);
        context.ClientApplications.Add(clientApplication);
        return await context.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateAsync(ClientApplicationDO clientApplication)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        // 检查客户端密钥是否需要重新哈希（如果提供了新密钥）
        // 如果客户端密钥不是BCrypt哈希格式，则进行哈希处理
        if (!string.IsNullOrEmpty(clientApplication.ClientSecret) &&
            !clientApplication.ClientSecret.StartsWith("$2"))
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
        await using var context = await contextFactory.CreateDbContextAsync();
        var clientApplication =
            await context.ClientApplications.FirstOrDefaultAsync(c =>
                c.ClientId == clientId);

        if (clientApplication is not { IsActive: true })
            return null;

        // 只接受 BCrypt 哈希。以前的 "ClientSecret == clientSecret" 明文相等分支
        // 会让 ClientAppVO 里返回的哈希本身变成可用的 client_secret —— 任何能读到
        // 应用列表的人都能冒充该客户端。明文存储的旧数据必须重新生成密钥。
        if (string.IsNullOrEmpty(clientApplication.ClientSecret) ||
            !clientApplication.ClientSecret.StartsWith("$2"))
        {
            return null;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(clientSecret, clientApplication.ClientSecret)
                ? clientApplication
                : null;
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return null;
        }
    }
}
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace iOSClub.WebAPI.Common.Security;

/// <summary>
/// OAuth 客户端密钥的可恢复存储。
/// <para>
/// 数据库里同时保存两份：BCrypt 哈希用于校验，Data Protection 密文用于
/// Founder/President 在管理页查看。明文绝不落库，也不进入任何列表 VO。
/// </para>
/// </summary>
public interface IClientSecretProtector
{
    string Protect(string secret);

    /// <summary>密文为空、格式错误或密钥已轮换时返回 null。</summary>
    string? Unprotect(string? cipher);
}

public sealed class ClientSecretProtector : IClientSecretProtector
{
    private readonly IDataProtector _protector;

    public ClientSecretProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("iOSClub.ClientApplication.ClientSecret.v1");

    public string Protect(string secret) => _protector.Protect(secret);

    public string? Unprotect(string? cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return null;

        try
        {
            return _protector.Unprotect(cipher);
        }
        catch (CryptographicException)
        {
            // 数据保护密钥被轮换或密文被篡改：视为不可恢复，由调用方提示重置密钥。
            return null;
        }
    }
}

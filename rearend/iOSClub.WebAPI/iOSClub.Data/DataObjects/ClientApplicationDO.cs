using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace iOSClub.Data.DataObjects;

/// <summary>
/// 第三方应用客户端模型
/// </summary>
[Table("ClientApplications")]
public class ClientApplicationDO
{
    /// <summary>
    /// 客户端ID（主键）
    /// </summary>
    [Key]
    [MaxLength(50)]
    public string ClientId { get; init; } = "";

    /// <summary>
    /// 客户端密钥的 BCrypt 哈希，仅用于校验，不可还原。
    /// </summary>
    [MaxLength(100)]
    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// 用 ASP.NET Data Protection 加密后的原始密钥，仅供 Founder/President 在管理页查看。
    /// 旧数据没有这一列，查看时会提示重置密钥。
    /// </summary>
    [MaxLength(1024)]
    public string? ClientSecretCipher { get; set; }

    /// <summary>
    /// 应用名称
    /// </summary>
    [MaxLength(100)]
    public string ApplicationName { get; set; } = "";

    /// <summary>
    /// 应用描述
    /// </summary>
    [MaxLength(500)]
    public string Description { get; set; } = "";

    /// <summary>
    /// 应用主页URL
    /// </summary>
    [MaxLength(200)]
    public string HomepageUrl { get; set; } = "";

    /// <summary>
    /// 回调URL白名单（多个URL用分号分隔）
    /// </summary>
    [MaxLength(1000)]
    public string RedirectUris { get; set; } = "";

    /// <summary>
    /// 应用图标URL
    /// </summary>
    [MaxLength(200)]
    public string LogoUrl { get; set; } = "";

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 是否支持PKCE
    /// </summary>
    public bool SupportsPkce { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 是否需要邮箱验证
    /// </summary>
    public bool IsNeedEMail { get; set; }

    /// <summary>
    /// 验证回调URL是否在白名单中
    /// </summary>
    /// <param name="redirectUri">要验证的回调URL</param>
    /// <returns>是否在白名单中</returns>
    public bool IsRedirectUriValid(string redirectUri)
    {
        if (string.IsNullOrEmpty(RedirectUris) || string.IsNullOrEmpty(redirectUri))
            return false;

        var uris = RedirectUris.Split(';', StringSplitOptions.RemoveEmptyEntries);
        return uris.Any(uri => string.Equals(uri.Trim(), redirectUri, StringComparison.OrdinalIgnoreCase));
    }
}

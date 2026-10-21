namespace iOSClub.Data.VOs;

/// <summary>
/// 客户端应用视图对象 - 含 ClientSecret
/// </summary>
public class ClientAppVO
{
    public string ClientId { get; set; } = "";

    /// <summary>
    /// 客户端密钥。数据库中以 BCrypt 哈希形式存储，这里返回的就是那串哈希，
    /// 不是创建/重置时签发的原始密钥。由于 ValidateCredentialsAsync 存在明文相等分支，
    /// 该哈希值本身可作为 client_secret 使用。
    /// </summary>
    public string ClientSecret { get; set; } = "";
    public string ApplicationName { get; set; } = "";
    public string Description { get; set; } = "";
    public string HomepageUrl { get; set; } = "";

    /// <summary>
    /// 回调地址列表。与 ClientAppResultVO.RedirectUris 保持同一类型，
    /// 避免同一个 JSON key（redirectUris）出现字符串/数组两种形状。
    /// </summary>
    public List<string> RedirectUris { get; set; } = [];
    public string LogoUrl { get; set; } = "";
    public bool IsActive { get; set; }
    public bool SupportsPkce { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsNeedEMail { get; set; }
}

namespace iOSClub.Data.VOs;

/// <summary>
/// 客户端应用视图对象。
/// <para>
/// 不包含 ClientSecret：数据库里存的是 BCrypt 哈希，任何"回显密钥"的读取接口
/// 都会把哈希本身变成可用的口令。原始密钥只在创建与重置时通过
/// <c>ClientAppResultVO</c> 返回一次。
/// </para>
/// </summary>
public class ClientAppVO
{
    public string ClientId { get; set; } = "";

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

    /// <summary>
    /// 是否保存了可恢复的加密密钥。旧应用只有 BCrypt 哈希时为 false，
    /// 前端据此提示"需重置密钥后才能查看"。
    /// </summary>
    public bool HasRecoverableSecret { get; set; }
}

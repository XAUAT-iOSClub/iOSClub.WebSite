namespace iOSClub.Data.VOs;

/// <summary>
/// OAuth 客户端密钥查看结果。仅 Founder/President 可调用对应接口，
/// 且该接口会进入操作审计。
/// </summary>
public class ClientSecretVO
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
}

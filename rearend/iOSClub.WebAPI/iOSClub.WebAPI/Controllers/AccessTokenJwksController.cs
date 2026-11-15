using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using iOSClub.WebAPI.Common.Security;

namespace iOSClub.WebAPI.Controllers;

/// <summary>
/// access token 的 JWKS 端点。
/// <para>
/// 与 <c>/SSO/jwks</c> 分开：那个端点公布的是 SSO 的 <c>rsa-key.xml</c>，服务的是 id_token；
/// 本端点公布 <see cref="RsaKeyManager"/> 的 PEM 公钥，服务的是 access token。
/// 两套密钥不同，混用会导致验签失败（见 docs/PRD-通用工作流引擎.md §6.2 与附录 E）。
/// </para>
/// </summary>
[ApiController]
[Route(".well-known/access-token")]
[AllowAnonymous]
public class AccessTokenJwksController(
    RsaKeyManager rsaKeyManager,
    ILogger<AccessTokenJwksController> logger) : ControllerBase
{
    /// <summary>
    /// 公布当前公钥；若刚发生过轮换，同时公布上一代公钥，覆盖旧 token 的剩余有效期。
    /// </summary>
    [HttpGet("jwks")]
    public IActionResult GetJwks()
    {
        var keys = new List<JwkKey>();

        // 注意：GetCurrentPublicKey 返回的是带缓存的对象，不能 Dispose，
        // 否则后续签发/验签会用到已释放的 RSA 实例。
        var current = rsaKeyManager.GetCurrentPublicKey();
        keys.Add(ToJwk(current, "RS256"));

        using (var previous = rsaKeyManager.TryLoadPreviousPublicKey())
        {
            if (previous is not null)
            {
                // 上一代公钥对应的 token 是用旧的 XML-DSig URI alg 签的。
                // 这里省略 alg 字段（而不是写 RS256），让严格按 alg 匹配公钥的客户端
                // （如 jose 会校验 JWK.alg）在切换窗口内仍能选中这把 key。
                var previousJwk = ToJwk(previous, alg: null);
                // 备份文件若恰好就是当前密钥（重算同一把），不重复公布
                if (keys.All(key => key.Kid != previousJwk.Kid))
                {
                    keys.Add(previousJwk);
                }
            }
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("公布 access token JWKS，密钥数量：{Count}", keys.Count);
        }

        return Ok(new JwksDocument { Keys = keys });
    }

    private static JwkKey ToJwk(RSA rsa, string? alg)
    {
        var parameters = rsa.ExportParameters(false);
        if (parameters.Modulus is null || parameters.Exponent is null)
        {
            throw new InvalidOperationException("RSA 公钥参数不完整，无法生成 JWK");
        }

        return new JwkKey
        {
            Kty = "RSA",
            Alg = alg,
            Use = "sig",
            Kid = RsaKeyManager.ComputeKeyId(rsa),
            N = Base64Url(parameters.Modulus),
            E = Base64Url(parameters.Exponent)
        };
    }

    /// <summary>JWK 要求 base64url 无填充。</summary>
    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>JWKS 文档。属性名按 ASP.NET 默认 camelCase 序列化为 e/kid/n/use/alg/kty。</summary>
public class JwksDocument
{
    public List<JwkKey> Keys { get; set; } = [];
}

/// <summary>单个 RSA 公钥的 JWK 表示。</summary>
public class JwkKey
{
    public string Kty { get; set; } = "RSA";

    /// <summary>
    /// 签名算法。上一代公钥为 null（旧 token 用 XML-DSig URI alg 签发），序列化时省略该字段，
    /// 让按 alg 严格匹配的客户端在切换窗口内也能选中它。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Alg { get; set; }

    public string Use { get; set; } = "sig";

    public string Kid { get; set; } = "";

    /// <summary>模数（base64url）</summary>
    public string N { get; set; } = "";

    /// <summary>指数（base64url）</summary>
    public string E { get; set; } = "";
}

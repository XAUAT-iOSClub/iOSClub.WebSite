using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using iOSClub.Data.VOs;
using iOSClub.WebAPI.Common.Config;
using iOSClub.WebAPI.Common.Security;
using iOSClub.WebAPI.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace iOSClub.Tests.SecurityTests;

/// <summary>
/// access token 的 JWKS 端点（B1）与标准 RS256 签名（B2）的回归测试。
/// 对应 docs/PRD-通用工作流引擎.md §6.2 的方案 B。
/// </summary>
public class AccessTokenJwksTests
{
    private readonly JwtConfig _jwtConfig;
    private readonly RsaKeyManager _rsaKeyManager;
    private readonly JwtService _jwtService;
    private readonly AccessTokenJwksController _controller;
    private readonly string _testDirectory;

    public AccessTokenJwksTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "iOSClubTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        _jwtConfig = new JwtConfig
        {
            RsaPrivateKeyPath = Path.Combine(_testDirectory, "test_private_key.pem"),
            RsaPublicKeyPath = Path.Combine(_testDirectory, "test_public_key.pem"),
            KeyRotationDays = 90,
            AccessTokenExpiryMinutes = 60,
            Issuer = "test-issuer",
            Audience = "test-audience"
        };

        _rsaKeyManager = new RsaKeyManager(_jwtConfig, NullLogger<RsaKeyManager>.Instance);
        _jwtService = new JwtService(_jwtConfig, _rsaKeyManager, NullLogger<JwtService>.Instance);
        _controller = new AccessTokenJwksController(_rsaKeyManager, NullLogger<AccessTokenJwksController>.Instance);
    }

    private static MemberVO NewMember() => new()
    {
        UserId = "20240001",
        UserName = "张三",
        Identity = "President"
    };

    private JwksDocument GetJwks()
    {
        var result = _controller.GetJwks();
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<JwksDocument>(ok.Value);
    }

    private static JwtSecurityToken ReadToken(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    /// <summary>按 RFC 7518 的要求反解 base64url（测试侧独立实现，不复用被测代码）。</summary>
    private static byte[] FromBase64Url(string value) => Base64UrlEncoder.DecodeBytes(value);

    /// <summary>kid 的规格：公钥 PKCS#1 DER 的 SHA256，Base64 前 16 位。测试侧独立计算。</summary>
    private static string ExpectedKid(RSA rsa) =>
        Convert.ToBase64String(SHA256.HashData(rsa.ExportRSAPublicKey()))[..16];

    /// <summary>B2：签发的 token 必须使用标准 RS256，而不是 XML-DSig URI。</summary>
    [Fact]
    public void GenerateAccessToken_UsesStandardRs256Alg()
    {
        var (accessToken, _) = _jwtService.GenerateTokens(NewMember());

        var header = ReadToken(accessToken).Header;

        Assert.Equal("RS256", header.Alg);
        Assert.False(string.IsNullOrWhiteSpace(header.Kid));
    }

    /// <summary>B1：JWKS 的 kid 必须与 token header 的 kid 一致，否则客户端按 kid 取不到钥匙。</summary>
    [Fact]
    public void Jwks_ShouldMatchSigningKeyAndKid()
    {
        var (accessToken, _) = _jwtService.GenerateTokens(NewMember());
        var tokenHeader = ReadToken(accessToken).Header;

        var jwk = Assert.Single(GetJwks().Keys);

        Assert.Equal("RSA", jwk.Kty);
        Assert.Equal("RS256", jwk.Alg);
        Assert.Equal("sig", jwk.Use);
        Assert.Equal(tokenHeader.Kid, jwk.Kid);
        Assert.Equal(ExpectedKid(_rsaKeyManager.GetCurrentPublicKey()), jwk.Kid);
    }

    /// <summary>B1：把 n/e 解回 RSAParameters 后必须与签名公钥完全一致，且不带填充和 +/。</summary>
    [Fact]
    public void Jwks_ShouldExposeCurrentPublicKeyMaterial()
    {
        var expected = _rsaKeyManager.GetCurrentPublicKey().ExportParameters(false);

        var jwk = Assert.Single(GetJwks().Keys);

        Assert.Equal(expected.Modulus, FromBase64Url(jwk.N));
        Assert.Equal(expected.Exponent, FromBase64Url(jwk.E));
        Assert.DoesNotContain('=', jwk.N);
        Assert.DoesNotContain('+', jwk.N);
        Assert.DoesNotContain('/', jwk.N);
        Assert.DoesNotContain('=', jwk.E);
    }

    /// <summary>没有发生轮换时，JWKS 只应公布当前公钥。</summary>
    [Fact]
    public void Jwks_ShouldHaveSingleKeyBeforeRotation()
    {
        Assert.Single(GetJwks().Keys);
    }

    /// <summary>
    /// B1：轮换后 JWKS 同时公布新公钥与上一代公钥；
    /// 上一代 JWK 不得写死 alg（旧 token 用的是 XML-DSig URI），否则严格匹配 alg 的客户端选不中它。
    /// </summary>
    [Fact]
    public void Jwks_ShouldIncludePreviousKeyAfterRotation()
    {
        var (oldToken, _) = _jwtService.GenerateTokens(NewMember());
        var oldKid = ReadToken(oldToken).Header.Kid;
        var oldParams = _rsaKeyManager.GetCurrentPublicKey().ExportParameters(false);

        _rsaKeyManager.RotateKeys();

        var jwks = GetJwks();
        var currentKid = ExpectedKid(_rsaKeyManager.GetCurrentPublicKey());

        Assert.Equal(2, jwks.Keys.Count);
        Assert.Contains(jwks.Keys, key => key.Kid == currentKid);

        var previous = Assert.Single(jwks.Keys, key => key.Kid == oldKid);
        Assert.Null(previous.Alg);
        Assert.Equal(oldParams.Modulus, FromBase64Url(previous.N));
        Assert.Equal(oldParams.Exponent, FromBase64Url(previous.E));
    }

    /// <summary>
    /// 轮换窗口：轮换后旧 token（旧私钥签发）在服务端也必须还能通过校验，
    /// 否则轮换瞬间所有在途会话会被自家服务判成 401。
    /// </summary>
    [Fact]
    public void ValidateAccessToken_ShouldAcceptTokenSignedByPreviousKeyAfterRotation()
    {
        var (oldToken, _) = _jwtService.GenerateTokens(NewMember());

        _rsaKeyManager.RotateKeys();

        var (isValid, _) = _jwtService.ValidateAccessToken(oldToken);

        Assert.True(isValid, "轮换后旧私钥签发的 token 仍必须通过校验");
    }

    /// <summary>
    /// 灰度窗口：切换前签发的 token 的 alg 仍是 XML-DSig URI，
    /// 服务端必须还能验证，否则 B2 上线瞬间所有在途 token 全部失效。
    /// </summary>
    [Fact]
    public void ValidateAccessToken_ShouldStillAcceptLegacyUriAlgToken()
    {
        var rsa = _rsaKeyManager.GetCurrentPrivateKey();
        var now = DateTime.UtcNow;

        var legacyToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: _jwtConfig.Issuer,
            audience: _jwtConfig.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, "20240001"),
                new Claim(ClaimTypes.Role, "President"),
                new Claim("usage", "access")
            ],
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new RsaSecurityKey(rsa) { KeyId = ExpectedKid(rsa) },
                SecurityAlgorithms.RsaSha256Signature)));

        var (isValid, claims) = _jwtService.ValidateAccessToken(legacyToken);

        Assert.True(isValid, "切换窗口内旧 alg 的 token 仍必须通过校验");
        Assert.Contains(claims, claim => claim.Type == "usage" && claim.Value == "access");
    }

    /// <summary>已有的拒绝路径不能被这次改动带坏。</summary>
    [Fact]
    public void ValidateAccessToken_ShouldRejectNonAccessUsage()
    {
        var rsa = _rsaKeyManager.GetCurrentPrivateKey();
        var now = DateTime.UtcNow;

        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: _jwtConfig.Issuer,
            audience: _jwtConfig.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, "20240001"),
                new Claim("usage", "refresh")
            ],
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new RsaSecurityKey(rsa) { KeyId = ExpectedKid(rsa) },
                SecurityAlgorithms.RsaSha256)));

        var (isValid, _) = _jwtService.ValidateAccessToken(token);

        Assert.False(isValid);
    }

    /// <summary>B1：路由必须真的挂在 <c>/.well-known/access-token/jwks</c> 上。</summary>
    [Fact]
    public async Task JwksEndpoint_ShouldServeAtWellKnownPath()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(AccessTokenJwksController).Assembly);
                services.AddSingleton(_jwtConfig);
                services.AddSingleton(_rsaKeyManager);
                services.AddLogging();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        using var server = new TestServer(builder);
        using var client = server.CreateClient();

        var response = await client.GetAsync("/.well-known/access-token/jwks");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var keys = document.RootElement.GetProperty("keys");
        Assert.Equal(1, keys.GetArrayLength());

        var key = keys[0];
        Assert.Equal("RSA", key.GetProperty("kty").GetString());
        Assert.Equal("RS256", key.GetProperty("alg").GetString());
        Assert.Equal("sig", key.GetProperty("use").GetString());
        Assert.False(string.IsNullOrEmpty(key.GetProperty("kid").GetString()));
        Assert.DoesNotContain('=', key.GetProperty("n").GetString()!);
        Assert.False(key.TryGetProperty("d", out _), "JWKS 不得泄露私钥参数");
    }
}

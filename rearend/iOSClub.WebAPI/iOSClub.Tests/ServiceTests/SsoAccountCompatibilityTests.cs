using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text.Json;
using iOSClub.Data.DataObjects;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common.Config;
using iOSClub.WebAPI.Common.Security;
using iOSClub.WebAPI.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using StackExchange.Redis;

namespace iOSClub.Tests.ServiceTests;

/// <summary>
/// 账号模型一致性（C18）：产品已决定不再支持只有 Staff 档案的账号。
/// 主站 /User/data、SSO 授权码交换与 userinfo 都必须要求 Student 档案。
/// </summary>
public class SsoAccountCompatibilityTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"SsoAccount-{Guid.NewGuid()}");
    private readonly Mock<IConnectionMultiplexer> _redis = new();
    private readonly Mock<IDatabase> _database = new();
    private readonly Mock<ILoginService> _loginService = new();
    private readonly Mock<IClientApplicationRepository> _clientRepository = new();
    private readonly Mock<IStudentRepository> _studentRepository = new();
    private readonly Mock<IStaffRepository> _staffRepository = new();
    private readonly JwtService _jwtService;

    public SsoAccountCompatibilityTests()
    {
        Directory.CreateDirectory(_directory);
        var config = new JwtConfig
        {
            RsaPrivateKeyPath = Path.Combine(_directory, "private.pem"),
            RsaPublicKeyPath = Path.Combine(_directory, "public.pem"),
            Issuer = "sso-account-tests",
            Audience = "sso-account-tests"
        };
        var keys = new RsaKeyManager(config, NullLogger<RsaKeyManager>.Instance);
        keys.EnsureKeysValid();
        _jwtService = new JwtService(config, keys, NullLogger<JwtService>.Instance);

        _redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_database.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private SSOController CreateController(HttpContext httpContext) =>
        new(_loginService.Object, _studentRepository.Object, _staffRepository.Object,
            _clientRepository.Object, _redis.Object, new ConfigurationBuilder().Build(),
            NullLogger<SSOController>.Instance, _jwtService)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

    private static DefaultHttpContext FormContext(Dictionary<string, string> form)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Features.Set<IFormFeature>(new FormFeature(new FormCollection(
            form.ToDictionary(pair => pair.Key, pair => new StringValues(pair.Value)))));
        return context;
    }

    [Fact]
    public async Task Token_AuthorizationCodeForStaffOnlyAccount_IsRejected()
    {
        var authCode = new SSOController.AuthCodeInfo
        {
            ClientId = "demo",
            RedirectUri = "https://app.example/cb",
            UserId = "0000000000",
            Scope = ""
        };
        _database.Setup(d => d.StringGetAsync(It.Is<RedisKey>(key => key == "oauth:code:code1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(JsonSerializer.Serialize(authCode));

        var client = new ClientApplicationDO
        {
            ClientId = "demo",
            ClientSecret = "secret",
            ApplicationName = "Demo",
            RedirectUris = "https://app.example/cb"
        };
        _clientRepository.Setup(r => r.ValidateCredentialsAsync("demo", "secret")).ReturnsAsync(client);

        _studentRepository.Setup(r => r.GetByIdAsync("0000000000")).ReturnsAsync((StudentDO?)null);
        _staffRepository.Setup(r => r.GetStaffByIdWithoutOtherData("0000000000"))
            .ReturnsAsync(new StaffDO { UserId = "0000000000", Name = "创始人", Identity = "Founder" });
        _loginService.Setup(s => s.GetToken("0000000000", "demo")).ReturnsAsync("exists-access-token");
        _loginService.Setup(s => s.GetRefreshToken("0000000000", "demo")).ReturnsAsync("exists-refresh-token");

        var result = await CreateController(FormContext(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = "code1",
            ["client_id"] = "demo",
            ["client_secret"] = "secret",
            ["redirect_uri"] = "https://app.example/cb"
        })).Token();

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var payload = JsonSerializer.Serialize(badRequest.Value);
        using var document = JsonDocument.Parse(payload);
        Assert.Equal("invalid_grant", document.RootElement.GetProperty("error").GetString());
        Assert.Equal("用户不存在", document.RootElement.GetProperty("error_description").GetString());
    }

    [Fact]
    public async Task UserInfo_ForStaffOnlyAccount_IsUnauthorized()
    {
        var (token, _) = _jwtService.GenerateTokens(new MemberVO
        {
            UserId = "0000000000", UserName = "创始人", Identity = "Founder"
        });

        _loginService.Setup(s => s.ValidateToken("0000000000", token, It.IsAny<string>()))
            .ReturnsAsync(true);
        _studentRepository.Setup(r => r.GetByIdAsync("0000000000")).ReturnsAsync((StudentDO?)null);
        _staffRepository.Setup(r => r.GetStaffByIdWithoutOtherData("0000000000"))
            .ReturnsAsync(new StaffDO { UserId = "0000000000", Name = "创始人", Identity = "Founder" });

        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token).ToString();

        var result = await CreateController(context).UserInfo();

        Assert.IsType<UnauthorizedResult>(result);
    }
}

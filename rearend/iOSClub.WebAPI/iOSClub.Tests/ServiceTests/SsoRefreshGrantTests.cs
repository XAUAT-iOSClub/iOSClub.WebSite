using System.Text;
using System.Text.Json;
using iOSClub.Data.DataObjects;
using iOSClub.DataApi.Repositories;
using iOSClub.DataApi.Services;
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
/// C22：/SSO/token 按 RFC 6749 支持 grant_type=refresh_token。
/// 刷新请求只携带刷新令牌与客户端凭据，服务端通过 LoginService 的
/// refresh-owner 反向索引定位 userId，再走既有的刷新令牌校验与轮换。
/// </summary>
public class SsoRefreshGrantTests
{
    private const string UserId = "2024000001";
    private const string ClientId = "demo";
    private const string ClientSecret = "secret";
    private const string DefaultScore = "profile openid role";

    private readonly Mock<ILoginService> _loginService = new();
    private readonly Mock<IClientApplicationRepository> _clientRepository = new();
    private readonly Mock<IDatabase> _database = new();
    private readonly SSOController _controller;

    public SsoRefreshGrantTests()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_database.Object);

        _clientRepository.Setup(r => r.ValidateCredentialsAsync(ClientId, ClientSecret))
            .ReturnsAsync(new ClientApplicationDO
            {
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                ApplicationName = "Demo",
                RedirectUris = "https://app.example/cb"
            });

        _controller = new SSOController(
            _loginService.Object,
            Mock.Of<IStudentRepository>(),
            Mock.Of<IStaffRepository>(),
            _clientRepository.Object,
            redis.Object,
            new ConfigurationBuilder().Build(),
            NullLogger<SSOController>.Instance,
            null!);
    }

    private static DefaultHttpContext FormContext(Dictionary<string, string> form)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Features.Set<IFormFeature>(new FormFeature(new FormCollection(
            form.ToDictionary(pair => pair.Key, pair => new StringValues(pair.Value)))));
        return context;
    }

    private static DefaultHttpContext JsonContext(string body)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return context;
    }

    private async Task<IActionResult> PostAsync(DefaultHttpContext context)
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = context };
        return await _controller.Token();
    }

    private static Dictionary<string, string> RefreshForm(string? refreshToken = "valid-refresh-token") => new()
    {
        ["grant_type"] = "refresh_token",
        ["refresh_token"] = refreshToken ?? "",
        ["client_id"] = ClientId,
        ["client_secret"] = ClientSecret
    };

    [Fact]
    public async Task Token_RefreshGrant_MissingClientId_IsRejectedEvenWithRedirectUri()
    {
        // 回归：以前 client_id 缺失时会用 redirect_uri 反查应用并回填 client_secret，
        // 等于任何人只要知道回调地址就能通过客户端认证。
        var form = RefreshForm();
        form.Remove("client_id");
        form["redirect_uri"] = "https://app.example/cb";

        var result = await PostAsync(FormContext(form));

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("client_id", JsonSerializer.Serialize(bad.Value));
        _loginService.Verify(
            s => s.RefreshToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Token_AuthorizationCode_MissingClientId_IsRejectedEvenWithRedirectUri()
    {
        var result = await PostAsync(FormContext(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = "any-code",
            ["redirect_uri"] = "https://app.example/cb",
            ["client_secret"] = ClientSecret
        }));

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("client_id", JsonSerializer.Serialize(bad.Value));
    }

    [Fact]
    public async Task Token_RefreshGrant_ReturnsRotatedTokenPair()
    {
        _loginService.Setup(s => s.GetUserIdByRefreshToken("valid-refresh-token")).ReturnsAsync(UserId);
        _loginService.Setup(s => s.RefreshToken(UserId, "valid-refresh-token", ClientId, It.IsAny<string>()))
            .ReturnsAsync("new-access-token");
        _loginService.Setup(s => s.GetRefreshToken(UserId, ClientId)).ReturnsAsync("new-refresh-token");

        var result = await PostAsync(FormContext(RefreshForm()));

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = JsonSerializer.Serialize(ok.Value);
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal("new-access-token", root.GetProperty("access_token").GetString());
        Assert.Equal("new-refresh-token", root.GetProperty("refresh_token").GetString());
        Assert.Equal("Bearer", root.GetProperty("token_type").GetString());
        Assert.Equal(7200, root.GetProperty("expires_in").GetInt32());
        Assert.Equal(DefaultScore, root.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task Token_RefreshGrant_JsonSnakeCaseBody_IsAccepted()
    {
        _loginService.Setup(s => s.GetUserIdByRefreshToken("valid-refresh-token")).ReturnsAsync(UserId);
        _loginService.Setup(s => s.RefreshToken(UserId, "valid-refresh-token", ClientId, It.IsAny<string>()))
            .ReturnsAsync("new-access-token");
        _loginService.Setup(s => s.GetRefreshToken(UserId, ClientId)).ReturnsAsync("new-refresh-token");

        var result = await PostAsync(JsonContext(
            """{"grant_type":"refresh_token","refresh_token":"valid-refresh-token","client_id":"demo","client_secret":"secret"}"""));

        var ok = Assert.IsType<OkObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.Equal("new-access-token", document.RootElement.GetProperty("access_token").GetString());
    }

    [Fact]
    public async Task Token_RefreshGrant_UnknownToken_ReturnsInvalidGrant()
    {
        // 反向索引查不到 userId：令牌不存在或已被撤销。
        _loginService.Setup(s => s.GetUserIdByRefreshToken(It.IsAny<string>())).ReturnsAsync("");

        var result = await PostAsync(FormContext(RefreshForm("unknown-refresh-token")));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value));
        Assert.Equal("invalid_grant", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Token_RefreshGrant_ClientMismatch_ReturnsInvalidGrant()
    {
        // 反向索引能定位 userId，但令牌不属于该客户端：LoginService 校验失败。
        _loginService.Setup(s => s.GetUserIdByRefreshToken("valid-refresh-token")).ReturnsAsync(UserId);
        _loginService.Setup(s => s.RefreshToken(UserId, "valid-refresh-token", ClientId, It.IsAny<string>()))
            .ReturnsAsync("");

        var result = await PostAsync(FormContext(RefreshForm()));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value));
        Assert.Equal("invalid_grant", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Token_RefreshGrant_MissingRefreshToken_ReturnsInvalidRequest()
    {
        var result = await PostAsync(FormContext(RefreshForm(null)));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value));
        Assert.Equal("invalid_request", document.RootElement.GetProperty("error").GetString());
        Assert.Contains("refresh_token", document.RootElement.GetProperty("error_description").GetString());
    }

    [Fact]
    public async Task Token_RefreshGrant_InvalidClientCredentials_ReturnsInvalidClient()
    {
        _clientRepository.Setup(r => r.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((ClientApplicationDO?)null);

        var result = await PostAsync(FormContext(RefreshForm()));

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(unauthorized.Value));
        Assert.Equal("invalid_client", document.RootElement.GetProperty("error").GetString());
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.Common.Config;
using iOSClub.WebAPI.Common.Security;
using iOSClub.WebAPI.Controllers;
using iOSClub.WebAPI.IdentityModels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using StackExchange.Redis;

namespace iOSClub.Tests.IntegrationTests;

public sealed class SessionAuthorizationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"SessionAuthorization-{Guid.NewGuid()}");
    private readonly Dictionary<string, RedisValue> _cache = [];
    private readonly Mock<IDatabase> _database = new();
    private readonly LoginService _loginService;
    private readonly JwtService _jwt;
    private readonly TestServer _server;
    private readonly HttpClient _client;

    public SessionAuthorizationTests()
    {
        var config = new JwtConfig
        {
            RsaPrivateKeyPath = Path.Combine(_directory, "private.pem"),
            RsaPublicKeyPath = Path.Combine(_directory, "public.pem"),
            Issuer = "session-tests",
            Audience = "session-tests"
        };
        var keys = new RsaKeyManager(config, NullLogger<RsaKeyManager>.Instance);
        _jwt = new JwtService(config, keys, NullLogger<JwtService>.Instance);
        keys.EnsureKeysValid();

        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_database.Object);
        _database.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) => _cache.GetValueOrDefault(key.ToString(), RedisValue.Null));
        _database.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey[] names, CommandFlags _) => names.LongCount(key => _cache.Remove(key.ToString())));
        var batch = new Mock<IBatch>();
        batch.Setup(b => b.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .Callback((RedisKey key, RedisValue value, Expiration _, ValueCondition _, CommandFlags _) => _cache[key.ToString()] = value)
            .ReturnsAsync(true);
        _database.Setup(d => d.CreateBatch(It.IsAny<object>())).Returns(batch.Object);

        var redisServer = new Mock<IServer>();
        redis.Setup(r => r.GetEndPoints(It.IsAny<bool>())).Returns([new IPEndPoint(IPAddress.Loopback, 6379)]);
        redis.Setup(r => r.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>())).Returns(redisServer.Object);
        redisServer.Setup(s => s.Keys(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(),
                It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(() => _cache.Keys.Select(key => (RedisKey)key).ToArray());

        _loginService = new LoginService(Mock.Of<IStudentRepository>(), Mock.Of<IStaffRepository>(),
            Mock.Of<ITokenGenerator>(), redis.Object, Mock.Of<IEmailService>(),
            Mock.Of<IClientApplicationRepository>(), NullLogger<LoginService>.Instance);

        var factory = new TestDbContextFactory(new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"SessionArticles-{Guid.NewGuid()}").Options);
        using (var context = factory.CreateDbContext())
        {
            context.Articles.Add(new ArticleDO
            {
                Path = "founder-only", Title = "创始人受限文章", Content = "受限正文", Identity = "Founder"
            });
            context.SaveChanges();
        }

        _server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ILoginService>(_loginService);
                services.AddSingleton(_jwt);
                services.AddSingleton(redis.Object);
                services.AddSingleton<IArticleRepository>(new ArticleRepository(factory, new CategoryRepository(factory)));
                // C25 起 ArticleController 需要 ICategoryRepository 才能激活；这里补齐手工宿主的最小注册。
                services.AddSingleton<ICategoryRepository>(new CategoryRepository(factory));
                services.AddSingleton<IStaffRepository>(new StaffRepository(factory));
                services.AddSingleton<IDepartmentRepository>(new DepartmentRepository(factory));
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options => options.TokenValidationParameters = _jwt.GetDefaultValidationParameters());
                services.AddAuthorization();
                services.AddControllers(options =>
                    {
                        options.Filters.Add<GlobalAuthorizationFilter>();
                        options.Filters.Add<ApiResponseResultFilter>();
                    })
                    .AddApplicationPart(typeof(CacheController).Assembly)
                    .AddApplicationPart(typeof(SessionProbeController).Assembly);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            }));
        _client = _server.CreateClient();
    }

    private async Task<string> SignInAsync(string identity = "Member")
    {
        var (token, refresh) = _jwt.GenerateTokens(new MemberVO
        {
            UserId = "2024000001", UserName = "测试成员", Identity = identity
        });
        await _loginService.StoreMemberToken("2024000001", token, refresh);
        Assert.True(await _loginService.ValidateToken("2024000001", token));
        Assert.True(_jwt.ValidateAccessToken(token).IsValid);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return token;
    }

    [Fact]
    public async Task ProtectedRequest_AfterLogout_RejectsSignedUnexpiredToken()
    {
        await SignInAsync();
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/session-probe")).StatusCode);

        Assert.True(await _loginService.Logout("2024000001"));

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/session-probe")).StatusCode);
    }

    [Fact]
    public async Task ProtectedRequest_AfterSessionReplacement_RejectsOldToken()
    {
        await SignInAsync();
        var (replacement, refresh) = _jwt.GenerateTokens(new MemberVO
        {
            UserId = "2024000001", UserName = "测试成员", Identity = "Member"
        });
        await _loginService.StoreMemberToken("2024000001", replacement, refresh);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/session-probe")).StatusCode);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", replacement);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/session-probe")).StatusCode);
    }

    [Fact]
    public async Task ProtectedRequest_WhenSessionStoreUnavailable_DoesNotRunAction()
    {
        await SignInAsync();
        _database.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "test outage"));

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/session-probe")).StatusCode);
    }

    [Fact]
    public async Task AnonymousRequest_ToPublicAction_RemainsAccessible()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/session-probe/public")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/session-probe")).StatusCode);
    }

    [Fact]
    public async Task RestrictedArticle_AfterLogout_DoesNotUseRevokedFounderIdentity()
    {
        await SignInAsync("Founder");
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/Article/founder-only")).StatusCode);

        Assert.True(await _loginService.Logout("2024000001"));

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/Article/founder-only")).StatusCode);
    }

    [Fact]
    public async Task AllowAnonymousAction_AfterLogout_DoesNotUseRevokedIdentity()
    {
        await SignInAsync("Founder");
        Assert.True(await _loginService.Logout("2024000001"));

        var response = await _client.GetAsync("/session-probe/public");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(payload.GetProperty("authenticated").GetBoolean());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LegacyCacheGet_DoesNotClearSessions(bool anonymous)
    {
        var token = await SignInAsync("Founder");
        if (anonymous) _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/clean");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.True(await _loginService.ValidateToken("2024000001", token));
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Member", HttpStatusCode.Forbidden)]
    [InlineData("Department", HttpStatusCode.Forbidden)]
    [InlineData("Minister", HttpStatusCode.Forbidden)]
    [InlineData("President", HttpStatusCode.Forbidden)]
    public async Task CacheCleanup_RejectsUnauthorizedCallers_WithoutClearingSessions(string? identity, HttpStatusCode expected)
    {
        var token = await SignInAsync(identity ?? "Founder");
        if (identity == null) _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsync("/clean", null);

        Assert.Equal(expected, response.StatusCode);
        Assert.True(await _loginService.ValidateToken("2024000001", token));
    }

    [Fact]
    public async Task CacheCleanup_ActiveFounderCanPost_AndRevokedFounderCannot()
    {
        var token = await SignInAsync("Founder");
        _cache["unrelated:keep"] = "keep";

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync("/clean", null)).StatusCode);
        Assert.False(await _loginService.ValidateToken("2024000001", token));
        Assert.Equal("keep", _cache["unrelated:keep"].ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsync("/clean", null)).StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
        Directory.Delete(_directory, true);
    }
}

[ApiController]
[Authorize]
[Route("session-probe")]
public sealed class SessionProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { userId = User.GetUser()?.UserId });

    [AllowAnonymous]
    [HttpGet("public")]
    public IActionResult Public() => Ok(new { authenticated = User.Identity?.IsAuthenticated == true });
}

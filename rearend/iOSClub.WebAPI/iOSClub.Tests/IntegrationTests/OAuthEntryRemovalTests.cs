using System.Net;
using iOSClub.Data;
using iOSClub.DataApi.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;

namespace iOSClub.Tests.IntegrationTests;

/// <summary>
/// C21 回归测试：移除不可用的 OAuth2 Cookie 入口。
/// <para>
/// 回归背景：OAuth2 以 Cookie 方案注册，Challenge 指向该方案自身的 LoginPath，
/// 形成 /OAuth/login → /OAuth/login 的循环；没有注册任何外部 OAuth 处理器，
/// callback 无法建立身份；logout 又使用未注册的 Cookies 方案。
/// 决策：移除整个 /OAuth 入口，第三方登录统一走 /SSO。这里锁定移除结果，
/// 同时确认 SSO 的发现端点仍然可用，避免误删 SSO。
/// </para>
/// </summary>
public class OAuthEntryRemovalTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public OAuthEntryRemovalTests(WebApplicationFactory<Program> factory)
    {
        var options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"OAuthEntryRemoval-{Guid.NewGuid()}")
            .Options;
        var database = Mock.Of<IDatabase>();
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database);
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing").ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(IDbContextFactory<ClubContext>));
            services.Remove(descriptor);
            services.AddSingleton<IDbContextFactory<ClubContext>>(new TestDbContextFactory(options));
            services.AddSingleton(redis.Object);
            services.AddSingleton(Mock.Of<IEmailService>());
            services.AddAuthentication(authentication =>
            {
                authentication.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                authentication.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        }));
        _client = _factory.CreateClient();
    }

    [Theory]
    [InlineData("/OAuth/login")]
    [InlineData("/OAuth/exchange")]
    [InlineData("/OAuth/callback")]
    [InlineData("/OAuth/logout")]
    [InlineData("/OAuth/access-denied")]
    public async Task RemovedOAuthEntry_IsNotRouted(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-Anonymous", "true");

        var response = await _client.SendAsync(request);

        // 修复前 /OAuth/login 返回 200（OAuth2 未配置的业务失败体），logout 返回 401；
        // 移除后整个入口不应再由任何控制器应答。
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SsoDiscoveryEndpoint_StillWorks()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/openid-configuration");
        request.Headers.Add("X-Test-Anonymous", "true");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

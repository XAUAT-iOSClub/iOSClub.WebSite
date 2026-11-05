using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.Common.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;

namespace iOSClub.Tests.IntegrationTests;

/// <summary>
/// 社长 / Founder 查看 OAuth 密钥的权限与审计。
/// <para>
/// 密钥仍然只以密文形式保存；Minister 可以管理应用但看不到原始密钥。
/// </para>
/// </summary>
public class ClientSecretRevealTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string ClientId = "demo";
    private const string PlainSecret = "plain-secret";

    private readonly DbContextOptions<ClubContext> _options;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ClientSecretRevealTests(WebApplicationFactory<Program> factory)
    {
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"ClientSecretReveal-{Guid.NewGuid()}")
            .Options;

        var redisDb = new Mock<IDatabase>();
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(redisDb.Object);

        // 用确定性的假实现替代 Data Protection，避免测试依赖密钥环。
        var protector = new Mock<IClientSecretProtector>();
        protector.Setup(p => p.Unprotect(It.IsAny<string?>()))
            .Returns((string? cipher) =>
                cipher is not null && cipher.StartsWith("enc:") ? cipher[4..] : null);

        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing").ConfigureServices(services =>
        {
            var dbFactory = services.Single(d => d.ServiceType == typeof(IDbContextFactory<ClubContext>));
            services.Remove(dbFactory);
            services.AddSingleton<IDbContextFactory<ClubContext>>(new TestDbContextFactory(_options));

            var existingProtector = services.SingleOrDefault(d => d.ServiceType == typeof(IClientSecretProtector));
            if (existingProtector != null) services.Remove(existingProtector);
            services.AddSingleton(protector.Object);

            services.AddSingleton(redis.Object);
            services.AddSingleton(Mock.Of<IEmailService>());

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        }));

        _client = _factory.CreateClient();
    }

    private void SignInAs(string identity, string userId = "2023000001")
    {
        _client.DefaultRequestHeaders.Remove("X-Test-Identity");
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-Identity", identity);
        _client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
    }

    private async Task SeedAsync(string? cipher = "enc:" + PlainSecret)
    {
        await using var context = new ClubContext(_options);
        context.ClientApplications.Add(new ClientApplicationDO
        {
            ClientId = ClientId,
            ClientSecret = BCrypt.Net.BCrypt.HashPassword(PlainSecret),
            ClientSecretCipher = cipher,
            ApplicationName = "Demo",
            RedirectUris = "https://app.example/cb",
            IsActive = true
        });
        await context.SaveChangesAsync();
    }

    [Theory]
    [InlineData("Founder")]
    [InlineData("President")]
    public async Task FounderOrPresident_CanRevealSecret(string role)
    {
        await SeedAsync();
        SignInAs(role);

        var response = await _client.GetAsync($"/ClientApp/{ClientId}/secret");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(PlainSecret, envelope.GetProperty("data").GetProperty("clientSecret").GetString());
    }

    [Fact]
    public async Task Minister_CannotRevealSecret()
    {
        await SeedAsync();
        SignInAs("Minister");

        var response = await _client.GetAsync($"/ClientApp/{ClientId}/secret");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotRevealSecret()
    {
        await SeedAsync();
        _client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");

        var response = await _client.GetAsync($"/ClientApp/{ClientId}/secret");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LegacyClientWithoutCipher_AsksForReset()
    {
        await SeedAsync(cipher: null);
        SignInAs("Founder");

        var response = await _client.GetAsync($"/ClientApp/{ClientId}/secret");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ErrorCode.InvalidStatusForOperation, envelope.GetProperty("errorCode").GetInt32());
    }

    [Fact]
    public async Task RevealSecret_IsWrittenToOperationLog()
    {
        await SeedAsync();
        SignInAs("Founder", "0000000000");

        var response = await _client.GetAsync($"/ClientApp/{ClientId}/secret");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = new ClubContext(_options);
        var log = await context.OperationLogs.SingleAsync();
        Assert.Equal("查看 OAuth 应用密钥", log.Action);
        Assert.Contains(ClientId, log.Target);
        Assert.Equal("0000000000", log.OperatorId);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

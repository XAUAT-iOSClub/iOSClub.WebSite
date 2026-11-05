using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
/// 操作日志读取权限：仅 Founder / President；匿名 401，其他角色 403。
/// 另外验证被拒绝的写请求不会产生审计行。
/// </summary>
public class OperationLogsAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public OperationLogsAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        var options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"OperationLogsAuthorization-{Guid.NewGuid()}")
            .Options;

        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(Mock.Of<IDatabase>());

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

    private void SetIdentity(string role, string userId = "2023000001")
    {
        _client.DefaultRequestHeaders.Remove("X-Test-Anonymous");
        _client.DefaultRequestHeaders.Remove("X-Test-Identity");
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-Identity", role);
        _client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
    }

    private void SetAnonymous()
    {
        _client.DefaultRequestHeaders.Remove("X-Test-Identity");
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
    }

    [Fact]
    public async Task Anonymous_CannotRead()
    {
        SetAnonymous();

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/OperationLogs")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/OperationLogs/options")).StatusCode);
    }

    [Theory]
    [InlineData("Member")]
    [InlineData("Department")]
    [InlineData("Minister")]
    public async Task NonReaderRoles_CannotRead(string role)
    {
        SetIdentity(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/OperationLogs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/OperationLogs/options")).StatusCode);
    }

    [Theory]
    [InlineData("Founder")]
    [InlineData("President")]
    public async Task ReaderRoles_CanRead(string role)
    {
        SetIdentity(role);

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/OperationLogs")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/OperationLogs/options")).StatusCode);
    }

    [Fact]
    public async Task DeniedWrite_DoesNotCreateAuditRow()
    {
        // Minister 无权调用 Founder-only 的权限调整接口。
        SetIdentity("Minister", "2023000001");
        var denied = await _client.PostAsJsonAsync("/FounderPermission/update-role",
            new { userId = "2023000002", identity = "President", departmentName = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        SetIdentity("Founder", "0000000000");
        var response = await _client.GetAsync("/OperationLogs");
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, envelope.GetProperty("data").GetProperty("totalCount").GetInt32());
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

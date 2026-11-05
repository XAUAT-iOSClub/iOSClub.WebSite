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
/// C07 回归测试：监控统计重置的权限分级。
/// <para>
/// 回归背景：MonitoringController.ResetDataStats 没有任何鉴权，匿名请求即可清空 Redis
/// 中的数据访问/变化统计。决策：重置仅限 Founder / President；其余只读接口的公开性
/// 属于产品决策，本次不变更，但用测试锁定“只读接口仍匿名可访问”，防止误删全类级保护。
/// </para>
/// </summary>
public class MonitoringAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string ResetDataStatsEndpoint = "/Monitoring/reset-data-stats";
    private const string ReadDataAccessStatsEndpoint = "/Monitoring/data-access-stats";

    private readonly Mock<IDataAccessStatisticsService> _statistics = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public MonitoringAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        _statistics
            .Setup(s => s.ResetStatisticsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _statistics
            .Setup(s => s.GetDataAccessStatisticsAsync(It.IsAny<string?>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DataAccessStatisticsResult());
        _statistics
            .Setup(s => s.GetDataChangeStatisticsAsync(It.IsAny<string?>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DataChangeStatisticsResult());

        var options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"MonitoringAuthorization-{Guid.NewGuid()}")
            .Options;
        var database = Mock.Of<IDatabase>();
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database);
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing").ConfigureServices(services =>
        {
            var dbDescriptor = services.Single(d => d.ServiceType == typeof(IDbContextFactory<ClubContext>));
            services.Remove(dbDescriptor);
            services.AddSingleton<IDbContextFactory<ClubContext>>(new TestDbContextFactory(options));

            var statisticsDescriptor =
                services.Single(d => d.ServiceType == typeof(IDataAccessStatisticsService));
            services.Remove(statisticsDescriptor);
            services.AddSingleton(_statistics.Object);

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

    private void SetIdentity(string role)
    {
        _client.DefaultRequestHeaders.Remove("X-Test-Anonymous");
        _client.DefaultRequestHeaders.Remove("X-Test-Identity");
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-Identity", role);
        _client.DefaultRequestHeaders.Add("X-Test-UserId", "2023000001");
    }

    private void SetAnonymous()
    {
        _client.DefaultRequestHeaders.Remove("X-Test-Identity");
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
    }

    [Fact]
    public async Task Anonymous_CannotResetDataStats()
    {
        SetAnonymous();

        var response = await _client.PostAsync(ResetDataStatsEndpoint, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _statistics.Verify(
            s => s.ResetStatisticsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Member")]
    [InlineData("Department")]
    [InlineData("Minister")]
    public async Task NonAdminRoles_CannotResetDataStats(string role)
    {
        SetIdentity(role);

        var response = await _client.PostAsync(ResetDataStatsEndpoint, null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _statistics.Verify(
            s => s.ResetStatisticsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Founder")]
    [InlineData("President")]
    public async Task FounderAndPresident_CanResetDataStats(string role)
    {
        SetIdentity(role);

        var response = await _client.PostAsync(ResetDataStatsEndpoint, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _statistics.Verify(
            s => s.ResetStatisticsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReadOnlyDataAccessStats_RemainsAccessibleForAnonymous()
    {
        SetAnonymous();

        var response = await _client.GetAsync(ReadDataAccessStatsEndpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

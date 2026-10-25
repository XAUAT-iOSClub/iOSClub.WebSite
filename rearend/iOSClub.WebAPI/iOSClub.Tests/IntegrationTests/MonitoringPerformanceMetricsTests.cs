using System.Net;
using System.Net.Http.Json;
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
/// C30 回归测试：/Monitoring/performance 的字段名、单位与统计窗口必须自洽。
/// <para>
/// 回归背景：接口把累计 CPU 秒数命名为 <c>cpuUsage</c>（读起来像占用率），
/// 又把累计请求数除以硬编码 3600 后命名为 <c>requestsPerSecond</c>（读起来像实时速率），
/// 两个名字都与值的真实含义不符。决策：窗口固定为 3600 秒并显式返回
/// <c>http.windowSeconds</c>；速率改名 <c>http.averageRequestsPerSecond</c>，
/// 只表示该固定窗口内的平均值；CPU 指标改名 <c>system.cpuSecondsTotal</c>，
/// 单位是进程启动以来的累计 CPU 秒数。
/// </para>
/// </summary>
public class MonitoringPerformanceMetricsTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const int ExpectedWindowSeconds = 3600;

    private readonly Mock<IDataAccessStatisticsService> _statistics = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public MonitoringPerformanceMetricsTests(WebApplicationFactory<Program> factory)
    {
        _statistics
            .Setup(s => s.GetDataAccessStatisticsAsync(It.IsAny<string?>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DataAccessStatisticsResult());

        var options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"MonitoringPerformance-{Guid.NewGuid()}")
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

    [Fact]
    public async Task Performance_ReportsFixedWindowAndHonestMetricNames()
    {
        // 只读监控接口保持匿名可访问（C07 决策），这里顺带锁定匿名路径可用。
        _client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");

        var response = await _client.GetAsync("/Monitoring/performance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        var http = envelope.GetProperty("data").GetProperty("http");
        var system = envelope.GetProperty("data").GetProperty("system");

        // 固定窗口必须显式给出，速率字段的名字必须体现"窗口平均"而不是实时速率。
        Assert.True(http.TryGetProperty("windowSeconds", out var windowSeconds),
            "响应缺少 http.windowSeconds，调用方无法知道速率对应的时间窗口");
        Assert.Equal(ExpectedWindowSeconds, windowSeconds.GetInt32());
        Assert.True(http.TryGetProperty("averageRequestsPerSecond", out var averageRequestsPerSecond),
            "响应缺少 http.averageRequestsPerSecond");
        Assert.False(http.TryGetProperty("requestsPerSecond", out _),
            "旧字段 http.requestsPerSecond 应已改名为 http.averageRequestsPerSecond");

        // 窗口平均值必须与 totalRequests / windowSeconds 一致（同一响应内自洽）。
        var totalRequests = http.GetProperty("totalRequests").GetInt64();
        Assert.Equal(totalRequests / (double)ExpectedWindowSeconds, averageRequestsPerSecond.GetDouble(), 6);

        // CPU 指标值来自 process_cpu_seconds_total，是累计秒数，不能叫 usage。
        Assert.True(system.TryGetProperty("cpuSecondsTotal", out var cpuSecondsTotal),
            "响应缺少 system.cpuSecondsTotal");
        Assert.True(cpuSecondsTotal.GetDouble() >= 0, "system.cpuSecondsTotal 应是不小于 0 的累计秒数");
        Assert.False(system.TryGetProperty("cpuUsage", out _),
            "旧字段 system.cpuUsage 应已改名为 system.cpuSecondsTotal");

        // 其余既有字段保持不变，避免让前端连带迁移。
        Assert.True(http.TryGetProperty("totalRequests", out _));
        Assert.True(http.TryGetProperty("errorRate", out _));
        Assert.True(http.TryGetProperty("averageResponseTime", out _));
        Assert.True(system.TryGetProperty("memoryUsage", out _));
        Assert.True(system.TryGetProperty("gcCollections", out _));
    }

    [Fact]
    public async Task HttpStats_ReportsSameFixedWindowAndAverageName()
    {
        // /Monitoring/http-stats 与 /performance 使用同一套指标含义：
        // 累计请求数除以固定窗口只能叫窗口平均，不能叫每秒请求数。
        _client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");

        var response = await _client.GetAsync("/Monitoring/http-stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = envelope.GetProperty("data");

        Assert.True(data.TryGetProperty("windowSeconds", out var windowSeconds),
            "响应缺少 windowSeconds，调用方无法知道速率对应的时间窗口");
        Assert.Equal(ExpectedWindowSeconds, windowSeconds.GetInt32());
        Assert.True(data.TryGetProperty("averageRequestsPerSecond", out _),
            "响应缺少 averageRequestsPerSecond");
        Assert.False(data.TryGetProperty("requestsPerSecond", out _),
            "旧字段 requestsPerSecond 应已改名为 averageRequestsPerSecond");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

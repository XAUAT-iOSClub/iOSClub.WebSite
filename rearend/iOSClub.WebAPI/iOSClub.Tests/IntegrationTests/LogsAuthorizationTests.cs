using System.Globalization;
using System.Net;
using iOSClub.Data;
using iOSClub.DataApi.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;

namespace iOSClub.Tests.IntegrationTests;

/// <summary>
/// C06 回归测试：系统日志的读取与清理权限分级。
/// <para>
/// 回归背景：LogsController 原先只有类级 [Authorize]，任何已登录角色（Member/Department）
/// 都能读取系统日志，甚至能调用 cleanup 删除日志库数据。
/// 决策：读取 = Founder / President / Minister；清理 = 仅 Founder / President；匿名 401。
/// </para>
/// <para>
/// 日志库由 Testing 环境下的 Serilog SQLite sink 在进程 CWD 的 logs/log.db 创建
/// （列：Timestamp/Level/Exception/Properties/RenderedMessage），测试直接向该库插入带唯一
/// 前缀的行，验证 President cleanup 的删除范围，并在 Dispose 中清掉自己插入的行。
/// </para>
/// </summary>
public class LogsAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string ProbeMessagePrefix = "C06-Probe-";
    private const string ReadLogsEndpoint = "/Logs";
    private const string ReadStatisticsEndpoint = "/Logs/statistics";
    private const string ReadDistributionEndpoint = "/Logs/distribution?timeRange=7";

    // days=1 时 LogsController 会删除 Timestamp < DateTime.Now.AddDays(-1) 的行。
    private const string CleanupEndpoint = "/Logs/cleanup?days=1";

    private static readonly string LogDatabasePath =
        Path.Combine(Environment.CurrentDirectory, "logs", "log.db");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public LogsAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        var options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"LogsAuthorization-{Guid.NewGuid()}")
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
        EnsureLogStore();
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

    /// <summary>
    /// 确保 Serilog 的 SQLite 库/表存在（正常情况下 host 启动时已经创建）。
    /// 表结构与 Serilog.Sinks.SQLite 一致，使用 IF NOT EXISTS，不会破坏已有库。
    /// </summary>
    private static void EnsureLogStore()
    {
        var directory = Path.GetDirectoryName(LogDatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = OpenLogDatabase();
        using var command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS Logs (Timestamp TEXT, Level VARCHAR(10), Exception TEXT, " +
            "RenderedMessage TEXT, Properties TEXT)";
        command.ExecuteNonQuery();
    }

    private static SqliteConnection OpenLogDatabase()
    {
        // DefaultTimeout 用于在 Serilog 后台写入时等待 SQLite 锁，避免 "database is locked" 抖动。
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = LogDatabasePath,
            DefaultTimeout = 30
        }.ToString();
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static void InsertProbeLog(string message, DateTime timestamp, string level = "Information")
    {
        using var connection = OpenLogDatabase();
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO Logs (Timestamp, Level, Exception, RenderedMessage, Properties) " +
            "VALUES (@Timestamp, @Level, NULL, @Message, '{}')";
        command.Parameters.AddWithValue("@Timestamp",
            timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@Level", level);
        command.Parameters.AddWithValue("@Message", message);
        command.ExecuteNonQuery();
    }

    private static bool ProbeLogExists(string message)
    {
        using var connection = OpenLogDatabase();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Logs WHERE RenderedMessage = @Message";
        command.Parameters.AddWithValue("@Message", message);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static void DeleteProbeLogs()
    {
        using var connection = OpenLogDatabase();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Logs WHERE RenderedMessage LIKE @Pattern";
        command.Parameters.AddWithValue("@Pattern", $"{ProbeMessagePrefix}%");
        command.ExecuteNonQuery();
    }

    [Fact]
    public async Task Anonymous_CannotReadOrCleanup()
    {
        SetAnonymous();

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(ReadLogsEndpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(ReadStatisticsEndpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(ReadDistributionEndpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsync(CleanupEndpoint, null)).StatusCode);
    }

    [Theory]
    [InlineData("Member")]
    [InlineData("Department")]
    public async Task NonReaderRoles_CannotReadOrCleanup(string role)
    {
        SetIdentity(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync(ReadLogsEndpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync(ReadStatisticsEndpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync(ReadDistributionEndpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsync(CleanupEndpoint, null)).StatusCode);
    }

    [Theory]
    [InlineData("Founder")]
    [InlineData("President")]
    [InlineData("Minister")]
    public async Task ReaderRoles_CanReadAllLogEndpoints(string role)
    {
        SetIdentity(role);

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(ReadLogsEndpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(ReadStatisticsEndpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(ReadDistributionEndpoint)).StatusCode);
    }

    [Theory]
    [InlineData("Member")]
    [InlineData("Department")]
    [InlineData("Minister")]
    public async Task NonCleanerRoles_CannotCleanup(string role)
    {
        SetIdentity(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsync(CleanupEndpoint, null)).StatusCode);
    }

    [Theory]
    [InlineData("Founder")]
    [InlineData("President")]
    public async Task FounderAndPresident_CanCleanup(string role)
    {
        SetIdentity(role);

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync(CleanupEndpoint, null)).StatusCode);
    }

    [Fact]
    public async Task PresidentCleanup_RemovesOldLogsAndKeepsRecentOnes()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var oldMessage = $"{ProbeMessagePrefix}{suffix}-old";
        var freshMessage = $"{ProbeMessagePrefix}{suffix}-new";
        InsertProbeLog(oldMessage, DateTime.Now.AddDays(-10));
        InsertProbeLog(freshMessage, DateTime.Now.AddHours(-1));

        SetIdentity("President");
        var response = await _client.PostAsync(CleanupEndpoint, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(ProbeLogExists(oldMessage));
        Assert.True(ProbeLogExists(freshMessage));
    }

    public void Dispose()
    {
        try
        {
            DeleteProbeLogs();
        }
        catch (SqliteException)
        {
            // 尽力清理：测试库位于临时 artifacts 目录，清理失败不影响断言结论。
        }

        _client.Dispose();
        _factory.Dispose();
    }
}

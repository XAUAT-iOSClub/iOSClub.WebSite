using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.DataApi.Repositories;
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
/// C28 回归测试：/MemberManagement/update-many 的响应必须携带逐行结果。
/// <para>
/// 回归背景：接口先过滤不合格行再写库，且只返回 bool。整批行都被过滤时库没有任何变化，
/// 客户端却收到"批量更新学生成功"，管理员无法发现名单没有生效。
/// 决策：DTO 逐行返回新增/更新/跳过/失败数量与原因；零写入且存在被跳过或失败的行时，
/// 业务码必须是失败（<c>ErrorCode.OperationFailed</c>，HTTP 状态码按统一约定跟随 code），
/// 同时 data 仍保留数量与原因，便于界面直接展示。
/// </para>
/// </summary>
public class MemberBulkUpdateTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string MinisterUserId = "2023000001";
    private const string ExistingUserId = "2022000001";
    private const string NewUserId = "2024999999";

    private readonly DbContextOptions<ClubContext> _options;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public MemberBulkUpdateTests(WebApplicationFactory<Program> factory)
    {
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"MemberBulkUpdate-{Guid.NewGuid()}")
            .Options;
        var database = Mock.Of<IDatabase>();
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database);
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing").ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(IDbContextFactory<ClubContext>));
            services.Remove(descriptor);
            services.AddSingleton<IDbContextFactory<ClubContext>>(new TestDbContextFactory(_options));
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

    private void SignInAsMinister()
    {
        _client.DefaultRequestHeaders.Remove("X-Test-Identity");
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-Identity", "Minister");
        _client.DefaultRequestHeaders.Add("X-Test-UserId", MinisterUserId);
    }

    private async Task SeedExistingStudentAsync()
    {
        await using var context = new ClubContext(_options);
        context.Students.Add(new StudentDO
        {
            UserId = ExistingUserId,
            UserName = "原姓名",
            Academy = "计算机与信息工程学院",
            PoliticalLandscape = "群众",
            Gender = "女",
            ClassName = "计科2201",
            PhoneNum = "13900139000",
            PasswordHash = DataTool.StringToHash("OldPass123")
        });
        await context.SaveChangesAsync();
    }

    private static async Task<JsonElement> ReadEnvelopeAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task UpdateMany_混合行_返回逐行数量并只写入合法行()
    {
        await SeedExistingStudentAsync();
        SignInAsMinister();

        // 三行输入：一条新增（合法）、一条更新（合法）、一条手机号缺失（被仓库层过滤）。
        var body = new object[]
        {
            new
            {
                userId = NewUserId, userName = "新成员", academy = "计算机与信息工程学院",
                politicalLandscape = "群众", gender = "男", className = "计科2301", phoneNum = "13800138000"
            },
            new
            {
                userId = ExistingUserId, userName = "更新姓名", academy = "计算机与信息工程学院",
                politicalLandscape = "群众", gender = "女", className = "计科2201", phoneNum = "13900139000"
            },
            new { userId = "2024000001", userName = "跳过成员" }
        };

        var response = await _client.PostAsJsonAsync("/MemberManagement/update-many", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await ReadEnvelopeAsync(response);
        Assert.Equal(200, envelope.GetProperty("code").GetInt32());
        Assert.Equal(0, envelope.GetProperty("errorCode").GetInt32());
        Assert.Equal("新增 1，更新 1，跳过 1，失败 0", envelope.GetProperty("message").GetString());

        var data = envelope.GetProperty("data");
        Assert.Equal(1, data.GetProperty("created").GetInt32());
        Assert.Equal(1, data.GetProperty("updated").GetInt32());
        Assert.Equal(1, data.GetProperty("skipped").GetInt32());
        Assert.Equal(0, data.GetProperty("failed").GetInt32());
        var issue = Assert.Single(data.GetProperty("issues").EnumerateArray());
        Assert.Equal("2024000001", issue.GetProperty("userId").GetString());
        Assert.Equal("skipped", issue.GetProperty("kind").GetString());
        Assert.False(string.IsNullOrWhiteSpace(issue.GetProperty("reason").GetString()));

        // 合法行真实写库：新学生存在，老学生姓名被更新，被跳过的行没有落库。
        await using var verify = new ClubContext(_options);
        Assert.True(await verify.Students.AnyAsync(s => s.UserId == NewUserId));
        var existing = await verify.Students.SingleAsync(s => s.UserId == ExistingUserId);
        Assert.Equal("更新姓名", existing.UserName);
        Assert.False(await verify.Students.AnyAsync(s => s.UserId == "2024000001"));
        var repository = new StudentRepository(new TestDbContextFactory(_options));
        Assert.True(await repository.Login(ExistingUserId, "OldPass123"));
    }

    [Fact]
    public async Task UpdateMany_全部被跳过_返回失败码且数据仍带原因()
    {
        SignInAsMinister();

        // 旧实现会返回"成功"；决策要求零写入且有跳过行时明确失败。
        var body = new object[]
        {
            new { userId = "2024000002", userName = "跳过一" },
            new { userId = "2024000003", userName = "跳过二" }
        };

        var response = await _client.PostAsJsonAsync("/MemberManagement/update-many", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await ReadEnvelopeAsync(response);
        Assert.Equal(400, envelope.GetProperty("code").GetInt32());
        Assert.Equal(2001, envelope.GetProperty("errorCode").GetInt32()); // ErrorCode.OperationFailed
        Assert.Equal("新增 0，更新 0，跳过 2，失败 0", envelope.GetProperty("message").GetString());

        var data = envelope.GetProperty("data");
        Assert.Equal(0, data.GetProperty("created").GetInt32());
        Assert.Equal(0, data.GetProperty("updated").GetInt32());
        Assert.Equal(2, data.GetProperty("skipped").GetInt32());
        Assert.Equal(0, data.GetProperty("failed").GetInt32());
        var issues = data.GetProperty("issues").EnumerateArray().ToList();
        Assert.Equal(2, issues.Count);
        Assert.All(issues, issue =>
        {
            Assert.Equal("skipped", issue.GetProperty("kind").GetString());
            Assert.False(string.IsNullOrWhiteSpace(issue.GetProperty("reason").GetString()));
        });

        await using var verify = new ClubContext(_options);
        Assert.Empty(await verify.Students.ToListAsync());
    }

    [Fact]
    public async Task UpdateMany_空列表_返回空结果且不报失败()
    {
        SignInAsMinister();

        var response = await _client.PostAsJsonAsync("/MemberManagement/update-many", Array.Empty<object>());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await ReadEnvelopeAsync(response);
        Assert.Equal(0, envelope.GetProperty("errorCode").GetInt32());
        Assert.Equal("新增 0，更新 0，跳过 0，失败 0", envelope.GetProperty("message").GetString());
        var data = envelope.GetProperty("data");
        Assert.Equal(0, data.GetProperty("created").GetInt32());
        Assert.Equal(0, data.GetProperty("updated").GetInt32());
        Assert.Equal(0, data.GetProperty("skipped").GetInt32());
        Assert.Equal(0, data.GetProperty("failed").GetInt32());
        Assert.Empty(data.GetProperty("issues").EnumerateArray());
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

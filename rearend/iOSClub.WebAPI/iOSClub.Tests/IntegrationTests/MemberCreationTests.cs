using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.DataApi.Repositories;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Controllers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;

namespace iOSClub.Tests.IntegrationTests;

/// <summary>
/// 管理员新增成员的权限与初始凭证回归测试。
/// <para>
/// 回归背景：成员管理页面的"添加成员"复用了 update 接口，而该接口在学号不存在时
/// 必然返回"学生不存在"，于是页面提供了一个当前接口无法完成的创建操作。
/// 这里锁定：管理角色可以通过独立创建接口新增学生、初始密码可登录、重复学号被拒绝
/// 且不覆盖已有档案，非管理角色无法创建。
/// </para>
/// </summary>
public class MemberCreationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string MinisterUserId = "2023000001";
    private const string ExistingUserId = "2022000001";
    private const string NewUserId = "2024999999";
    private const string InitialPassword = "InitPass123";

    private readonly DbContextOptions<ClubContext> _options;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public MemberCreationTests(WebApplicationFactory<Program> factory)
    {
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"MemberCreation-{Guid.NewGuid()}")
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

    private void SignInAs(string identity, string userId = MinisterUserId)
    {
        _client.DefaultRequestHeaders.Remove("X-Test-Identity");
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-Identity", identity);
        _client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
    }

    private static StudentCreateDTO ValidCreate(string userId = NewUserId, string userName = "新成员")
        => new()
        {
            UserId = userId,
            UserName = userName,
            Academy = "计算机与信息工程学院",
            PoliticalLandscape = "群众",
            Gender = "男",
            ClassName = "计科2301",
            PhoneNum = "13800138000",
            Password = InitialPassword,
            EMail = null
        };

    private static async Task<JsonElement> ReadEnvelopeAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>
    /// 通过成员查询分页接口读取刚创建的学生，走 GZip 解压后按学号查找。
    /// 这是"创建后再次查询"的真实读取路径，而不是直接读内存库。
    /// </summary>
    private async Task<JsonElement?> QueryMemberFromApiAsync(string userId)
    {
        var response = await _client.GetAsync(
            $"/MemberQuery/all-data/page?pageNum=1&pageSize=10&searchTerm={userId}&searchCondition=userid");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await ReadEnvelopeAsync(response);
        var compressed = envelope.GetProperty("data").GetString();
        Assert.False(string.IsNullOrWhiteSpace(compressed));

        var decompressed = GZipServer.DecompressString(compressed!);
        using var document = JsonDocument.Parse(decompressed);
        foreach (var member in document.RootElement.GetProperty("data").EnumerateArray())
        {
            if (member.GetProperty("userId").GetString() == userId)
                return member.Clone();
        }

        return null;
    }

    [Fact]
    public async Task Create_AsMinister_CreatesLoginableStudentAndIsQueryable()
    {
        SignInAs("Minister");

        var response = await _client.PostAsJsonAsync("/MemberManagement/create", ValidCreate());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await ReadEnvelopeAsync(response);
        Assert.Equal(0, envelope.GetProperty("errorCode").GetInt32());
        Assert.Equal(NewUserId, envelope.GetProperty("data").GetProperty("userId").GetString());
        // 响应不得回传密码或密码哈希
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(InitialPassword, raw);
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);

        await using (var context = new ClubContext(_options))
        {
            var saved = await context.Students.SingleAsync(s => s.UserId == NewUserId);
            Assert.Equal("新成员", saved.UserName);
            Assert.False(string.IsNullOrWhiteSpace(saved.PasswordHash));
            Assert.NotEqual(InitialPassword, saved.PasswordHash);
        }

        // 初始凭证真实可用
        var repository = new StudentRepository(new TestDbContextFactory(_options));
        Assert.True(await repository.Login(NewUserId, InitialPassword));
        Assert.False(await repository.Login(NewUserId, "WrongPass123"));

        // 再次查询：分页接口能查到，且未建 Staff 档案时身份为 Member
        var member = await QueryMemberFromApiAsync(NewUserId);
        Assert.NotNull(member);
        Assert.Equal("新成员", member!.Value.GetProperty("userName").GetString());
        Assert.Equal("Member", member!.Value.GetProperty("identity").GetString());
    }

    [Fact]
    public async Task Create_DuplicateStudentId_IsRejectedWithoutOverwritingExistingProfile()
    {
        await using (var context = new ClubContext(_options))
        {
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

        SignInAs("Minister");
        var response = await _client.PostAsJsonAsync("/MemberManagement/create",
            ValidCreate(ExistingUserId, "被覆盖的姓名"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await ReadEnvelopeAsync(response);
        Assert.Equal(2000, envelope.GetProperty("errorCode").GetInt32()); // ErrorCode.ResourceAlreadyExists

        await using var verify = new ClubContext(_options);
        var saved = await verify.Students.SingleAsync(s => s.UserId == ExistingUserId);
        Assert.Equal("原姓名", saved.UserName);
        var repository = new StudentRepository(new TestDbContextFactory(_options));
        Assert.True(await repository.Login(ExistingUserId, "OldPass123"));
        Assert.False(await repository.Login(ExistingUserId, InitialPassword));
        Assert.Single(await verify.Students.Where(s => s.UserId == ExistingUserId).ToListAsync());
    }

    [Fact]
    public async Task Create_WithInvalidPassword_IsRejected()
    {
        SignInAs("Minister");
        var dto = ValidCreate();
        dto.Password = "short";

        var response = await _client.PostAsJsonAsync("/MemberManagement/create", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = new ClubContext(_options);
        Assert.False(await context.Students.AnyAsync(s => s.UserId == NewUserId));
    }

    [Theory]
    [InlineData("Department")]
    [InlineData("Member")]
    public async Task Create_AsNonManagementRole_IsRejected(string identity)
    {
        SignInAs(identity, "2023000009");

        var response = await _client.PostAsJsonAsync("/MemberManagement/create", ValidCreate());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var context = new ClubContext(_options);
        Assert.False(await context.Students.AnyAsync(s => s.UserId == NewUserId));
    }

    [Fact]
    public async Task Create_Anonymous_IsRejected()
    {
        _client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");

        var response = await _client.PostAsJsonAsync("/MemberManagement/create", ValidCreate());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

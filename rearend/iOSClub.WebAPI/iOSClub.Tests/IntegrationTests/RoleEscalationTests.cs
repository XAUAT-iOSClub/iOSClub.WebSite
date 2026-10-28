using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
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
/// C1/C2 回归测试：职级不能被低职级管理员绕过，也不能被公开注册抢注。
/// <para>
/// 背景：登录身份来自 Staffs 表，而公开注册只检查 Students 表；同时部门导入、
/// 全量数据导入和员工管理接口都没有校验"操作者能否授予目标职级"。
/// 结果是 Minister 可以造出 President 甚至 Founder，或直接接管只有 Staff 的账号。
/// 本文件锁定每条已知路径的拒绝行为。
/// </para>
/// </summary>
public class RoleEscalationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string FounderUserId = "0000000000";
    private const string MinisterUserId = "2023000001";

    private readonly DbContextOptions<ClubContext> _options;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public RoleEscalationTests(WebApplicationFactory<Program> factory)
    {
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"RoleEscalation-{Guid.NewGuid()}")
            .Options;

        var tokenGenerator = new Mock<ITokenGenerator>();
        tokenGenerator.Setup(t => t.GetMemberToken(
                It.IsAny<MemberVO>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(("test-access-token", "test-refresh-token"));

        var redisDb = new Mock<IDatabase>();
        redisDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
        var batch = new Mock<IBatch>();
        batch.Setup(b => b.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        redisDb.Setup(d => d.CreateBatch(It.IsAny<object>())).Returns(batch.Object);

        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(redisDb.Object);

        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing").ConfigureServices(services =>
        {
            var dbFactory = services.SingleOrDefault(d => d.ServiceType == typeof(IDbContextFactory<ClubContext>));
            if (dbFactory != null) services.Remove(dbFactory);
            services.AddSingleton<IDbContextFactory<ClubContext>>(new TestDbContextFactory(_options));

            var generator = services.SingleOrDefault(d => d.ServiceType == typeof(ITokenGenerator));
            if (generator != null) services.Remove(generator);
            services.AddSingleton(tokenGenerator.Object);

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

    private static StudentCreateDTO ValidSignup(string userId) => new()
    {
        UserId = userId,
        UserName = "抢注者",
        Academy = "计算机与信息工程学院",
        PoliticalLandscape = "群众",
        Gender = "男",
        ClassName = "计科2301",
        PhoneNum = "13800138000",
        Password = "Attack123",
        EMail = null
    };

    private async Task SeedAsync(Action<ClubContext> seed)
    {
        await using var context = new ClubContext(_options);
        seed(context);
        await context.SaveChangesAsync();
    }

    // ===== C1：公开注册不能占用已有 Staff 的学号 =====

    [Fact]
    public async Task SignUp_CannotClaimExistingStaffUserId()
    {
        await SeedAsync(context =>
            context.Staffs.Add(new StaffDO { UserId = FounderUserId, Name = "root", Identity = "Founder" }));

        var response = await _client.PostAsJsonAsync("/Auth/signup", ValidSignup(FounderUserId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2000, envelope.GetProperty("errorCode").GetInt32()); // ResourceAlreadyExists

        await using var verify = new ClubContext(_options);
        Assert.False(await verify.Students.AnyAsync(s => s.UserId == FounderUserId));
    }

    [Fact]
    public async Task SignUp_NewUserId_StillSucceeds()
    {
        var response = await _client.PostAsJsonAsync("/Auth/signup", ValidSignup("2024999998"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var verify = new ClubContext(_options);
        Assert.True(await verify.Students.AnyAsync(s => s.UserId == "2024999998"));
    }

    // ===== C2：员工管理不能越级授予身份 =====

    [Fact]
    public async Task CreateStaff_AsMinister_CannotCreatePresident()
    {
        SignInAs("Minister");

        var response = await _client.PostAsJsonAsync("/Staff/Create",
            new StaffCreateDTO { UserId = "2024000002", Name = "新社长", Identity = "President" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var verify = new ClubContext(_options);
        Assert.False(await verify.Staffs.AnyAsync(s => s.UserId == "2024000002"));
    }

    [Fact]
    public async Task CreateStaff_AsMinister_CanCreateMinister()
    {
        SignInAs("Minister");

        var response = await _client.PostAsJsonAsync("/Staff/Create",
            new StaffCreateDTO { UserId = "2024000004", Name = "新部长", Identity = "Minister" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var verify = new ClubContext(_options);
        Assert.Equal("Minister", (await verify.Staffs.SingleAsync(s => s.UserId == "2024000004")).Identity);
    }

    [Fact]
    public async Task AssignRole_AsMinister_CannotPromoteToPresident()
    {
        await SeedAsync(context =>
        {
            var department = new DepartmentDO { Key = "tech", Name = "技术部" };
            context.Departments.Add(department);
            context.Staffs.Add(new StaffDO
            {
                UserId = "2024000003", Name = "部员", Identity = "Department", Department = department
            });
        });

        SignInAs("Minister");
        var response = await _client.PostAsJsonAsync("/Staff/assign-role",
            new StaffRoleAssignDTO { UserId = "2024000003", Identity = "President" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var verify = new ClubContext(_options);
        Assert.Equal("Department",
            (await verify.Staffs.SingleAsync(s => s.UserId == "2024000003")).Identity);
    }

    // ===== C1/C2：成员管理不能碰高于自己的账号 =====

    [Fact]
    public async Task ResetPassword_AsMinister_CannotResetFounderPassword()
    {
        await SeedAsync(context =>
        {
            context.Staffs.Add(new StaffDO { UserId = FounderUserId, Name = "root", Identity = "Founder" });
            context.Students.Add(new StudentDO
            {
                UserId = FounderUserId,
                UserName = "root",
                PasswordHash = DataTool.StringToHash("OldPass123")
            });
        });

        SignInAs("Minister");
        var response = await _client.PostAsJsonAsync("/MemberManagement/reset-password",
            new ResetPasswordData(FounderUserId, "HackedPass123"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var repository = new StudentRepository(new TestDbContextFactory(_options));
        Assert.True(await repository.Login(FounderUserId, "OldPass123"));
        Assert.False(await repository.Login(FounderUserId, "HackedPass123"));
    }

    [Fact]
    public async Task CreateStudent_AsMinister_CannotCreateLoginForFounder()
    {
        // 生产环境旧逻辑会留下"只有 Staff、没有 Student"的 Founder，
        // Minister 只要替他创建学号档案并设定密码，就能直接登录成 Founder。
        await SeedAsync(context =>
            context.Staffs.Add(new StaffDO { UserId = FounderUserId, Name = "root", Identity = "Founder" }));

        SignInAs("Minister");
        var response = await _client.PostAsJsonAsync("/MemberManagement/create", ValidSignup(FounderUserId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var verify = new ClubContext(_options);
        Assert.False(await verify.Students.AnyAsync(s => s.UserId == FounderUserId));
    }

    [Fact]
    public async Task ResetPassword_AsFounder_CanResetPresidentPassword()
    {
        await SeedAsync(context =>
        {
            context.Staffs.Add(new StaffDO { UserId = FounderUserId, Name = "root", Identity = "Founder" });
            context.Staffs.Add(new StaffDO { UserId = "2024000005", Name = "社长", Identity = "President" });
            context.Students.Add(new StudentDO
            {
                UserId = "2024000005", UserName = "社长", PasswordHash = DataTool.StringToHash("OldPass123")
            });
        });

        SignInAs("Founder", FounderUserId);
        var response = await _client.PostAsJsonAsync("/MemberManagement/reset-password",
            new ResetPasswordData("2024000005", "NewPass123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var repository = new StudentRepository(new TestDbContextFactory(_options));
        Assert.True(await repository.Login("2024000005", "NewPass123"));
    }

    private async Task<HttpResponseMessage> PostDataImportAsync(string json)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        form.Add(file, "file", "data.json");
        return await _client.PostAsync("/DataCentre/update-from-json", form);
    }

    // ===== C2：全量导入不能夹带 Founder =====

    [Fact]
    public async Task DataImport_AsMinister_CannotCreateFounder()
    {
        SignInAs("Minister");

        const string json = """
        {
          "students": [],
          "staffs": [ { "userId": "2024000009", "name": "伪造创始人", "identity": "Founder" } ],
          "presidents": [],
          "resources": [],
          "departments": [],
          "categories": [],
          "articles": []
        }
        """;

        var response = await PostDataImportAsync(json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var verify = new ClubContext(_options);
        Assert.False(await verify.Staffs.AnyAsync(s => s.UserId == "2024000009"));
    }

    [Fact]
    public async Task DataImport_AsMinister_CannotDemotePresident()
    {
        await SeedAsync(context =>
            context.Staffs.Add(new StaffDO { UserId = "2024000010", Name = "社长", Identity = "President" }));

        SignInAs("Minister");

        const string json = """
        {
          "students": [],
          "staffs": [ { "userId": "2024000010", "name": "社长", "identity": "Department" } ],
          "presidents": [],
          "resources": [],
          "departments": [],
          "categories": [],
          "articles": []
        }
        """;

        var response = await PostDataImportAsync(json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var verify = new ClubContext(_options);
        Assert.Equal("President", (await verify.Staffs.SingleAsync(s => s.UserId == "2024000010")).Identity);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

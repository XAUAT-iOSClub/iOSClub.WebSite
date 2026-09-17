using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace iOSClub.Tests.IntegrationTests;

public class DepartmentImportControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly DbContextOptions<ClubContext> _options;

    public DepartmentImportControllerTests(WebApplicationFactory<Program> factory)
    {
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase(databaseName: "DepartmentImportControllerTestDatabase")
            .Options;

        var redisMock = new Mock<IConnectionMultiplexer>();
        var redisDbMock = new Mock<IDatabase>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(redisDbMock.Object);
        redisDbMock.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
        var batchMock = new Mock<IBatch>();
        batchMock.Setup(b => b.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        redisDbMock.Setup(r => r.CreateBatch(It.IsAny<object>())).Returns(batchMock.Object);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDbContextFactory<ClubContext>));
                if (descriptor != null) services.Remove(descriptor);
                services.AddSingleton<IDbContextFactory<ClubContext>>(new TestDbContextFactory(_options));

                services.AddSingleton(redisMock.Object);
                services.AddSingleton(Mock.Of<IEmailService>());

                // 用测试认证方案替换 JWT：请求头 X-Test-Identity / X-Test-UserId 决定身份，
                // X-Test-Anonymous=true 时返回未认证，用于验证 401。
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
        });

        _client = _factory.CreateClient();
    }

    private async Task SeedAsync()
    {
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        context.Departments.Add(new DepartmentDO { Key = "tech", Name = "技术部", Description = "技术部" });
        context.Staffs.Add(new StaffDO { UserId = "0000000000", Name = "社长", Identity = "Founder" });
        await context.SaveChangesAsync();
    }

    private void ActAsFounder()
    {
        _client.DefaultRequestHeaders.Remove("X-Test-Anonymous");
        _client.DefaultRequestHeaders.Remove("X-Test-Identity");
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-Identity", "Founder");
        _client.DefaultRequestHeaders.Add("X-Test-UserId", "0000000000");
    }

    private static DepartmentImportDTO Roster() => new()
    {
        FileName = "技术部名单.xlsx",
        Members =
        [
            new DepartmentImportMemberDTO { UserId = "2023000001", Name = "张三", Identity = "President" },
            new DepartmentImportMemberDTO { UserId = "2023000002", Name = "李四", Identity = "Minister" },
            new DepartmentImportMemberDTO { UserId = "2023000003", Name = "王五", Identity = "Department" }
        ]
    };

    [Fact]
    public async Task Import_ThenHistoryAndBackup_EndToEnd()
    {
        await SeedAsync();
        ActAsFounder();

        var importResponse = await _client.PostAsJsonAsync("/Department/技术部/import", Roster());
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        var importResult = JsonConvert.DeserializeObject<ApiResponse<DepartmentImportResultVO>>(
            await importResponse.Content.ReadAsStringAsync());
        Assert.NotNull(importResult);
        Assert.Equal(200, importResult.Code);
        Assert.Equal(0, importResult.Data!.BeforeCount);
        Assert.Equal(3, importResult.Data.AfterCount);
        Assert.Equal(1, importResult.Data.RoleChanges["President"][1]);
        Assert.Equal(1, importResult.Data.RoleChanges["Minister"][1]);
        Assert.Equal(1, importResult.Data.RoleChanges["Department"][1]);
        Assert.NotNull(importResult.Data.HistoryId);

        // 第二次导入覆盖，应备份前一次的 3 人
        var second = await _client.PostAsJsonAsync("/Department/技术部/import", new DepartmentImportDTO
        {
            FileName = "技术部名单v2.csv",
            Members =
            [
                new DepartmentImportMemberDTO { UserId = "2023000002", Name = "李四", Identity = "Minister" },
                new DepartmentImportMemberDTO { UserId = "2023000004", Name = "赵六", Identity = "Department" }
            ]
        });
        var secondResult = JsonConvert.DeserializeObject<ApiResponse<DepartmentImportResultVO>>(
            await second.Content.ReadAsStringAsync());
        Assert.Equal(3, secondResult!.Data!.BeforeCount);
        Assert.Equal(2, secondResult.Data.AfterCount);

        await using (var context = new ClubContext(_options))
        {
            var staff = await context.Staffs.Where(s => s.Department != null && s.Department.Name == "技术部")
                .ToListAsync();
            Assert.Equal(2, staff.Count);
            Assert.DoesNotContain(staff, s => s.UserId == "2023000001");
            Assert.Contains(staff, s => s.UserId == "2023000004");
        }

        var historyResponse = await _client.GetAsync("/Department/技术部/import-history");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = JsonConvert.DeserializeObject<ApiResponse<List<ImportHistoryVO>>>(
            await historyResponse.Content.ReadAsStringAsync());
        Assert.NotNull(history);
        Assert.Equal(2, history.Data!.Count);
        Assert.Equal("技术部", history.Data[0].DepartmentName);
        Assert.Equal("社长", history.Data[0].OperatorName);

        var backupResponse = await _client.GetAsync($"/Department/import-history/{history.Data[0].Id}/backup");
        Assert.Equal(HttpStatusCode.OK, backupResponse.StatusCode);
        var backupJson = await backupResponse.Content.ReadAsStringAsync();
        Assert.Contains("2023000001", backupJson);
        Assert.Contains("2023000003", backupJson);
    }

    [Fact]
    public async Task Import_WithoutAuthentication_ReturnsUnauthorized()
    {
        await SeedAsync();
        _client.DefaultRequestHeaders.Remove("X-Test-Identity");
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");

        var response = await _client.PostAsJsonAsync("/Department/技术部/import", Roster());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Import_InvalidIdentity_ReturnsValidationFailure()
    {
        await SeedAsync();
        ActAsFounder();

        var dto = new DepartmentImportDTO
        {
            Members = [new DepartmentImportMemberDTO { UserId = "2023000009", Name = "非法", Identity = "Founder" }]
        };
        var response = await _client.PostAsJsonAsync("/Department/技术部/import", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = JsonConvert.DeserializeObject<ApiResponse<DepartmentImportResultVO>>(
            await response.Content.ReadAsStringAsync());
        Assert.NotNull(result);
        Assert.Equal(ErrorCode.ParameterValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Import_DuplicateUserId_ReturnsAlreadyExists()
    {
        await SeedAsync();
        ActAsFounder();

        var dto = new DepartmentImportDTO
        {
            Members =
            [
                new DepartmentImportMemberDTO { UserId = "2023000009", Name = "甲", Identity = "Department" },
                new DepartmentImportMemberDTO { UserId = "2023000009", Name = "乙", Identity = "Department" }
            ]
        };
        var response = await _client.PostAsJsonAsync("/Department/技术部/import", dto);

        // 业务错误码 2000 按区间映射为 400（见 ErrorCodeHttpMapper）
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = JsonConvert.DeserializeObject<ApiResponse<DepartmentImportResultVO>>(
            await response.Content.ReadAsStringAsync());
        Assert.NotNull(result);
        Assert.Equal(ErrorCode.ResourceAlreadyExists, result.ErrorCode);
    }

    [Fact]
    public async Task Import_WithProfileFields_PersistsStudentAndBackup()
    {
        await SeedAsync();
        ActAsFounder();

        var dto = new DepartmentImportDTO
        {
            FileName = "技术部名单.xlsx",
            Members =
            [
                new DepartmentImportMemberDTO
                {
                    UserId = "2023000001", Name = "张三", Identity = "President",
                    Academy = "计算机学院", ClassName = "计科2301", PhoneNum = "13800138001",
                    PoliticalLandscape = "共青团员", Gender = "男", EMail = "zhang@example.com"
                }
            ]
        };

        var response = await _client.PostAsJsonAsync("/Department/技术部/import", dto);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using (var context = new ClubContext(_options))
        {
            var student = await context.Students.SingleAsync(s => s.UserId == "2023000001");
            Assert.Equal("计算机学院", student.Academy);
            Assert.Equal("计科2301", student.ClassName);
            Assert.Equal("13800138001", student.PhoneNum);
            Assert.Equal("共青团员", student.PoliticalLandscape);
            Assert.Equal("男", student.Gender);
            Assert.Equal("zhang@example.com", student.EMail);
            Assert.True(DataTool.IsOk("13800138001", student.PasswordHash));
        }

        // 备份（第二次导入后）应包含被覆盖成员的完整档案
        var second = await _client.PostAsJsonAsync("/Department/技术部/import", new DepartmentImportDTO
        {
            Members = [new DepartmentImportMemberDTO { UserId = "2023000002", Name = "李四", Identity = "Department" }]
        });
        var secondResult = JsonConvert.DeserializeObject<ApiResponse<DepartmentImportResultVO>>(
            await second.Content.ReadAsStringAsync());
        var backedUp = Assert.Single(secondResult!.Data!.Backup, m => m.UserId == "2023000001");
        Assert.Equal("计算机学院", backedUp.Academy);
        Assert.Equal("13800138001", backedUp.PhoneNum);
        Assert.Equal("zhang@example.com", backedUp.EMail);
    }

    [Fact]
    public async Task Import_InvalidPhone_ReturnsValidationFailure()
    {
        await SeedAsync();
        ActAsFounder();

        var dto = new DepartmentImportDTO
        {
            Members =
            [
                new DepartmentImportMemberDTO
                {
                    UserId = "2023000001", Name = "张三", Identity = "Department", PhoneNum = "123"
                }
            ]
        };

        var response = await _client.PostAsJsonAsync("/Department/技术部/import", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = JsonConvert.DeserializeObject<ApiResponse<DepartmentImportResultVO>>(
            await response.Content.ReadAsStringAsync());
        Assert.NotNull(result);
        Assert.Equal(ErrorCode.ParameterValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Import_MinimalColumns_StillWorks()
    {
        await SeedAsync();
        ActAsFounder();

        var response = await _client.PostAsJsonAsync("/Department/技术部/import", new DepartmentImportDTO
        {
            Members =
            [
                new DepartmentImportMemberDTO { UserId = "2023000001", Name = "张三", Identity = "Department" }
            ]
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = JsonConvert.DeserializeObject<ApiResponse<DepartmentImportResultVO>>(
            await response.Content.ReadAsStringAsync());
        Assert.Equal(1, result!.Data!.AfterCount);

        await using var context = new ClubContext(_options);
        var student = await context.Students.SingleAsync(s => s.UserId == "2023000001");
        Assert.Equal("张三", student.UserName);
        Assert.False(string.IsNullOrEmpty(student.PasswordHash));
    }

}

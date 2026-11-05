using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
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
/// 操作日志写入回归：成功/失败/畸形 JSON、脱敏、读取与认证端点不记录、
/// 3 个 GET 删除特例、multipart 元数据。
/// </summary>
public class OperationLogRecordingTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string PresidentUserId = "2024000003";
    private const string NewUserId = "2024999999";
    private const string InitialPassword = "InitPass123";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public OperationLogRecordingTests(WebApplicationFactory<Program> factory)
    {
        var options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"OperationLogRecording-{Guid.NewGuid()}")
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

    private IDbContextFactory<ClubContext> DbFactory
        => _factory.Services.GetRequiredService<IDbContextFactory<ClubContext>>();

    private void SetIdentity(string role, string userId)
    {
        _client.DefaultRequestHeaders.Remove("X-Test-Anonymous");
        _client.DefaultRequestHeaders.Remove("X-Test-Identity");
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-Identity", role);
        _client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
    }

    private async Task SeedStaffAsync()
    {
        await using var context = await DbFactory.CreateDbContextAsync();
        if (!await context.Staffs.AnyAsync(s => s.UserId == PresidentUserId))
        {
            context.Staffs.Add(new StaffDO
            {
                UserId = PresidentUserId,
                Name = "社长",
                Identity = "President"
            });
            await context.SaveChangesAsync();
        }
    }

    private async Task ClearLogsAsync()
    {
        await using var context = await DbFactory.CreateDbContextAsync();
        context.OperationLogs.RemoveRange(await context.OperationLogs.ToListAsync());
        await context.SaveChangesAsync();
    }

    private async Task<JsonElement> GetLogsDataAsync()
    {
        SetIdentity("Founder", "0000000000");
        var response = await _client.GetAsync("/OperationLogs?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        return envelope.GetProperty("data");
    }

    private static StudentCreateDTO ValidCreate()
        => new()
        {
            UserId = NewUserId,
            UserName = "新成员",
            Academy = "计算机与信息工程学院",
            PoliticalLandscape = "群众",
            Gender = "男",
            ClassName = "计科2301",
            PhoneNum = "13800138000",
            Password = InitialPassword,
            EMail = null
        };

    [Fact]
    public async Task CreateMember_ProducesRedactedAuditRow()
    {
        await SeedStaffAsync();
        await ClearLogsAsync();

        SetIdentity("President", PresidentUserId);
        var response = await _client.PostAsJsonAsync("/MemberManagement/create", ValidCreate());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await GetLogsDataAsync();
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());

        var item = page.GetProperty("items")[0];
        Assert.Equal("成员管理", item.GetProperty("module").GetString());
        Assert.Equal("新增成员", item.GetProperty("action").GetString());
        Assert.True(item.GetProperty("success").GetBoolean());
        Assert.Equal(200, item.GetProperty("httpStatus").GetInt32());
        Assert.Equal(PresidentUserId, item.GetProperty("operatorId").GetString());
        Assert.Equal("社长", item.GetProperty("operatorName").GetString());

        var content = item.GetProperty("changeContent").GetString();
        Assert.NotNull(content);
        Assert.Contains(NewUserId, content);
        Assert.Contains("新成员", content);
        Assert.DoesNotContain(InitialPassword, content);
        Assert.DoesNotContain("13800138000", content);
        Assert.Contains("138****8000", content);
    }

    [Fact]
    public async Task FailedOperation_IsRecordedAsFailure()
    {
        await SeedStaffAsync();
        await ClearLogsAsync();

        SetIdentity("President", PresidentUserId);
        var response = await _client.PostAsync("/MemberManagement/delete/9999999999", null);
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);

        var page = await GetLogsDataAsync();
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());

        var item = page.GetProperty("items")[0];
        Assert.False(item.GetProperty("success").GetBoolean());
        Assert.Equal("删除成员", item.GetProperty("action").GetString());
        Assert.Contains("9999999999", item.GetProperty("target").GetString());
    }

    [Fact]
    public async Task MalformedJson_IsRecordedAsFailure()
    {
        await ClearLogsAsync();

        SetIdentity("President", PresidentUserId);
        var content = new StringContent("{", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/MemberManagement/create", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var page = await GetLogsDataAsync();
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());

        var item = page.GetProperty("items")[0];
        Assert.False(item.GetProperty("success").GetBoolean());
        Assert.Equal(400, item.GetProperty("httpStatus").GetInt32());
    }

    [Fact]
    public async Task ReadsAndAuthEndpoints_AreNotRecorded()
    {
        await ClearLogsAsync();

        SetIdentity("President", PresidentUserId);
        await _client.GetAsync("/Category/all");
        await _client.PostAsync("/Auth/login",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        var page = await GetLogsDataAsync();
        Assert.Equal(0, page.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task MutatingGetDeletes_AreRecorded()
    {
        await ClearLogsAsync();

        SetIdentity("Founder", "0000000000");
        await _client.GetAsync("/Category/Delete/no-such-category");
        await _client.GetAsync("/Department/Delete/no-such-department");
        await _client.GetAsync("/Staff/Delete/9999999999");

        var page = await GetLogsDataAsync();
        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());

        var actions = page.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("action").GetString())
            .ToList();
        Assert.Contains("删除分类", actions);
        Assert.Contains("删除部门", actions);
        Assert.Contains("删除员工", actions);
        Assert.All(page.GetProperty("items").EnumerateArray(),
            x => Assert.Equal("GET", x.GetProperty("method").GetString()));
    }

    [Fact]
    public async Task Multipart_RecordsMetadataAndKeepsBusinessResponseWorking()
    {
        await ClearLogsAsync();

        SetIdentity("President", PresidentUserId);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("abc")), "file", "test.xlsx");

        var response = await _client.PostAsync("/Activity/not-exist/import", form);
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);

        var page = await GetLogsDataAsync();
        var item = page.GetProperty("items")[0];
        var content = item.GetProperty("changeContent").GetString();
        Assert.NotNull(content);
        Assert.Contains("multipart/form-data", content);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

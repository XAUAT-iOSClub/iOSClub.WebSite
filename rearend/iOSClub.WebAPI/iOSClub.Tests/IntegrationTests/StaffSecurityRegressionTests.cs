using System.Net;
using System.Net.Http.Json;
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

public class StaffSecurityRegressionTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly DbContextOptions<ClubContext> _options;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public StaffSecurityRegressionTests(WebApplicationFactory<Program> factory)
    {
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"StaffSecurity-{Guid.NewGuid()}")
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

    private async Task SeedAsync(string role)
    {
        _client.DefaultRequestHeaders.Add("X-Test-Identity", role);
        _client.DefaultRequestHeaders.Add("X-Test-UserId", "2023000001");
        await using var context = new ClubContext(_options);
        var department = new DepartmentDO { Key = "tech", Name = "技术部", Description = "技术部" };
        context.Departments.Add(department);
        context.Departments.Add(new DepartmentDO { Key = "design", Name = "设计部", Description = "设计部" });
        context.Staffs.AddRange(
            new StaffDO { UserId = "2023000000", Name = "创始人", Identity = "Founder" },
            new StaffDO { UserId = "2023000001", Name = "操作员", Identity = role, Department = department },
            new StaffDO { UserId = "2023000002", Name = "部员", Identity = "Department", Department = department });
        await context.SaveChangesAsync();
    }

    [Theory]
    [InlineData("Minister")]
    [InlineData("President")]
    [InlineData("Founder")]
    public async Task Create_FounderThroughOrdinaryEndpoint_IsRejected(string role)
    {
        await SeedAsync(role);
        var response = await _client.PostAsJsonAsync("/Staff/Create", new StaffCreateDTO
        {
            UserId = "2023000003", Name = "新创始人", Identity = "Founder"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var context = new ClubContext(_options);
        Assert.False(await context.Staffs.AnyAsync(s => s.UserId == "2023000003"));
    }

    [Theory]
    [InlineData("Minister", "2023000001", "Founder")]
    [InlineData("President", "2023000001", "Founder")]
    [InlineData("Minister", "2023000002", "Founder")]
    [InlineData("President", "2023000002", "Founder")]
    [InlineData("Minister", "2023000000", "President")]
    [InlineData("President", "2023000000", "President")]
    [InlineData("Founder", "2023000000", "President")]
    [InlineData("Minister", "2023000000", "Founder")]
    [InlineData("Minister", "2023000002", "UnknownRole")]
    [InlineData("Minister", "2023000001", "President")]
    public async Task Update_ProtectedRoleChange_IsRejected(string role, string targetId, string identity)
    {
        await SeedAsync(role);
        await using var before = new ClubContext(_options);
        var original = await before.Staffs.AsNoTracking().SingleAsync(s => s.UserId == targetId);

        var response = await _client.PostAsJsonAsync("/Staff/Update", new StaffCreateDTO
        {
            UserId = targetId, Name = "已篡改", Identity = identity
        });

        Assert.Equal(identity == "UnknownRole" ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden, response.StatusCode);
        await using var context = new ClubContext(_options);
        var saved = await context.Staffs.SingleAsync(s => s.UserId == targetId);
        Assert.Equal(original.Name, saved.Name);
        Assert.Equal(original.Identity, saved.Identity);
    }

    [Theory]
    [InlineData("Minister", "2023000000")]
    [InlineData("President", "2023000000")]
    [InlineData("Founder", "2023000000")]
    [InlineData("Minister", "2023000001")]
    public async Task Delete_FounderOrSelf_IsRejected(string role, string targetId)
    {
        await SeedAsync(role);

        var response = await _client.GetAsync($"/Staff/Delete/{targetId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var context = new ClubContext(_options);
        Assert.True(await context.Staffs.AnyAsync(s => s.UserId == targetId));
        Assert.True(await context.Staffs.AnyAsync(s => s.Identity == "Founder"));
    }

    [Theory]
    [InlineData("Minister", "2023000000")]
    [InlineData("President", "2023000000")]
    [InlineData("Founder", "2023000000")]
    [InlineData("Minister", "2023000001")]
    public async Task ChangeDepartment_FounderOrSelf_IsRejected(string role, string targetId)
    {
        await SeedAsync(role);
        await using var before = new ClubContext(_options);
        var original = await before.Staffs.Include(s => s.Department).SingleAsync(s => s.UserId == targetId);
        var originalDepartment = original.Department?.Name;

        var response = await _client.PostAsync($"/Staff/change-department/{targetId}?departmentName={Uri.EscapeDataString("设计部")}", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var context = new ClubContext(_options);
        var saved = await context.Staffs.Include(s => s.Department).SingleAsync(s => s.UserId == targetId);
        Assert.Equal(originalDepartment, saved.Department?.Name);
    }

    [Fact]
    public async Task AssignRole_SelfPromotion_IsRejected()
    {
        await SeedAsync("Minister");

        var response = await _client.PostAsJsonAsync("/Staff/assign-role", new StaffRoleAssignDTO
        {
            UserId = "2023000001", Identity = "President"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var context = new ClubContext(_options);
        Assert.Equal("Minister", (await context.Staffs.SingleAsync(s => s.UserId == "2023000001")).Identity);
    }

    [Fact]
    public async Task OrdinaryStaffMaintenance_StillCreatesUpdatesAndDeletesNonFounder()
    {
        await SeedAsync("Minister");
        var create = await _client.PostAsJsonAsync("/Staff/Create", new StaffCreateDTO
        {
            UserId = "2023000003", Name = "新部员", Identity = "Department", DepartmentName = "技术部"
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var update = await _client.PostAsJsonAsync("/Staff/Update", new StaffCreateDTO
        {
            UserId = "2023000003", Name = "新部长", Identity = "Minister", DepartmentName = "技术部"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        await using (var context = new ClubContext(_options))
        {
            var saved = await context.Staffs.SingleAsync(s => s.UserId == "2023000003");
            Assert.Equal("新部长", saved.Name);
            Assert.Equal("Minister", saved.Identity);
        }

        var delete = await _client.GetAsync("/Staff/Delete/2023000003");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        await using var verify = new ClubContext(_options);
        Assert.False(await verify.Staffs.AnyAsync(s => s.UserId == "2023000003"));
    }

    [Fact]
    public async Task FounderPermissionEndpoint_StillPromotesAnotherMember()
    {
        await SeedAsync("Founder");

        var response = await _client.PostAsJsonAsync("/FounderPermission/update-role", new
        {
            UserId = "2023000002", Identity = "Founder", DepartmentName = (string?)null
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var context = new ClubContext(_options);
        Assert.Equal("Founder", (await context.Staffs.SingleAsync(s => s.UserId == "2023000002")).Identity);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

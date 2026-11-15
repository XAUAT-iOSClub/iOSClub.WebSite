using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.DataApi.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;

namespace iOSClub.Tests.IntegrationTests;

public sealed class ApiKeyTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<ClubContext> _options;
    private readonly WebApplicationFactory<Program> _factory;

    public ApiKeyTests(WebApplicationFactory<Program> factory)
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<ClubContext>().UseSqlite(_connection).Options;
        using var db = new ClubContext(_options);
        db.Database.EnsureCreated();

        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(Mock.Of<IDatabase>());
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing").ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextFactory<ClubContext>>();
            services.AddSingleton<IDbContextFactory<ClubContext>>(new TestDbContextFactory(_options));
            services.AddSingleton(redis.Object);
            services.AddSingleton(Mock.Of<IEmailService>());
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        }));
    }

    [Fact]
    public async Task BasicCredentialsRespectLifetimePermissionAndRevocation()
    {
        using var admin = _factory.CreateClient();
        using var keyClient = _factory.CreateClient();
        keyClient.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");

        var read = await CreateAsync(admin, ApiKeyPermission.ReadOnly, null);
        SetCredentials(keyClient, read.Id, read.Secret);
        Assert.Equal(HttpStatusCode.OK, (await keyClient.GetAsync("/Resource")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await keyClient.PostAsJsonAsync("/Resource", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await keyClient.GetAsync("/ApiKey")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await keyClient.GetAsync("/OperationLogs")).StatusCode);

        var write = await CreateAsync(admin, ApiKeyPermission.ReadWrite, DateTimeOffset.UtcNow.AddDays(1));
        SetCredentials(keyClient, write.Id, write.Secret);
        using var created = await keyClient.PostAsJsonAsync("/Resource", new
        {
            name = "API Key test", url = "https://example.com", description = "Integration test", tags = "test"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);

        var listed = await admin.GetStringAsync("/ApiKey");
        Assert.Contains(read.Id, listed);
        Assert.DoesNotContain(read.Secret, listed);
        Assert.DoesNotContain(write.Secret, listed);

        await using (var db = new ClubContext(_options))
        {
            Assert.Single(await db.OperationLogs.Where(log => log.OperatorId == write.Id).ToListAsync());
            await db.ApiKeys.Where(key => key.Id == Guid.Parse(write.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(key => key.ExpiresAt, DateTime.UtcNow.AddDays(-1)));
        }
        using var expired = await keyClient.GetAsync("/Resource");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Equal("Basic", Assert.Single(expired.Headers.WwwAuthenticate).Scheme);

        SetCredentials(keyClient, read.Id, new string('0', 64));
        Assert.Equal(HttpStatusCode.Unauthorized, (await keyClient.GetAsync("/Resource")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/ApiKey/{read.Id}")).StatusCode);
        SetCredentials(keyClient, read.Id, read.Secret);
        Assert.Equal(HttpStatusCode.Unauthorized, (await keyClient.GetAsync("/Resource")).StatusCode);
    }

    [Fact]
    public async Task OnlyAdminsCanManageKeysAndCreateRequiresValidPermissionAndExpiry()
    {
        using var member = _factory.CreateClient();
        member.DefaultRequestHeaders.Add("X-Test-Identity", "Minister");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/ApiKey")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync("/ApiKey", new
        {
            name = "unauthorized", permission = 1
        })).StatusCode);

        using var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Test-Identity", "President");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/ApiKey")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/ApiKey", new
        {
            name = "expired", permission = 1, expiresAt = DateTimeOffset.UtcNow.AddDays(-1)
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/ApiKey", new
        {
            name = "invalid", permission = 99
        })).StatusCode);
    }

    private static async Task<(string Id, string Secret)> CreateAsync(
        HttpClient client, ApiKeyPermission permission, DateTimeOffset? expiresAt)
    {
        using var response = await client.PostAsJsonAsync("/ApiKey", new { name = "test", permission, expiresAt });
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");
        return (data.GetProperty("id").GetString()!, data.GetProperty("secret").GetString()!);
    }

    private static void SetCredentials(HttpClient client, string id, string secret) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{id}:{secret}")));

    public void Dispose()
    {
        _factory.Dispose();
        _connection.Dispose();
    }
}

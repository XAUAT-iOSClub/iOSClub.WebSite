using System.Net.Http.Json;
using System.Text.Json;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;

namespace iOSClub.Tests.IntegrationTests;

/// <summary>
/// 资源创建接口的回归测试（C24）。
/// <para>
/// 回归背景：服务端在映射出的 DO 上生成资源 Id，控制器却用请求 DTO 的空 Id 组装
/// 201 响应与 Location，调用方拿到的 id 无法立即查询、更新或删除新资源。
/// </para>
/// </summary>
public class ResourceCreationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string FounderUserId = "0000000000";
    private const string ResourceName = "C24 回归资源";

    private readonly DbContextOptions<ClubContext> _options;
    private readonly WebApplicationFactory<Program> _factory;

    public ResourceCreationTests(WebApplicationFactory<Program> factory)
    {
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"ResourceCreation-{Guid.NewGuid()}")
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
    }

    private async Task SeedAsync()
    {
        await using var context = new ClubContext(_options);
        context.Staffs.Add(new StaffDO { UserId = FounderUserId, Name = "创始人", Identity = "Founder" });
        await context.SaveChangesAsync();
    }

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Identity", "Founder");
        client.DefaultRequestHeaders.Add("X-Test-UserId", FounderUserId);
        return client;
    }

    [Fact]
    public async Task CreatedResource_Id_CanImmediatelyBeQueriedUpdatedAndDeleted()
    {
        await SeedAsync();
        using var client = CreateClient();

        // 创建资源
        var createBody = await SendAsync(client, HttpMethod.Post, "/Resource", new ResourceCreateUpdateDTO
        {
            Name = ResourceName,
            Description = "验证创建响应回填服务端生成的 Id",
            Tag = "c24"
        });
        Assert.Equal(0, GetPropertyIgnoreCase(createBody, "errorCode").GetInt32());
        var createdId = GetPropertyIgnoreCase(GetPropertyIgnoreCase(createBody, "data"), "id").GetString();
        Assert.False(string.IsNullOrEmpty(createdId), $"创建资源的响应 id 为空：{createBody}");

        // 返回的 Id 必须能立即查询到同一条资源
        var getBody = await SendAsync(client, HttpMethod.Get, $"/Resource/{createdId}", null);
        Assert.Equal(0, GetPropertyIgnoreCase(getBody, "errorCode").GetInt32());
        var fetched = GetPropertyIgnoreCase(getBody, "data");
        Assert.Equal(createdId, GetPropertyIgnoreCase(fetched, "id").GetString());
        Assert.Equal(ResourceName, GetPropertyIgnoreCase(fetched, "name").GetString());

        // 返回的 Id 必须能立即更新
        const string updatedName = "C24 回归资源（已更新）";
        var updateBody = await SendAsync(client, HttpMethod.Put, "/Resource", new ResourceCreateUpdateDTO
        {
            Id = createdId,
            Name = updatedName,
            Description = "更新后的描述",
            Tag = "c24-updated"
        });
        Assert.Equal(0, GetPropertyIgnoreCase(updateBody, "errorCode").GetInt32());

        var getUpdatedBody = await SendAsync(client, HttpMethod.Get, $"/Resource/{createdId}", null);
        Assert.Equal(0, GetPropertyIgnoreCase(getUpdatedBody, "errorCode").GetInt32());
        Assert.Equal(updatedName,
            GetPropertyIgnoreCase(GetPropertyIgnoreCase(getUpdatedBody, "data"), "name").GetString());

        // 返回的 Id 必须能立即删除，删除后查询返回资源不存在
        var deleteBody = await SendAsync(client, HttpMethod.Delete, $"/Resource/{createdId}", null);
        Assert.Equal(0, GetPropertyIgnoreCase(deleteBody, "errorCode").GetInt32());

        var getDeletedBody = await SendAsync(client, HttpMethod.Get, $"/Resource/{createdId}", null);
        Assert.Equal(ErrorCode.ResourceNotFound, GetPropertyIgnoreCase(getDeletedBody, "errorCode").GetInt32());
    }

    private static async Task<JsonElement> SendAsync(HttpClient client, HttpMethod method, string url, object? payload)
    {
        using var request = new HttpRequestMessage(method, url);
        if (payload != null)
        {
            request.Content = JsonContent.Create(payload);
        }

        using var response = await client.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(raw), $"{method} {url} 响应体为空（HTTP {(int)response.StatusCode}）");
        using var document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    private static JsonElement GetPropertyIgnoreCase(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        throw new KeyNotFoundException($"响应中缺少字段 '{name}'：{element}");
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}

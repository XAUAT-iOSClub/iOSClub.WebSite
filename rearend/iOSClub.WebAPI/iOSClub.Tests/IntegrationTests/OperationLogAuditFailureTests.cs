using System.Net;
using System.Net.Http.Json;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.DataApi.Repositories;
using iOSClub.DataApi.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;

namespace iOSClub.Tests.IntegrationTests;

/// <summary>
/// 审计写入失败不能影响业务响应：仓库抛异常时，成员创建仍应成功。
/// </summary>
public class OperationLogAuditFailureTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly Mock<IOperationLogRepository> _repository = new();

    public OperationLogAuditFailureTests(WebApplicationFactory<Program> factory)
    {
        _repository.Setup(r => r.AddAsync(It.IsAny<OperationLogDO>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit database is down"));

        var options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"OperationLogAuditFailure-{Guid.NewGuid()}")
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

            services.RemoveAll<IOperationLogRepository>();
            services.AddScoped(_ => _repository.Object);

            services.AddAuthentication(authentication =>
            {
                authentication.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                authentication.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        }));

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task AuditWriteFailure_DoesNotBreakBusinessResponse()
    {
        _client.DefaultRequestHeaders.Add("X-Test-Identity", "President");
        _client.DefaultRequestHeaders.Add("X-Test-UserId", "2024000003");

        var response = await _client.PostAsJsonAsync("/MemberManagement/create", new StudentCreateDTO
        {
            UserId = "2024999999",
            UserName = "新成员",
            Academy = "计算机与信息工程学院",
            PoliticalLandscape = "群众",
            Gender = "男",
            ClassName = "计科2301",
            PhoneNum = "13800138000",
            Password = "InitPass123"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _repository.Verify(r => r.AddAsync(It.IsAny<OperationLogDO>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

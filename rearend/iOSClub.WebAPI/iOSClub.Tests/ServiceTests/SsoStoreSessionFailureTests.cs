using System.Security.Claims;
using System.Text;
using System.Text.Json;
using iOSClub.DataApi.Repositories;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace iOSClub.Tests.ServiceTests;

/// <summary>
/// C29：/SSO/store-session 在写入 Redis 会话时若内部缓存抛异常，旧实现只记录日志
/// 仍返回 success=true，前端会继续成功回调而后端实际没有会话。这里锁定
/// “Redis 写入失败必须返回 HTTP 500 + success=false”，成功路径保持原响应。
/// </summary>
public class SsoStoreSessionFailureTests
{
    private const string UserId = "2024000001";
    private const string BearerToken = "test-access-token";

    private readonly Mock<IDatabase> _database = new();
    private readonly Mock<ILoginService> _loginService = new();
    private readonly SSOController _controller;

    public SsoStoreSessionFailureTests()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_database.Object);

        _controller = new SSOController(
            _loginService.Object,
            Mock.Of<IStudentRepository>(),
            Mock.Of<IStaffRepository>(),
            Mock.Of<IClientApplicationRepository>(),
            redis.Object,
            new ConfigurationBuilder().Build(),
            NullLogger<SSOController>.Instance,
            null!);
    }

    private static DefaultHttpContext AuthenticatedContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {BearerToken}";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, UserId),
            new Claim(ClaimTypes.Role, "Member")
        ], "JWT"));
        return context;
    }

    private static string EncodeState(string state) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new SSOController.AuthState { State = state, ClientId = "demo" })));

    private async Task<IActionResult> StoreSessionAsync(string state)
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = AuthenticatedContext() };
        return await _controller.StoreSession(new SSOController.StoreSessionRequest
        {
            State = state,
            ClientId = "demo"
        });
    }

    private void SetupRedisFailures()
    {
        var failure = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "模拟会话缓存故障");
        // 控制器调用的是 (key, value, TimeSpan) 形态，实际绑定到 Expiration 重载。
        _database.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(failure);
        _database.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(failure);
        _database.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<When>()))
            .ThrowsAsync(failure);
        _database.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(failure);
    }

    [Fact]
    public async Task StoreSession_RedisWriteFails_ReturnsServerError()
    {
        SetupRedisFailures();

        var result = await StoreSessionAsync(EncodeState("state-1"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(objectResult.Value));
        Assert.False(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("会话存储失败", document.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task StoreSession_RedisWriteSucceeds_ReturnsSuccess()
    {
        _database.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await StoreSessionAsync(EncodeState("state-1"));

        var ok = Assert.IsType<OkObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("会话存储成功", document.RootElement.GetProperty("message").GetString());
        // 会话必须真正落到 oauth:auth:{state} 键，前端回调才能取到用户信息。
        _database.Verify(d => d.StringSetAsync(
            It.Is<RedisKey>(key => key == "oauth:auth:state-1"),
            It.IsAny<RedisValue>(), It.IsAny<Expiration>(), It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task StoreSession_WithoutState_ReturnsSuccessWithoutRedisWrite()
    {
        var result = await StoreSessionAsync("");

        var ok = Assert.IsType<OkObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        _database.Verify(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()), Times.Never);
    }
}

using System.Text.Json;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.DataApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace iOSClub.Tests.ServiceTests;

/// <summary>
/// C22：/SSO/token 需要支持 refresh_token 授权。刷新令牌是不透明 GUID，
/// 刷新请求只有令牌本身、没有 userId，因此 LoginService 必须维护
/// refresh-owner:{refreshToken} -> userId 的反向索引，并在签发、轮换、
/// 登出时同步维护，否则刷新端点无法定位账号。
/// </summary>
public class LoginServiceRefreshTokenIndexTests
{
    private const string UserId = "2024000001";
    private const string RefreshOwnerPrefix = "refresh-owner:";
    private const string RefreshTokenPrefix = "refresh:";
    private const string UserInfoPrefix = "user:";

    private readonly Dictionary<string, RedisValue> _cache = new();
    private readonly Mock<IStudentRepository> _studentRepository = new();
    private readonly Mock<IStaffRepository> _staffRepository = new();
    private readonly Mock<ITokenGenerator> _tokenGenerator = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IClientApplicationRepository> _clientAppRepository = new();
    private readonly Mock<IDatabase> _database = new();
    private readonly LoginService _loginService;

    public LoginServiceRefreshTokenIndexTests()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_database.Object);

        // 用字典模拟 Redis，断言的是真实写入/删除的键，而不是 mock 调用次数。
        _database.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
                _cache.TryGetValue(key.ToString(), out var value) ? value : RedisValue.Null);
        _database.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Callback((RedisKey key, RedisValue value, TimeSpan? _, When _, CommandFlags _) =>
                _cache[key.ToString()] = value)
            .ReturnsAsync(true);
        _database.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .Callback((RedisKey key, RedisValue value, Expiration _, ValueCondition _, CommandFlags _) =>
                _cache[key.ToString()] = value)
            .ReturnsAsync(true);
        _database.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) => _cache.Remove(key.ToString()));
        _database.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey[] keys, CommandFlags _) => (long)keys.Count(key => _cache.Remove(key.ToString())));
        _database.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Login/StoreMemberToken 走 CreateBatch 合并写入；batch 在 Execute 前不生效，
        // 这里直接在 StringSetAsync 回调里落库，等价于批量写入后的最终状态。
        var batch = new Mock<IBatch>();
        batch.Setup(b => b.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Callback((RedisKey key, RedisValue value, TimeSpan? _, When _, CommandFlags _) =>
                _cache[key.ToString()] = value)
            .ReturnsAsync(true);
        batch.Setup(b => b.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .Callback((RedisKey key, RedisValue value, Expiration _, ValueCondition _, CommandFlags _) =>
                _cache[key.ToString()] = value)
            .ReturnsAsync(true);
        _database.Setup(d => d.CreateBatch(It.IsAny<object>())).Returns(batch.Object);

        _loginService = new LoginService(
            _studentRepository.Object,
            _staffRepository.Object,
            _tokenGenerator.Object,
            redis.Object,
            _emailService.Object,
            _clientAppRepository.Object,
            Mock.Of<ILogger<LoginService>>());
    }

    [Fact]
    public async Task Login_WritesReverseIndexMappingRefreshTokenToUserId()
    {
        var student = new StudentDO { UserId = UserId, UserName = "测试同学" };
        _studentRepository.Setup(r => r.LoginAndGetStudentAsync(UserId, "password123")).ReturnsAsync(student);
        _staffRepository.Setup(r => r.GetStaffByIdWithoutOtherData(UserId)).ReturnsAsync((StaffDO?)null);
        _tokenGenerator.Setup(t => t.GetMemberToken(It.IsAny<MemberVO>(), It.IsAny<bool>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .Returns(("access-token-1", "refresh-token-1"));

        await _loginService.Login(new LoginDTO { UserId = UserId, Password = "password123" });

        Assert.True(_cache.ContainsKey($"{RefreshOwnerPrefix}refresh-token-1"));
        Assert.Equal(UserId, _cache[$"{RefreshOwnerPrefix}refresh-token-1"].ToString());
    }

    [Fact]
    public async Task StoreMemberToken_WritesReverseIndexMappingRefreshTokenToUserId()
    {
        _studentRepository.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(new StudentDO { UserId = UserId, UserName = "测试同学" });
        _staffRepository.Setup(r => r.GetStaffByIdWithoutOtherData(UserId)).ReturnsAsync((StaffDO?)null);

        await _loginService.StoreMemberToken(UserId, "signup-access-token", "signup-refresh-token");

        Assert.True(_cache.ContainsKey($"{RefreshOwnerPrefix}signup-refresh-token"));
        Assert.Equal(UserId, _cache[$"{RefreshOwnerPrefix}signup-refresh-token"].ToString());
    }

    [Fact]
    public async Task Logout_DeletesReverseIndexOfStoredRefreshToken()
    {
        _cache[$"{RefreshTokenPrefix}{UserId}"] = "refresh-token-1";
        _cache[$"{RefreshOwnerPrefix}refresh-token-1"] = UserId;

        var result = await _loginService.Logout(UserId);

        Assert.True(result);
        Assert.False(_cache.ContainsKey($"{RefreshOwnerPrefix}refresh-token-1"));
        Assert.False(_cache.ContainsKey($"{RefreshTokenPrefix}{UserId}"));
    }

    [Fact]
    public async Task RefreshToken_Rotation_MovesReverseIndexToNewTokenAndRevokesOld()
    {
        _cache[$"{RefreshTokenPrefix}{UserId}"] = "old-refresh-token";
        _cache[$"{UserInfoPrefix}{UserId}"] = JsonSerializer.Serialize(new MemberVO
        {
            UserId = UserId, UserName = "测试同学", Identity = "Member"
        });
        _cache[$"{RefreshOwnerPrefix}old-refresh-token"] = UserId;
        _tokenGenerator.Setup(t => t.GetMemberToken(It.IsAny<MemberVO>(), It.IsAny<bool>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .Returns(("new-access-token", "new-refresh-token"));

        var newAccessToken = await _loginService.RefreshToken(UserId, "old-refresh-token");

        Assert.Equal("new-access-token", newAccessToken);
        Assert.True(_cache.TryGetValue($"{RefreshOwnerPrefix}new-refresh-token", out var newOwner));
        Assert.Equal(UserId, newOwner.ToString());
        Assert.False(_cache.ContainsKey($"{RefreshOwnerPrefix}old-refresh-token"));
        // 旧令牌已被轮换，反向索引也必须失效，避免刷新端点再次定位到该账号。
        Assert.Equal("", await _loginService.GetUserIdByRefreshToken("old-refresh-token"));
        Assert.Equal(UserId, await _loginService.GetUserIdByRefreshToken("new-refresh-token"));
    }

    [Fact]
    public async Task GetUserIdByRefreshToken_AfterLogin_ResolvesUserId()
    {
        var student = new StudentDO { UserId = UserId, UserName = "测试同学" };
        _studentRepository.Setup(r => r.LoginAndGetStudentAsync(UserId, "password123")).ReturnsAsync(student);
        _staffRepository.Setup(r => r.GetStaffByIdWithoutOtherData(UserId)).ReturnsAsync((StaffDO?)null);
        _tokenGenerator.Setup(t => t.GetMemberToken(It.IsAny<MemberVO>(), It.IsAny<bool>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .Returns(("access-token-1", "refresh-token-1"));

        await _loginService.Login(new LoginDTO { UserId = UserId, Password = "password123" });

        Assert.Equal(UserId, await _loginService.GetUserIdByRefreshToken("refresh-token-1"));
    }

    [Fact]
    public async Task GetUserIdByRefreshToken_UnknownToken_ReturnsEmpty()
    {
        Assert.Equal("", await _loginService.GetUserIdByRefreshToken("not-issued-by-this-server"));
        Assert.Equal("", await _loginService.GetUserIdByRefreshToken(""));
    }
}

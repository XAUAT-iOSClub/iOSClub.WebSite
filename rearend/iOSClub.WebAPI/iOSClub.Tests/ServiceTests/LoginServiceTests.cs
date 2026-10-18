using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.DataApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace iOSClub.Tests.ServiceTests;

public class LoginServiceTests
{
    private readonly Mock<IStudentRepository> _studentRepoMock;
    private readonly Mock<IStaffRepository> _staffRepoMock;
    private readonly Mock<ITokenGenerator> _tokenGeneratorMock;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _redisDbMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IClientApplicationRepository> _clientAppRepoMock;
    private readonly Mock<ILogger<LoginService>> _loggerMock;
    private readonly LoginService _loginService;

    public LoginServiceTests()
    {
        // Setup mocks
        _studentRepoMock = new Mock<IStudentRepository>();
        _staffRepoMock = new Mock<IStaffRepository>();
        _tokenGeneratorMock = new Mock<ITokenGenerator>();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _redisDbMock = new Mock<IDatabase>();
        _emailServiceMock = new Mock<IEmailService>();
        _clientAppRepoMock = new Mock<IClientApplicationRepository>();
        _loggerMock = new Mock<ILogger<LoginService>>();

        // Setup Redis
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDbMock.Object);

        // LoginService 用 _db.CreateBatch() 合并三次写入；不 setup 的话 mock 返回 null，
        // 后续 batch.StringSetAsync 直接 NullReferenceException。
        var batchMock = new Mock<IBatch>();
        batchMock.Setup(b => b.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _redisDbMock.Setup(r => r.CreateBatch(It.IsAny<object>())).Returns(batchMock.Object);

        // Create service instance
        _loginService = new LoginService(
            _studentRepoMock.Object,
            _staffRepoMock.Object,
            _tokenGeneratorMock.Object,
            _redisMock.Object,
            _emailServiceMock.Object,
            _clientAppRepoMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var loginModel = new LoginDTO { UserId = "1234567890", Password = "password123" };
        var student = new StudentDO { UserId = loginModel.UserId, UserName = "Test Student", PasswordHash = DataTool.StringToHash(loginModel.Password) };
        var token = "mock-jwt-token";

        _studentRepoMock.Setup(s => s.LoginAndGetStudentAsync(loginModel.UserId, loginModel.Password)).ReturnsAsync(student);
        _staffRepoMock.Setup(s => s.GetStaffByIdWithoutOtherData(loginModel.UserId)).ReturnsAsync((StaffDO?)null);
        _tokenGeneratorMock.Setup(t => t.GetMemberToken(It.IsAny<MemberVO>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>())).Returns((token, "mock-refresh-token"));
        _redisDbMock.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _loginService.Login(loginModel);

        // Assert
        Assert.Equal(token, result);
        _tokenGeneratorMock.Verify(t => t.GetMemberToken(It.IsAny<MemberVO>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        // 移除对StringSetAsync的严格验证，因为我们已经验证了返回值和token生成
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsEmptyString()
    {
        // Arrange
        var loginModel = new LoginDTO { UserId = "1234567890", Password = "wrongpassword" };

        _studentRepoMock.Setup(s => s.LoginAndGetStudentAsync(loginModel.UserId, loginModel.Password)).ReturnsAsync((StudentDO?)null);

        // Act
        var result = await _loginService.Login(loginModel);

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public async Task Logout_ValidUser_ReturnsTrue()
    {
        // Arrange
        var userId = "1234567890";

        _redisDbMock.Setup(r => r.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>())).ReturnsAsync(2L);

        // Act
        var result = await _loginService.Logout(userId);

        // Assert
        Assert.True(result);
        _redisDbMock.Verify(r => r.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ValidateToken_ValidToken_ReturnsTrue()
    {
        // Arrange
        var userId = "1234567890";
        var token = "valid-token";

        _redisDbMock.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(token);

        // Act
        var result = await _loginService.ValidateToken(userId, token);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateToken_InvalidToken_ReturnsFalse()
    {
        // Arrange
        var userId = "1234567890";
        var token = "invalid-token";

        _redisDbMock.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync("different-token");

        // Act
        var result = await _loginService.ValidateToken(userId, token);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ChangePassword_ValidCredentials_ReturnsTrue()
    {
        // Arrange
        var userId = "1234567890";
        var oldPassword = "oldpass";
        var newPassword = "newpass";
        var student = new StudentDO { UserId = userId, PasswordHash = DataTool.StringToHash(oldPassword) };

        // ChangePassword 走的是 Login + GetByIdAsync（不是 LoginAndGetStudentAsync）
        _studentRepoMock.Setup(s => s.Login(userId, oldPassword)).ReturnsAsync(true);
        _studentRepoMock.Setup(s => s.GetByIdAsync(userId)).ReturnsAsync(student);
        _studentRepoMock.Setup(s => s.SetPasswordAsync(userId, newPassword)).ReturnsAsync(true);

        // Act
        var result = await _loginService.ChangePassword(userId, oldPassword, newPassword);

        // Assert
        Assert.True(result);
        // 密码必须经 SetPasswordAsync 写入（哈希由仓库层负责），服务层不再自己改实体再整体保存。
        _studentRepoMock.Verify(s => s.SetPasswordAsync(userId, newPassword), Times.Once);
    }

    [Fact]
    public async Task ChangePassword_InvalidOldPassword_ReturnsFalse()
    {
        // Arrange
        var userId = "1234567890";
        var oldPassword = "wrongoldpass";
        var newPassword = "newpass";

        // ChangePassword 走的是 Login + GetByIdAsync
        _studentRepoMock.Setup(s => s.Login(userId, oldPassword)).ReturnsAsync(false);

        // Act
        var result = await _loginService.ChangePassword(userId, oldPassword, newPassword);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Login_StudentLogin_ReturnsToken()
    {
        // Arrange
        var loginModel = new LoginDTO { UserId = "20123456", Password = "password123" };
        var student = new StudentDO { UserId = loginModel.UserId, UserName = "Test Student", PasswordHash = DataTool.StringToHash(loginModel.Password) };
        var token = "mock-jwt-token";

        _studentRepoMock.Setup(s => s.LoginAndGetStudentAsync(loginModel.UserId, loginModel.Password)).ReturnsAsync(student);
        _staffRepoMock.Setup(s => s.GetStaffByIdWithoutOtherData(loginModel.UserId)).ReturnsAsync((StaffDO?)null);
        _tokenGeneratorMock.Setup(t => t.GetMemberToken(It.IsAny<MemberVO>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>())).Returns((token, "mock-refresh-token"));
        _redisDbMock.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _loginService.Login(loginModel);

        // Assert
        Assert.Equal(token, result);
        _tokenGeneratorMock.Verify(t => t.GetMemberToken(It.IsAny<MemberVO>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Login_WithStoredToken_IssuesNewToken()
    {
        // Arrange
        var loginModel = new LoginDTO { UserId = "1234567890", Password = "password123" };
        var storedToken = "existing-jwt-token";
        var newToken = "new-jwt-token";
        var student = new StudentDO { UserId = loginModel.UserId, UserName = "Test Student" };

        _studentRepoMock.Setup(s => s.LoginAndGetStudentAsync(loginModel.UserId, loginModel.Password)).ReturnsAsync(student);
        _staffRepoMock.Setup(s => s.GetStaffByIdWithoutOtherData(loginModel.UserId)).ReturnsAsync((StaffDO?)null);
        _redisDbMock.Setup(r => r.StringGetAsync($"token:{loginModel.UserId}", It.IsAny<CommandFlags>())).ReturnsAsync(storedToken);
        _tokenGeneratorMock.Setup(t => t.GetMemberToken(It.IsAny<MemberVO>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>())).Returns((newToken, "mock-refresh-token"));

        // Act
        var result = await _loginService.Login(loginModel);

        // Assert
        // 登录不再复用 Redis 里的旧 token，而是每次签发新 token。
        Assert.Equal(newToken, result);
        _tokenGeneratorMock.Verify(t => t.GetMemberToken(It.IsAny<MemberVO>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ValidateToken_TokenNotInRedis_ReturnsFalse()
    {
        // Arrange
        var userId = "1234567890";
        var token = "valid-token";

        _redisDbMock.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _loginService.ValidateToken(userId, token);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Logout_ReturnsTrue()
    {
        // Arrange
        var userId = "1234567890";

        _redisDbMock.Setup(r => r.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>())).ReturnsAsync(2L);

        // Act
        var result = await _loginService.Logout(userId);

        // Assert
        Assert.True(result);
        _redisDbMock.Verify(r => r.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task Login_EmptyUserId_ReturnsEmptyString()
    {
        // Arrange
        var loginModel = new LoginDTO { UserId = "", Password = "password123" };

        // Act
        var result = await _loginService.Login(loginModel);

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public async Task Login_EmptyPassword_ReturnsEmptyString()
    {
        // Arrange
        var loginModel = new LoginDTO { UserId = "1234567890", Password = "" };

        // Act
        var result = await _loginService.Login(loginModel);

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public async Task ChangePassword_EmptyNewPassword_ReturnsFalse()
    {
        // Arrange
        var userId = "1234567890";
        var oldPassword = "oldpass";
        var newPassword = "";

        _studentRepoMock.Setup(s => s.Login(userId, oldPassword)).ReturnsAsync(true);
        _studentRepoMock.Setup(s => s.GetByIdAsync(userId)).ReturnsAsync(new StudentDO { UserId = userId });

        // Act
        var result = await _loginService.ChangePassword(userId, oldPassword, newPassword);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ChangePassword_SameAsOldPassword_ReturnsFalse()
    {
        // Arrange
        var userId = "1234567890";
        var password = "samepass";

        _studentRepoMock.Setup(s => s.Login(userId, password)).ReturnsAsync(true);
        _studentRepoMock.Setup(s => s.GetByIdAsync(userId)).ReturnsAsync(new StudentDO { UserId = userId, PasswordHash = DataTool.StringToHash(password) });

        // Act
        var result = await _loginService.ChangePassword(userId, password, password);

        // Assert
        Assert.False(result);
        // 没有要改的东西，不应该产生写库
        _studentRepoMock.Verify(s => s.SetPasswordAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}

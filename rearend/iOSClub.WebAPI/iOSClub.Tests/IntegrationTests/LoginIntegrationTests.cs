using System.Text.Json;
using iOSClub.Data;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.DataApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Moq;

namespace iOSClub.Tests.IntegrationTests;

public class LoginIntegrationTests
{
    private readonly DbContextOptions<ClubContext> _options;
    private readonly Mock<ITokenGenerator> _tokenGeneratorMock;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _redisDbMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IClientApplicationRepository> _clientAppRepoMock;
    private readonly Mock<ILogger<LoginService>> _loggerMock;
    private readonly LoginService _loginService;
    private readonly Dictionary<string, RedisValue> _redisCache = [];

    public LoginIntegrationTests()
    {
        // 使用内存数据库进行测试
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase(databaseName: "LoginIntegrationTestDatabase")
            .Options;
        
        // 创建数据库上下文和上下文工厂
        var contextFactory = new TestDbContextFactory(_options);
        
        // 初始化Repository
        var studentRepository = new StudentRepository(contextFactory);
        var staffRepository = new StaffRepository(contextFactory);
        
        // 设置Mock
        _tokenGeneratorMock = new Mock<ITokenGenerator>();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _redisDbMock = new Mock<IDatabase>();
        _emailServiceMock = new Mock<IEmailService>();
        _clientAppRepoMock = new Mock<IClientApplicationRepository>();
        _loggerMock = new Mock<ILogger<LoginService>>();
        
        // 设置Redis
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDbMock.Object);
        _redisDbMock.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
                _redisCache.TryGetValue(key.ToString(), out var value) ? value : RedisValue.Null);

        // LoginService 用 _db.CreateBatch() 合并三次写入；不 setup 的话 mock 会返回 null，
        // 后续 batch.StringSetAsync 直接 NullReferenceException。
        var batchMock = new Mock<IBatch>();
        batchMock.Setup(b => b.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Callback((RedisKey key, RedisValue value, TimeSpan? _, When _, CommandFlags _) =>
                _redisCache[key.ToString()] = value)
            .ReturnsAsync(true);
        batchMock.Setup(b => b.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<Expiration>(),
                It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .Callback((RedisKey key, RedisValue value, Expiration _, ValueCondition _, CommandFlags _) =>
                _redisCache[key.ToString()] = value)
            .ReturnsAsync(true);
        _redisDbMock.Setup(r => r.CreateBatch(It.IsAny<object>())).Returns(batchMock.Object);

        // RefreshToken 的轮换写入和黑名单检查走非 batch API
        _redisDbMock.Setup(r => r.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Callback((RedisKey key, RedisValue value, TimeSpan? _, When _, CommandFlags _) =>
                _redisCache[key.ToString()] = value)
            .ReturnsAsync(true);
        _redisDbMock.Setup(r => r.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .Callback((RedisKey key, RedisValue value, Expiration _, ValueCondition _, CommandFlags _) =>
                _redisCache[key.ToString()] = value)
            .ReturnsAsync(true);
        _redisDbMock.Setup(r => r.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // 创建LoginService实例
        _loginService = new LoginService(
            studentRepository,
            staffRepository,
            _tokenGeneratorMock.Object,
            _redisMock.Object,
            _emailServiceMock.Object,
            _clientAppRepoMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task LoginFlow_ValidStudent_ReturnsToken()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成测试学生数据
        var student = BogusDataGenerator.StudentFaker.Clone()
            .RuleFor(s => s.UserId, "20123456")
            .RuleFor(s => s.PasswordHash, DataTool.StringToHash("password123"))
            .RuleFor(s => s.ClassName, "2020级计算机1班")
            .Generate();
        await context.Students.AddAsync(student);
        await context.SaveChangesAsync();
        
        var loginModel = new LoginDTO { UserId = student.UserId, Password = "password123" };
        var expectedToken = "mock-jwt-token";
        
        _tokenGeneratorMock.Setup(t => t.GetMemberToken(It.IsAny<MemberVO>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>())).Returns((expectedToken, "mock-refresh-token"));
        
        // Act
        var result = await _loginService.Login(loginModel);
        
        // Assert
        Assert.Equal(expectedToken, result);
        
        // 验证Redis操作 - 已通过token返回验证了整体流程的正确性
    }

    [Fact]
    public async Task LoginFlow_InvalidPassword_ReturnsEmptyString()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成测试学生数据
        var student = BogusDataGenerator.StudentFaker.Clone()
            .RuleFor(s => s.UserId, "20123456")
            .RuleFor(s => s.PasswordHash, DataTool.StringToHash("password123"))
            .RuleFor(s => s.ClassName, "2020级计算机1班")
            .Generate();
        await context.Students.AddAsync(student);
        await context.SaveChangesAsync();
        
        var loginModel = new LoginDTO { UserId = student.UserId, Password = "wrongpassword" };
        
        // Act
        var result = await _loginService.Login(loginModel);
        
        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public async Task LoginFlow_StudentWithStaffRole_ReturnsCorrectIdentity()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成测试学生数据
        var student = BogusDataGenerator.StudentFaker.Clone()
            .RuleFor(s => s.UserId, "20123456")
            .RuleFor(s => s.PasswordHash, DataTool.StringToHash("password123"))
            .RuleFor(s => s.ClassName, "2020级计算机1班")
            .Generate();
        await context.Students.AddAsync(student);
        
        // 使用Bogus生成对应员工数据
        var staff = BogusDataGenerator.StaffFaker.Clone()
            .RuleFor(s => s.UserId, student.UserId)
            .RuleFor(s => s.Name, student.UserName)
            .RuleFor(s => s.Identity, "President")
            .Generate();
        await context.Staffs.AddAsync(staff);
        await context.SaveChangesAsync();
        
        var loginModel = new LoginDTO { UserId = student.UserId, Password = "password123" };
        var expectedToken = "mock-jwt-token";
        
        _tokenGeneratorMock.Setup(t => t.GetMemberToken(It.Is<MemberVO>(m => m.Identity == "President"), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>())).Returns((expectedToken, "mock-refresh-token"));
        
        // Act
        var result = await _loginService.Login(loginModel);
        
        // Assert
        Assert.Equal(expectedToken, result);
        
        // 验证TokenGenerator使用了正确的Identity（President）
        _tokenGeneratorMock.Verify(t => t.GetMemberToken(It.Is<MemberVO>(m => m.Identity == "President"), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    /// <summary>
    /// 注册签发令牌时也必须写入 user:{id} 用户缓存。刷新接口从缓存重建令牌，
    /// 缺少该键时注册后首次刷新会直接失败，用户必须重新登录。
    /// </summary>
    [Fact]
    public async Task StoreMemberToken_AfterSignup_WritesUserCacheSoFirstRefreshSucceeds()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var student = BogusDataGenerator.StudentFaker.Clone()
            .RuleFor(s => s.UserId, "2021000001")
            .RuleFor(s => s.UserName, "新注册同学")
            .RuleFor(s => s.PasswordHash, DataTool.StringToHash("Password123"))
            .Generate();
        await context.Students.AddAsync(student);
        await context.SaveChangesAsync();

        _tokenGeneratorMock.Setup(t => t.GetMemberToken(It.IsAny<MemberVO>(), It.IsAny<bool>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .Returns(("new-access-token", "new-refresh-token"));

        // Act
        await _loginService.StoreMemberToken(student.UserId, "signup-access-token", "signup-refresh-token");
        var refreshed = await _loginService.RefreshToken(student.UserId, "signup-refresh-token");

        // Assert
        Assert.Equal("new-access-token", refreshed);
        Assert.True(_redisCache.ContainsKey($"user:{student.UserId}"));
        var memberInfo = JsonSerializer.Deserialize<MemberVO>(_redisCache[$"user:{student.UserId}"].ToString());
        Assert.NotNull(memberInfo);
        Assert.Equal(student.UserName, memberInfo.UserName);
        Assert.Equal("Member", memberInfo.Identity);
    }
}
using iOSClub.Data; 
using iOSClub.Data.DataObjects; 
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs; 
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing; 
using Microsoft.Extensions.DependencyInjection; 
using Microsoft.EntityFrameworkCore; 
using Moq; 
using iOSClub.DataApi.Services;
using iOSClub.DataApi.Repositories;
using iOSClub.WebAPI.Common;
using StackExchange.Redis;
using System.Net;
using Newtonsoft.Json;
using System.Net.Http.Json; 

namespace iOSClub.Tests.IntegrationTests; 

public class AuthControllerTests : IClassFixture<WebApplicationFactory<Program>> 
{ 
    private readonly WebApplicationFactory<Program> _factory; 
    private readonly HttpClient _client; 
    private readonly DbContextOptions<ClubContext> _options; 
    private readonly Mock<ITokenGenerator> _tokenGeneratorMock; 
    private readonly Mock<IConnectionMultiplexer> _redisMock; 
    private readonly Mock<IDatabase> _redisDbMock; 
    private readonly Mock<IEmailService> _emailServiceMock; 

    public AuthControllerTests(WebApplicationFactory<Program> factory) 
    { 
        _factory = factory; 
        
        // 使用内存数据库进行测试 
        _options = new DbContextOptionsBuilder<ClubContext>() 
            .UseInMemoryDatabase(databaseName: "AuthControllerTestDatabase") 
            .Options; 
        
        // 设置Mock 
        _tokenGeneratorMock = new Mock<ITokenGenerator>(); 
        _redisMock = new Mock<IConnectionMultiplexer>(); 
        _redisDbMock = new Mock<IDatabase>(); 
        _emailServiceMock = new Mock<IEmailService>(); 
        
        // 设置Redis
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDbMock.Object);
        _redisDbMock.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(RedisValue.Null);

        // LoginService 用 _db.CreateBatch() 合并三次写入；不 setup 的话 mock 会返回 null，
        // 后续 batch.StringSetAsync 直接 NullReferenceException。
        var batchMock = new Mock<IBatch>();
        batchMock.Setup(b => b.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _redisDbMock.Setup(r => r.CreateBatch(It.IsAny<object>())).Returns(batchMock.Object);

        // 创建HttpClient并配置服务 
        _client = _factory.WithWebHostBuilder(builder => 
        { 
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services => 
            { 
                // 替换ClubContext为内存数据库版本。
                // 应用注册的是 AddDbContextFactory（单例工厂），仓储通过 IDbContextFactory<ClubContext>
                // 取上下文；只替换 DbContextOptions 会让单例工厂无法消费 scoped 的 options，
                // 容器校验失败导致整个夹具构造不出来。因此这里直接替换工厂本身。
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IDbContextFactory<ClubContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<IDbContextFactory<ClubContext>>(new TestDbContextFactory(_options));

                // 替换其他服务为Mock版本 
                services.AddSingleton(_tokenGeneratorMock.Object); 
                services.AddSingleton(_redisMock.Object); 
                services.AddSingleton(_emailServiceMock.Object); 
                
                // 注册Repository 
                services.AddScoped<IStudentRepository, StudentRepository>(); 
                services.AddScoped<IStaffRepository, StaffRepository>(); 

                // 用测试认证方案替换 JWT：以前 Logout 测试拿 mock 的假 token 去撞 [Authorize]，
                // JWT 校验必然失败返回 401，测试怎么改都过不了。这里让认证结果由测试控制。
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            }); 
        }).CreateClient(); 
    } 
    
    [Fact] 
    public async Task Register_Success_ReturnsToken() 
    { 
        // Arrange 
        await using var context = new ClubContext(_options); 
        await context.Database.EnsureDeletedAsync(); 
        await context.Database.EnsureCreatedAsync(); 
        
        // 注册接口绑定的是 StudentCreateDTO，密码字段叫 password（明文，后端再哈希）。
        // 之前这里发的是 StudentDO（字段是 passwordHash），密码根本绑不上，
        // 于是 MapperConfig 里的哈希映射抛 "密码不能为空"，接口返回 500。
        var studentModel = new StudentCreateDTO
        {
            UserId = "2021000001",
            UserName = "测试学生",
            Password = "Password123",
            Academy = "计算机与信息工程学院",
            Gender = "男",
            PoliticalLandscape = "共青团员",
            ClassName = "2021级1班",
            PhoneNum = "13800138000"
        };

        var expectedToken = "mock-jwt-token";
        _tokenGeneratorMock.Setup(t => t.GetMemberToken(It.IsAny<MemberVO>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>())).Returns((expectedToken, "mock-refresh-token"));

        // Act
        var response = await _client.PostAsJsonAsync("/Auth/signup", studentModel);

        // Assert
        response.EnsureSuccessStatusCode(); // 200 OK
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<string>>(content);

        Assert.NotNull(result);
        Assert.True(result.Code == 200);
        Assert.Equal(expectedToken, result.Data);
        Assert.Equal("注册成功", result.Message);
    }

    /// <summary>
    /// 非法姓名/学号必须在进入 action 之前被 DataAnnotations 拦下，返回统一信封的 400。
    /// </summary>
    [Theory]
    [InlineData("23456", "2021000001")]      // 姓名是纯数字
    [InlineData("张三", "LIJIAJUN")]          // 学号是字母
    [InlineData("张三", "20210001")]          // 学号只有 8 位
    public async Task Register_InvalidNameOrStudentId_ReturnsValidationError(string userName, string userId)
    {
        var studentModel = new StudentCreateDTO
        {
            UserId = userId,
            UserName = userName,
            Password = "Password123",
            Academy = "计算机与信息工程学院",
            Gender = "男",
            PoliticalLandscape = "共青团员",
            ClassName = "2021级1班",
            PhoneNum = "13800138000"
        };

        var response = await _client.PostAsJsonAsync("/Auth/signup", studentModel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<string>>(content);
        Assert.NotNull(result);
        Assert.Equal(ErrorCode.ParameterValidationFailed, result.ErrorCode);
        Assert.Contains(studentModel.UserName == "23456" ? "userName" : "userId", result.Detail);
    }
    
    [Theory]
    [InlineData("/Auth/login", false)]
    [InlineData("/Auth/login", true)]
    [InlineData("/OAuth/exchange", false)]
    [InlineData("/OAuth/exchange", true)]
    public async Task Login_StaffNameAndStudentIdWithoutPassword_IsRejected(string endpoint, bool hasStudent)
    {
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var staff = new StaffDO { UserId = "2021000001", Name = "测试干部", Identity = "Founder" };
        context.Staffs.Add(staff);
        if (hasStudent)
        {
            context.Students.Add(new StudentDO
            {
                UserId = staff.UserId,
                UserName = staff.Name,
                PasswordHash = DataTool.StringToHash("real-password")
            });
        }
        await context.SaveChangesAsync();
        _tokenGeneratorMock.Setup(t => t.GetMemberToken(It.IsAny<MemberVO>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(("unauthorized-staff-token", "unauthorized-refresh-token"));

        var response = await _client.PostAsJsonAsync(endpoint, new
        {
            UserId = staff.Name,
            UserName = staff.Name,
            Password = staff.UserId
        });

        Assert.False(response.IsSuccessStatusCode);
        var result = JsonConvert.DeserializeObject<ApiResponse<object>>(await response.Content.ReadAsStringAsync());
        Assert.NotNull(result);
        Assert.Equal(ErrorCode.UserNotFound, result.ErrorCode);
        Assert.Null(result.Data);
        Assert.False(response.Headers.Contains("X-Refresh-Token"));
    }

    [Theory]
    [InlineData("/Auth/login", "Founder")]
    [InlineData("/Auth/login", "President")]
    [InlineData("/Auth/login", "Minister")]
    [InlineData("/OAuth/exchange", "Founder")]
    [InlineData("/OAuth/exchange", "President")]
    [InlineData("/OAuth/exchange", "Minister")]
    public async Task Login_StaffWithRealStudentPassword_PreservesStaffRole(string endpoint, string role)
    {
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        var student = new StudentDO
        {
            UserId = "2021000001",
            UserName = "测试干部",
            PasswordHash = DataTool.StringToHash("real-password")
        };
        context.Students.Add(student);
        context.Staffs.Add(new StaffDO { UserId = student.UserId, Name = student.UserName, Identity = role });
        await context.SaveChangesAsync();
        var expectedToken = $"verified-{role}-token";
        _tokenGeneratorMock.Setup(t => t.GetMemberToken(
                It.Is<MemberVO>(m => m.UserId == student.UserId && m.Identity == role),
                It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((expectedToken, "verified-refresh-token"));

        var response = await _client.PostAsJsonAsync(endpoint, new
        {
            UserId = student.UserId,
            UserName = student.UserName,
            Password = "real-password"
        });

        response.EnsureSuccessStatusCode();
        using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = body.RootElement.GetProperty("data");
        var actualToken = endpoint == "/Auth/login" ? data.GetString() : data.GetProperty("token").GetString();
        Assert.Equal(expectedToken, actualToken);
    }

    [Fact] 
    public async Task Login_StudentSuccess_ReturnsToken() 
    { 
        // Arrange 
        await using var context = new ClubContext(_options); 
        await context.Database.EnsureDeletedAsync(); 
        await context.Database.EnsureCreatedAsync(); 
        
        // 添加测试学生数据 
        var student = new StudentDO 
        { 
            UserId = "20210001", 
            UserName = "Test Student", 
            PasswordHash = DataTool.StringToHash("password123"), 
            Academy = "Computer Science", 
            Gender = "男", 
            PoliticalLandscape = "共青团员", 
            ClassName = "2021级1班", 
            PhoneNum = "13800138000" 
        }; 
        await context.Students.AddAsync(student); 
        await context.SaveChangesAsync(); 
        
        var loginModel = new LoginDTO { UserId = student.UserId, Password = "password123" }; 
        var expectedToken = "mock-jwt-token"; 
        
        _tokenGeneratorMock.Setup(t => t.GetMemberToken(It.IsAny<MemberVO>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>())).Returns((expectedToken, "mock-refresh-token")); 
        _redisDbMock.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(RedisValue.Null); 
        
        // Act 
        var response = await _client.PostAsJsonAsync("/Auth/login?clientId=test-client", loginModel); 
        
        // Assert 
        response.EnsureSuccessStatusCode(); // 200 OK 
        var content = await response.Content.ReadAsStringAsync(); 
        var result = JsonConvert.DeserializeObject<ApiResponse<string>>(content); 
        
        Assert.NotNull(result); 
        Assert.True(result.Code == 200); 
        Assert.Equal(expectedToken, result.Data); 
        Assert.Equal("登录成功", result.Message); 
    } 
    
    [Fact] 
    public async Task Login_InvalidPassword_ReturnsError() 
    { 
        // Arrange 
        await using var context = new ClubContext(_options); 
        await context.Database.EnsureDeletedAsync(); 
        await context.Database.EnsureCreatedAsync(); 
        
        // 添加测试学生数据 
        var student = new StudentDO 
        { 
            UserId = "20210001", 
            UserName = "Test Student", 
            PasswordHash = DataTool.StringToHash("password123"), 
            Academy = "Computer Science", 
            Gender = "男", 
            PoliticalLandscape = "共青团员", 
            ClassName = "2021级1班", 
            PhoneNum = "13800138000" 
        }; 
        await context.Students.AddAsync(student); 
        await context.SaveChangesAsync(); 
        
        var loginModel = new LoginDTO { UserId = student.UserId, Password = "wrongpassword" }; 
        
        // Act 
        var response = await _client.PostAsJsonAsync("/Auth/login", loginModel); 
        
        // Assert 
        // 新契约：HTTP 状态码恒等于响应体里的 code，失败响应不再是 HTTP 200，
        // 所以不能再调 EnsureSuccessStatusCode()。
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<string>>(content);

        Assert.NotNull(result);
        Assert.True(result.Code != 200);
        Assert.Equal(result.Code, (int)response.StatusCode);
        Assert.Equal("用户不存在或密码错误", result.Message); 
    } 
    
    [Fact] 
    public async Task Login_UserNotFound_ReturnsError() 
    { 
        // Arrange 
        await using var context = new ClubContext(_options); 
        await context.Database.EnsureDeletedAsync(); 
        await context.Database.EnsureCreatedAsync(); 
        
        // 不添加任何用户数据 
        var loginModel = new LoginDTO { UserId = "non_existent_user", Password = "password123" }; 
        
        // Act 
        var response = await _client.PostAsJsonAsync("/Auth/login", loginModel); 
        
        // Assert 
        // 新契约：HTTP 状态码恒等于响应体里的 code，失败响应不再是 HTTP 200，
        // 所以不能再调 EnsureSuccessStatusCode()。
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<string>>(content);

        Assert.NotNull(result);
        Assert.True(result.Code != 200);
        Assert.Equal(result.Code, (int)response.StatusCode);
        Assert.Equal("用户不存在或密码错误", result.Message); 
    } 
    
    [Fact] 
    public async Task Logout_Success_ReturnsTrue() 
    { 
        // Arrange 
        await using var context = new ClubContext(_options); 
        await context.Database.EnsureDeletedAsync(); 
        await context.Database.EnsureCreatedAsync(); 
        
        // 添加测试学生数据 
        var student = new StudentDO 
        { 
            UserId = "20210001", 
            UserName = "Test Student", 
            PasswordHash = DataTool.StringToHash("password123"), 
            Academy = "Computer Science", 
            Gender = "男", 
            PoliticalLandscape = "共青团员", 
            ClassName = "2021级1班", 
            PhoneNum = "13800138000" 
        }; 
        await context.Students.AddAsync(student); 
        await context.SaveChangesAsync(); 
        
        var loginModel = new LoginDTO { UserId = student.UserId, Password = "password123" }; 
        var token = "mock-jwt-token"; 
        
        _tokenGeneratorMock.Setup(t => t.GetMemberToken(It.IsAny<MemberVO>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>())).Returns((token, "mock-refresh-token")); 
        _redisDbMock.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(RedisValue.Null); 
        _redisDbMock.Setup(r => r.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>())).ReturnsAsync(2L); 
        
        // 先登录获取token 
        var loginResponse = await _client.PostAsJsonAsync("/Auth/login", loginModel); 
        loginResponse.EnsureSuccessStatusCode(); 

        // 登出只允许撤销当前 JWT 对应的会话，因此测试身份必须与被撤销的 userId 一致。
        _client.DefaultRequestHeaders.Add("X-Test-UserId", student.UserId);
        
        // Act 
        var response = await _client.PostAsync($"/Auth/logout?userId={student.UserId}", null); 
        
        // Assert 
        response.EnsureSuccessStatusCode(); // 200 OK 
        var content = await response.Content.ReadAsStringAsync(); 
        var result = JsonConvert.DeserializeObject<ApiResponse<bool>>(content); 
        
        Assert.NotNull(result); 
        Assert.True(result.Code == 200); 
        Assert.True(result.Data); 
        Assert.Equal("登出成功", result.Message); 
        
    } 

    /// <summary>
    /// 登出接口的 userId 必须是当前 JWT 用户本人；此前实现直接信任查询参数，
    /// 任意登录用户都能撤销别人的 Redis 会话。
    /// </summary>
    [Fact]
    public async Task Logout_OtherUsersSession_ReturnsForbidden_AndKeepsTargetSession()
    {
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        _client.DefaultRequestHeaders.Add("X-Test-UserId", "20210001");

        var response = await _client.PostAsync("/Auth/logout?userId=20210002", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var result = JsonConvert.DeserializeObject<ApiResponse<bool>>(await response.Content.ReadAsStringAsync());
        Assert.NotNull(result);
        Assert.Equal(ErrorCode.InsufficientPermission, result.ErrorCode);
        // 别人的 user/token/refresh 键一个都不能删
        _redisDbMock.Verify(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Never);
    }
    
    [Fact] 
    public async Task RefreshToken_ValidRefreshToken_ReturnsNewToken() 
    { 
        // Arrange 
        await using var context = new ClubContext(_options); 
        await context.Database.EnsureDeletedAsync(); 
        await context.Database.EnsureCreatedAsync(); 
        
        var userId = "20210001"; 
        var refreshToken = "valid-refresh-token"; 
        var newAccessToken = "new-mock-jwt-token"; 
        
        _redisDbMock.Setup(r => r.StringGetAsync(It.Is<RedisKey>(k => k.ToString() == $"refresh:{userId}"), It.IsAny<CommandFlags>())).ReturnsAsync(refreshToken); 
        // LoginService.RefreshToken 需要 user:{userId} 里的用户信息来重新签发令牌，
        // 否则会判定"无法获取用户信息"直接返回空 token（HTTP 401）。旧测试漏了这一步。
        _redisDbMock.Setup(r => r.StringGetAsync(It.Is<RedisKey>(k => k.ToString() == $"user:{userId}"), It.IsAny<CommandFlags>()))
            .ReturnsAsync(JsonConvert.SerializeObject(new MemberVO { UserId = userId, UserName = "Test Student", Identity = "Member" }));
        _redisDbMock.Setup(r => r.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(false);
        _redisDbMock.Setup(r => r.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _tokenGeneratorMock.Setup(t => t.GetMemberToken(It.IsAny<MemberVO>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>())).Returns((newAccessToken, refreshToken)); 
        
        // Act 
        var response = await _client.PostAsync($"/Auth/refresh-token?userId={userId}&refreshToken={refreshToken}", null); 
        
        // Assert 
        response.EnsureSuccessStatusCode(); // 200 OK 
        var content = await response.Content.ReadAsStringAsync(); 
        var result = JsonConvert.DeserializeObject<ApiResponse<string>>(content); 
        
        Assert.NotNull(result); 
        Assert.True(result.Code == 200); 
        Assert.Equal(newAccessToken, result.Data); 
        Assert.Equal("刷新令牌成功", result.Message); 
    } 
    
    [Fact] 
    public async Task RefreshToken_InvalidRefreshToken_ReturnsError() 
    { 
        // Arrange 
        var userId = "20210001"; 
        var invalidRefreshToken = "invalid-refresh-token"; 
        
        _redisDbMock.Setup(r => r.StringGetAsync(It.Is<RedisKey>(k => k.ToString() == $"refresh:{userId}"), It.IsAny<CommandFlags>())).ReturnsAsync(RedisValue.Null); 
        
        // Act 
        var response = await _client.PostAsync($"/Auth/refresh-token?userId={userId}&refreshToken={invalidRefreshToken}", null); 
        
        // Assert 
        // 新契约：HTTP 状态码恒等于响应体里的 code，失败响应不再是 HTTP 200，
        // 所以不能再调 EnsureSuccessStatusCode()。
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<string>>(content);

        Assert.NotNull(result);
        Assert.True(result.Code != 200);
        Assert.Equal(result.Code, (int)response.StatusCode);
        Assert.Equal("无效的刷新令牌", result.Message); 
    } 
    
    [Fact]
    public async Task RefreshToken_MissingParameters_ReturnsError()
    {
        // Act - 缺少userId和refreshToken参数
        var response = await _client.PostAsync("/Auth/refresh-token", null);

        // Assert
        // userId / refreshToken 是非空查询参数，[ApiController] 会在进入 action 之前
        // 就判定模型无效并返回统一信封（errorCode 1003 / HTTP 400）。
        // action 里那句 "用户ID和刷新令牌不能为空" 对"完全不传"的情况因此永远走不到。
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<string>>(content);

        Assert.NotNull(result);
        Assert.True(result.Code != 200);
        Assert.Equal(result.Code, (int)response.StatusCode);
        Assert.Equal(400, result.Code);
        Assert.Equal(ErrorCode.ParameterValidationFailed, result.ErrorCode);
        Assert.Contains("userId", result.Detail ?? "");
    }
}

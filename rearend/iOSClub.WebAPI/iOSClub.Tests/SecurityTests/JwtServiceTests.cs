using iOSClub.WebAPI.Common.Security;
using iOSClub.WebAPI.Common.Config;
using Microsoft.Extensions.Logging;
using Moq;
using iOSClub.Data.VOs;
using Bogus;

namespace iOSClub.Tests.SecurityTests;

public class JwtServiceTests : IDisposable
{
    private readonly Mock<ILogger<JwtService>> _loggerMock;
    private readonly Mock<ILogger<RsaKeyManager>> _rsaLoggerMock;
    private readonly JwtConfig _jwtConfig;
    private readonly RsaKeyManager _rsaKeyManager;
    private readonly JwtService _jwtService;
    private readonly string _testDirectory;
    private readonly Faker<MemberVO> _memberFaker;

    public JwtServiceTests()
    {
        // 创建临时测试目录
        _testDirectory = Path.Combine(Path.GetTempPath(), "iOSClubTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        
        // 设置JwtConfig
        _jwtConfig = new JwtConfig
        {
            RsaPrivateKeyPath = Path.Combine(_testDirectory, "test_private_key.pem"),
            RsaPublicKeyPath = Path.Combine(_testDirectory, "test_public_key.pem"),
            KeyRotationDays = 90,
            AccessTokenExpiryMinutes = 60,
            Issuer = "test-issuer",
            Audience = "test-audience"
        };
        
        // 设置Logger Mocks
        _loggerMock = new Mock<ILogger<JwtService>>();
        _rsaLoggerMock = new Mock<ILogger<RsaKeyManager>>();
        
        // 创建MemberVO的Bogus生成器
        _memberFaker = new Faker<MemberVO>()
            .RuleFor(m => m.UserId, f => f.Random.AlphaNumeric(10))
            .RuleFor(m => m.UserName, f => f.Name.FullName())
            .RuleFor(m => m.Identity, f => f.PickRandom("Student", "Staff", "President", "Founder"));
        
        // 创建RsaKeyManager和JwtService实例
        _rsaKeyManager = new RsaKeyManager(_jwtConfig, _rsaLoggerMock.Object);
        _jwtService = new JwtService(_jwtConfig, _rsaKeyManager, _loggerMock.Object);
    }
    
    [Fact]
    public void GenerateTokens_ReturnsValidTokens()
    {
        // Arrange
        var memberModel = _memberFaker.Generate();
        var result = _jwtService.GenerateTokens(memberModel);
        
        // Assert
        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RefreshToken);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        
        // 验证令牌格式
        var tokenParts = result.AccessToken.Split('.');
        Assert.Equal(3, tokenParts.Length);
        
        // 验证刷新令牌格式
        Assert.Equal(32, result.RefreshToken.Length);
    }
    
    [Fact]
    public void GenerateTokens_HandlesMultipleUsers()
    {
        // Arrange
        var members = _memberFaker.Generate(3);
        
        // Act & Assert
        foreach (var member in members)
        {
            var result = _jwtService.GenerateTokens(member);
            Assert.NotNull(result.AccessToken);
            Assert.NotNull(result.RefreshToken);
        }
    }
    
    [Fact]
    public void ValidateAccessToken_ReturnsTrueForValidToken()
    {
        // Arrange
        var memberModel = _memberFaker.Generate();
        var result = _jwtService.GenerateTokens(memberModel);
        
        // Act
        var validationResult = _jwtService.ValidateAccessToken(result.AccessToken);
        
        // Assert
        Assert.True(validationResult.IsValid, "Valid access token should be validated successfully");
        Assert.NotEmpty(validationResult.Claims);
    }
    
    [Fact]
    public void ValidateAccessToken_ReturnsFalseForInvalidToken()
    {
        // Arrange
        var invalidToken = "invalid.jwt.token";
        
        // Act
        var validationResult = _jwtService.ValidateAccessToken(invalidToken);
        
        // Assert
        Assert.False(validationResult.IsValid, "Invalid access token should be rejected");
        Assert.Empty(validationResult.Claims);
    }
    
    [Fact]
    public void ValidateAccessToken_ReturnsFalseForExpiredToken()
    {
        // Arrange
        var memberModel = _memberFaker.Generate();
        
        // 生成一个正常令牌
        var result = _jwtService.GenerateTokens(memberModel);
        
        // 获取默认验证参数
        var validationParameters = _jwtService.GetDefaultValidationParameters();
        
        // 创建一个自定义的生命周期验证器，总是返回false，模拟令牌过期
        validationParameters.LifetimeValidator = (notBefore, expires, securityToken, validationParameters) =>
        {
            // 总是返回false，表示令牌已过期
            return false;
        };
        
        // Act - 使用自定义验证参数验证令牌
        var validationResult = _jwtService.ValidateAccessToken(result.AccessToken, validationParameters);
        
        // Assert
        Assert.False(validationResult.IsValid, "Expired access token should be rejected");
    }
    
    [Fact]
    public void ExtractClaimsFromToken_ReturnsCorrectClaims()
    {
        // Arrange
        var memberModel = _memberFaker.Generate();
        var result = _jwtService.GenerateTokens(memberModel);
        
        // Act
        var claims = _jwtService.ExtractClaimsFromToken(result.AccessToken);
        
        // Assert
        Assert.NotNull(claims);
        Assert.NotEmpty(claims);
        
        // 验证包含预期的声明
        var subClaim = claims.FirstOrDefault(c => c.Type == "sub");
        Assert.NotNull(subClaim);
        Assert.Equal(memberModel.UserId, subClaim.Value);
        
        var nameClaim = claims.FirstOrDefault(c => c.Type == "unique_name");
        Assert.NotNull(nameClaim);
        Assert.Equal(memberModel.UserName, nameClaim.Value);
        
        var roleClaim = claims.FirstOrDefault(c => c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
        Assert.NotNull(roleClaim);
        Assert.Equal(memberModel.Identity, roleClaim.Value);
    }
    
    [Fact]
    public void ExtractClaimsFromToken_ReturnsEmptyForInvalidToken()
    {
        // Arrange
        var invalidToken = "invalid.jwt.token";
        
        // Act
        var claims = _jwtService.ExtractClaimsFromToken(invalidToken);
        
        // Assert
        Assert.NotNull(claims);
        Assert.Empty(claims);
    }
    
    [Fact]
    public void GenerateTokens_WithScopeAndClientId_IncludesThemInClaims()
    {
        // Arrange
        var memberModel = _memberFaker.Generate();
        var scope = "profile email role";
        var clientId = "test-client-123";
        
        // Act
        var result = _jwtService.GenerateTokens(memberModel, scope, clientId);
        var claims = _jwtService.ExtractClaimsFromToken(result.AccessToken);
        
        // Assert
        Assert.NotNull(claims);
        
        var scopeClaim = claims.FirstOrDefault(c => c.Type == "scope");
        Assert.NotNull(scopeClaim);
        Assert.Equal(scope, scopeClaim.Value);
        
        var clientIdClaim = claims.FirstOrDefault(c => c.Type == "client_id");
        Assert.NotNull(clientIdClaim);
        Assert.Equal(clientId, clientIdClaim.Value);
    }
    
    [Fact]
    public void ValidateAccessToken_ReturnsFalseForMalformedToken()
    {
        // Arrange - 各种格式错误的令牌
        var malformedTokens = new[]
        {
            "",
            "onlyonepart",
            "two.parts",
            "invalid.token.format.with.more.parts",
            "invalid.base64.encoding.???."
        };
        
        // Act & Assert - 所有格式错误的令牌都应被拒绝
        foreach (var token in malformedTokens)
        {
            var validationResult = _jwtService.ValidateAccessToken(token);
            Assert.False(validationResult.IsValid, $"Malformed token '{token}' should be rejected");
        }
    }
    
    [Fact]
    public void GenerateTokens_ReturnsDifferentTokensOnSubsequentCalls()
    {
        // Arrange
        var memberModel = _memberFaker.Generate();
        
        // Act - 生成两组令牌
        var result1 = _jwtService.GenerateTokens(memberModel);
        var result2 = _jwtService.GenerateTokens(memberModel);
        
        // Assert - 验证令牌不同
        Assert.NotEqual(result1.AccessToken, result2.AccessToken);
        Assert.NotEqual(result1.RefreshToken, result2.RefreshToken);
    }
    
    [Fact]
    public void GetDefaultValidationParameters_ReturnsValidParameters()
    {
        // Act
        var parameters = _jwtService.GetDefaultValidationParameters();
        
        // Assert
        Assert.NotNull(parameters);
        Assert.True(parameters.ValidateIssuerSigningKey);
        Assert.True(parameters.ValidateIssuer);
        Assert.True(parameters.ValidateAudience);
        Assert.True(parameters.ValidateLifetime);
        Assert.NotNull(parameters.IssuerSigningKey);
        Assert.Equal(_jwtConfig.Issuer, parameters.ValidIssuer);
        Assert.Equal(_jwtConfig.Audience, parameters.ValidAudience);
    }
    
    [Fact]
    public void RefreshAccessToken_GeneratesNewToken()
    {
        // Arrange
        var memberModel = _memberFaker.Generate();
        var result = _jwtService.GenerateTokens(memberModel);
        
        // Act
        var newAccessToken = _jwtService.RefreshAccessToken(result.RefreshToken, memberModel);
        
        // Assert
        Assert.NotNull(newAccessToken);
        Assert.NotEmpty(newAccessToken);
        Assert.NotEqual(result.AccessToken, newAccessToken);
        
        // 验证新令牌有效
        var validationResult = _jwtService.ValidateAccessToken(newAccessToken);
        Assert.True(validationResult.IsValid);
    }
    
    // 清理临时文件
    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}

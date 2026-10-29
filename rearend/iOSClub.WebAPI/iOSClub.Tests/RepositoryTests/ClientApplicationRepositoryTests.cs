using iOSClub.Data;
using iOSClub.DataApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.Tests.RepositoryTests;

public class ClientApplicationRepositoryTests
{
    private readonly DbContextOptions<ClubContext> _options;
    private readonly ClientApplicationRepository _clientAppRepository;

    public ClientApplicationRepositoryTests()
    {
        // 使用内存数据库进行测试
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase(databaseName: "ClientApplicationRepositoryTestDatabase")
            .Options;
        
        var contextFactory = new TestDbContextFactory(_options);
        _clientAppRepository = new ClientApplicationRepository(contextFactory);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllClientApplicationDO()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成2个客户端应用
        var clientApps = BogusDataGenerator.GenerateClientApplications(2);
        await context.ClientApplications.AddRangeAsync(clientApps);
        await context.SaveChangesAsync();

        // Act
        var result = await _clientAppRepository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task GetByClientIdAsync_ReturnsCorrectClientApplicationDO()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成1个客户端应用
        var clientApp = BogusDataGenerator.ClientApplicationFaker.Generate();
        await context.ClientApplications.AddAsync(clientApp);
        await context.SaveChangesAsync();

        // Act
        var result = await _clientAppRepository.GetByClientIdAsync(clientApp.ClientId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(clientApp.ClientId, result.ClientId);
    }

    [Fact]
    public async Task CreateAsync_CreatesNewClientApplicationDO()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成1个客户端应用
        var clientApp = BogusDataGenerator.ClientApplicationFaker.Generate();

        // Act
        var result = await _clientAppRepository.CreateAsync(clientApp);
        
        // 创建新的context实例来查询更新后的结果
        await using var newContext = new ClubContext(_options);
        var savedClientApp = await newContext.ClientApplications.FirstOrDefaultAsync(c => c.ClientId == clientApp.ClientId);

        // Assert
        Assert.True(result);
        Assert.NotNull(savedClientApp);
        Assert.Equal(clientApp.ClientId, savedClientApp.ClientId);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingClientApplicationDO()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成1个客户端应用
        var clientApp = BogusDataGenerator.ClientApplicationFaker.Generate();
        await context.ClientApplications.AddAsync(clientApp);
        await context.SaveChangesAsync();
        
        // 修改客户端应用
        clientApp.IsActive = false;
        clientApp.RedirectUris = $"{clientApp.RedirectUris};http://localhost:{new Random().Next(3000, 9999)}";

        // Act
        var result = await _clientAppRepository.UpdateAsync(clientApp);
        
        // 创建新的context实例来查询更新后的结果
        await using var newContext = new ClubContext(_options);
        var updatedClientApp = await newContext.ClientApplications.FirstOrDefaultAsync(c => c.ClientId == clientApp.ClientId);

        // Assert
        Assert.True(result);
        Assert.NotNull(updatedClientApp);
        Assert.False(updatedClientApp.IsActive);
        Assert.Equal(clientApp.RedirectUris, updatedClientApp.RedirectUris);
    }

    [Fact]
    public async Task DeleteAsync_RemovesClientApplicationDO()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成1个客户端应用
        var clientApp = BogusDataGenerator.ClientApplicationFaker.Generate();
        await context.ClientApplications.AddAsync(clientApp);
        await context.SaveChangesAsync();

        // Act
        var result = await _clientAppRepository.DeleteAsync(clientApp.ClientId);
        
        // 创建新的context实例来查询更新后的结果
        await using var newContext = new ClubContext(_options);
        var deletedClientApp = await newContext.ClientApplications.FirstOrDefaultAsync(c => c.ClientId == clientApp.ClientId);

        // Assert
        Assert.True(result);
        Assert.Null(deletedClientApp);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_ReturnsClientApplicationDOWhenCredentialsAreValid()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成1个客户端应用
        var clientApp = BogusDataGenerator.ClientApplicationFaker.Generate();
        // Faker 的 IsActive 是随机的，而 ValidateCredentialsAsync 只接受 IsActive 的应用，
        // 不固定的话这个测试会随机失败。
        clientApp.IsActive = true;
        var originalSecret = clientApp.ClientSecret;
        // 与生产写入路径一致：数据库只保存 BCrypt 哈希，校验时拿原始密钥比对。
        clientApp.ClientSecret = BCrypt.Net.BCrypt.HashPassword(originalSecret);
        await context.ClientApplications.AddAsync(clientApp);
        await context.SaveChangesAsync();

        // Act
        var result = await _clientAppRepository.ValidateCredentialsAsync(clientApp.ClientId, originalSecret);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(clientApp.ClientId, result.ClientId);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_RejectsStoredHashAsSecret()
    {
        // 回归：以前 ValidateCredentialsAsync 有明文相等分支，配合 ClientAppVO 回显哈希，
        // 任何能读取应用列表的人都能拿哈希当 client_secret 冒充客户端。
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var clientApp = BogusDataGenerator.ClientApplicationFaker.Generate();
        clientApp.IsActive = true;
        clientApp.ClientSecret = BCrypt.Net.BCrypt.HashPassword("real-secret");
        await context.ClientApplications.AddAsync(clientApp);
        await context.SaveChangesAsync();

        var result = await _clientAppRepository.ValidateCredentialsAsync(clientApp.ClientId, clientApp.ClientSecret);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_ReturnsNull_WhenCredentialsAreInvalid()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成1个客户端应用
        var clientApp = BogusDataGenerator.ClientApplicationFaker.Generate();
        await context.ClientApplications.AddAsync(clientApp);
        await context.SaveChangesAsync();

        // Act
        var result = await _clientAppRepository.ValidateCredentialsAsync(clientApp.ClientId, "wrong-secret");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByRedirectUriAsync_ReturnsClientApplicationDOWhenRedirectUriExists()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成1个客户端应用
        var clientApp = BogusDataGenerator.ClientApplicationFaker.Generate();
        // 获取其中一个重定向URI用于测试
        var redirectUri = clientApp.RedirectUris.Split(';').First();
        await context.ClientApplications.AddAsync(clientApp);
        await context.SaveChangesAsync();

        // Act
        var result = await _clientAppRepository.GetByRedirectUriAsync(redirectUri);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(clientApp.ClientId, result.ClientId);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_LegacyPlaintextSecret_WorksAndUpgradesToBCrypt()
    {
        // 回归：H1 删除了明文相等分支后，历史明文存储的客户端会全部 invalid_client。
        // 兼容路径只对"存储值不是 BCrypt 哈希"的行生效，并在首次成功后升级。
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var clientApp = BogusDataGenerator.ClientApplicationFaker.Generate();
        clientApp.IsActive = true;
        clientApp.ClientSecret = "legacy-plain-secret";
        await context.ClientApplications.AddAsync(clientApp);
        await context.SaveChangesAsync();

        var result = await _clientAppRepository.ValidateCredentialsAsync(clientApp.ClientId, "legacy-plain-secret");

        Assert.NotNull(result);
        await using (var verify = new ClubContext(_options))
        {
            var saved = await verify.ClientApplications.SingleAsync(c => c.ClientId == clientApp.ClientId);
            Assert.StartsWith("$2", saved.ClientSecret);
            Assert.True(BCrypt.Net.BCrypt.Verify("legacy-plain-secret", saved.ClientSecret));
        }

        // 升级后平台继续使用同一个密钥
        Assert.NotNull(await _clientAppRepository.ValidateCredentialsAsync(clientApp.ClientId, "legacy-plain-secret"));
    }

    [Fact]
    public async Task ValidateCredentialsAsync_LegacyPlaintextSecret_WrongSecret_ReturnsNull()
    {
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var clientApp = BogusDataGenerator.ClientApplicationFaker.Generate();
        clientApp.IsActive = true;
        clientApp.ClientSecret = "legacy-plain-secret";
        await context.ClientApplications.AddAsync(clientApp);
        await context.SaveChangesAsync();

        var result = await _clientAppRepository.ValidateCredentialsAsync(clientApp.ClientId, "wrong-secret");

        Assert.Null(result);
        await using var verify = new ClubContext(_options);
        var saved = await verify.ClientApplications.SingleAsync(c => c.ClientId == clientApp.ClientId);
        Assert.Equal("legacy-plain-secret", saved.ClientSecret); // 失败时不升级
    }
}

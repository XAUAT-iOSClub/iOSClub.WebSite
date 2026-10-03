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
        // 保存原始密钥到数据库，不进行哈希处理
        await context.ClientApplications.AddAsync(clientApp);
        await context.SaveChangesAsync();

        // Act
        var result = await _clientAppRepository.ValidateCredentialsAsync(clientApp.ClientId, originalSecret);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(clientApp.ClientId, result.ClientId);
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
}

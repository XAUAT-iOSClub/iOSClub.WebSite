using iOSClub.Data;
using iOSClub.DataApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.Tests.RepositoryTests;

public class ResourceRepositoryTests
{
    private readonly DbContextOptions<ClubContext> _options;
    private readonly TestDbContextFactory _contextFactory;
    private readonly ResourceRepository _resourceRepository;

    public ResourceRepositoryTests()
    {
        // 使用内存数据库进行测试；每个测试类实例使用独立库名，避免并发或残留数据互相影响
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase(databaseName: $"ResourceRepositoryTestDatabase_{Guid.NewGuid()}")
            .Options;

        _contextFactory = new TestDbContextFactory(_options);
        _resourceRepository = new ResourceRepository(_contextFactory);
    }

    [Fact]
    public async Task GetAllResourcesAsync_ReturnsAllResources()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成2个资源
        var resources = BogusDataGenerator.GenerateResources(2);
        await context.Resources.AddRangeAsync(resources);
        await context.SaveChangesAsync();

        // Act
        var result = await _resourceRepository.GetAllResourcesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetResourceByIdAsync_ReturnsCorrectResource()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成1个资源
        var resource = BogusDataGenerator.ResourceFaker.Generate();
        await context.Resources.AddAsync(resource);
        await context.SaveChangesAsync();

        // Act
        var result = await _resourceRepository.GetResourceByIdAsync(resource.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(resource.Id, result.Id);
        Assert.Equal(resource.Name, result.Name);
    }

    [Fact]
    public async Task GetResourcesByTagAsync_ReturnsResourcesWithMatchingTag()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成3个资源
        var resources = BogusDataGenerator.GenerateResources(3);
        // 确保其中2个资源使用相同的标签
        var commonTag = "common-tag";
        resources[0].Tag = commonTag;
        resources[1].Tag = commonTag;
        resources[2].Tag = "other-tag";
        
        await context.Resources.AddRangeAsync(resources);
        await context.SaveChangesAsync();

        // Act
        var result = await _resourceRepository.GetResourcesByTagAsync(commonTag);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(commonTag, r.Tag));
    }

    [Fact]
    public async Task SearchResourcesByNameAsync_ReturnsResourcesWithMatchingName()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成3个资源
        var resources = BogusDataGenerator.GenerateResources(3);
        // 确保其中1个资源名称包含"Test"
        resources[0].Name = "Test Resource";
        resources[1].Name = "Another Resource";
        resources[2].Name = "Resource 3";
        
        await context.Resources.AddRangeAsync(resources);
        await context.SaveChangesAsync();

        // Act
        var result = await _resourceRepository.SearchResourcesByNameAsync("Test");

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Test Resource", result[0].Name);
    }

    [Fact]
    public async Task AddResourceAsync_AddsNewResource()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成1个资源
        var resource = BogusDataGenerator.ResourceFaker.Generate();
        resource.Id = string.Empty; // 确保是新资源

        // Act
        var result = await _resourceRepository.AddResourceAsync(resource);

        // 创建新的context实例来查询更新后的结果
        await using var newContext = new ClubContext(_options);
        var savedResource = await newContext.Resources.FirstOrDefaultAsync(r => r.Name == resource.Name);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(savedResource);
        Assert.Equal(resource.Name, savedResource.Name);
        Assert.Equal(resource.Id, savedResource.Id);
        Assert.False(string.IsNullOrEmpty(savedResource.Id));
    }

    [Fact]
    public async Task AddResourceAsync_ReturnsPersistedResourceWithGeneratedId()
    {
        // 回归 C24：仓库在内部生成 Id 后必须把持久化后的 DO 交还调用方，
        // 否则控制器只能拿着请求 DTO 的空 Id 响应，调用方无法立即按 Id 查询/更新/删除。
        // 使用独立内存库，避免与其他用例共享状态。
        var options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase(databaseName: $"ResourceAdd_{Guid.NewGuid()}")
            .Options;
        var repository = new ResourceRepository(new TestDbContextFactory(options));

        var resource = BogusDataGenerator.ResourceFaker.Generate();
        resource.Id = string.Empty;
        var expectedName = resource.Name;

        // Act
        var added = await repository.AddResourceAsync(resource);

        // Assert
        Assert.NotNull(added);
        Assert.False(string.IsNullOrEmpty(added!.Id), "新增资源必须返回服务端生成的非空 Id");
        Assert.Equal(expectedName, added.Name);

        // 返回的 Id 必须能立即查到同一条资源
        var persisted = await repository.GetResourceByIdAsync(added.Id);
        Assert.NotNull(persisted);
        Assert.Equal(added.Id, persisted!.Id);
        Assert.Equal(expectedName, persisted.Name);
    }

    [Fact]
    public async Task UpdateResourceAsync_UpdatesExistingResource()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成1个资源
        var resource = BogusDataGenerator.ResourceFaker.Generate();
        await context.Resources.AddAsync(resource);
        await context.SaveChangesAsync();

        // 修改资源信息
        resource.Name = "Updated Name";
        resource.Tag = "updated-tag";

        // Act
        var result = await _resourceRepository.UpdateResourceAsync(resource);

        // 创建新的context实例来查询更新后的结果
        await using var newContext = new ClubContext(_options);
        var updatedResource = await newContext.Resources.FirstOrDefaultAsync(r => r.Id == resource.Id);

        // Assert
        Assert.True(result);
        Assert.NotNull(updatedResource);
        Assert.Equal("Updated Name", updatedResource.Name);
        Assert.Equal("updated-tag", updatedResource.Tag);
    }

    [Fact]
    public async Task DeleteResourceAsync_RemovesResource()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成1个资源
        var resource = BogusDataGenerator.ResourceFaker.Generate();
        await context.Resources.AddAsync(resource);
        await context.SaveChangesAsync();

        // Act
        var result = await _resourceRepository.DeleteResourceAsync(resource.Id);

        // 创建新的context实例来查询更新后的结果
        await using var newContext = new ClubContext(_options);
        var deletedResource = await newContext.Resources.FirstOrDefaultAsync(r => r.Id == resource.Id);

        // Assert
        Assert.True(result);
        Assert.Null(deletedResource);
    }

    [Fact]
    public async Task GetAllTagsAsync_ReturnsAllUniqueTags()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成3个资源
        var resources = BogusDataGenerator.GenerateResources(3);
        // 确保其中2个资源使用相同的标签
        var commonTag = "common-tag";
        resources[0].Tag = commonTag;
        resources[1].Tag = commonTag;
        resources[2].Tag = "other-tag";
        
        await context.Resources.AddRangeAsync(resources);
        await context.SaveChangesAsync();

        // Act
        var result = await _resourceRepository.GetAllTagsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(commonTag, result);
        Assert.Contains("other-tag", result);
    }

    [Fact]
    public async Task GetResourceCountAsync_ReturnsCorrectCount()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成3个资源
        var resources = BogusDataGenerator.GenerateResources(3);
        await context.Resources.AddRangeAsync(resources);
        await context.SaveChangesAsync();

        // Act
        var result = await _resourceRepository.GetResourceCountAsync();

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task ResourceExistsAsync_ReturnsTrue_WhenResourceExists()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成1个资源
        var resource = BogusDataGenerator.ResourceFaker.Generate();
        await context.Resources.AddAsync(resource);
        await context.SaveChangesAsync();

        // Act
        var result = await _resourceRepository.ResourceExistsAsync(resource.Id);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ResourceExistsAsync_ReturnsFalse_WhenResourceDoesNotExist()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // Act
        var result = await _resourceRepository.ResourceExistsAsync("non-existent-resource");

        // Assert
        Assert.False(result);
    }
}
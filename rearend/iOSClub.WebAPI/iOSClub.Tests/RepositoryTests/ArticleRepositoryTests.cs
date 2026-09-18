using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.DataApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.Tests.RepositoryTests;

public class ArticleRepositoryTests
{
    private readonly DbContextOptions<ClubContext> _options;
    private readonly ArticleRepository _articleRepository;
    private readonly CategoryRepository _categoryRepository;

    public ArticleRepositoryTests()
    {
        // 使用内存数据库进行测试
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase(databaseName: "ArticleRepositoryTestDatabase")
            .Options;
        
        var contextFactory = new TestDbContextFactory(_options);
        _categoryRepository = new CategoryRepository(contextFactory);
        _articleRepository = new ArticleRepository(contextFactory, _categoryRepository);
    }

    [Fact]
    public async Task GetAll_ReturnsAllArticles()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成分类
        var category = BogusDataGenerator.CategoryFaker.Generate();
        await context.Categories.AddAsync(category);
        
        // 使用Bogus生成2篇文章
        var articles = BogusDataGenerator.GenerateArticles(2);
        foreach (var article in articles)
        {
            article.CategoryId = category.Id;
            article.Identity = "Member";
        }
        
        await context.Articles.AddRangeAsync(articles);
        await context.SaveChangesAsync();

        // Act
        var result = await _articleRepository.GetAll();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task GetFromPath_ReturnsCorrectArticle()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成文章
        var article = BogusDataGenerator.ArticleFaker.Generate();
        article.Identity = "Member";
        await context.Articles.AddAsync(article);
        await context.SaveChangesAsync();

        // Act
        var result = await _articleRepository.GetFromPath(article.Path);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(article.Title, result.Title);
        Assert.Equal(article.Content, result.Content);
    }

    [Fact]
    public async Task CreateOrUpdate_CreatesNewArticle()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成文章
        var article = BogusDataGenerator.ArticleFaker.Generate();
        article.Identity = "Member";

        // Act
        var result = await _articleRepository.CreateOrUpdate(article);
        var savedArticle = await context.Articles.FirstOrDefaultAsync(a => a.Path == article.Path);

        // Assert
        Assert.True(result);
        Assert.NotNull(savedArticle);
        Assert.Equal(article.Title, savedArticle.Title);
    }

    [Fact]
    public async Task CreateOrUpdate_UpdatesExistingArticle()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成文章
        var article = BogusDataGenerator.ArticleFaker.Generate();
        article.Identity = "Member";
        await context.Articles.AddAsync(article);
        await context.SaveChangesAsync();
        
        // 更新文章信息
        article.Title = "Updated Title";
        article.Content = "Updated Content";

        // Act
        var result = await _articleRepository.CreateOrUpdate(article);
        var updatedArticle = await context.Articles.FirstOrDefaultAsync(a => a.Path == article.Path);

        // Assert
        Assert.True(result);
        Assert.NotNull(updatedArticle);
        Assert.Equal("Updated Title", updatedArticle.Title);
        Assert.Equal("Updated Content", updatedArticle.Content);
    }

    [Fact]
    public async Task Delete_RemovesArticle()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成文章
        var article = BogusDataGenerator.ArticleFaker.Generate();
        article.Identity = "Member";
        await context.Articles.AddAsync(article);
        await context.SaveChangesAsync();

        // Act
        var result = await _articleRepository.Delete(article.Path);
        var deletedArticle = await context.Articles.FirstOrDefaultAsync(a => a.Path == article.Path);

        // Assert
        Assert.True(result);
        Assert.Null(deletedArticle);
    }

    [Fact]
    public async Task GetAllCategoryArticles_ReturnsArticlesGroupedByCategory()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成分类；Name 是随机单词，可能重复，而结果按 Name 分组，
        // 重复会让两个分类被合并导致断言随机失败，这里强制区分。
        var category1 = BogusDataGenerator.CategoryFaker.Generate();
        var category2 = BogusDataGenerator.CategoryFaker.Generate();
        category1.Name = "测试分类A";
        category2.Name = "测试分类B";
        await context.Categories.AddRangeAsync(category1, category2);
        
        // 使用Bogus生成3篇文章
        var articles = BogusDataGenerator.GenerateArticles(3);
        
        // 将前2篇文章分配给category1
        for (int i = 0; i < 2; i++)
        {
            articles[i].CategoryId = category1.Id;
            articles[i].Identity = "Member";
        }
        
        // 将第3篇文章分配给category2
        articles[2].CategoryId = category2.Id;
        articles[2].Identity = "Member";
        
        await context.Articles.AddRangeAsync(articles);
        await context.SaveChangesAsync();

        // Act
        var result = await _articleRepository.GetAllCategoryArticles(new ArticleAccessScope("Member"));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(category1.Name));
        Assert.True(result.ContainsKey(category2.Name));
        Assert.Equal(2, result[category1.Name].Count());
        Assert.Single(result[category2.Name]);
    }

    [Theory]
    [InlineData("Department", "科技部", true)]
    [InlineData("Minister", "科技部", true)]
    [InlineData("Department", "宣传部", false)]
    [InlineData("President", null, true)]
    [InlineData("Founder", null, true)]
    public async Task GetFromPath_EnforcesDepartmentVisibility(
        string identity,
        string? departmentName,
        bool shouldBeVisible)
    {
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var department = new DepartmentDO { Name = "科技部", Key = "technology" };
        var article = BogusDataGenerator.ArticleFaker.Generate();
        article.Identity = "Department";
        article.VisibleToDepartment = department.Name;
        await context.Departments.AddAsync(department);
        await context.Articles.AddAsync(article);
        await context.SaveChangesAsync();

        var result = await _articleRepository.GetFromPath(
            article.Path,
            new ArticleAccessScope(identity, departmentName));

        Assert.Equal(shouldBeVisible, result != null);
    }

    [Theory]
    [InlineData("President")]
    [InlineData("Founder")]
    public async Task GetAllCategoryArticles_GlobalManagersSeeDepartmentArticles(string identity)
    {
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var department = new DepartmentDO { Name = "科技部", Key = "technology" };
        var category = BogusDataGenerator.CategoryFaker.Generate();
        var article = BogusDataGenerator.ArticleFaker.Generate();
        article.Identity = "Department";
        article.VisibleToDepartment = department.Name;
        article.CategoryId = category.Id;
        await context.Departments.AddAsync(department);
        await context.Categories.AddAsync(category);
        await context.Articles.AddAsync(article);
        await context.SaveChangesAsync();

        var result = await _articleRepository.GetAllCategoryArticles(new ArticleAccessScope(identity));

        Assert.Contains(result.SelectMany(group => group.Value), item => item.Path == article.Path);
    }
}

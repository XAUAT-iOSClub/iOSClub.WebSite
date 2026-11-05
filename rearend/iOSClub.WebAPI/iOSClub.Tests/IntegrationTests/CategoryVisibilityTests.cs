using System.Text.Json;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;

namespace iOSClub.Tests.IntegrationTests;

/// <summary>
/// 分类接口（GET /Category/articles/{id} 与 GET /Category/{name}）的文章可见性回归测试。
/// <para>
/// 回归背景：这两个公开接口曾经无条件返回分类下全部文章的标题/路径，绕过
/// <c>ArticleAccessPolicy</c> 与文章详情接口的既有过滤，导致匿名用户也能看到
/// "部长仅见""其他部门限定"文章的存在性（docs 冲突清单 C08）。
/// </para>
/// <para>
/// 这里锁定：分类文章列表出现 ⇔ GET /Article/{path} 详情可取（不可取时 errorCode=4001），
/// 防止分类接口与详情接口两套可见性规则再次漂移。
/// </para>
/// </summary>
public class CategoryVisibilityTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string CategoryId = "tech-category";
    private const string CategoryName = "技术";

    private const string MinisterOnlyPath = "minister-only";
    private const string MinisterOnlyTitle = "部长专属";
    private const string TechnologyPath = "technology-article";
    private const string TechnologyTitle = "科技部文章";
    private const string PublicityPath = "publicity-article";
    private const string PublicityTitle = "宣传部文章";

    private const string DepartmentUserId = "2024000001";
    private const string MinisterUserId = "2024000002";
    private const string PresidentUserId = "2024000003";
    private const string FounderUserId = "2024000004";
    private const string MemberUserId = "2024000005";

    private static readonly string[] AllPaths = [MinisterOnlyPath, TechnologyPath, PublicityPath];

    private static readonly Dictionary<string, string> TitlesByPath = new()
    {
        [MinisterOnlyPath] = MinisterOnlyTitle,
        [TechnologyPath] = TechnologyTitle,
        [PublicityPath] = PublicityTitle
    };

    private readonly DbContextOptions<ClubContext> _options;
    private readonly WebApplicationFactory<Program> _factory;

    public CategoryVisibilityTests(WebApplicationFactory<Program> factory)
    {
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"CategoryVisibility-{Guid.NewGuid()}")
            .Options;

        var database = Mock.Of<IDatabase>();
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database);

        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing").ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(IDbContextFactory<ClubContext>));
            services.Remove(descriptor);
            services.AddSingleton<IDbContextFactory<ClubContext>>(new TestDbContextFactory(_options));
            services.AddSingleton(redis.Object);
            services.AddSingleton(Mock.Of<IEmailService>());
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        }));
    }

    /// <summary>
    /// 种子数据：科技部 / 宣传部；科技部部员、科技部部长、社长、创始人、无部门普通成员；
    /// 分类"技术"下三篇文章：部长仅见、科技部限定、宣传部限定。
    /// </summary>
    private async Task SeedAsync()
    {
        await using var context = new ClubContext(_options);
        var technology = new DepartmentDO { Key = "technology", Name = "科技部", Description = "科技部" };
        var publicity = new DepartmentDO { Key = "publicity", Name = "宣传部", Description = "宣传部" };
        context.Departments.AddRange(technology, publicity);

        context.Categories.Add(new CategoryDO
        {
            Id = CategoryId,
            Name = CategoryName,
            Order = 1,
            Description = "技术文章"
        });

        context.Staffs.AddRange(
            new StaffDO
            {
                UserId = DepartmentUserId, Name = "科技部部员", Identity = "Department", Department = technology
            },
            new StaffDO
            {
                UserId = MinisterUserId, Name = "科技部部长", Identity = "Minister", Department = technology
            },
            new StaffDO { UserId = PresidentUserId, Name = "社长", Identity = "President" },
            new StaffDO { UserId = FounderUserId, Name = "创始人", Identity = "Founder" },
            new StaffDO { UserId = MemberUserId, Name = "普通成员", Identity = "Member" });

        context.Articles.AddRange(
            new ArticleDO
            {
                Path = MinisterOnlyPath,
                Title = MinisterOnlyTitle,
                Content = "只有部长及以上职级可以看到的内容。",
                Identity = "Minister",
                CategoryId = CategoryId,
                ArticleOrder = 1
            },
            new ArticleDO
            {
                Path = TechnologyPath,
                Title = TechnologyTitle,
                Content = "科技部成员可以看到的内容。",
                Identity = "Department",
                VisibleToDepartment = technology.Name,
                CategoryId = CategoryId,
                ArticleOrder = 2
            },
            new ArticleDO
            {
                Path = PublicityPath,
                Title = PublicityTitle,
                Content = "宣传部成员可以看到的内容。",
                Identity = "Department",
                VisibleToDepartment = publicity.Name,
                CategoryId = CategoryId,
                ArticleOrder = 3
            });

        await context.SaveChangesAsync();
    }

    private HttpClient CreateClient(string? identity, string? userId, bool anonymous = false)
    {
        var client = _factory.CreateClient();
        if (anonymous)
        {
            client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
        }
        else
        {
            client.DefaultRequestHeaders.Add("X-Test-Identity", identity ?? "Founder");
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId ?? FounderUserId);
        }

        return client;
    }

    /// <summary>
    /// 可见性矩阵（按 ArticleAccessPolicy 语义离线推导）：
    /// 匿名/普通成员看不到任何一篇（全部文章要求 Department 及以上）；
    /// 科技部部员只看科技部限定；科技部部长看部长仅见 + 科技部限定；
    /// 社长/创始人跨部门全见。
    /// </summary>
    public static TheoryData<string, string, bool, string[]> VisibilityCases => new()
    {
        { "", "", true, [] },
        { "Member", MemberUserId, false, [] },
        { "Department", DepartmentUserId, false, [TechnologyPath] },
        { "Minister", MinisterUserId, false, [MinisterOnlyPath, TechnologyPath] },
        { "President", PresidentUserId, false, [MinisterOnlyPath, TechnologyPath, PublicityPath] },
        { "Founder", FounderUserId, false, [MinisterOnlyPath, TechnologyPath, PublicityPath] }
    };

    [Theory]
    [MemberData(nameof(VisibilityCases))]
    public async Task CategoryEndpoints_OnlyExposeAccessibleArticles(
        string identity, string userId, bool anonymous, string[] expectedPaths)
    {
        await SeedAsync();
        using var client = CreateClient(identity, userId, anonymous);
        var expectedTitles = expectedPaths
            .Select(path => TitlesByPath[path])
            .OrderBy(title => title, StringComparer.Ordinal)
            .ToArray();

        var listTitles = (await GetCategoryArticlesTitlesAsync(client))
            .OrderBy(title => title, StringComparer.Ordinal)
            .ToArray();
        var categoryTitles = (await GetCategoryTitlesAsync(client))
            .OrderBy(title => title, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedTitles, listTitles);
        Assert.Equal(expectedTitles, categoryTitles);

        // 与详情接口对照：分类列表出现 ⇔ /Article/{path} 可取，防止两套规则漂移。
        foreach (var path in AllPaths)
        {
            var (accessible, detailTitle) = await GetArticleDetailAsync(client, path);
            var inList = listTitles.Contains(TitlesByPath[path]);
            var inCategory = categoryTitles.Contains(TitlesByPath[path]);

            Assert.True(accessible == inList,
                $"{identity}/{path}：详情可取={accessible}，但 GET /Category/articles/{CategoryId} 中出现={inList}");
            Assert.True(accessible == inCategory,
                $"{identity}/{path}：详情可取={accessible}，但 GET /Category/{CategoryName} 中出现={inCategory}");
            if (accessible)
            {
                Assert.Equal(TitlesByPath[path], detailTitle);
            }
        }
    }

    [Fact]
    public async Task Anonymous_CategoryEndpoints_DoNotLeakRestrictedArticles()
    {
        await SeedAsync();
        using var client = CreateClient(null, null, anonymous: true);

        var articlesRaw = await GetRawAsync(client, $"/Category/articles/{CategoryId}");
        var categoryRaw = await GetRawAsync(client, $"/Category/{Uri.EscapeDataString(CategoryName)}");

        foreach (var path in AllPaths)
        {
            Assert.DoesNotContain(TitlesByPath[path], articlesRaw);
            Assert.DoesNotContain(path, articlesRaw);
            Assert.DoesNotContain(TitlesByPath[path], categoryRaw);
            Assert.DoesNotContain(path, categoryRaw);
        }
    }

    [Fact]
    public async Task DepartmentMember_CategoryEndpoints_DoNotLeakMinisterOnlyOrOtherDepartmentArticles()
    {
        await SeedAsync();
        using var client = CreateClient("Department", DepartmentUserId);

        var listTitles = await GetCategoryArticlesTitlesAsync(client);
        var categoryTitles = await GetCategoryTitlesAsync(client);

        Assert.Contains(TechnologyTitle, listTitles);
        Assert.DoesNotContain(MinisterOnlyTitle, listTitles);
        Assert.DoesNotContain(PublicityTitle, listTitles);
        Assert.Contains(TechnologyTitle, categoryTitles);
        Assert.DoesNotContain(MinisterOnlyTitle, categoryTitles);
        Assert.DoesNotContain(PublicityTitle, categoryTitles);
    }

    private async Task<List<string>> GetCategoryArticlesTitlesAsync(HttpClient client)
    {
        var (body, raw) = await ReadApiAsync(await client.GetAsync($"/Category/articles/{CategoryId}"));
        AssertSuccess(body, raw, $"/Category/articles/{CategoryId}");
        var data = GetPropertyIgnoreCase(body, "data");
        return data.EnumerateArray()
            .Select(item => GetPropertyIgnoreCase(item, "title").GetString() ?? "")
            .ToList();
    }

    private async Task<List<string>> GetCategoryTitlesAsync(HttpClient client)
    {
        var (body, raw) = await ReadApiAsync(
            await client.GetAsync($"/Category/{Uri.EscapeDataString(CategoryName)}"));
        AssertSuccess(body, raw, $"/Category/{CategoryName}");
        var articles = GetPropertyIgnoreCase(GetPropertyIgnoreCase(body, "data"), "articles");
        return articles.EnumerateArray()
            .Select(item => GetPropertyIgnoreCase(item, "title").GetString() ?? "")
            .ToList();
    }

    private async Task<(bool Accessible, string? Title)> GetArticleDetailAsync(HttpClient client, string path)
    {
        var (body, raw) = await ReadApiAsync(await client.GetAsync($"/Article/{path}"));
        var errorCode = GetPropertyIgnoreCase(body, "errorCode").GetInt32();
        if (errorCode == 0)
        {
            return (true, GetPropertyIgnoreCase(GetPropertyIgnoreCase(body, "data"), "title").GetString());
        }

        Assert.True(errorCode == ErrorCode.ArticleNotFound,
            $"GET /Article/{path} 返回了预期之外的错误码 {errorCode}：{raw}");
        return (false, null);
    }

    private static async Task<string> GetRawAsync(HttpClient client, string url)
    {
        var (body, raw) = await ReadApiAsync(await client.GetAsync(url));
        AssertSuccess(body, raw, url);
        return raw;
    }

    private static void AssertSuccess(JsonElement body, string raw, string url)
    {
        var errorCode = GetPropertyIgnoreCase(body, "errorCode").GetInt32();
        Assert.True(errorCode == 0, $"{url} 返回错误码 {errorCode}：{raw}");
    }

    private static async Task<(JsonElement Body, string Raw)> ReadApiAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(raw), $"响应体为空（HTTP {(int)response.StatusCode}）");
        using var document = JsonDocument.Parse(raw);
        return (document.RootElement.Clone(), raw);
    }

    private static JsonElement GetPropertyIgnoreCase(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        throw new KeyNotFoundException($"响应中缺少字段 '{name}'：{element}");
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}

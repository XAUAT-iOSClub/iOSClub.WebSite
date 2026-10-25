using System.Security.Claims;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.Tests.IntegrationTests;

/// <summary>
/// 文章写接口的权限回归测试。
/// <para>
/// 回归背景：部员（Department）能通过 update/delete 接口修改"部长仅见"的文章，
/// 因为写接口从不校验文章自身的访问权限。这里锁定：写权限不得宽于读权限。
/// </para>
/// </summary>
public class ArticlePermissionTests
{
    private const string DepartmentUserId = "2024000001";
    private const string MinisterUserId = "2024000002";
    private const string MinisterOnlyPath = "minister-only";
    private const string DepartmentArticlePath = "department-article";
    private const string OtherDepartmentArticlePath = "other-department-article";

    private readonly DbContextOptions<ClubContext> _options;
    private readonly ArticleRepository _articleRepository;
    private readonly StaffRepository _staffRepository;
    private readonly DepartmentRepository _departmentRepository;

    public ArticlePermissionTests()
    {
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase(databaseName: $"ArticlePermission_{Guid.NewGuid()}")
            .Options;

        var factory = new TestDbContextFactory(_options);
        _articleRepository = new ArticleRepository(factory, new CategoryRepository(factory));
        _staffRepository = new StaffRepository(factory);
        _departmentRepository = new DepartmentRepository(factory);
    }

    private ArticleController CreateController(string userId, string identity)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, identity),
        ], "JWT"));

        return new ArticleController(_articleRepository, _staffRepository, _departmentRepository)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }

    private static ApiResponse Unwrap(ActionResult<ApiResponse> response)
    {
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        return Assert.IsType<ApiResponse>(ok.Value);
    }

    private static ArticleUpdateDTO ValidUpdate(string title, string? identity = "Department", string? department = "科技部")
        => new()
        {
            Title = title,
            Content = "这是一段足够长的文章内容，用于通过最小长度校验。",
            Identity = identity,
            VisibleToDepartment = department
        };

    private async Task SeedAsync()
    {
        await using var context = new ClubContext(_options);
        var technology = new DepartmentDO { Name = "科技部", Key = "technology" };
        var publicity = new DepartmentDO { Name = "宣传部", Key = "publicity" };

        await context.Departments.AddRangeAsync(technology, publicity);

        context.Staffs.AddRange(
            new StaffDO
            {
                UserId = DepartmentUserId,
                Name = "部员",
                Identity = "Department",
                Department = technology
            },
            new StaffDO
            {
                UserId = MinisterUserId,
                Name = "部长",
                Identity = "Minister",
                Department = technology
            });

        context.Articles.AddRange(
            new ArticleDO
            {
                Path = MinisterOnlyPath,
                Title = "部长专属",
                Content = "只有部长及以上职级可以看到的内容。",
                Identity = "Minister"
            },
            new ArticleDO
            {
                Path = DepartmentArticlePath,
                Title = "科技部文章",
                Content = "科技部成员可以看到的内容。",
                Identity = "Department",
                VisibleToDepartment = technology.Name
            },
            new ArticleDO
            {
                Path = OtherDepartmentArticlePath,
                Title = "宣传部文章",
                Content = "宣传部成员可以看到的内容。",
                Identity = "Department",
                VisibleToDepartment = publicity.Name
            });

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Department_CannotUpdateMinisterOnlyArticle()
    {
        await SeedAsync();
        var controller = CreateController(DepartmentUserId, "Department");

        var response = await controller.UpdateArticle(MinisterOnlyPath, ValidUpdate("被篡改的标题", "Member", null));

        Assert.Equal(ErrorCode.InsufficientPermission, Unwrap(response).ErrorCode);
        var stored = await _articleRepository.GetFromPath(MinisterOnlyPath);
        Assert.Equal("部长专属", stored!.Title);
    }

    [Fact]
    public async Task Department_CannotDeleteMinisterOnlyArticle()
    {
        await SeedAsync();
        var controller = CreateController(DepartmentUserId, "Department");

        var response = await controller.DeleteArticle(MinisterOnlyPath);

        Assert.Equal(ErrorCode.InsufficientPermission, Unwrap(response).ErrorCode);
        Assert.NotNull(await _articleRepository.GetFromPath(MinisterOnlyPath));
    }

    [Fact]
    public async Task Department_CannotUpdateArticleOfAnotherDepartment()
    {
        await SeedAsync();
        var controller = CreateController(DepartmentUserId, "Department");

        var response = await controller.UpdateArticle(
            OtherDepartmentArticlePath,
            ValidUpdate("跨部门篡改", "Member", null));

        Assert.Equal(ErrorCode.InsufficientPermission, Unwrap(response).ErrorCode);
    }

    [Fact]
    public async Task Department_CanUpdateArticleVisibleToOwnDepartment()
    {
        await SeedAsync();
        var controller = CreateController(DepartmentUserId, "Department");

        var response = await controller.UpdateArticle(
            DepartmentArticlePath,
            ValidUpdate("科技部新标题"));

        Assert.Equal(ErrorCode.Success, Unwrap(response).ErrorCode);
        var stored = await _articleRepository.GetFromPath(DepartmentArticlePath);
        Assert.Equal("科技部新标题", stored!.Title);
    }

    [Fact]
    public async Task Department_CannotRaiseArticlePermissionAboveOwnRank()
    {
        await SeedAsync();
        var controller = CreateController(DepartmentUserId, "Department");

        var response = await controller.UpdateArticle(
            DepartmentArticlePath,
            ValidUpdate("试图提升权限", "Minister"));

        Assert.Equal(ErrorCode.InsufficientPermission, Unwrap(response).ErrorCode);
        var stored = await _articleRepository.GetFromPath(DepartmentArticlePath);
        Assert.Equal("Department", stored!.Identity);
    }

    [Fact]
    public async Task Department_UpdateWithUnknownIdentityIsRejected()
    {
        await SeedAsync();
        var controller = CreateController(DepartmentUserId, "Department");

        var response = await controller.UpdateArticle(
            DepartmentArticlePath,
            ValidUpdate("非法权限", "Admin"));

        Assert.Equal(ErrorCode.ParameterValidationFailed, Unwrap(response).ErrorCode);
    }

    [Fact]
    public async Task Minister_CanUpdateMinisterOnlyArticle()
    {
        await SeedAsync();
        var controller = CreateController(MinisterUserId, "Minister");

        var response = await controller.UpdateArticle(
            MinisterOnlyPath,
            ValidUpdate("部长修改后的标题", "Minister", null));

        Assert.Equal(ErrorCode.Success, Unwrap(response).ErrorCode);
        var stored = await _articleRepository.GetFromPath(MinisterOnlyPath);
        Assert.Equal("部长修改后的标题", stored!.Title);
    }

    [Fact]
    public async Task GetArticles_OnlyReturnsAccessibleArticles()
    {
        await SeedAsync();
        var controller = CreateController(DepartmentUserId, "Department");

        var response = await controller.GetArticles();

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var api = Assert.IsType<ApiResponse<IEnumerable<ArticleListItemVO>>>(ok.Value);
        var paths = api.Data!.Select(article => article.Path).ToHashSet();

        Assert.Contains(DepartmentArticlePath, paths);
        Assert.DoesNotContain(MinisterOnlyPath, paths);
        Assert.DoesNotContain(OtherDepartmentArticlePath, paths);
    }
}

using Mapster;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.IdentityModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace iOSClub.WebAPI.Controllers;

[ApiController]
[Route("[controller]")] // 使用C#推荐的API路径格式
public class ArticleController(
    IArticleRepository articleRepository,
    IStaffRepository staffRepository,
    IDepartmentRepository departmentRepository)
    : ControllerBase
{
    /// <summary>
    /// 获取当前用户可见的文章列表
    /// </summary>
    [Authorize(Roles = "Founder,President,Minister,Department")]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<ArticleListItemVO>>>> GetArticles()
    {
        var accessScope = await GetAccessScope();
        var articles = (await articleRepository.GetAll())
            .Where(article => ArticleAccessPolicy.CanAccess(accessScope, article));
        return Ok(ApiResponse<IEnumerable<ArticleListItemVO>>.Success(articles.Adapt<List<ArticleListItemVO>>()
            .OrderByDescending(x => x.LastWriteTime)));
    }

    /// <summary>
    /// 根据路径获取文章（公开访问）
    /// </summary>
    [HttpGet("{path}")]
    public async Task<ActionResult<ApiResponse<ArticleVO>>> GetArticle(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Ok(ApiResponse<ArticleVO>.Fail(ErrorCode.ParameterEmpty, "路径不能为空"));
        }

        var article = await articleRepository.GetFromPath(path, await GetAccessScope());
        return Ok(article == null
            ? ApiResponse<ArticleVO>.Fail(ErrorCode.ArticleNotFound, $"未找到路径为 '{path}' 的文章")
            : ApiResponse<ArticleVO>.Success(article.Adapt<ArticleVO>()));
    }

    /// <summary>
    /// 创建新文章（需要社团成员身份）
    /// </summary>
    [Authorize(Roles = "Founder,President,Minister,Department")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ArticleVO>>> CreateArticle([FromBody] ArticleCreateDTO createDto)
    {
        // 数据验证
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(createDto, new ValidationContext(createDto), validationResults, true))
        {
            var errorMessage = string.Join(", ", validationResults.Select(v => v.ErrorMessage));
            return Ok(ApiResponse<ArticleVO>.Fail(ErrorCode.ParameterValidationFailed, errorMessage));
        }

        if (!ArticleAccessPolicy.IsKnownIdentity(createDto.Identity))
        {
            return Ok(ApiResponse<ArticleVO>.Fail(ErrorCode.ParameterValidationFailed,
                "访问权限取值无效，允许的值为 Founder、President、Minister、Department、Member 或留空"));
        }

        var accessScope = await GetAccessScope();
        if (!ArticleAccessPolicy.CanAssignIdentity(accessScope, createDto.Identity))
        {
            return Ok(ApiResponse<ArticleVO>.Fail(ErrorCode.InsufficientPermission,
                "不能将文章访问权限设置为高于自己的职级"));
        }

        var visibleToDepartment = NormalizeDepartment(createDto.VisibleToDepartment);
        if (visibleToDepartment != null &&
            !await departmentRepository.DepartmentExistsAsync(visibleToDepartment))
        {
            return Ok(ApiResponse<ArticleVO>.Fail(ErrorCode.ResourceNotFound,
                $"部门 '{visibleToDepartment}' 不存在"));
        }

        // 检查路径是否已存在
        var existingArticle = await articleRepository.GetFromPath(createDto.Path);
        if (existingArticle != null)
        {
            return Ok(ApiResponse<ArticleVO>.Fail(ErrorCode.ResourceAlreadyExists,
                $"路径 '{createDto.Path}' 已存在"));
        }

        var articleModel = new ArticleDO
        {
            Path = createDto.Path,
            Title = createDto.Title,
            Content = createDto.Content,
            Identity = createDto.Identity,
            VisibleToDepartment = visibleToDepartment,
            Category = string.IsNullOrEmpty(createDto.Category)
                ? null
                : new CategoryDO() { Name = createDto.Category },
            ArticleOrder = createDto.ArticleOrder,
            LastWriteTime = DateTime.UtcNow
        };

        var success = await articleRepository.CreateOrUpdate(articleModel);
        if (!success)
        {
            return Ok(ApiResponse<ArticleVO>.Fail(ErrorCode.OperationFailed, "创建文章失败"));
        }

        var createdArticle = await articleRepository.GetFromPath(createDto.Path);
        if (createdArticle == null)
        {
            return Ok(ApiResponse<ArticleVO>.Fail(ErrorCode.InternalServerError, "创建文章成功，但获取文章失败"));
        }

        return CreatedAtAction(nameof(GetArticle), new { path = createDto.Path },
            ApiResponse<ArticleVO>.Success(createdArticle.Adapt<ArticleVO>(), "文章创建成功"));
    }

    /// <summary>
    /// 更新文章（需要社团成员身份）- 使用POST更安全
    /// </summary>
    [Authorize(Roles = "Founder,President,Minister,Department")]
    [HttpPost("update/{path}")]
    public async Task<ActionResult<ApiResponse>> UpdateArticle(string path, [FromBody] ArticleUpdateDTO updateDto)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Ok(ApiResponse.Fail(ErrorCode.ParameterEmpty, "路径不能为空"));
        }

        // 验证更新数据
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(updateDto, new ValidationContext(updateDto), validationResults, true))
        {
            var errorMessage = string.Join(", ", validationResults.Select(v => v.ErrorMessage));
            return Ok(ApiResponse.Fail(ErrorCode.ParameterValidationFailed, errorMessage));
        }

        if (!ArticleAccessPolicy.IsKnownIdentity(updateDto.Identity))
        {
            return Ok(ApiResponse.Fail(ErrorCode.ParameterValidationFailed,
                "访问权限取值无效，允许的值为 Founder、President、Minister、Department、Member 或留空"));
        }

        var accessScope = await GetAccessScope();
        if (!ArticleAccessPolicy.CanAssignIdentity(accessScope, updateDto.Identity))
        {
            return Ok(ApiResponse.Fail(ErrorCode.InsufficientPermission,
                "不能将文章访问权限设置为高于自己的职级"));
        }

        var visibleToDepartment = NormalizeDepartment(updateDto.VisibleToDepartment);
        if (visibleToDepartment != null &&
            !await departmentRepository.DepartmentExistsAsync(visibleToDepartment))
        {
            return Ok(ApiResponse.Fail(ErrorCode.ResourceNotFound,
                $"部门 '{visibleToDepartment}' 不存在"));
        }

        // 检查文章是否存在，且当前用户是否有权修改
        var existingArticle = await articleRepository.GetFromPath(path);
        if (existingArticle == null)
        {
            return Ok(ApiResponse.Fail(ErrorCode.ArticleNotFound, $"未找到路径为 '{path}' 的文章"));
        }

        if (!ArticleAccessPolicy.CanAccess(accessScope, existingArticle))
        {
            return Ok(ApiResponse.Fail(ErrorCode.InsufficientPermission, "无权修改该文章"));
        }

        // 更新文章信息
        existingArticle.Title = updateDto.Title;
        existingArticle.Content = updateDto.Content;
        existingArticle.Identity = updateDto.Identity;
        existingArticle.VisibleToDepartment = visibleToDepartment;
        existingArticle.Category = string.IsNullOrEmpty(updateDto.Category)
            ? null
            : new CategoryDO() { Name = updateDto.Category };
        existingArticle.ArticleOrder = updateDto.ArticleOrder;
        existingArticle.LastWriteTime = DateTime.UtcNow;

        var success = await articleRepository.CreateOrUpdate(existingArticle);
        return !success
            ? Ok(ApiResponse.Fail(ErrorCode.OperationFailed, "更新文章失败"))
            : Ok(ApiResponse.Success("文章更新成功"));
    }

    /// <summary>
    /// 删除文章（需要管理员身份）
    /// </summary>
    [Authorize(Roles = "Founder,President,Minister,Department")]
    [HttpPost("delete/{path}")]
    public async Task<ActionResult<ApiResponse>> DeleteArticle(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Ok(ApiResponse.Fail(ErrorCode.ParameterEmpty, "路径不能为空"));
        }

        // 检查文章是否存在，且当前用户是否有权删除
        var existingArticle = await articleRepository.GetFromPath(path);
        if (existingArticle == null)
        {
            return Ok(ApiResponse.Fail(ErrorCode.ArticleNotFound, $"未找到路径为 '{path}' 的文章"));
        }

        if (!ArticleAccessPolicy.CanAccess(await GetAccessScope(), existingArticle))
        {
            return Ok(ApiResponse.Fail(ErrorCode.InsufficientPermission, "无权删除该文章"));
        }

        var success = await articleRepository.Delete(path);
        if (!success)
        {
            return Ok(ApiResponse.Fail(ErrorCode.OperationFailed, "删除文章失败"));
        }

        return Ok(ApiResponse.Success("文章删除成功"));
    }

    /// <summary>
    /// 搜索文章并返回高亮片段（公开访问）
    /// </summary>
    [HttpGet("search/highlights")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ArticleSearchResult>>>> SearchArticlesWithHighlights(
        [FromQuery] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return Ok(ApiResponse<IEnumerable<ArticleSearchResult>>.Fail(ErrorCode.ParameterEmpty, "搜索关键词不能为空"));
        }

        var articles = await articleRepository.SearchArticlesWithHighlights(keyword, await GetAccessScope());
        return Ok(ApiResponse<IEnumerable<ArticleSearchResult>>.Success(
            articles.OrderByDescending(a => a.LastWriteTime)));
    }

    /// <summary>
    /// 获取文章分类列表
    /// </summary>
    [HttpGet("category")]
    public async Task<ActionResult<ApiResponse<object>>> GetAllCategoryArticles()
    {
        var articles = await articleRepository.GetAllCategoryArticles(await GetAccessScope());
        return Ok(ApiResponse<object>.Success(articles));
    }

    /// <summary>
    /// 批量更新文章顺序（需要社团成员身份）
    /// </summary>
    [Authorize(Roles = "Founder,President,Minister,Department")]
    [HttpPost("update-orders")]
    public async Task<ActionResult<ApiResponse>> UpdateArticleOrders([FromBody] Dictionary<string, int>? articleOrders)
    {
        if (articleOrders == null || articleOrders.Count == 0)
        {
            return Ok(ApiResponse.Fail(ErrorCode.ParameterEmpty, "文章顺序字典不能为空"));
        }

        // 顺序调整也是一种修改：只能操作自己可见的文章
        var accessScope = await GetAccessScope();
        var accessiblePaths = (await articleRepository.GetAll())
            .Where(article => ArticleAccessPolicy.CanAccess(accessScope, article))
            .Select(article => article.Path)
            .ToHashSet(StringComparer.Ordinal);

        if (articleOrders.Keys.Any(path => !accessiblePaths.Contains(path)))
        {
            return Ok(ApiResponse.Fail(ErrorCode.InsufficientPermission, "无权调整未授权文章的顺序"));
        }

        var success = await articleRepository.UpdateArticleOrders(articleOrders);
        return Ok(success ? ApiResponse.Success("文章顺序更新成功") : ApiResponse.Fail(ErrorCode.OperationFailed, "文章顺序更新失败"));
    }

    private async Task<ArticleAccessScope> GetAccessScope()
    {
        var user = HttpContext.User.GetUser();
        if (user == null)
            return new ArticleAccessScope();

        if (user.Identity is "Founder" or "President")
            return new ArticleAccessScope(user.Identity);

        var staff = await staffRepository.GetStaffByIdAsync(user.UserId);
        return new ArticleAccessScope(user.Identity, staff?.Department?.Name);
    }

    private static string? NormalizeDepartment(string? departmentName)
        => string.IsNullOrWhiteSpace(departmentName) ? null : departmentName.Trim();
}
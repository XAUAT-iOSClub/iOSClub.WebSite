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
public class CategoryController(
    ICategoryRepository categoryRepository,
    IStaffRepository staffRepository)
    : ControllerBase
{
    /// <summary>
    /// 获取所有分类（公开访问）
    /// </summary>
    [HttpGet("all")]
    public async Task<ActionResult<ApiResponse<IEnumerable<CategoryVO>>>> GetAllCategories()
    {
        var categories = await categoryRepository.GetAll();
        return Ok(ApiResponse<IEnumerable<CategoryVO>>.Success(categories.Adapt<List<CategoryVO>>()));
    }

    /// <summary>
    /// 根据名称获取分类（公开访问）
    /// </summary>
    [HttpGet("{name}")]
    public async Task<ActionResult<ApiResponse<CategoryVO>>> GetCategory(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Ok(ApiResponse<CategoryVO>.Fail(ErrorCode.ParameterEmpty, "分类名称不能为空"));
        }

        // 分类详情里带有文章列表，必须按访问者可见性过滤（与 ArticleController 同一套语义）。
        var category = await categoryRepository.GetByName(name, await GetAccessScope());
        if (category == null)
        {
            return Ok(ApiResponse<CategoryVO>.Fail(ErrorCode.CategoryNotFound, $"未找到名称为 '{name}' 的分类"));
        }

        return Ok(ApiResponse<CategoryVO>.Success(category.Adapt<CategoryVO>()));
    }

    [HttpGet("byId/{id}")]
    public async Task<ActionResult<ApiResponse<CategoryVO>>> GetCategoryById(string id)
    {
        var category = await categoryRepository.GetById(id);
        if (category == null)
        {
            return Ok(ApiResponse<CategoryVO>.Fail(ErrorCode.CategoryNotFound, $"未找到ID为 '{id}' 的分类"));
        }

        return Ok(ApiResponse<CategoryVO>.Success(category.Adapt<CategoryVO>()));
    }

    [HttpGet("articles/{id}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ArticleListItemVO>>>> GetArticles(string id)
    {
        // 公开接口，但文章列表必须遵循 ArticleAccessPolicy，不能绕过文章详情接口的可见性。
        var articles = await categoryRepository.GetArticlesById(id, await GetAccessScope());
        return Ok(ApiResponse<IEnumerable<ArticleListItemVO>>.Success(articles.Adapt<List<ArticleListItemVO>>()));
    }

    /// <summary>
    /// 创建或更新分类（需要管理员身份）
    /// </summary>
    [Authorize(Roles = "Founder, President")]
    [HttpPost("CreateOrUpdate")]
    public async Task<ActionResult<ApiResponse<string>>> CreateOrUpdateCategory([FromBody] CategoryCreateUpdateDTO dto)
    {
        // 数据验证
        var validationResults = new List<ValidationResult>();
        var entity = dto.Adapt<CategoryDO>();
        if (!Validator.TryValidateObject(entity, new ValidationContext(entity), validationResults, true))
        {
            var errorMessage = string.Join(", ", validationResults.Select(v => v.ErrorMessage));
            return Ok(ApiResponse<string>.Fail(ErrorCode.ParameterValidationFailed, errorMessage));
        }

        var result = await categoryRepository.CreateOrUpdate(entity);
        if (result)
        {
            return Ok(ApiResponse<string>.Success("分类创建/更新成功"));
        }

        return Ok(ApiResponse<string>.Fail(ErrorCode.OperationFailed, "分类创建/更新失败"));
    }

    /// <summary>
    /// 删除分类（需要管理员身份）
    /// </summary>
    [Authorize(Roles = "Founder, President")]
    [HttpGet("Delete/{name}")]
    public async Task<ActionResult<ApiResponse<string>>> DeleteCategory(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Ok(ApiResponse<string>.Fail(ErrorCode.ParameterEmpty, "分类名称不能为空"));
        }

        var result = await categoryRepository.Delete(name);
        if (result)
        {
            return Ok(ApiResponse<string>.Success("分类删除成功"));
        }

        return Ok(ApiResponse<string>.Fail(ErrorCode.CategoryNotFound, "分类不存在"));
    }

    /// <summary>
    /// 更新分类顺序（需要管理员身份）
    /// </summary>
    [Authorize(Roles = "Founder, President")]
    [HttpPost("UpdateOrder/{name}/{order:int}")]
    public async Task<ActionResult<ApiResponse<string>>> UpdateCategoryOrder(string name, int order)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Ok(ApiResponse<string>.Fail(ErrorCode.ParameterEmpty, "分类名称不能为空"));
        }

        var result = await categoryRepository.UpdateCategoryOrder(name, order);
        if (result)
        {
            return Ok(ApiResponse<string>.Success("分类顺序更新成功"));
        }

        return Ok(ApiResponse<string>.Fail(ErrorCode.CategoryNotFound, "分类不存在"));
    }

    /// <summary>
    /// 批量更新分类顺序（需要管理员身份）
    /// </summary>
    [Authorize(Roles = "Founder, President")]
    [HttpPost("UpdateOrders")]
    public async Task<ActionResult<ApiResponse<string>>> UpdateCategoryOrders([FromBody] Dictionary<string, int>? categoryOrders)
    {
        if (categoryOrders == null || categoryOrders.Count == 0)
        {
            return Ok(ApiResponse<string>.Fail(ErrorCode.ParameterEmpty, "分类顺序字典不能为空"));
        }

        var result = await categoryRepository.UpdateCategoryOrders(categoryOrders);
        if (result)
        {
            return Ok(ApiResponse<string>.Success("分类顺序批量更新成功"));
        }

        return Ok(ApiResponse<string>.Fail(ErrorCode.OperationFailed, "部分或全部分类更新失败"));
    }

    /// <summary>
    /// 当前访问者的文章可见范围，语义与 ArticleController.GetAccessScope 保持一致：
    /// 匿名以普通成员处理；Founder/President 不受部门约束；其他角色按员工表取部门。
    /// </summary>
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
}

using Mapster;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.IdentityModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")] // 使用C#推荐的API路径格式
public class ResourceController(
    IDbContextFactory<ClubContext> factory,
    IHttpContextAccessor httpContextAccessor,
    IResourceRepository resourceRepository,
    ILogger<ResourceController> logger)
    : ControllerBase
{
    /// <summary>
    /// 获取所有资源（需要社团成员身份）
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ResourceVO>>>> GetAllResources()
    {
        var userJwt = httpContextAccessor.HttpContext?.User.GetUser();
        if (userJwt == null)
            return Ok(ApiResponse<List<ResourceVO>>.Fail(ErrorCode.Unauthorized, "用户未认证"));

        await using var context = await factory.CreateDbContextAsync();
        var user = await context.Staffs.FirstOrDefaultAsync(x => x.UserId == userJwt.UserId);

        // 检查用户身份：必须是社团成员及以上
        if (user == null || !IsClubMember(user.Identity))
            return Ok(ApiResponse<List<ResourceVO>>.Fail(ErrorCode.InsufficientPermission, "权限不足，需要社团成员身份"));

        var resources = await resourceRepository.GetAllResourcesAsync();
        return Ok(ApiResponse<List<ResourceVO>>.Success(resources.Adapt<List<ResourceVO>>(), "获取所有资源成功"));
    }

    /// <summary>
    /// 根据ID获取资源（需要社团成员身份）
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ResourceVO>>> GetResourceById(string id)
    {
        var userJwt = httpContextAccessor.HttpContext?.User.GetUser();
        if (userJwt == null)
            return Ok(ApiResponse<ResourceVO>.Fail(ErrorCode.Unauthorized, "用户未认证"));

        await using var context = await factory.CreateDbContextAsync();
        var user = await context.Staffs.FirstOrDefaultAsync(x => x.UserId == userJwt.UserId);

        // 检查用户身份：必须是社团成员及以上
        if (user == null || !IsClubMember(user.Identity))
            return Ok(ApiResponse<ResourceVO>.Fail(ErrorCode.InsufficientPermission, "权限不足，需要社团成员身份"));

        var resource = await resourceRepository.GetResourceByIdAsync(id);
        if (resource == null)
            return Ok(ApiResponse<ResourceVO>.Fail(ErrorCode.ResourceNotFound, "资源不存在"));

        return Ok(ApiResponse<ResourceVO>.Success(resource.Adapt<ResourceVO>(), "获取资源成功"));
    }

    /// <summary>
    /// 根据标签筛选资源（需要社团成员身份）
    /// </summary>
    [HttpGet("tag/{tag}")]
    public async Task<ActionResult<ApiResponse<List<ResourceVO>>>> GetResourcesByTag(string tag)
    {
        var userJwt = httpContextAccessor.HttpContext?.User.GetUser();
        if (userJwt == null)
            return Ok(ApiResponse<List<ResourceVO>>.Fail(ErrorCode.Unauthorized, "用户未认证"));

        await using var context = await factory.CreateDbContextAsync();
        var user = await context.Staffs.FirstOrDefaultAsync(x => x.UserId == userJwt.UserId);

        // 检查用户身份：必须是社团成员及以上
        if (user == null || !IsClubMember(user.Identity))
            return Ok(ApiResponse<List<ResourceVO>>.Fail(ErrorCode.InsufficientPermission, "权限不足，需要社团成员身份"));

        var resources = await resourceRepository.GetResourcesByTagAsync(tag);
        return Ok(ApiResponse<List<ResourceVO>>.Success(resources.Adapt<List<ResourceVO>>(), "根据标签获取资源成功"));
    }

    /// <summary>
    /// 搜索资源（需要社团成员身份）
    /// </summary>
    [HttpGet("search/{name}")]
    public async Task<ActionResult<ApiResponse<List<ResourceVO>>>> SearchResources(string name)
    {
        var userJwt = httpContextAccessor.HttpContext?.User.GetUser();
        if (userJwt == null)
            return Ok(ApiResponse<List<ResourceVO>>.Fail(ErrorCode.Unauthorized, "用户未认证"));

        await using var context = await factory.CreateDbContextAsync();
        var user = await context.Staffs.FirstOrDefaultAsync(x => x.UserId == userJwt.UserId);

        // 检查用户身份：必须是社团成员及以上
        if (user == null || !IsClubMember(user.Identity))
            return Ok(ApiResponse<List<ResourceVO>>.Fail(ErrorCode.InsufficientPermission, "权限不足，需要社团成员身份"));

        var resources = await resourceRepository.SearchResourcesByNameAsync(name);
        return Ok(ApiResponse<List<ResourceVO>>.Success(resources.Adapt<List<ResourceVO>>(), "搜索资源成功"));
    }

    /// <summary>
    /// 添加资源（需要社长、团支书或部长身份）
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Founder,President,Minister")]
    public async Task<ActionResult<ApiResponse<ResourceVO>>> AddResource([FromBody] ResourceCreateUpdateDTO resource)
    {
        var userJwt = httpContextAccessor.HttpContext?.User.GetUser();
        if (userJwt == null)
            return Ok(ApiResponse<ResourceVO>.Fail(ErrorCode.Unauthorized, "用户未认证"));

        // 双重验证：除了角色授权外，再检查用户身份
        await using var context = await factory.CreateDbContextAsync();
        var user = await context.Staffs.FirstOrDefaultAsync(x => x.UserId == userJwt.UserId);

        if (user == null || !IsAdmin(user.Identity))
            return Ok(ApiResponse<ResourceVO>.Fail(ErrorCode.InsufficientPermission, "权限不足，需要管理员身份"));

        var createdResource = await resourceRepository.AddResourceAsync(resource.Adapt<ResourceDO>());
        if (createdResource == null)
            return Ok(ApiResponse<ResourceVO>.Fail(ErrorCode.OperationFailed, "添加资源失败"));

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("添加资源成功，资源ID: {ResourceId}", createdResource.Id);
        }

        // 响应与 Location 一律使用持久化后的 DO：ID 由仓库生成，请求 DTO 里的 ID 始终为空。
        return CreatedAtAction(nameof(GetResourceById), new { id = createdResource.Id },
            ApiResponse<ResourceVO>.Success(createdResource.Adapt<ResourceVO>(), "添加资源成功"));
    }

    /// <summary>
    /// 更新资源（需要社长、团支书或部长身份）
    /// </summary>
    [HttpPut]
    [Authorize(Roles = "Founder,President,Minister")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateResource([FromBody] ResourceCreateUpdateDTO resource)
    {
        if (string.IsNullOrEmpty(resource.Id))
        {
            return Ok(ApiResponse<object>.Fail(ErrorCode.ResourceNotFound, "资源不存在"));
        }

        var userJwt = httpContextAccessor.HttpContext?.User.GetUser();
        if (userJwt == null)
            return Ok(ApiResponse<object>.Fail(ErrorCode.Unauthorized, "用户未认证"));

        // 双重验证
        await using var context = await factory.CreateDbContextAsync();
        var user = await context.Staffs.FirstOrDefaultAsync(x => x.UserId == userJwt.UserId);

        if (user == null || !IsAdmin(user.Identity))
            return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, "权限不足，需要管理员身份"));

        // 检查资源是否存在
        var existingResource = await resourceRepository.GetResourceByIdAsync(resource.Id);
        if (existingResource == null)
            return Ok(ApiResponse<object>.Fail(ErrorCode.ResourceNotFound, "资源不存在"));

        var result = await resourceRepository.UpdateResourceAsync(resource.Adapt<ResourceDO>());
        if (!result)
            return Ok(ApiResponse<object>.Fail(ErrorCode.OperationFailed, "更新资源失败"));

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("更新资源成功，资源ID: {ResourceId}", resource.Id);
        }

        return Ok(ApiResponse.Success("资源更新成功"));
    }

    /// <summary>
    /// 删除资源（需要社长、团支书或部长身份）
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Founder,President,Minister")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteResource(string id)
    {
        var userJwt = httpContextAccessor.HttpContext?.User.GetUser();
        if (userJwt == null)
            return Ok(ApiResponse<object>.Fail(ErrorCode.Unauthorized, "用户未认证"));

        // 双重验证
        await using var context = await factory.CreateDbContextAsync();
        var user = await context.Staffs.FirstOrDefaultAsync(x => x.UserId == userJwt.UserId);

        if (user == null || !IsAdmin(user.Identity))
            return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, "权限不足，需要管理员身份"));

        // 检查资源是否存在
        var existingResource = await resourceRepository.GetResourceByIdAsync(id);
        if (existingResource == null)
            return Ok(ApiResponse<object>.Fail(ErrorCode.ResourceNotFound, "资源不存在"));

        var result = await resourceRepository.DeleteResourceAsync(id);
        if (!result)
            return Ok(ApiResponse<object>.Fail(ErrorCode.OperationFailed, "删除资源失败"));

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("删除资源成功，资源ID: {ResourceId}", id);
        }

        return Ok(ApiResponse.Success("资源删除成功"));
    }

    /// <summary>
    /// 获取所有标签（需要社团成员身份）
    /// </summary>
    [HttpGet("tags")]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetAllTags()
    {
        var userJwt = httpContextAccessor.HttpContext?.User.GetUser();
        if (userJwt == null)
            return Ok(ApiResponse<List<string>>.Fail(ErrorCode.Unauthorized, "用户未认证"));

        await using var context = await factory.CreateDbContextAsync();
        var user = await context.Staffs.FirstOrDefaultAsync(x => x.UserId == userJwt.UserId);

        // 检查用户身份：必须是社团成员及以上
        if (user == null || !IsClubMember(user.Identity))
            return Ok(ApiResponse<List<string>>.Fail(ErrorCode.InsufficientPermission, "权限不足，需要社团成员身份"));

        var tags = await resourceRepository.GetAllTagsAsync();
        return Ok(ApiResponse<List<string>>.Success(tags, "获取所有标签成功"));
    }

    /// <summary>
    /// 获取资源统计（需要社团成员身份）
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<ApiResponse<object>>> GetResourceStatistics()
    {
        var userJwt = httpContextAccessor.HttpContext?.User.GetUser();
        if (userJwt == null)
            return Ok(ApiResponse<object>.Fail(ErrorCode.Unauthorized, "用户未认证"));

        await using var context = await factory.CreateDbContextAsync();
        var user = await context.Staffs.FirstOrDefaultAsync(x => x.UserId == userJwt.UserId);

        // 检查用户身份：必须是社团成员及以上
        if (user == null || !IsClubMember(user.Identity))
            return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, "权限不足，需要社团成员身份"));

        var count = await resourceRepository.GetResourceCountAsync();
        var tags = await resourceRepository.GetAllTagsAsync();

        var result = new
        {
            TotalCount = count,
            TagCount = tags.Count,
            Tags = tags
        };

        return Ok(ApiResponse<object>.Success(result, "获取资源统计成功"));
    }

    /// <summary>
    /// 检查用户是否是社团成员及以上身份
    /// </summary>
    private static bool IsClubMember(string? identity)
    {
        return identity is "Founder" or "President" or "Minister" or "Department";
    }

    /// <summary>
    /// 检查用户是否是管理员身份
    /// </summary>
    private static bool IsAdmin(string? identity)
    {
        return identity is "Founder" or "President" or "Minister";
    }
}
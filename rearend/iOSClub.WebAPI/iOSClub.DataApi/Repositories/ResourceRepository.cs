using iOSClub.Data;
using iOSClub.Data.DataObjects;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.DataApi.Repositories;

/// <summary>
/// 资源仓库接口，提供资源数据的CRUD操作和查询功能
/// </summary>
public interface IResourceRepository
{
    /// <summary>
    /// 获取所有资源
    /// </summary>
    /// <returns>资源列表</returns>
    Task<List<ResourceDO>> GetAllResourcesAsync();
    
    /// <summary>
    /// 根据ID获取资源
    /// </summary>
    /// <param name="id">资源ID</param>
    /// <returns>资源模型，如果找不到则返回null</returns>
    Task<ResourceDO?> GetResourceByIdAsync(string id);
    
    /// <summary>
    /// 根据标签获取资源
    /// </summary>
    /// <param name="tag">资源标签</param>
    /// <returns>资源列表</returns>
    Task<List<ResourceDO>> GetResourcesByTagAsync(string tag);
    
    /// <summary>
    /// 根据名称搜索资源
    /// </summary>
    /// <param name="name">资源名称搜索词</param>
    /// <returns>资源列表</returns>
    Task<List<ResourceDO>> SearchResourcesByNameAsync(string name);
    
    /// <summary>
    /// 添加资源
    /// </summary>
    /// <param name="resource">资源模型</param>
    /// <returns>持久化后的资源模型（包含服务端生成的 ID）；失败时返回 null</returns>
    Task<ResourceDO?> AddResourceAsync(ResourceDO resource);
    
    /// <summary>
    /// 更新资源
    /// </summary>
    /// <param name="resource">资源模型</param>
    /// <returns>是否更新成功</returns>
    Task<bool> UpdateResourceAsync(ResourceDO resource);
    
    /// <summary>
    /// 删除资源
    /// </summary>
    /// <param name="id">资源ID</param>
    /// <returns>是否删除成功</returns>
    Task<bool> DeleteResourceAsync(string id);
    
    /// <summary>
    /// 获取所有标签
    /// </summary>
    /// <returns>标签列表</returns>
    Task<List<string>> GetAllTagsAsync();
    
    /// <summary>
    /// 获取资源数量
    /// </summary>
    /// <returns>资源数量</returns>
    Task<int> GetResourceCountAsync();
    
    /// <summary>
    /// 检查资源是否存在
    /// </summary>
    /// <param name="id">资源ID</param>
    /// <returns>资源是否存在</returns>
    Task<bool> ResourceExistsAsync(string id);
}

public class ResourceRepository(IDbContextFactory<ClubContext> factory) : IResourceRepository
{

    /// <summary>
    /// 获取所有资源
    /// </summary>
    public async Task<List<ResourceDO>> GetAllResourcesAsync()
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.Resources.ToListAsync();
    }

    /// <summary>
    /// 根据ID获取资源
    /// </summary>
    public async Task<ResourceDO?> GetResourceByIdAsync(string id)
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.Resources.FindAsync(id);
    }

    /// <summary>
    /// 根据标签获取资源
    /// </summary>
    public async Task<List<ResourceDO>> GetResourcesByTagAsync(string tag)
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.Resources
            .Where(r => r.Tag != null && r.Tag.Contains(tag))
            .ToListAsync();
    }

    /// <summary>
    /// 根据名称搜索资源
    /// </summary>
    public async Task<List<ResourceDO>> SearchResourcesByNameAsync(string name)
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.Resources
            .Where(r => r.Name.Contains(name))
            .ToListAsync();
    }

    /// <summary>
    /// 添加资源
    /// </summary>
    /// <returns>持久化后的资源模型（包含服务端生成的 ID）；失败时返回 null</returns>
    public async Task<ResourceDO?> AddResourceAsync(ResourceDO resource)
    {
        await using var context = await factory.CreateDbContextAsync();
        try
        {
            // 生成唯一ID。ID 在此处生成，因此必须把持久化后的 DO 返回给调用方，
            // 否则控制器只能使用请求 DTO 里的空 ID 组装响应。
            resource.Id = Guid.NewGuid().ToString("N");
            await context.Resources.AddAsync(resource);
            await context.SaveChangesAsync();
            return resource;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 更新资源
    /// </summary>
    public async Task<bool> UpdateResourceAsync(ResourceDO resource)
    {
        await using var context = await factory.CreateDbContextAsync();
        try
        {
            context.Resources.Update(resource);
            await context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 删除资源
    /// </summary>
    public async Task<bool> DeleteResourceAsync(string id)
    {
        await using var context = await factory.CreateDbContextAsync();
        try
        {
            var resource = await GetResourceByIdAsync(id);
            if (resource == null)
                return false;

            context.Resources.Remove(resource);
            await context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查资源是否存在
    /// </summary>
    public async Task<bool> ResourceExistsAsync(string id)
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.Resources.AnyAsync(r => r.Id == id);
    }

    /// <summary>
    /// 获取所有标签
    /// </summary>
    public async Task<List<string>> GetAllTagsAsync()
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.Resources
            .Where(r => r.Tag != null)
            .Select(r => r.Tag!)
            .Distinct()
            .ToListAsync();
    }

    /// <summary>
    /// 获取资源数量
    /// </summary>
    public async Task<int> GetResourceCountAsync()
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.Resources.CountAsync();
    }
}
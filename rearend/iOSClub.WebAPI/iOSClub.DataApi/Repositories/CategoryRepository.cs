using iOSClub.Data;
using iOSClub.Data.DataObjects;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.DataApi.Repositories;

/// <summary>
/// 分类仓库接口，提供分类数据的CRUD操作和查询功能
/// </summary>
public interface ICategoryRepository
{
    /// <summary>
    /// 获取所有分类
    /// </summary>
    /// <returns>分类列表</returns>
    public Task<IEnumerable<CategoryDO>> GetAll();
    
    /// <summary>
    /// 根据名称获取分类
    /// </summary>
    /// <param name="name">分类名称</param>
    /// <param name="accessScope">访问者身份和部门；为空时用于后台操作，不进行可见性过滤</param>
    /// <returns>分类模型，如果找不到则返回null</returns>
    public Task<CategoryDO?> GetByName(string name, ArticleAccessScope? accessScope = null);
    
    /// <summary>
    /// 创建或更新分类
    /// </summary>
    /// <param name="model">分类模型</param>
    /// <returns>是否操作成功</returns>
    public Task<bool> CreateOrUpdate(CategoryDO model);
    
    /// <summary>
    /// 删除分类
    /// </summary>
    /// <param name="name">分类名称</param>
    /// <returns>是否删除成功</returns>
    public Task<bool> Delete(string name);
    
    /// <summary>
    /// 更新分类顺序
    /// </summary>
    /// <param name="name">分类名称</param>
    /// <param name="order">新的顺序</param>
    /// <returns>是否更新成功</returns>
    public Task<bool> UpdateCategoryOrder(string name, int order);
    
    /// <summary>
    /// 批量更新分类顺序
    /// </summary>
    /// <param name="categoryOrders">分类ID和顺序的字典，可为null</param>
    /// <returns>是否更新成功</returns>
    public Task<bool> UpdateCategoryOrders(Dictionary<string, int>? categoryOrders);
    
    /// <summary>
    /// 根据ID获取分类
    /// </summary>
    /// <param name="id">分类ID</param>
    /// <returns>分类模型，如果找不到则返回null</returns>
    Task<CategoryDO?> GetById(string id);
    
    /// <summary>
    /// 根据分类ID获取文章列表
    /// </summary>
    /// <param name="id">分类ID</param>
    /// <param name="accessScope">访问者身份和部门；为空时用于后台操作，不进行可见性过滤</param>
    /// <returns>文章数组</returns>
    public Task<ArticleDO[]> GetArticlesById(string id, ArticleAccessScope? accessScope = null);
}

public class CategoryRepository(IDbContextFactory<ClubContext> factory) : ICategoryRepository
{
    public async Task<IEnumerable<CategoryDO>> GetAll()
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.Categories
            .OrderBy(c => c.Order)
            .ToListAsync();
    }

    public async Task<CategoryDO?> GetByName(string name, ArticleAccessScope? accessScope = null)
    {
        await using var context = await factory.CreateDbContextAsync();
        var category = await context.Categories.Include(x => x.Articles)
            .FirstOrDefaultAsync(c => c.Name == name);

        if (category == null || accessScope == null)
            return category;

        // 分类 VO 里带文章列表，必须与文章详情接口使用同一套可见性规则，
        // 否则分类接口会泄露"部长仅见""其他部门限定"文章的标题/路径。
        category.Articles = category.Articles
            .Where(article => ArticleAccessPolicy.CanAccess(accessScope, article))
            .ToList();
        return category;
    }

    public async Task<bool> CreateOrUpdate(CategoryDO model)
    {
        await using var context = await factory.CreateDbContextAsync();

        var category = await context.Categories
            .FirstOrDefaultAsync(c => c.Name == model.Name);

        if (category == null)
        {
            // 如果是新分类，确保Id唯一
            if (string.IsNullOrEmpty(model.Id))
            {
                model.Id = Guid.NewGuid().ToString();
            }

            model.Order = await context.Categories.CountAsync() + 1;

            context.Categories.Add(model);
        }
        else
        {
            // 更新现有分类
            category.Description = model.Description;
            category.Order = model.Order;
        }

        return await context.SaveChangesAsync() == 1;
    }

    public async Task<bool> Delete(string name)
    {
        await using var context = await factory.CreateDbContextAsync();

        var category = await context.Categories
            .FirstOrDefaultAsync(c => c.Name == name);

        if (category == null)
        {
            return false;
        }

        context.Categories.Remove(category);
        return await context.SaveChangesAsync() == 1;
    }

    public async Task<bool> UpdateCategoryOrder(string name, int order)
    {
        await using var context = await factory.CreateDbContextAsync();

        var category = await context.Categories
            .FirstOrDefaultAsync(c => c.Name == name);

        if (category == null)
        {
            return false;
        }

        category.Order = order;
        return await context.SaveChangesAsync() == 1;
    }

    public async Task<bool> UpdateCategoryOrders(Dictionary<string, int>? categoryOrders)
    {
        if (categoryOrders == null || categoryOrders.Count == 0)
        {
            return true;
        }

        await using var context = await factory.CreateDbContextAsync();

        // 开始事务
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            // 获取所有需要更新的分类 - 使用ID而不是名称
            var categoryIds = categoryOrders.Keys.ToList();
            var categories = await context.Categories
                .Where(c => categoryIds.Contains(c.Id))
                .ToListAsync();

            // 更新每个分类的顺序
            foreach (var category in categories)
            {
                if (categoryOrders.TryGetValue(category.Id, out var order))
                {
                    category.Order = order;
                }
            }

            // 保存更改
            var result = await context.SaveChangesAsync();

            // 提交事务
            await transaction.CommitAsync();

            // 返回是否所有请求的分类都被更新
            return result == categories.Count;
        }
        catch
        {
            // 回滚事务
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<CategoryDO?> GetById(string id)
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.Categories.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<ArticleDO[]> GetArticlesById(string id, ArticleAccessScope? accessScope = null)
    {
        await using var context = await factory.CreateDbContextAsync();

        // 后台调用（accessScope == null）保持既有契约：只回填排序用的最小字段。
        if (accessScope == null)
        {
            return await context.Articles.Where(x => x.CategoryId == id).Select(x => new ArticleDO()
                {
                    Path = x.Path,
                    Title = x.Title,
                    ArticleOrder = x.ArticleOrder
                })
                .ToArrayAsync();
        }

        // 前台调用需要文章自身的职级/部门字段才能复用 ArticleAccessPolicy。
        var articles = await context.Articles.Where(x => x.CategoryId == id).Select(x => new ArticleDO()
            {
                Path = x.Path,
                Title = x.Title,
                ArticleOrder = x.ArticleOrder,
                Identity = x.Identity,
                VisibleToDepartment = x.VisibleToDepartment
            })
            .ToArrayAsync();

        return articles
            .Where(article => ArticleAccessPolicy.CanAccess(accessScope, article))
            .Select(article => new ArticleDO()
            {
                Path = article.Path,
                Title = article.Title,
                ArticleOrder = article.ArticleOrder
            })
            .ToArray();
    }
}

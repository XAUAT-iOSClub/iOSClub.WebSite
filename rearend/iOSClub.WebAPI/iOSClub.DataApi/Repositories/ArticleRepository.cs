using iOSClub.Data;
using iOSClub.Data.DataObjects;
using Microsoft.EntityFrameworkCore;
using ParadeDB.EntityFrameworkCore;
using ParadeDB.EntityFrameworkCore.Extensions;

namespace iOSClub.DataApi.Repositories;

/// <summary>
/// 文章仓库接口，提供文章数据的CRUD操作和查询功能
/// </summary>
public interface IArticleRepository
{
    /// <summary>
    /// 获取所有文章
    /// </summary>
    /// <returns>文章列表</returns>
    public Task<IEnumerable<ArticleDO>> GetAll();

    /// <summary>
    /// 根据路径获取文章
    /// </summary>
    /// <param name="path">文章路径</param>
    /// <param name="accessScope">访问者身份和部门；为空时用于后台操作，不进行可见性过滤</param>
    /// <returns>文章模型，如果找不到或没有权限则返回null</returns>
    public Task<ArticleDO?> GetFromPath(string path, ArticleAccessScope? accessScope = null);

    /// <summary>
    /// 创建或更新文章
    /// </summary>
    /// <param name="model">文章模型</param>
    /// <returns>是否操作成功</returns>
    public Task<bool> CreateOrUpdate(ArticleDO model);

    /// <summary>
    /// 删除文章
    /// </summary>
    /// <param name="key">文章路径</param>
    /// <returns>是否删除成功</returns>
    public Task<bool> Delete(string key);

    /// <summary>
    /// 获取所有分类文章
    /// </summary>
    /// <param name="accessScope">访问者身份和部门</param>
    /// <returns>按分类分组的文章字典</returns>
    public Task<Dictionary<string, IEnumerable<ArticleDO>>> GetAllCategoryArticles(ArticleAccessScope accessScope);

    /// <summary>
    /// 更新文章顺序
    /// </summary>
    /// <param name="articleOrders">文章路径和顺序的字典，可为null</param>
    /// <returns>是否更新成功</returns>
    public Task<bool> UpdateArticleOrders(Dictionary<string, int>? articleOrders);

    /// <summary>
    /// 搜索文章并返回高亮结果
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <param name="accessScope">访问者身份和部门</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="pageNumber">页码</param>
    /// <returns>高亮的文章搜索结果</returns>
    public Task<IEnumerable<ArticleSearchResult>> SearchArticlesWithHighlights(string keyword, ArticleAccessScope accessScope,
        int pageSize = 20,
        int pageNumber = 1);
}

public sealed record ArticleAccessScope(string Identity = "Member", string? DepartmentName = null);

// 添加一个用于搜索结果的模型
[Serializable]
public class ArticleSearchResult : ArticleDO
{
    public string HighlightedTitle { get; set; } = "";
    public string HighlightedContent { get; set; } = "";
    public float Rank { get; set; }
}

public class ArticleRepository(IDbContextFactory<ClubContext> factory, ICategoryRepository repository)
    : IArticleRepository
{
    public async Task<IEnumerable<ArticleDO>> GetAll()
    {
        await using var context = await factory.CreateDbContextAsync();
        var articles = await context.Articles.Include(x => x.Category).Select(x => new ArticleDO()
        {
            Path = x.Path,
            Title = x.Title,
            LastWriteTime = x.LastWriteTime,
            Category = x.Category == null ? null : new CategoryDO() { Name = x.Category.Name },
            Identity = x.Identity,
            VisibleToDepartment = x.VisibleToDepartment
        }).ToListAsync();
        return articles;
    }

    public async Task<ArticleDO?> GetFromPath(string path, ArticleAccessScope? accessScope = null)
    {
        await using var context = await factory.CreateDbContextAsync();
        var article = await context.Articles.Include(x => x.Category)
            .Select(x => new ArticleDO()
            {
                Path = x.Path,
                Title = x.Title,
                Content = x.Content,
                LastWriteTime = x.LastWriteTime,
                Category = x.Category == null ? null : new CategoryDO() { Name = x.Category.Name },
                ArticleOrder = x.ArticleOrder,
                Identity = x.Identity,
                VisibleToDepartment = x.VisibleToDepartment,
                CategoryId = x.CategoryId,
            })
            .FirstOrDefaultAsync(x => x.Path == path);

        if (article == null)
            return null;

        return accessScope == null || IsAccessible(accessScope, article) ? article : null;
    }

    public async Task<bool> CreateOrUpdate(ArticleDO model)
    {
        await using var context = await factory.CreateDbContextAsync();

        var article = await context.Articles.FirstOrDefaultAsync(x => x.Path == model.Path);
        if (article == null)
        {
            model.LastWriteTime = DateTime.UtcNow;
            if (string.IsNullOrEmpty(model.Category?.Name))
            {
                model.CategoryId = null;
            }
            else
            {
                var category = await repository.GetByName(model.Category.Name);
                if (category == null)
                {
                    await repository.CreateOrUpdate(new CategoryDO() { Name = model.Category.Name });
                    category = await repository.GetByName(model.Category.Name);
                }

                model.CategoryId = category?.Id;
            }

            model.Category = null;

            context.Articles.Add(model);
        }
        else
        {
            article.Content = model.Content;
            article.LastWriteTime = DateTime.UtcNow;
            article.Title = model.Title;
            article.Identity = model.Identity;
            article.VisibleToDepartment = model.VisibleToDepartment;
            article.ArticleOrder = model.ArticleOrder;

            if (string.IsNullOrEmpty(model.Category?.Name))
            {
                article.CategoryId = null;
            }
            else
            {
                var category = await repository.GetByName(model.Category.Name);
                if (category == null)
                {
                    await repository.CreateOrUpdate(new CategoryDO() { Name = model.Category.Name });
                    category = await repository.GetByName(model.Category.Name);
                }

                article.CategoryId = category?.Id;
            }
        }

        return await context.SaveChangesAsync() > 0;
    }

    public async Task<bool> Delete(string key)
    {
        await using var context = await factory.CreateDbContextAsync();
        var article = await context.Articles.FirstOrDefaultAsync(x => x.Path == key);

        if (article == null) return false;
        context.Articles.Remove(article);
        return await context.SaveChangesAsync() > 0;
    }

    private static readonly Dictionary<string, int> IdentityLevels = new()
    {
        ["Founder"] = 0, ["President"] = 1, ["Minister"] = 2,
        ["Department"] = 3, ["Member"] = 4, [""] = 5
    };

    public async Task<Dictionary<string, IEnumerable<ArticleDO>>> GetAllCategoryArticles(ArticleAccessScope accessScope)
    {
        await using var context = await factory.CreateDbContextAsync();

        var allowedIdentities = GetAllowedIdentities(accessScope.Identity);
        var canAccessAllDepartments = CanAccessAllDepartments(accessScope.Identity);

        var query = context.Articles
            .AsNoTracking()
            .Where(a => a.Identity == null || ((IEnumerable<string>)allowedIdentities).Contains(a.Identity))
            .Where(a => a.VisibleToDepartment == null || canAccessAllDepartments ||
                        a.VisibleToDepartment == accessScope.DepartmentName)
            .Select(a => new ArticleProjection
            {
                Path = a.Path,
                Title = a.Title,
                ArticleOrder = a.ArticleOrder,
                LastWriteTime = a.LastWriteTime,
                CategoryName = a.Category != null ? a.Category.Name : "其他",
                CategoryOrder = a.Category != null ? a.Category.Order : int.MaxValue,
                Identity = a.Identity,
                VisibleToDepartment = a.VisibleToDepartment
            })
            .OrderBy(a => a.CategoryOrder)
            .ThenBy(a => a.ArticleOrder);

        var articles = await query.ToListAsync();

        return articles
            .GroupBy(article => article.CategoryName)
            .ToDictionary(group => group.Key, group => group.Select(x => new ArticleDO()
            {
                Path = x.Path,
                Title = x.Title,
                ArticleOrder = x.ArticleOrder,
                Identity = x.Identity,
                VisibleToDepartment = x.VisibleToDepartment,
                LastWriteTime = x.LastWriteTime,
                Category = new CategoryDO()
                {
                    Name = x.CategoryName
                }
            }).AsEnumerable());
    }

    private static bool IsIdentityExist(string identity, string? neededIdentity)
    {
        if (string.IsNullOrEmpty(neededIdentity) ||
            (identity == "" && neededIdentity == "Member"))
            return true;

        return IdentityLevels.TryGetValue(identity, out var identityLevel) &&
               IdentityLevels.TryGetValue(neededIdentity, out var neededLevel) &&
               identityLevel <= neededLevel;
    }

    private static bool IsAccessible(ArticleAccessScope accessScope, ArticleDO article)
    {
        if (!IsIdentityExist(accessScope.Identity, article.Identity))
            return false;

        return string.IsNullOrEmpty(article.VisibleToDepartment) ||
               CanAccessAllDepartments(accessScope.Identity) ||
               article.VisibleToDepartment == accessScope.DepartmentName;
    }

    private static bool CanAccessAllDepartments(string identity)
        => identity is "Founder" or "President";

    private static string[] GetAllowedIdentities(string identity)
    {
        if (!IdentityLevels.TryGetValue(identity, out var level))
            return [""];

        return IdentityLevels
            .Where(kvp => kvp.Value >= level)
            .Select(kvp => kvp.Key)
            .ToArray();
    }

    [Serializable]
    private class ArticleProjection
    {
        public string Path { get; set; } = "";
        public string Title { get; set; } = "";
        public DateTime LastWriteTime { get; set; } = DateTime.UtcNow;
        public int ArticleOrder { get; set; }
        public string CategoryName { get; set; } = "";
        public int CategoryOrder { get; set; }
        public string? Identity { get; set; }
        public string? VisibleToDepartment { get; set; }
    }

    public async Task<bool> UpdateArticleOrders(Dictionary<string, int>? articleOrders)
    {
        if (articleOrders == null || articleOrders.Count == 0)
        {
            return true;
        }

        await using var context = await factory.CreateDbContextAsync();

        // 开始事务
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            // 获取所有需要更新的文章
            var articlePaths = articleOrders.Keys.ToList();
            var articles = await context.Articles
                .Where(a => articlePaths.Contains(a.Path))
                .ToListAsync();

            // 更新每篇文章的顺序
            foreach (var article in articles)
            {
                if (articleOrders.TryGetValue(article.Path, out var order))
                {
                    article.ArticleOrder = order;
                }
            }

            // 保存更改
            var result = await context.SaveChangesAsync();

            // 提交事务
            await transaction.CommitAsync();

            // 返回是否所有请求的文章都被更新
            return result > 0;
        }
        catch
        {
            // 回滚事务
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<IEnumerable<ArticleSearchResult>> SearchArticlesWithHighlights(
        string keyword,
        ArticleAccessScope accessScope,
        int pageSize = 20,
        int pageNumber = 1)
    {
        await using var context = await factory.CreateDbContextAsync();

        var allowedIdentities = GetAllowedIdentities(accessScope.Identity);
        var canAccessAllDepartments = CanAccessAllDepartments(accessScope.Identity);

        return await context.Articles
            .AsNoTracking()
            .Where(a => a.Identity == null || ((IEnumerable<string>)allowedIdentities).Contains(a.Identity))
            .Where(a => a.VisibleToDepartment == null || canAccessAllDepartments ||
                        a.VisibleToDepartment == accessScope.DepartmentName)
            .Where(a =>
                EF.Functions.MatchAny(a.Title, keyword) ||
                EF.Functions.MatchAny(a.Content, keyword))
            .Select(a => new ArticleSearchResult
            {
                Path = a.Path,
                Title = a.Title,
                LastWriteTime = a.LastWriteTime,
                Identity = a.Identity,
                VisibleToDepartment = a.VisibleToDepartment,
                CategoryId = a.CategoryId,
                ArticleOrder = a.ArticleOrder,
                HighlightedTitle = EF.Functions.Snippet(a.Title,
                    new SnippetOptions { StartTag = "<b>", EndTag = "</b>" }) ?? "",
                HighlightedContent = EF.Functions.Snippet(a.Content,
                    new SnippetOptions { StartTag = "<b>", EndTag = "</b>", MaxNumChars = 200 }) ?? "",
                Rank = EF.Functions.Score(a.Path)
            })
            .OrderByDescending(a => a.Rank)
            .ThenByDescending(a => a.LastWriteTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}

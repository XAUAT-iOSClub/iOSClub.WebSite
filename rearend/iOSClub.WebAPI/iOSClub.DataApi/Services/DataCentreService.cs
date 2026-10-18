using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.DataApi.Services;

/// <summary>
/// 数据中心服务接口，提供各种统计数据的查询功能
/// </summary>
public interface IDataCentreService
{
    /// <summary>
    /// 获取按学年统计的数据
    /// </summary>
    /// <returns>学年统计数据列表</returns>
    public Task<List<YearCountVO>> GetYearDataAsync();

    /// <summary>
    /// 获取按学院统计的数据
    /// </summary>
    /// <returns>学院统计数据列表</returns>
    public Task<List<AcademyCountVO>> GetCollegeDataAsync();

    /// <summary>
    /// 获取按年级统计的数据
    /// </summary>
    /// <returns>年级统计数据列表</returns>
    public Task<List<GradeCountVO>> GetGradeDataAsync();

    /// <summary>
    /// 获取按政治面貌统计的数据
    /// </summary>
    /// <returns>政治面貌统计数据列表</returns>
    public Task<List<LandscapeCountVO>> GetLandscapeDataAsync();

    /// <summary>
    /// 获取按性别统计的数据
    /// </summary>
    /// <returns>性别统计数据列表</returns>
    public Task<List<GenderCountVO>> GetGenderDataAsync();

    /// <summary>
    /// 导入完整备份数据。按主键合并：库中没有的插入，已存在的就地更新，
    /// 因此重复导入同一份备份（例如把导出的备份再传回来）不会因主键冲突失败。
    /// </summary>
    /// <param name="data">备份数据</param>
    /// <returns>各表合计的导入统计</returns>
    public Task<DataImportResultVO> ImportAllDataAsync(AllDataImportDTO data);
}

public class DataCentreService(IDbContextFactory<ClubContext> contextFactory) : IDataCentreService
{
    // 获取按学年统计数据
    public async Task<List<YearCountVO>> GetYearDataAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var yearData = new List<YearCountVO>();

        var total = await context.Students.CountAsync();
        var (year, month, _) = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);

        // 添加历史学年数据
        yearData.AddRange([
            new YearCountVO { Year = "2019学年", Value = 33 },
            new YearCountVO { Year = "2020学年", Value = 1 },
            new YearCountVO { Year = "2021学年", Value = 274 },
            new YearCountVO { Year = "2022学年", Value = 329 }
        ]);

        if (total <= 430) return yearData;

        // 一次性获取所有学生数据，并添加AsNoTracking()减少EF Core跟踪开销
        var students = await context.Students.AsNoTracking().ToListAsync();

        for (var i = year - 2024; i >= 0; i--)
        {
            var date = new DateTime(year - i, 9, 1, 0, 0, 0, DateTimeKind.Utc);
            var a = year - i - 2005;

            // 使用客户端评估来处理字符串到整数的转换
            var v = students.Count(s => s.JoinTime < date && int.Parse(s.UserId[..2]) > a);
            yearData.Add(new YearCountVO { Year = $"{year - i - 1}学年", Value = v });
        }

        if (month < 9) return yearData;

        // 复用已获取的学生数据，不再重复查询数据库
        var value = students.Count(s => int.Parse(s.UserId[..2]) > year - 2004);
        yearData.Add(new YearCountVO { Year = $"{year}学年", Value = value });

        return yearData;
    }

    // 获取按学院统计数据
    public async Task<List<AcademyCountVO>> GetCollegeDataAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        // 使用LINQ查询替代原始SQL以确保跨数据库兼容性，并添加AsNoTracking()减少EF Core跟踪开销
        return await context.Students.AsNoTracking()
            .GroupBy(s => s.Academy)
            .Select(g => new AcademyCountVO { Type = g.Key, Value = g.Count() })
            .OrderByDescending(ac => ac.Value)
            .ToListAsync();
    }

    // 获取按年级统计数据
    public async Task<List<GradeCountVO>> GetGradeDataAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        // 使用客户端评估处理年级数据，并添加AsNoTracking()减少EF Core跟踪开销
        var students = await context.Students.AsNoTracking().ToListAsync();
        var groupedStudents = students.GroupBy(s => s.UserId.Substring(0, 2));

        var gradeData = groupedStudents.Select(group => new GradeCountVO { Grade = group.Key + "级", Value = group.Count() }).ToList();

        gradeData.Sort((x, y) => string.Compare(x.Grade, y.Grade, StringComparison.Ordinal));

        return gradeData;
    }

    // 获取按政治面貌统计数据
    public async Task<List<LandscapeCountVO>> GetLandscapeDataAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        // 使用客户端评估处理政治面貌数据，并添加AsNoTracking()减少EF Core跟踪开销
        var students = await context.Students.AsNoTracking().ToListAsync();
        var groupedStudents = students.GroupBy(s => s.PoliticalLandscape);

        return groupedStudents.Select(group => new LandscapeCountVO { Type = group.Key, Value = group.Count() }).ToList();
    }

    // 获取按性别统计数据
    public async Task<List<GenderCountVO>> GetGenderDataAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var genderData = new List<GenderCountVO>();

        // 对于Count查询，AsNoTracking()不影响结果，但添加也不会有负面影响
        var man = await context.Students.AsNoTracking().CountAsync(x => x.Gender == "男");
        var woman = await context.Students.AsNoTracking().CountAsync(x => x.Gender == "女");

        genderData.AddRange(new List<GenderCountVO>
        {
            new GenderCountVO { Type = "男", Value = man },
            new GenderCountVO { Type = "女", Value = woman }
        });

        return genderData;
    }

    // 导入完整备份数据：按主键合并，而不是无脑 AddRange
    public async Task<DataImportResultVO> ImportAllDataAsync(AllDataImportDTO data)
    {
        // 与 StudentRepository.UpdateManyAsync 保持一致：导入前先清洗学号里的空格和入社时间
        foreach (var student in data.Students)
        {
            student.Standardization();

            // 新增学生没带密码哈希时用手机号做默认密码，否则导入后这个账号登不进来
            if (string.IsNullOrWhiteSpace(student.PasswordHash) && !string.IsNullOrWhiteSpace(student.PhoneNum))
                student.PasswordHash = DataTool.StringToHash(student.PhoneNum);
        }

        var result = new DataImportResultVO();

        // 部门里可能夹带部员列表，本接口只导入部门本身；
        // 不清理的话 Add 会级联插入这些部员，又会在 Staffs 主键上冲突
        foreach (var department in data.Departments)
        {
            result.Skipped += department.Staffs.Count;
            department.Staffs.Clear();
        }

        // 文章的分类导航对象同理，只保留分类ID，避免级联插入 Categories
        foreach (var article in data.Articles)
        {
            article.Category = null;
        }

        await using var context = await contextFactory.CreateDbContextAsync();

        // 先把库中已有的记录按主键读出来，导入时据此分流成插入还是更新
        var students = await context.Students.ToDictionaryAsync(x => x.UserId);
        var departments = await context.Departments.ToDictionaryAsync(x => x.Name);
        var staffs = await context.Staffs.ToDictionaryAsync(x => x.UserId);
        var resources = await context.Resources.ToDictionaryAsync(x => x.Id);
        var articles = await context.Articles.ToDictionaryAsync(x => x.Path);

        result.Add(Merge(context, students, data.Students, x => x.UserId, (current, incoming) => current.Update(incoming)));
        result.Add(Merge(context, departments, data.Departments, x => x.Name, ApplyDepartment));
        result.Add(Merge(context, staffs, data.Presidents.Where(x => x.Identity == "President"), x => x.UserId, ApplyStaff));
        result.Add(Merge(context, resources, data.Resources, x => x.Id, ApplyResource));
        result.Add(Merge(context, articles, data.Articles, x => x.Path, ApplyArticle));

        // 只提交一次：EF 会按外键依赖排序，部门先于引用它的部员和文章入库
        await context.SaveChangesAsync();

        return result;
    }

    /// <summary>
    /// 按主键合并一批导入数据：库中不存在的插入，已存在的交给 <paramref name="apply"/> 就地更新。
    /// </summary>
    /// <param name="context">当前上下文</param>
    /// <param name="existing">库中已有的实体，按主键索引；新插入的实体会写回这里，用于合并文件内重复的记录</param>
    /// <param name="incoming">待导入的实体</param>
    /// <param name="keySelector">主键取值</param>
    /// <param name="apply">把导入数据合并到库中已有实体上</param>
    private static DataImportResultVO Merge<TEntity>(
        ClubContext context,
        Dictionary<string, TEntity> existing,
        IEnumerable<TEntity> incoming,
        Func<TEntity, string> keySelector,
        Action<TEntity, TEntity> apply) where TEntity : class
    {
        var result = new DataImportResultVO();
        var dbKeys = existing.Keys.ToHashSet(); // 快照：真正在库里的主键

        foreach (var entity in incoming)
        {
            var key = keySelector(entity);
            if (string.IsNullOrWhiteSpace(key))
            {
                // 没有主键的数据入库没有意义，跳过它而不是让整批导入失败
                result.Skipped++;
                continue;
            }

            if (dbKeys.Contains(key))
            {
                apply(existing[key], entity);
                result.Updated++;
            }
            else if (existing.TryGetValue(key, out var pending))
            {
                // 同一份文件里重复出现的记录，合并到后出现的那条
                apply(pending, entity);
            }
            else
            {
                context.Add(entity);
                existing[key] = entity;
                result.Added++;
            }
        }

        return result;
    }

    private static void ApplyDepartment(DepartmentDO current, DepartmentDO incoming)
    {
        if (!string.IsNullOrEmpty(incoming.Key)) current.Key = incoming.Key;
        if (!string.IsNullOrEmpty(incoming.Description)) current.Description = incoming.Description;
    }

    private static void ApplyStaff(StaffDO current, StaffDO incoming)
    {
        if (!string.IsNullOrEmpty(incoming.Name)) current.Name = incoming.Name;

        // 备份里的 Founder 可能还是旧身份，不能因此把现任创始人降级
        if (current.Identity == "Founder") return;
        if (!string.IsNullOrEmpty(incoming.Identity)) current.Identity = incoming.Identity;
    }

    private static void ApplyResource(ResourceDO current, ResourceDO incoming)
    {
        if (!string.IsNullOrEmpty(incoming.Name)) current.Name = incoming.Name;
        if (!string.IsNullOrEmpty(incoming.Description)) current.Description = incoming.Description;
        if (!string.IsNullOrEmpty(incoming.Tag)) current.Tag = incoming.Tag;
    }

    private static void ApplyArticle(ArticleDO current, ArticleDO incoming)
    {
        if (!string.IsNullOrEmpty(incoming.Title)) current.Title = incoming.Title;
        if (!string.IsNullOrEmpty(incoming.Content)) current.Content = incoming.Content;
        if (incoming.LastWriteTime != default) current.LastWriteTime = incoming.LastWriteTime;
        if (!string.IsNullOrEmpty(incoming.Identity)) current.Identity = incoming.Identity;
        if (!string.IsNullOrEmpty(incoming.CategoryId)) current.CategoryId = incoming.CategoryId;
        if (incoming.VisibleToDepartment != null) current.VisibleToDepartment = incoming.VisibleToDepartment;
        if (incoming.ArticleOrder != 0) current.ArticleOrder = incoming.ArticleOrder;
    }
}

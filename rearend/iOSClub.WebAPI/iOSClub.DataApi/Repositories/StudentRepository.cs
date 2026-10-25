using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.VOs;
using Microsoft.EntityFrameworkCore;
using ParadeDB.EntityFrameworkCore.Extensions;

namespace iOSClub.DataApi.Repositories;

/// <summary>
/// 学生仓库接口，提供学生数据的CRUD操作和查询功能
/// </summary>
public interface IStudentRepository
{
    /// <summary>
    /// 获取所有学生
    /// </summary>
    /// <returns>学生列表</returns>
    public Task<List<StudentDO>> GetAll();

    /// <summary>
    /// 根据ID获取学生
    /// </summary>
    /// <param name="id">学生ID</param>
    /// <returns>学生模型，如果找不到则返回null</returns>
    public Task<StudentDO?> Get(string id) => GetByIdAsync(id);

    /// <summary>
    /// 创建学生
    /// </summary>
    /// <param name="model">学生模型</param>
    /// <returns>创建的学生模型，如果创建失败则返回null</returns>
    /// <exception cref="ArgumentException">当输入参数无效时抛出</exception>
    public Task<StudentDO?> Create(StudentDO model);

    /// <summary>
    /// 更新学生档案字段
    /// <para>
    /// 永不触碰 PasswordHash —— 与 <see cref="StudentDO.Update"/> 的白名单契约一致，
    /// 调用方不传哪个字段就不改哪个字段。改密码请走 <see cref="SetPasswordAsync"/>。
    /// </para>
    /// </summary>
    /// <param name="model">学生模型，UserId 用作定位键</param>
    /// <returns>学生不存在或字段校验失败时返回 false</returns>
    public Task<bool> UpdateProfileAsync(StudentDO model);

    /// <summary>
    /// 设置学生密码 —— 密码的唯一写入口
    /// <para>
    /// 调用方传明文，哈希在这里完成。集中一处是为了让"改密码"成为一个显式的、
    /// 可审计、可加权限校验的动作，而不是"更新档案"的副作用。
    /// </para>
    /// </summary>
    /// <param name="userId">学生ID</param>
    /// <param name="newPassword">明文新密码</param>
    /// <returns>学生不存在或密码为空时返回 false</returns>
    public Task<bool> SetPasswordAsync(string userId, string newPassword);

    /// <summary>
    /// 删除学生
    /// </summary>
    /// <param name="id">学生ID</param>
    /// <returns>是否删除成功</returns>
    public Task<bool> Delete(string id) => DeleteAsync(id);

    /// <summary>
    /// 学生登录验证
    /// </summary>
    /// <param name="userId">学生ID</param>
    /// <param name="password">密码</param>
    /// <returns>是否登录成功</returns>
    public Task<bool> Login(string userId, string password);

    /// <summary>
    /// 登录验证并返回学生信息（一次数据库查询完成密码验证+获取学生数据）
    /// </summary>
    /// <param name="userId">学生ID</param>
    /// <param name="password">密码</param>
    /// <returns>学生模型，验证失败返回null</returns>
    public Task<StudentDO?> LoginAndGetStudentAsync(string userId, string password);

    /// <summary>
    /// 根据ID异步获取学生
    /// </summary>
    /// <param name="id">学生ID</param>
    /// <returns>学生模型，如果找不到则返回null</returns>
    public Task<StudentDO?> GetByIdAsync(string id);

    /// <summary>
    /// 异步删除学生
    /// </summary>
    /// <param name="id">学生ID</param>
    /// <returns>是否删除成功</returns>
    public Task<bool> DeleteAsync(string id);

    /// <summary>
    /// 异步批量更新学生
    /// <para>
    /// 逐行返回结果：新增、更新、被校验跳过和保存失败分别计数，并给出未写入行的原因。
    /// 空输入返回空结果且不写库；整批被过滤时不写库，但跳过数量与原因必须可见。
    /// </para>
    /// </summary>
    /// <param name="list">学生列表</param>
    /// <returns>逐行结果统计</returns>
    public Task<BulkUpdateResultVO> UpdateManyAsync(List<StudentDO> list);

    /// <summary>
    /// 异步获取所有成员
    /// </summary>
    /// <returns>成员列表</returns>
    public Task<List<MemberVO>> GetAllMembersAsync();

    /// <summary>
    /// 异步分页获取成员
    /// </summary>
    /// <param name="pageNum">页码</param>
    /// <param name="pageSize">每页大小</param>
    /// <returns>成员列表和总记录数</returns>
    public Task<(List<MemberVO>, int)> GetMembersPagedAsync(int pageNum, int pageSize);

    /// <summary>
    /// 带搜索功能的异步分页获取成员
    /// </summary>
    /// <param name="pageNum">页码</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="searchTerm">搜索词</param>
    /// <param name="searchCondition">搜索条件</param>
    /// <returns>成员列表和总记录数</returns>
    public Task<(List<MemberVO>, int)> GetMembersPagedAsync(int pageNum, int pageSize, string? searchTerm,
        string? searchCondition);

    /// <summary>
    /// 搜索学生
    /// </summary>
    /// <param name="searchTerm">搜索词</param>
    /// <param name="searchCondition">搜索条件</param>
    /// <returns>学生列表</returns>
    public Task<List<StudentDO>> Search(string searchTerm, string searchCondition);
}

public class StudentRepository(IDbContextFactory<ClubContext> factory) : IStudentRepository
{
    // 使用EF Core编译查询，缓存查询计划，提高重复查询性能
    private static readonly Func<ClubContext, string, Task<StudentDO?>> GetStudentByIdQuery =
        EF.CompileAsyncQuery((ClubContext context, string id) =>
            context.Students.AsNoTracking().FirstOrDefault(s => s.UserId == id));

    private static readonly Func<ClubContext, string, Task<StudentDO?>> LoginQuery =
        EF.CompileAsyncQuery((ClubContext context, string userId) =>
            context.Students.AsNoTracking()
                .FirstOrDefault(s => s.UserId == userId));

    public async Task<List<StudentDO>> GetAll()
    {
        await using var context = await factory.CreateDbContextAsync();
        var students = await context.Students.AsNoTracking().ToListAsync();
        return students;
    }

    public async Task<StudentDO?> GetByIdAsync(string id)
    {
        await using var context = await factory.CreateDbContextAsync();
        return await GetStudentByIdQuery(context, id);
    }

    public async Task<bool> UpdateProfileAsync(StudentDO model)
    {
        // 输入验证
        if (string.IsNullOrWhiteSpace(model.UserId))
        {
            return false;
        }

        // 验证手机号格式
        if (!string.IsNullOrWhiteSpace(model.PhoneNum) && !ValidationTool.IsValidPhoneNumber(model.PhoneNum))
        {
            return false;
        }

        // 验证邮箱格式
        if (!string.IsNullOrEmpty(model.EMail) && !ValidationTool.IsValidEmail(model.EMail))
        {
            return false;
        }

        await using var context = await factory.CreateDbContextAsync();
        var stu = await context.Students.FirstOrDefaultAsync(x => x.UserId == model.UserId);
        if (stu == null)
        {
            return false;
        }

        // StudentDO.Update 是白名单式的（不传就不改），所以这里不会误伤任何未提交的字段，
        // 尤其是 PasswordHash —— 它根本不在 Update 的覆写列表里。
        stu.Update(model);

        // 保存成功即视为成功。用户点了保存但没改动任何字段时 SaveChanges 返回 0，
        // 这仍然是"档案现在和你要的一致"，不该报失败。
        await context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> SetPasswordAsync(string userId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(newPassword))
        {
            return false;
        }

        await using var context = await factory.CreateDbContextAsync();
        var stu = await context.Students.FirstOrDefaultAsync(x => x.UserId == userId);
        if (stu == null)
        {
            return false;
        }

        // BCrypt 每次加盐，即便明文相同也会得到不同的哈希，因此这里总能检测到变更。
        stu.PasswordHash = DataTool.StringToHash(newPassword);

        return await context.SaveChangesAsync() > 0;
    }

    public async Task<StudentDO?> Create(StudentDO model)
    {
        // 输入验证
        if (string.IsNullOrWhiteSpace(model.UserId))
        {
            throw new ArgumentException("用户ID不能为空");
        }

        if (string.IsNullOrWhiteSpace(model.UserName))
        {
            throw new ArgumentException("用户名不能为空");
        }

        // 验证手机号格式
        if (!ValidationTool.IsValidPhoneNumber(model.PhoneNum))
        {
            throw new ArgumentException("手机号格式错误");
        }

        // 验证邮箱格式
        if (!string.IsNullOrEmpty(model.EMail) && !ValidationTool.IsValidEmail(model.EMail))
        {
            throw new ArgumentException("邮箱格式错误");
        }

        //如果密码不是哈希加密过的，则进行加密
        if (!string.IsNullOrEmpty(model.PasswordHash) && !DataTool.IsValidHash(model.PasswordHash))
        {
            model.PasswordHash = DataTool.StringToHash(model.PasswordHash);
        }

        await using var context = await factory.CreateDbContextAsync();
        var stu = await context.Students.FirstOrDefaultAsync(x => x.UserId == model.UserId);
        if (stu != null)
        {
            return null;
        }

        await context.Students.AddAsync(model);
        var result = await context.SaveChangesAsync();

        return result == 1 ? model : null;
    }

    public async Task<bool> Login(string userId, string password)
    {
        // 输入验证
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        await using var context = await factory.CreateDbContextAsync();


        // 使用编译查询提高性能
        var student = await LoginQuery(context, userId);
        if (student == null)
        {
            return false;
        }

        // 密码哈希为空只可能是历史遗留数据（Create 与 SetPasswordAsync 都必然写入哈希）。
        // 以前这里降级成"比对手机号"，等于把"密码字段被清空"从"登不进来"变成
        // "知道学号+手机号就能登" —— 不安全失败。现在直接拒绝，让异常状态暴露出来。
        if (string.IsNullOrEmpty(student.PasswordHash))
        {
            return false;
        }

        return DataTool.IsOk(password, student.PasswordHash);
    }


    public async Task<StudentDO?> LoginAndGetStudentAsync(string userId, string password)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        await using var context = await factory.CreateDbContextAsync();

        var student = await LoginQuery(context, userId);
        if (student == null) return null;

        // 同 Login：哈希为空的历史数据拒绝登录，不再降级为手机号比对。
        if (string.IsNullOrEmpty(student.PasswordHash))
        {
            return null;
        }

        return DataTool.IsOk(password, student.PasswordHash) ? student : null;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        // 输入验证
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        await using var context = await factory.CreateDbContextAsync();
        var stu = await context.Students.FirstOrDefaultAsync(x => x.UserId == id);
        if (stu == null)
        {
            return false;
        }

        context.Students.Remove(stu);
        var result = await context.SaveChangesAsync();

        return result > 0;
    }

    public async Task<BulkUpdateResultVO> UpdateManyAsync(List<StudentDO> list)
    {
        var outcome = new BulkUpdateResultVO();

        // 空输入：没有可报告的行，也不触碰数据库
        if (list.Count == 0)
        {
            return outcome;
        }

        // 逐行分类：被既有校验规则过滤掉的行单独记账，不再静默计入成功
        var validStudents = new List<StudentDO>();
        foreach (var model in list)
        {
            if (string.IsNullOrWhiteSpace(model.UserId))
            {
                outcome.Skipped++;
                outcome.Issues.Add(new BulkUpdateIssueVO
                {
                    UserId = model.UserId,
                    Kind = BulkUpdateIssueVO.KindSkipped,
                    Reason = "学号不能为空"
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(model.PhoneNum) || !ValidationTool.IsValidPhoneNumber(model.PhoneNum))
            {
                outcome.Skipped++;
                outcome.Issues.Add(new BulkUpdateIssueVO
                {
                    UserId = model.UserId,
                    Kind = BulkUpdateIssueVO.KindSkipped,
                    Reason = "手机号格式不正确"
                });
                continue;
            }

            validStudents.Add(model);
        }

        await using var context = await factory.CreateDbContextAsync();
        // 获取所有现有的学生ID
        var existingStudentIds = await context.Students
            .Select(s => s.UserId)
            .ToHashSetAsync();

        // 标准化所有学生数据
        var standardizedStudents = validStudents.Select(model => model.Standardization()).ToList();

        // 分离需要插入和更新的学生
        var newStudents = standardizedStudents
            .Where(model => !existingStudentIds.Contains(model.UserId))
            .ToList();

        var existingStudents = standardizedStudents
            .Where(model => existingStudentIds.Contains(model.UserId))
            .ToList();

        // 本次真正进入写库流程的学号；保存失败时据此逐行标记 failed。
        // EF Core 的 SaveChanges 是整批事务，一行失败整批都不落库，
        // 因此失败时不能把其中任何一行算作新增或更新。
        var pendingUserIds = new List<string>();

        // 批量添加新学生
        if (newStudents.Count > 0)
        {
            // 为新学生设置默认密码
            foreach (var student in newStudents.Where(student => string.IsNullOrWhiteSpace(student.PasswordHash)))
            {
                student.PasswordHash = DataTool.StringToHash(student.PhoneNum);
            }

            await context.Students.AddRangeAsync(newStudents);
            pendingUserIds.AddRange(newStudents.Select(student => student.UserId));
        }

        // 批量更新现有学生：逐个取出实体后调用 Update 处理字段覆写规则
        var updatedCount = 0;
        foreach (var student in existingStudents)
        {
            var existingStudent = await context.Students.FirstOrDefaultAsync(s => s.UserId == student.UserId);
            if (existingStudent == null)
            {
                // 现有ID快照之后记录被删除：这行没有写入成功，按跳过记账并说明原因
                outcome.Skipped++;
                outcome.Issues.Add(new BulkUpdateIssueVO
                {
                    UserId = student.UserId,
                    Kind = BulkUpdateIssueVO.KindSkipped,
                    Reason = "目标学生不存在，未能更新"
                });
                continue;
            }

            existingStudent.Update(student);
            updatedCount++;
            pendingUserIds.Add(student.UserId);
        }

        // 与旧实现一致：有变化才调用一次 SaveChanges，保持批量语义与调用次数不变
        if (pendingUserIds.Count == 0)
        {
            return outcome;
        }

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // 保存失败：整批回滚，逐行标记为 failed，避免把未落库的行算作成功
            outcome.Failed = pendingUserIds.Count;
            outcome.Issues.AddRange(pendingUserIds.Select(userId => new BulkUpdateIssueVO
            {
                UserId = userId,
                Kind = BulkUpdateIssueVO.KindFailed,
                Reason = "保存失败，记录未写入数据库"
            }));
            return outcome;
        }

        outcome.Created = newStudents.Count;
        outcome.Updated = updatedCount;
        return outcome;
    }

    public async Task<List<MemberVO>> GetAllMembersAsync()
    {
        await using var context = await factory.CreateDbContextAsync();
        var query = from student in context.Students.AsNoTracking()
            join staff in context.Staffs.AsNoTracking()
                on student.UserId equals staff.UserId into staffGroup
            from staff in staffGroup.DefaultIfEmpty() // LEFT JOIN
            select new MemberVO
            {
                UserId = student.UserId,
                UserName = student.UserName,
                Academy = student.Academy,
                PoliticalLandscape = student.PoliticalLandscape,
                Gender = student.Gender,
                ClassName = student.ClassName,
                PhoneNum = student.PhoneNum,
                JoinTime = student.JoinTime,
                EMail = student.EMail,
                Identity = staff != null ? staff.Identity : "Member"
            };

        return await query.ToListAsync();
    }

    public async Task<(List<MemberVO>, int)> GetMembersPagedAsync(int pageNum, int pageSize)
    {
        // 调用带搜索参数的版本，传递null值表示无搜索条件
        return await GetMembersPagedAsync(pageNum, pageSize, null, null);
    }

    // 带搜索功能的分页方法实现
    public async Task<(List<MemberVO>, int)> GetMembersPagedAsync(int pageNum, int pageSize, string? searchTerm,
        string? searchCondition)
    {
        await using var context = await factory.CreateDbContextAsync();
        // 构建基础查询
        var query = context.Students.AsQueryable();

        // 如果提供了搜索词，则应用搜索条件
        query = ApplySearch(query, searchTerm, searchCondition);

        // 计算总记录数
        var totalCount = await query.CountAsync();
        var skipCount = (pageNum - 1) * pageSize;

        // 在数据库层面执行LEFT JOIN操作，避免内存中连接
        var memberQuery = from student in query.AsNoTracking()
            join staff in context.Staffs.AsNoTracking()
                on student.UserId equals staff.UserId into staffGroup
            from staff in staffGroup.DefaultIfEmpty() // LEFT JOIN
            orderby student.UserId // 确保结果一致性的排序
            select new
            {
                Student = student,
                StaffIdentity = staff != null ? staff.Identity : "Member"
            };

        // 应用分页并执行查询
        var memberData = await memberQuery
            .Skip(skipCount)
            .Take(pageSize)
            .ToListAsync();

        // 转换为MemberVO列表
        var results = memberData.Select(item =>
            new MemberVO
            {
                UserId = item.Student.UserId,
                UserName = item.Student.UserName,
                Academy = item.Student.Academy,
                PoliticalLandscape = item.Student.PoliticalLandscape,
                Gender = item.Student.Gender,
                ClassName = item.Student.ClassName,
                PhoneNum = item.Student.PhoneNum,
                JoinTime = item.Student.JoinTime,
                EMail = item.Student.EMail,
                Identity = item.StaffIdentity
            }).ToList();

        return (results, totalCount);
    }

    public async Task<List<StudentDO>> Search(string searchTerm, string searchCondition)
    {
        await using var context = await factory.CreateDbContextAsync();
        var query = context.Students.AsQueryable();

        // 如果提供了搜索词，则应用搜索条件
        query = ApplySearch(query, searchTerm, searchCondition);

        // 直接返回结果，避免多余的查询和内存操作
        return await query.AsNoTracking()
            .OrderBy(s => s.UserId) // 确保结果一致性
            .ToListAsync();
    }

    /// <summary>
    /// 应用成员搜索条件：指定字段的精确过滤（学号/姓名/班级/学院/手机号）沿用 LIKE，
    /// 未指定条件的模糊搜索对文本字段（姓名/班级/学院）使用 ParadeDB BM25 检索。
    /// </summary>
    private static IQueryable<StudentDO> ApplySearch(IQueryable<StudentDO> query, string? searchTerm,
        string? searchCondition)
    {
        if (string.IsNullOrEmpty(searchTerm))
            return query;

        return searchCondition?.ToLower() switch
        {
            // 结构化字段的精确过滤，沿用前缀/子串匹配，语义更精确
            "userid" => query.Where(s => s.UserId.StartsWith(searchTerm)),
            "username" => query.Where(s => s.UserName.Contains(searchTerm)),
            "classname" => query.Where(s => s.ClassName.Contains(searchTerm)),
            "academy" => query.Where(s => s.Academy.Contains(searchTerm)),
            "phone_num" => query.Where(s => s.PhoneNum.Contains(searchTerm)),
            // 无指定条件：文本字段走 BM25 相关度检索，标识符字段沿用子串匹配
            _ => query.Where(s =>
                EF.Functions.MatchAny(s.UserName, searchTerm) ||
                EF.Functions.MatchAny(s.ClassName, searchTerm) ||
                EF.Functions.MatchAny(s.Academy, searchTerm) ||
                s.UserId.Contains(searchTerm) ||
                s.PhoneNum.Contains(searchTerm))
        };
    }
}
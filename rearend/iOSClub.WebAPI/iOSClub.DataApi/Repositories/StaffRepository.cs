using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.VOs;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.DataApi.Repositories;

/// <summary>
/// 员工仓库接口，提供员工数据的CRUD操作和查询功能
/// </summary>
public interface IStaffRepository
{
    Task<IEnumerable<StaffVO>> GetPermissionMembersAsync();
    Task<(bool Success, string Error)> UpdateRoleAsync(string userId, string identity, string? departmentName, string operatorId);

    /// <summary>
    /// 将已存在的成员调整为部门内身份（部员/部长）或社团领导身份（社长）。
    /// 与 UpdateRoleAsync 不同：本方法面向管理员在部门管理页的人事调动，
    /// 只允许在 Department / Minister / President 之间调整，禁止触碰 Founder。
    /// </summary>
    /// <param name="userId">成员ID</param>
    /// <param name="identity">目标身份：Department / Minister / President</param>
    /// <param name="departmentName">目标部门名称；President 会被强制清空部门</param>
    /// <returns>是否成功及错误信息</returns>
    Task<(bool Success, string Error)> AssignRoleAsync(string userId, string identity, string? departmentName);
    /// <summary>
    /// 获取所有员工
    /// </summary>
    /// <returns>员工列表</returns>
    Task<IEnumerable<StaffDO>> GetAllStaffAsync();
    
    /// <summary>
    /// 获取所有员工并转换为成员模型
    /// </summary>
    /// <returns>成员模型列表</returns>
    Task<IEnumerable<MemberVO>> GetAllStaffToMembers();
    
    /// <summary>
    /// 根据ID获取员工
    /// </summary>
    /// <param name="userId">员工ID</param>
    /// <returns>员工模型，如果找不到则返回null</returns>
    Task<StaffDO?> GetStaffByIdAsync(string userId);
    
    /// <summary>
    /// 根据ID获取员工，不包含关联数据
    /// </summary>
    /// <param name="userId">员工ID</param>
    /// <returns>员工模型，如果找不到则返回null</returns>
    Task<StaffDO?> GetStaffByIdWithoutOtherData(string userId);
    
    /// <summary>
    /// 创建员工
    /// </summary>
    /// <param name="staff">员工模型</param>
    /// <returns>是否创建成功</returns>
    /// <exception cref="ArgumentException">当员工身份和部门不匹配时抛出</exception>
    Task<bool> CreateStaffAsync(StaffDO staff);
    
    /// <summary>
    /// 更新员工
    /// </summary>
    /// <param name="staff">员工模型</param>
    /// <returns>是否更新成功</returns>
    Task<bool> UpdateStaffAsync(StaffDO staff);
    
    /// <summary>
    /// 删除员工
    /// </summary>
    /// <param name="userId">员工ID</param>
    /// <returns>是否删除成功</returns>
    Task<bool> DeleteStaffAsync(string userId);
    
    /// <summary>
    /// 检查员工是否存在
    /// </summary>
    /// <param name="userId">员工ID</param>
    /// <returns>员工是否存在</returns>
    Task<bool> StaffExistsAsync(string userId);
    
    /// <summary>
    /// 根据身份获取员工
    /// </summary>
    /// <param name="identities">身份列表</param>
    /// <returns>员工列表</returns>
    Task<IEnumerable<StaffDO>> GetStaffsByIdentitiesAsync(params string[] identities);
    
    /// <summary>
    /// 更改员工部门
    /// </summary>
    /// <param name="userId">员工ID</param>
    /// <param name="departmentName">部门名称，可为null表示移除部门</param>
    /// <returns>是否更改成功</returns>
    Task<bool> ChangeStaffDepartmentAsync(string userId, string? departmentName);
    
    /// <summary>
    /// 获取所有员工及其身份
    /// </summary>
    /// <returns>成员模型列表，包含身份信息</returns>
    Task<IEnumerable<MemberVO>> GetAllStaffIdentity();
}

public class StaffRepository(IDbContextFactory<ClubContext> factory) : IStaffRepository
{
    public async Task<IEnumerable<StaffVO>> GetPermissionMembersAsync()
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.Staffs.Include(s => s.Department).Select(s => new StaffVO { UserId = s.UserId, Name = s.Name, Identity = s.Identity, DepartmentName = s.Department == null ? null : s.Department.Name }).ToListAsync();
    }

    public async Task<(bool Success, string Error)> UpdateRoleAsync(string userId, string identity, string? departmentName, string operatorId)
    {
        var allowed = new[] { "Member", "Department", "Minister", "President", "Founder" };
        if (!allowed.Contains(identity)) return (false, "身份标识无效");
        if (userId == operatorId) return (false, "不能修改自己的权限");
        await using var context = await factory.CreateDbContextAsync();
        var staff = await context.Staffs.FirstOrDefaultAsync(s => s.UserId == userId);
        if (staff == null) return (false, "成员不存在");
        if ((identity is "Minister" or "Department") && string.IsNullOrWhiteSpace(departmentName)) return (false, "该身份必须指定部门");
        if (identity == "Founder" && staff.Identity != "Founder" && !string.IsNullOrWhiteSpace(departmentName)) departmentName = null;
        DepartmentDO? department = null;
        if (!string.IsNullOrWhiteSpace(departmentName)) { department = await context.Departments.FirstOrDefaultAsync(d => d.Name == departmentName); if (department == null) return (false, "部门不存在"); }
        if (staff.Identity == "Founder" && identity != "Founder" && await context.Staffs.CountAsync(s => s.Identity == "Founder") <= 1) return (false, "至少需要保留一个 Founder");
        staff.Identity = identity; staff.Department = department;
        await context.SaveChangesAsync(); return (true, "");
    }

    public async Task<(bool Success, string Error)> AssignRoleAsync(string userId, string identity, string? departmentName)
    {
        // 允许的人事调动范围：部员 ↔ 部长 ↔ 社长。
        // Founder 不在此列，避免部门管理页误改创始人。
        var allowed = new[] { "Department", "Minister", "President" };
        if (!allowed.Contains(identity)) return (false, "身份标识无效");

        await using var context = await factory.CreateDbContextAsync();
        var staff = await context.Staffs.Include(s => s.Department).FirstOrDefaultAsync(s => s.UserId == userId);
        if (staff == null) return (false, "成员不存在");
        if (staff.Identity == "Founder") return (false, "不能调整创始人的身份");

        // 社长属于社团层面的身份，不隶属于任何部门。
        if (identity is "President")
        {
            staff.Identity = identity;
            staff.Department = null;
            return await SaveAsync(context);
        }

        // 部长 / 部员必须指定一个存在的部门。
        if (string.IsNullOrWhiteSpace(departmentName)) return (false, "该身份必须指定部门");
        var department = await context.Departments.FirstOrDefaultAsync(d => d.Name == departmentName);
        if (department == null) return (false, "部门不存在");

        staff.Identity = identity;
        staff.Department = department;
        return await SaveAsync(context);
    }

    private static async Task<(bool Success, string Error)> SaveAsync(ClubContext context)
    {
        try
        {
            await context.SaveChangesAsync();
            return (true, "");
        }
        catch
        {
            return (false, "保存失败");
        }
    }
    public async Task<IEnumerable<StaffDO>> GetAllStaffAsync()
    {
        await using var context = await factory.CreateDbContextAsync();
        return (await context.Staffs
                .Include(s => s.Department)
                .ToListAsync())
            .Select(x => x.OutputWhenOtherList());
    }

    public async Task<IEnumerable<MemberVO>> GetAllStaffToMembers()
    {
        await using var context = await factory.CreateDbContextAsync();
        var query = from staff in context.Staffs
            join student in context.Students
                on staff.UserId equals student.UserId into studentGroup
            from student in studentGroup.DefaultIfEmpty() // LEFT JOIN
            where student != null && staff.Identity != "Founder"
            select new MemberVO
            {
                UserId = staff.UserId,
                UserName = student != null ? student.UserName : staff.Name,
                Academy = student != null ? student.Academy : "",
                PoliticalLandscape = student != null ? student.PoliticalLandscape : "群众",
                Gender = student != null ? student.Gender : "",
                ClassName = student != null ? student.ClassName : "",
                PhoneNum = student != null ? student.PhoneNum : "",
                JoinTime = student != null ? student.JoinTime : DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
                EMail = student != null ? student.EMail : null,
                Identity = staff.Identity
            };

        return await query.ToListAsync();
    }

    public async Task<IEnumerable<MemberVO>> GetAllStaffIdentity()
    {
        await using var context = await factory.CreateDbContextAsync();
        var query = from staff in context.Staffs
            join student in context.Students
                on staff.UserId equals student.UserId into studentGroup
            from student in studentGroup.DefaultIfEmpty() // LEFT JOIN
            where student != null && staff.Identity != "Founder"
            select new MemberVO
            {
                UserId = staff.UserId,
                UserName = student != null ? student.UserName : staff.Name,
                Academy = student != null ? student.Academy : "",
                PoliticalLandscape = student != null ? student.PoliticalLandscape : "群众",
                Gender = student != null ? student.Gender : "",
                ClassName = student != null ? student.ClassName : "",
                PhoneNum = student != null ? student.PhoneNum : "",
                JoinTime = student != null ? student.JoinTime : DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
                EMail = student != null ? student.EMail : null,
                Identity = staff.Identity == "Minister" ? $"{staff.Department!.Name} 部长" :
                    staff.Identity == "Department" ? $"{staff.Department!.Name} 部员" :
                    staff.Identity == "President" ? "社长" :
                    staff.Identity == "Founder" ? "创始人" : staff.Identity,
            };

        return await query.ToListAsync();
    }

    public async Task<StaffDO?> GetStaffByIdWithoutOtherData(string userId)
    {
        await using var context = await factory.CreateDbContextAsync();
        var staff = await context.Staffs
            .FirstOrDefaultAsync(s => s.UserId == userId);
        return staff?.OutputWhenOtherList();
    }

    public async Task<StaffDO?> GetStaffByIdAsync(string userId)
    {
        await using var context = await factory.CreateDbContextAsync();
        var staff = await context.Staffs
            .Include(s => s.Department)
            .FirstOrDefaultAsync(s => s.UserId == userId);
        return staff?.OutputWhenOtherList();
    }

    public async Task<bool> CreateStaffAsync(StaffDO staff)
    {
        await using var context = await factory.CreateDbContextAsync();

        if (await StaffExistsAsync(staff.UserId))
            return false;

        if (staff.Identity == "Member" ||
            (!(staff.Identity == "Founder" || staff.Identity == "President" || staff.Identity == "Minister") &&
             staff.Department == null))
            throw new ArgumentException("会员必须指定部门");

        if (staff.Department != null)
        {
            staff.Department = await context.Departments.FirstOrDefaultAsync(d => d.Name == staff.Department.Name);
        }

        context.Staffs.Add(staff);
        return await context.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateStaffAsync(StaffDO staff)
    {
        await using var context = await factory.CreateDbContextAsync();

        var existingStaff = await context.Staffs.FirstOrDefaultAsync(s => s.UserId == staff.UserId);
        if (existingStaff == null)
            return false;

        // 更新字段
        existingStaff.Name = staff.Name;
        existingStaff.Identity = staff.Identity;

        context.Staffs.Update(existingStaff);
        return await context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteStaffAsync(string userId)
    {
        await using var context = await factory.CreateDbContextAsync();

        var staff = await context.Staffs.FirstOrDefaultAsync(s => s.UserId == userId);
        if (staff == null)
            return false;

        context.Staffs.Remove(staff);
        return await context.SaveChangesAsync() > 0;
    }

    public async Task<bool> StaffExistsAsync(string userId)
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.Staffs.AnyAsync(s => s.UserId == userId);
    }

    public async Task<IEnumerable<StaffDO>> GetStaffsByIdentitiesAsync(params string[] identities)
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.Staffs
            .Include(s => s.Department)
            .Where(s => ((IEnumerable<string>)identities).Contains(s.Identity))
            .ToListAsync();
    }

    public async Task<bool> ChangeStaffDepartmentAsync(string userId, string? departmentName)
    {
        await using var context = await factory.CreateDbContextAsync();

        var staff = await context.Staffs
            .Include(s => s.Department)
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (staff == null)
            return false;

        if (string.IsNullOrEmpty(departmentName))
        {
            staff.Department = null;
        }
        else
        {
            var department = await context.Departments.FirstOrDefaultAsync(d => d.Name == departmentName);
            if (department == null)
                return false;

            staff.Department = department;
        }

        return await context.SaveChangesAsync() > 0;
    }
}

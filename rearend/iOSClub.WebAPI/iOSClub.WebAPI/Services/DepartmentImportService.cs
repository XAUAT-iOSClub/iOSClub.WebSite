using System.Text.Json;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Exceptions;
using iOSClub.WebAPI.Common;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.WebAPI.Services;

/// <summary>
/// 部门名单导入服务接口。
/// 导入会覆盖指定部门的现有名单，并在覆盖前自动备份原名单、记录导入历史。
/// </summary>
public interface IDepartmentImportService
{
    /// <summary>
    /// 覆盖导入指定部门的名单（仅限 President / Minister / Department 身份）
    /// </summary>
    Task<DepartmentImportResultVO> ImportRosterAsync(string departmentName, DepartmentImportDTO dto,
        string operatorId, string operatorName);

    /// <summary>
    /// 获取指定部门的导入历史（按时间倒序）
    /// </summary>
    Task<List<ImportHistoryVO>> GetHistoryAsync(string departmentName);

    /// <summary>
    /// 获取指定导入历史的备份记录，找不到返回 null
    /// </summary>
    Task<ImportHistoryDO?> GetHistoryByIdAsync(string id);
}

public class DepartmentImportService(IDbContextFactory<ClubContext> factory) : IDepartmentImportService
{
    private static readonly string[] AllowedIdentities = ["President", "Minister", "Department"];

    public async Task<DepartmentImportResultVO> ImportRosterAsync(string departmentName, DepartmentImportDTO dto,
        string operatorId, string operatorName)
    {
        await using var context = await factory.CreateDbContextAsync();

        var department = await context.Departments.FirstOrDefaultAsync(d => d.Name == departmentName);
        if (department == null)
            throw new BusinessException(ErrorCode.ResourceNotFound, "部门不存在");

        var incoming = NormalizeMembers(dto.Members);

        // 备份、删除、upsert、历史记录都在同一次 SaveChanges 中提交，天然原子，无需显式事务。
        var current = await context.Staffs
            .Include(s => s.Department)
            .Where(s => s.Department != null && s.Department.Name == departmentName)
            .ToListAsync();

        // 不允许把 Founder 纳入部门名单覆盖流程
        if (current.Any(s => s.Identity == "Founder"))
            throw new BusinessException(ErrorCode.InvalidStatusForOperation, "部门名单中存在创始人，无法覆盖");

        // 一并加载当前部门成员的学生档案，用于备份完整信息
        var currentIds = current.Select(s => s.UserId).ToList();
        var studentMap = await context.Students
            .Where(s => currentIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId);

        var beforeRoles = CountRoles(current.Select(s => s.Identity));
        var backup = current
            .Select(s =>
            {
                studentMap.TryGetValue(s.UserId, out var student);
                return new DepartmentImportMemberVO
                {
                    UserId = s.UserId,
                    Name = s.Name,
                    Identity = s.Identity,
                    Academy = student?.Academy ?? "",
                    ClassName = student?.ClassName ?? "",
                    PhoneNum = student?.PhoneNum ?? "",
                    PoliticalLandscape = student?.PoliticalLandscape ?? "",
                    Gender = student?.Gender ?? "",
                    EMail = student?.EMail
                };
            })
            .ToList();

        var incomingIds = incoming.Select(m => m.UserId).ToHashSet(StringComparer.Ordinal);

        // 1. 删除不在新名单中的原部门成员（仅解除部门关系，保留其学生档案）
        foreach (var staff in current.Where(s => !incomingIds.Contains(s.UserId)))
            context.Staffs.Remove(staff);

        // 2. 逐条 upsert：Staff（部门/职位）+ Student（学生档案字段）
        foreach (var item in incoming)
        {
            var existing = current.FirstOrDefault(s => s.UserId == item.UserId)
                           ?? await context.Staffs.FirstOrDefaultAsync(s => s.UserId == item.UserId);

            if (existing == null)
            {
                context.Staffs.Add(new StaffDO
                {
                    UserId = item.UserId,
                    Name = item.Name,
                    Identity = item.Identity,
                    Department = department
                });
            }
            else
            {
                existing.Name = item.Name;
                existing.Identity = item.Identity;
                existing.Department = department;
            }

            var student = studentMap.GetValueOrDefault(item.UserId)
                          ?? await context.Students.FirstOrDefaultAsync(s => s.UserId == item.UserId);

            if (student == null)
            {
                student = new StudentDO { UserId = item.UserId, UserName = item.Name };
                // 新学生需要一个可登录的初始密码：优先手机号，其次学号（与批量导入的约定一致）
                student.PasswordHash = DataTool.StringToHash(
                    string.IsNullOrWhiteSpace(item.PhoneNum) ? item.UserId : item.PhoneNum);
                context.Students.Add(student);
            }
            else
            {
                student.UserName = item.Name;
            }

            ApplyProfile(student, item);
        }

        var afterRoles = CountRoles(incoming.Select(m => m.Identity));
        var roleChanges = BuildRoleChanges(beforeRoles, afterRoles);

        var history = new ImportHistoryDO
        {
            DepartmentName = departmentName,
            ImportedAt = DateTime.UtcNow,
            OperatorId = operatorId,
            OperatorName = operatorName,
            FileName = dto.FileName,
            MemberCount = incoming.Count,
            BackupJson = JsonSerializer.Serialize(backup),
            SummaryJson = JsonSerializer.Serialize(roleChanges)
        };
        context.ImportHistories.Add(history);

        await context.SaveChangesAsync();

        return new DepartmentImportResultVO
        {
            HistoryId = history.Id,
            DepartmentName = departmentName,
            BeforeCount = backup.Count,
            AfterCount = incoming.Count,
            RoleChanges = roleChanges,
            Backup = backup
        };
    }

    public async Task<List<ImportHistoryVO>> GetHistoryAsync(string departmentName)
    {
        await using var context = await factory.CreateDbContextAsync();
        var records = await context.ImportHistories
            .Where(h => h.DepartmentName == departmentName)
            .OrderByDescending(h => h.ImportedAt)
            .ToListAsync();

        return records.Select(ToVO).ToList();
    }

    public async Task<ImportHistoryDO?> GetHistoryByIdAsync(string id)
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.ImportHistories.FirstOrDefaultAsync(h => h.Id == id);
    }

    private static List<DepartmentImportMemberDTO> NormalizeMembers(IEnumerable<DepartmentImportMemberDTO> members)
    {
        var list = new List<DepartmentImportMemberDTO>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in members)
        {
            var userId = raw.UserId?.Trim() ?? "";
            var name = raw.Name?.Trim() ?? "";
            var identity = string.IsNullOrWhiteSpace(raw.Identity) ? "Department" : raw.Identity.Trim();

            if (userId.Length == 0)
                throw new BusinessException(ErrorCode.ParameterValidationFailed, "存在学号为空的记录");
            if (name.Length == 0)
                throw new BusinessException(ErrorCode.ParameterValidationFailed, $"学号 {userId} 的姓名为空");
            if (!AllowedIdentities.Contains(identity))
                throw new BusinessException(ErrorCode.ParameterValidationFailed, $"学号 {userId} 的职位 {identity} 不合法");
            if (!seen.Add(userId))
                throw new BusinessException(ErrorCode.ResourceAlreadyExists, $"学号 {userId} 重复");

            var phoneNum = raw.PhoneNum?.Trim() ?? "";
            var eMail = raw.EMail?.Trim();
            var gender = raw.Gender?.Trim() ?? "";

            if (phoneNum.Length > 0 && !ValidationTool.IsValidPhoneNumber(phoneNum))
                throw new BusinessException(ErrorCode.ParameterValidationFailed, $"学号 {userId} 的手机号格式错误");
            if (!string.IsNullOrEmpty(eMail) && !ValidationTool.IsValidEmail(eMail))
                throw new BusinessException(ErrorCode.ParameterValidationFailed, $"学号 {userId} 的邮箱格式错误");
            if (gender.Length > 0 && gender is not ("男" or "女"))
                throw new BusinessException(ErrorCode.ParameterValidationFailed, $"学号 {userId} 的性别 {gender} 不合法");

            list.Add(new DepartmentImportMemberDTO
            {
                UserId = userId,
                Name = name,
                Identity = identity,
                Academy = raw.Academy?.Trim() ?? "",
                ClassName = raw.ClassName?.Trim() ?? "",
                PhoneNum = phoneNum,
                PoliticalLandscape = raw.PoliticalLandscape?.Trim() ?? "",
                Gender = gender,
                EMail = string.IsNullOrEmpty(eMail) ? null : eMail
            });
        }

        return list;
    }

    /// <summary>
    /// 只覆盖导入文件中非空的档案字段，避免用空值抹掉已有信息。
    /// </summary>
    private static void ApplyProfile(StudentDO student, DepartmentImportMemberDTO item)
    {
        if (!string.IsNullOrWhiteSpace(item.Academy)) student.Academy = item.Academy;
        if (!string.IsNullOrWhiteSpace(item.ClassName)) student.ClassName = item.ClassName;
        if (!string.IsNullOrWhiteSpace(item.PhoneNum)) student.PhoneNum = item.PhoneNum;
        if (!string.IsNullOrWhiteSpace(item.PoliticalLandscape)) student.PoliticalLandscape = item.PoliticalLandscape;
        if (!string.IsNullOrWhiteSpace(item.Gender)) student.Gender = item.Gender;
        if (!string.IsNullOrWhiteSpace(item.EMail)) student.EMail = item.EMail;
    }

    private static Dictionary<string, int> CountRoles(IEnumerable<string> identities)
    {
        var counts = new Dictionary<string, int>();
        foreach (var identity in identities)
        {
            counts.TryGetValue(identity, out var value);
            counts[identity] = value + 1;
        }

        return counts;
    }

    private static Dictionary<string, int[]> BuildRoleChanges(Dictionary<string, int> before, Dictionary<string, int> after)
    {
        var changes = new Dictionary<string, int[]>();
        foreach (var identity in AllowedIdentities)
        {
            before.TryGetValue(identity, out var beforeCount);
            after.TryGetValue(identity, out var afterCount);
            if (beforeCount > 0 || afterCount > 0)
                changes[identity] = [beforeCount, afterCount];
        }

        return changes;
    }

    private static ImportHistoryVO ToVO(ImportHistoryDO record)
    {
        Dictionary<string, int[]> roleChanges;
        try
        {
            roleChanges = JsonSerializer.Deserialize<Dictionary<string, int[]>>(record.SummaryJson) ?? new();
        }
        catch (JsonException)
        {
            roleChanges = new Dictionary<string, int[]>();
        }

        return new ImportHistoryVO
        {
            Id = record.Id,
            DepartmentName = record.DepartmentName,
            ImportedAt = record.ImportedAt,
            OperatorId = record.OperatorId,
            OperatorName = record.OperatorName,
            FileName = record.FileName,
            MemberCount = record.MemberCount,
            RoleChanges = roleChanges
        };
    }
}

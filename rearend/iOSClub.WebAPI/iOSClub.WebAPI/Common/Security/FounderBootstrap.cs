using iOSClub.Data;
using iOSClub.Data.DataObjects;

namespace iOSClub.WebAPI.Common.Security;

/// <summary>
/// 首次部署时的 Founder 初始化。
/// <para>
/// 安全约束：Founder 的学号和初始密码必须由部署者通过环境变量显式提供，
/// 绝不使用固定学号或固定密码——否则公开注册接口可以抢注同一个学号，
/// 而登录身份又来自 Staffs 表，攻击者会直接拿到 Founder。
/// </para>
/// <para>
/// 账号模型要求 Founder 同时拥有 Staff（身份）与 Student（登录凭据）。
/// 只建 Staff 会留下"有角色但无法登录、且可被抢注"的窗口，因此两者必须一次写入。
/// </para>
/// </summary>
public static class FounderBootstrap
{
    public const int MinPasswordLength = 12;

    /// <summary>学号必须是 10 位数字，与 StudentCreateDTO 的规则一致。</summary>
    public static bool IsValidUserId(string? userId)
        => userId is { Length: 10 } && userId.All(char.IsAsciiDigit);

    /// <summary>初始化密码要求：至少 12 位，且同时包含字母和数字。</summary>
    public static bool IsValidPassword(string? password)
        => !string.IsNullOrWhiteSpace(password)
           && password.Length >= MinPasswordLength
           && password.Any(char.IsLetter)
           && password.Any(char.IsDigit);

    /// <summary>
    /// 把 Founder 的 Staff 与 Student 一起加入上下文（由调用方 SaveChanges，
    /// 保证两条记录在同一事务中落库）。
    /// </summary>
    public static StaffDO CreateFounder(ClubContext context, string name, string userId, string password)
    {
        var founder = new StaffDO
        {
            Identity = RoleHierarchy.Founder,
            Name = name,
            UserId = userId
        };

        context.Staffs.Add(founder);
        context.Students.Add(CreateStudent(userId, name, password));
        return founder;
    }

    /// <summary>为已有 Founder 补建 Student 档案。</summary>
    public static StudentDO CreateStudent(string userId, string userName, string password)
        => new()
        {
            UserId = userId,
            UserName = userName,
            Academy = "",
            PoliticalLandscape = "群众",
            Gender = "",
            ClassName = "",
            PhoneNum = "",
            EMail = null,
            PasswordHash = DataTool.StringToHash(password),
            JoinTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
        };
}

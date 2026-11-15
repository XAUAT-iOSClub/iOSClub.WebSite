namespace iOSClub.WebAPI.Common.Security;

/// <summary>
/// 身份层级规则。
/// <para>
/// 身份在这里是"可授予的权限"，不是普通展示字段。所有会创建账号、改身份、重置密码、
/// 删除成员的路径都必须经过本类判定，否则低职级管理员可以把自己或同伙提升到更高职级
/// （历史上 Minister 可以通过部门导入/全量导入造出 President 甚至 Founder）。
/// </para>
/// <para>
/// 规则：
/// <list type="bullet">
/// <item>Founder 不受层级限制。</item>
/// <item>授予身份（<see cref="CanAssign"/>）与普通维护（<see cref="CanManage"/>）允许
/// "不高于自己"：部长可以任命部长、可以改同级成员的姓名/部门，但不能越级。</item>
/// <item>接管登录凭据（<see cref="CanTakeOver"/>：重置密码、创建 Student 档案）要求
/// "严格低于自己"。同级之间互相重置密码等同于互相接管账号，必须禁止。</item>
/// </list>
/// </para>
/// </summary>
public static class RoleHierarchy
{
    public const string Founder = "Founder";
    public const string President = "President";
    public const string Minister = "Minister";
    public const string Department = "Department";
    public const string Member = "Member";

    private static readonly Dictionary<string, int> Ranks = new(StringComparer.Ordinal)
    {
        [Member] = 10,
        [Department] = 20,
        [Minister] = 30,
        [President] = 40,
        [Founder] = 50
    };

    public static bool IsKnownRole(string? role) => role is not null && Ranks.ContainsKey(role);

    public static int RankOf(string? role) => role is not null && Ranks.TryGetValue(role, out var rank) ? rank : 0;

    /// <summary>
    /// actor 是否可以把某个账号的身份设置为 <paramref name="targetRole"/>。
    /// 非 Founder 只能授予不高于自己的身份。
    /// </summary>
    public static bool CanAssign(string? actorRole, string? targetRole)
    {
        if (!IsKnownRole(actorRole) || !IsKnownRole(targetRole)) return false;
        if (actorRole == Founder) return true;
        return RankOf(targetRole) <= RankOf(actorRole);
    }

    /// <summary>
    /// actor 是否可以对该账号做普通维护（改姓名/部门、删除）。
    /// 允许同级，但不能动更高职级。
    /// </summary>
    public static bool CanManage(string? actorRole, string? targetRole)
    {
        if (!IsKnownRole(actorRole) || !IsKnownRole(targetRole)) return false;
        if (actorRole == Founder) return true;
        return RankOf(targetRole) <= RankOf(actorRole);
    }

    /// <summary>
    /// actor 是否可以接管该账号的登录凭据（重置密码、补建 Student 档案）。
    /// 必须严格高于目标职级：同级之间互相重置密码就是互相接管。
    /// </summary>
    public static bool CanTakeOver(string? actorRole, string? targetRole)
    {
        if (!IsKnownRole(actorRole) || !IsKnownRole(targetRole)) return false;
        if (actorRole == Founder) return true;
        return RankOf(targetRole) < RankOf(actorRole);
    }
}

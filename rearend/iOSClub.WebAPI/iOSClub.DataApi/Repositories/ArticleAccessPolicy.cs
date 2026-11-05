using iOSClub.Data.DataObjects;

namespace iOSClub.DataApi.Repositories;

/// <summary>
/// 文章访问与授权规则。
/// <para>
/// 读取和写入共用同一套判定：能编辑/删除一篇文章，前提是能看见它。
/// 否则就会出现"部员看不到、却能通过接口改部长专属文章"的越权。
/// </para>
/// </summary>
public static class ArticleAccessPolicy
{
    /// <summary>
    /// 职级数值越小，权限越高；空字符串表示文章不限制职级。
    /// </summary>
    private static readonly Dictionary<string, int> IdentityLevels = new()
    {
        ["Founder"] = 0,
        ["President"] = 1,
        ["Minister"] = 2,
        ["Department"] = 3,
        ["Member"] = 4,
        [""] = 5
    };

    /// <summary>
    /// 是否为可识别的职级取值；null/空字符串表示不限制职级。
    /// </summary>
    public static bool IsKnownIdentity(string? identity)
        => string.IsNullOrEmpty(identity) || IdentityLevels.ContainsKey(identity);

    /// <summary>
    /// 访问者能否查看该文章（职级达到要求，且部门匹配）。
    /// </summary>
    public static bool CanAccess(ArticleAccessScope accessScope, ArticleDO article)
    {
        if (!IsIdentitySufficient(accessScope.Identity, article.Identity))
            return false;

        return string.IsNullOrEmpty(article.VisibleToDepartment) ||
               CanAccessAllDepartments(accessScope.Identity) ||
               article.VisibleToDepartment == accessScope.DepartmentName;
    }

    /// <summary>
    /// 访问者能否把文章的访问权限设置为 <paramref name="identity"/>。
    /// <para>
    /// 不允许设置一个连自己都达不到的职级：否则部员可以把自己的文章改成
    /// "部长仅见"，让文章从自己的可见范围里消失，也把管理权交给更高职级。
    /// </para>
    /// </summary>
    public static bool CanAssignIdentity(ArticleAccessScope accessScope, string? identity)
        => IsKnownIdentity(identity) && IsIdentitySufficient(accessScope.Identity, identity);

    /// <summary>
    /// 是否可跨部门访问（社团领导不受限定部门约束）。
    /// </summary>
    public static bool CanAccessAllDepartments(string identity)
        => identity is "Founder" or "President";

    /// <summary>
    /// 访问者可见的职级集合，用于在 LINQ 查询里做前置过滤。
    /// </summary>
    public static string[] GetAllowedIdentities(string identity)
    {
        if (!IdentityLevels.TryGetValue(identity, out var level))
            return [""];

        return IdentityLevels
            .Where(kvp => kvp.Value >= level)
            .Select(kvp => kvp.Key)
            .ToArray();
    }

    /// <summary>
    /// 访问者职级是否达到文章要求（数值越小职级越高）。
    /// </summary>
    private static bool IsIdentitySufficient(string identity, string? neededIdentity)
    {
        if (string.IsNullOrEmpty(neededIdentity) ||
            (identity == "" && neededIdentity == "Member"))
            return true;

        return IdentityLevels.TryGetValue(identity, out var identityLevel) &&
               IdentityLevels.TryGetValue(neededIdentity, out var neededLevel) &&
               identityLevel <= neededLevel;
    }
}

namespace iOSClub.WebAPI.Common.Audit;

/// <summary>
/// 操作日志的中文模块/操作名称目录。
/// <para>
/// 集中维护，避免在几十个写接口上散落审计特性。未收录的接口仍会被记录，
/// 只是使用兜底名称；<c>OperationLogCatalogTests</c> 会检查现有写接口是否漏映射。
/// </para>
/// </summary>
public static class OperationLogCatalog
{
    private static readonly Dictionary<string, string> Modules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MemberManagement"] = "成员管理",
        ["Staff"] = "员工管理",
        ["FounderPermission"] = "权限管理",
        ["Department"] = "部门管理",
        ["Article"] = "文章管理",
        ["Category"] = "分类管理",
        ["Resource"] = "资源管理",
        ["Activity"] = "活动管理",
        ["ClientApp"] = "OAuth 应用",
        ["IpBlacklist"] = "IP 黑名单",
        ["DataCentre"] = "数据管理",
        ["Monitoring"] = "数据统计",
        ["Logs"] = "系统日志",
        ["Cache"] = "缓存",
        ["User"] = "个人资料"
    };

    private static readonly Dictionary<string, string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MemberManagement.Create"] = "新增成员",
        ["MemberManagement.Update"] = "编辑成员",
        ["MemberManagement.UpdateMany"] = "批量更新成员",
        ["MemberManagement.Delete"] = "删除成员",
        ["MemberManagement.ResetPassword"] = "重置成员密码",

        ["Staff.CreateStaff"] = "创建员工",
        ["Staff.UpdateStaff"] = "编辑员工",
        ["Staff.DeleteStaff"] = "删除员工",
        ["Staff.ChangeStaffDepartment"] = "调整员工部门",
        ["Staff.AssignRole"] = "分配员工角色",

        ["FounderPermission.UpdateRole"] = "调整成员角色",

        ["Department.CreateDepartment"] = "创建部门",
        ["Department.UpdateDepartment"] = "编辑部门",
        ["Department.DeleteDepartment"] = "删除部门",
        ["Department.ImportDepartmentRoster"] = "导入部门名单",
        ["Department.RollbackDepartmentRoster"] = "回滚部门名单",

        ["Article.CreateArticle"] = "创建文章",
        ["Article.UpdateArticle"] = "编辑文章",
        ["Article.DeleteArticle"] = "删除文章",
        ["Article.UpdateArticleOrders"] = "调整文章排序",

        ["Category.CreateOrUpdateCategory"] = "新增或编辑分类",
        ["Category.DeleteCategory"] = "删除分类",
        ["Category.UpdateCategoryOrder"] = "调整分类排序",
        ["Category.UpdateCategoryOrders"] = "批量调整分类排序",

        ["Resource.AddResource"] = "新增资源",
        ["Resource.UpdateResource"] = "编辑资源",
        ["Resource.DeleteResource"] = "删除资源",

        ["Activity.Create"] = "创建活动",
        ["Activity.Update"] = "编辑活动",
        ["Activity.Delete"] = "删除活动",
        ["Activity.ChangeStatus"] = "变更活动状态",
        ["Activity.SetSelfRegistration"] = "切换自助登记",
        ["Activity.AddParticipant"] = "添加活动参与者",
        ["Activity.UpdateParticipant"] = "修改活动参与者",
        ["Activity.DeleteParticipant"] = "删除活动参与者",
        ["Activity.BulkDeleteParticipants"] = "批量删除活动参与者",
        ["Activity.Register"] = "活动自助登记",
        ["Activity.Import"] = "导入活动参与者",
        ["Activity.ImportErrorReport"] = "导出活动导入错误报告",

        ["ClientApp.CreateClientApplication"] = "创建 OAuth 应用",
        ["ClientApp.UpdateClientApplication"] = "编辑 OAuth 应用",
        ["ClientApp.DeleteClientApplication"] = "删除 OAuth 应用",
        ["ClientApp.RegenerateClientSecret"] = "重置 OAuth 应用密钥",
        ["ClientApp.GetClientSecret"] = "查看 OAuth 应用密钥",

        ["IpBlacklist.AddToBlacklist"] = "添加 IP 黑名单",
        ["IpBlacklist.RemoveFromBlacklist"] = "移除 IP 黑名单",
        ["IpBlacklist.RefreshBlacklist"] = "刷新 IP 黑名单缓存",

        ["DataCentre.UpdateDataFromJson"] = "导入全量数据",

        ["Monitoring.ResetDataStats"] = "重置数据统计",

        ["Logs.CleanupOldLogs"] = "清理旧日志",

        ["Cache.CleanData"] = "清理缓存",

        ["User.UpdateProfile"] = "修改个人资料"
    };

    /// <summary>
    /// 用 GET 承载删除的既有接口。它们同样属于写操作，必须进入审计。
    /// </summary>
    private static readonly HashSet<string> MutatingGetActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Category.DeleteCategory",
        "Department.DeleteDepartment",
        "Staff.DeleteStaff",
        // 查看密钥不改变数据，但属于敏感读取，必须留下"谁在何时看过"的记录。
        "ClientApp.GetClientSecret"
    };

    /// <summary>当前目录中出现的所有中文模块名，供筛选下拉使用。</summary>
    public static IReadOnlyCollection<string> ModuleLabels { get; } =
        Modules.Values.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    public static bool IsMutatingGet(string controller, string action)
        => MutatingGetActions.Contains($"{controller}.{action}");

    public static bool TryResolve(string controller, string action, out string module, out string actionLabel)
    {
        module = Modules.GetValueOrDefault(controller, controller);
        if (Actions.TryGetValue($"{controller}.{action}", out var label))
        {
            actionLabel = label;
            return true;
        }

        actionLabel = "";
        return false;
    }

    public static (string Module, string Action) Resolve(string controller, string action)
    {
        if (TryResolve(controller, action, out var module, out var actionLabel))
        {
            return (module, actionLabel);
        }

        var fallbackModule = Modules.GetValueOrDefault(controller, controller);
        return (fallbackModule, $"执行操作（{action}）");
    }
}

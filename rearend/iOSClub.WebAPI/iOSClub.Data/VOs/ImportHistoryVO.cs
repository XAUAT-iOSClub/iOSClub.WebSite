namespace iOSClub.Data.VOs;

/// <summary>
/// 部门名单导入历史视图对象
/// </summary>
public class ImportHistoryVO
{
    public string Id { get; set; } = "";
    public string DepartmentName { get; set; } = "";
    public DateTime ImportedAt { get; set; }
    public string OperatorId { get; set; } = "";
    public string OperatorName { get; set; } = "";
    public string? FileName { get; set; }
    public int MemberCount { get; set; }

    /// <summary>
    /// 导入前后各身份数量变化，键为身份，值为 [before, after]
    /// </summary>
    public Dictionary<string, int[]> RoleChanges { get; set; } = new();

    /// <summary>
    /// 是否可回滚到该版本（存在名单快照）
    /// </summary>
    public bool CanRollback { get; set; }
}

/// <summary>
/// 导入名单中的成员（用于备份下载，含学生档案字段以便完整还原）
/// </summary>
public class DepartmentImportMemberVO
{
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Identity { get; set; } = "";
    public string Academy { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string PhoneNum { get; set; } = "";
    public string PoliticalLandscape { get; set; } = "";
    public string Gender { get; set; } = "";
    public string? EMail { get; set; }
}

/// <summary>
/// 部门名单导入结果
/// </summary>
public class DepartmentImportResultVO
{
    public string HistoryId { get; set; } = "";
    public string DepartmentName { get; set; } = "";
    public int BeforeCount { get; set; }
    public int AfterCount { get; set; }
    public Dictionary<string, int[]> RoleChanges { get; set; } = new();

    /// <summary>
    /// 导入前的名单备份，前端可直接下载
    /// </summary>
    public List<DepartmentImportMemberVO> Backup { get; set; } = [];
}

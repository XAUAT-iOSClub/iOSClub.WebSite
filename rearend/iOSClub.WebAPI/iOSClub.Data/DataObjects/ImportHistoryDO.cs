using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace iOSClub.Data.DataObjects;

/// <summary>
/// 部门名单导入历史。每次导入覆盖前都会先备份原名单，
/// 备份内容以 JSON 字符串保存在 <see cref="BackupJson"/>，供事后下载与回查。
/// </summary>
[Table("ImportHistories")]
public class ImportHistoryDO
{
    [Key] [MaxLength(32)] public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 被导入的部门名称
    /// </summary>
    [MaxLength(20)] public string DepartmentName { get; set; } = "";

    /// <summary>
    /// 导入时间（UTC）
    /// </summary>
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 操作人学号
    /// </summary>
    [MaxLength(10)] public string OperatorId { get; set; } = "";

    /// <summary>
    /// 操作人姓名
    /// </summary>
    [MaxLength(50)] public string OperatorName { get; set; } = "";

    /// <summary>
    /// 原始文件名（前端上传时的文件名，可为空）
    /// </summary>
    [MaxLength(200)] public string? FileName { get; set; }

    /// <summary>
    /// 导入后的成员总数
    /// </summary>
    public int MemberCount { get; set; }

    /// <summary>
    /// 导入前该部门的成员名单备份（JSON 数组）
    /// </summary>
    public string BackupJson { get; set; } = "[]";

    /// <summary>
    /// 本次导入后的部门名单快照（JSON 数组），作为可回滚的版本内容。
    /// 旧数据可能为空，表示该记录不可回滚。
    /// </summary>
    public string SnapshotJson { get; set; } = "[]";

    /// <summary>
    /// 导入前后各身份数量变化（JSON 对象），用于历史详情展示
    /// </summary>
    public string SummaryJson { get; set; } = "{}";
}

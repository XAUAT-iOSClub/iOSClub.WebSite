using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace iOSClub.Data.DataObjects;

/// <summary>活动系统的重要操作日志（记录操作人、时间、内容）。</summary>
[Table("ActivityOperationLogs")]
public class ActivityOperationLogDO
{
    [Key]
    public long Id { get; set; }

    [MaxLength(36)] public string? ActivityId { get; set; }
    [MaxLength(36)] public string? OperatorId { get; set; }
    [MaxLength(50)] public string Action { get; set; } = "";
    [MaxLength(1000)] public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
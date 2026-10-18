using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace iOSClub.Data.DataObjects;

/// <summary>活动状态常量。</summary>
public static class ActivityStatus
{
    public const string Upcoming = "Upcoming"; // 待开始
    public const string Ongoing = "Ongoing";   // 进行中
    public const string Finished = "Finished"; // 已结束

    public static bool IsValid(string? status) => status is Upcoming or Ongoing or Finished;
}

[Table("Activities")]
public class ActivityDO
{
    [Key, MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [MaxLength(50)] public string Name { get; set; } = "";
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime EndTime { get; set; } = DateTime.UtcNow;
    [MaxLength(15)] public string Location { get; set; } = "";
    [MaxLength(500)] public string? Description { get; set; }
    [MaxLength(20)] public string Status { get; set; } = ActivityStatus.Upcoming;

    /// <summary>自助登记通道开关。管理员可手动控制；自动开始/结束时同步开关。</summary>
    public bool SelfRegistrationEnabled { get; set; }

    /// <summary>创建人用户 ID。</summary>
    [MaxLength(36)] public string? CreatedBy { get; set; }

    /// <summary>旧版兼容字段。状态流转现在按当前状态实时判断，此字段不再参与自动流转逻辑。</summary>
    public bool IsManualStatus { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ActivityParticipantDO> Participants { get; set; } = [];
}
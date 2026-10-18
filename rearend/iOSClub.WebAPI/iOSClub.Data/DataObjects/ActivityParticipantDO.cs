using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace iOSClub.Data.DataObjects;

/// <summary>参与者来源。</summary>
public static class ParticipantSource
{
    public const string Manual = "Manual"; // 管理员代录
    public const string Self = "Self";     // 自助登记
    public const string Import = "Import"; // Excel 导入
}

[Table("ActivityParticipants")]
public class ActivityParticipantDO
{
    [Key, MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [MaxLength(36)] public string ActivityId { get; set; } = "";
    [MaxLength(20)] public string Name { get; set; } = "";
    [MaxLength(20)] public string StudentId { get; set; } = "";
    [MaxLength(50)] public string Academy { get; set; } = "";
    [MaxLength(30)] public string ClassName { get; set; } = "";
    [MaxLength(20)] public string Source { get; set; } = ParticipantSource.Manual;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ActivityDO? Activity { get; set; }
}
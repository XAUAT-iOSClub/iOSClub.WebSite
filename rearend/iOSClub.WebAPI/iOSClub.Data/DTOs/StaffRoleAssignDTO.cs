using System.ComponentModel.DataAnnotations;

namespace iOSClub.Data.DTOs;

/// <summary>
/// 人事调动请求：将已存在的成员调整为部门内身份或社团领导身份。
/// 与 StaffCreateDTO 不同，本请求针对的是数据库中已存在的 Staff 记录，
/// 不会新增成员，只更新其 identity / department。
/// </summary>
public class StaffRoleAssignDTO
{
    [Required][MaxLength(10)] public string UserId { get; set; } = "";

    /// <summary>
    /// 目标身份：Department / Minister / President
    /// </summary>
    [Required][MaxLength(20)] public string Identity { get; set; } = "";

    /// <summary>
    /// 目标部门；President 会被忽略并清空部门
    /// </summary>
    [MaxLength(20)] public string? DepartmentName { get; set; }
}

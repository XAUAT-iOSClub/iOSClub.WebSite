using System.ComponentModel.DataAnnotations;

namespace iOSClub.Data.DTOs;

/// <summary>
/// 部门名单导入时的单条成员记录。
/// 前端已完成文件解析与预检查，后端只负责覆盖写库。
/// </summary>
public class DepartmentImportMemberDTO
{
    [Required][MaxLength(10)] public string UserId { get; set; } = "";

    [Required][MaxLength(50)] public string Name { get; set; } = "";

    /// <summary>
    /// 允许的取值：President / Minister / Department
    /// </summary>
    [MaxLength(20)] public string Identity { get; set; } = "Department";

    // --- 学生档案字段（可选，导入时一并 upsert 到 Students 表） ---
    [MaxLength(50)] public string Academy { get; set; } = "";
    [MaxLength(20)] public string ClassName { get; set; } = "";
    [MaxLength(14)] public string PhoneNum { get; set; } = "";
    [MaxLength(10)] public string PoliticalLandscape { get; set; } = "";
    [MaxLength(2)] public string Gender { get; set; } = "";
    [MaxLength(256)] public string? EMail { get; set; }
}

/// <summary>
/// 部门名单导入请求
/// </summary>
public class DepartmentImportDTO
{
    [MaxLength(200)] public string? FileName { get; set; }

    public List<DepartmentImportMemberDTO> Members { get; set; } = [];
}

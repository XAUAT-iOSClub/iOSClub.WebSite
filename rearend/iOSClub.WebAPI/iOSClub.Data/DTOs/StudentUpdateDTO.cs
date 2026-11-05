using System.ComponentModel.DataAnnotations;
using iOSClub.Data.ShowModels;
using iOSClub.Data.Validation;

namespace iOSClub.Data.DTOs;

/// <summary>
/// 学生档案更新请求。除 UserId 外均为可选：空值表示"不修改该字段"，
/// 因此这里的格式校验特性对 null/空串一律放行（见 AllowedValuesFromAttribute）。
/// </summary>
public class StudentUpdateDTO
{
    /// <summary>
    /// 定位键，不能为空且必须是合法学号格式。
    /// </summary>
    [Required(ErrorMessage = "学号不能为空")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "学号必须为10位数字")]
    public string UserId { get; set; } = "";

    [RegularExpression(@"^[\p{L}· ]{2,20}$", ErrorMessage = "姓名应为2-20个汉字或字母，不能包含数字或特殊符号")]
    public string? UserName { get; set; }

    [AllowedValuesFrom(typeof(SignRecord), nameof(SignRecord.Academies), ErrorMessage = "学院名称不在有效列表中")]
    public string? Academy { get; set; }

    [AllowedValuesFrom(typeof(SignRecord), nameof(SignRecord.PoliticalLandscapes), ErrorMessage = "政治面貌不在有效列表中")]
    public string? PoliticalLandscape { get; set; }

    [AllowedValuesFrom(typeof(SignRecord), nameof(SignRecord.Genders), ErrorMessage = "性别必须是男或女")]
    public string? Gender { get; set; }

    [RegularExpression(@"^.{2,20}$", ErrorMessage = "班级名称长度需为2-20个字符")]
    public string? ClassName { get; set; }

    [RegularExpression(@"^1[3-9]\d{9}$", ErrorMessage = "手机号格式不正确")]
    public string? PhoneNum { get; set; }

    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    [MaxLength(256, ErrorMessage = "邮箱长度不能超过256个字符")]
    public string? EMail { get; set; }
}

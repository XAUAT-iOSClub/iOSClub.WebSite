using System.ComponentModel.DataAnnotations;
using iOSClub.Data.ShowModels;
using iOSClub.Data.Validation;

namespace iOSClub.Data.DTOs;

public class StudentCreateDTO
{
    /// <summary>
    /// 10 位数字学号，同时是登录用的 UserId。
    /// </summary>
    [Required(ErrorMessage = "学号不能为空")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "学号必须为10位数字")]
    public string UserId { get; set; } = "";

    /// <summary>
    /// 真实姓名：只接受汉字、字母、间隔号（少数民族姓名）和空格。
    /// 纯数字（如 23456）或带符号的输入会被拒绝。
    /// </summary>
    [Required(ErrorMessage = "姓名不能为空")]
    [RegularExpression(@"^[\p{L}· ]{2,20}$", ErrorMessage = "姓名应为2-20个汉字或字母，不能包含数字或特殊符号")]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "学院不能为空")]
    [AllowedValuesFrom(typeof(SignRecord), nameof(SignRecord.Academies), ErrorMessage = "学院名称不在有效列表中")]
    public string Academy { get; set; } = "";

    [Required(ErrorMessage = "政治面貌不能为空")]
    [AllowedValuesFrom(typeof(SignRecord), nameof(SignRecord.PoliticalLandscapes), ErrorMessage = "政治面貌不在有效列表中")]
    public string PoliticalLandscape { get; set; } = "";

    [Required(ErrorMessage = "性别不能为空")]
    [AllowedValuesFrom(typeof(SignRecord), nameof(SignRecord.Genders), ErrorMessage = "性别必须是男或女")]
    public string Gender { get; set; } = "";

    [Required(ErrorMessage = "班级不能为空")]
    [RegularExpression(@"^.{2,20}$", ErrorMessage = "班级名称长度需为2-20个字符")]
    public string ClassName { get; set; } = "";

    [Required(ErrorMessage = "手机号不能为空")]
    [RegularExpression(@"^1[3-9]\d{9}$", ErrorMessage = "手机号格式不正确")]
    public string PhoneNum { get; set; } = "";

    /// <summary>
    /// 明文密码，由 MapperConfig 映射时用 BCrypt 哈希进 StudentDO.PasswordHash。
    /// 必须在这里就挡住空值：DataTool.StringToHash 对空白输入会抛 ArgumentException，
    /// 而它是在 Mapster 映射表达式里调用的，一旦放行到映射阶段就只能变成 500。
    /// 强度规则需与前端 SignUp.vue 的 passwordValidator 保持一致。
    /// </summary>
    [Required(ErrorMessage = "密码不能为空")]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).{8,256}$", ErrorMessage = "密码至少8个字符，且包含大写字母和数字")]
    public string Password { get; set; } = "";

    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    [MaxLength(256, ErrorMessage = "邮箱长度不能超过256个字符")]
    public string? EMail { get; set; }
}

using FluentValidation;
using iOSClub.Data.DataObjects;
using iOSClub.Data.ShowModels;

namespace iOSClub.WebAPI.Validation;

public class StudentDOValidator : AbstractValidator<StudentDO>
{
    public StudentDOValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("学号不能为空")
            .Length(10).WithMessage("学号必须为10位")
            .Matches("^[0-9]+$").WithMessage("学号只能包含数字");

        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("姓名不能为空")
            .MaximumLength(50).WithMessage("姓名长度不能超过50个字符")
            .Matches(@"^[\p{L}· ]{2,20}$").WithMessage("姓名应为2-20个汉字或字母，不能包含数字或特殊符号");

        RuleFor(x => x.Academy)
            .NotEmpty().WithMessage("学院不能为空")
            .MaximumLength(50).WithMessage("学院名称长度不能超过50个字符")
            .Must(x => SignRecord.Academies.Contains(x))
            .WithMessage("学院名称不在有效列表中");

        RuleFor(x => x.PoliticalLandscape)
            .NotEmpty().WithMessage("政治面貌不能为空")
            .MaximumLength(10).WithMessage("政治面貌长度不能超过10个字符")
            .Must(x => SignRecord.PoliticalLandscapes.Contains(x)).WithMessage("政治面貌不在有效列表中");

        RuleFor(x => x.Gender)
            .NotEmpty().WithMessage("性别不能为空")
            .MaximumLength(2).WithMessage("性别只能是1-2个字符")
            .Must(x => SignRecord.Genders.Contains(x)).WithMessage("性别必须是男或女");

        RuleFor(x => x.ClassName)
            .NotEmpty().WithMessage("班级不能为空")
            .MaximumLength(20).WithMessage("班级名称长度不能超过20个字符");

        RuleFor(x => x.PhoneNum)
            .NotEmpty().WithMessage("电话号码不能为空")
            .Length(11).WithMessage("电话号码必须为11位")
            .Matches("^1[3-9]\\d{9}$").WithMessage("电话号码格式不正确");

        RuleFor(x => x.EMail)
            .EmailAddress().WithMessage("邮箱格式不正确")
            .MaximumLength(256).WithMessage("邮箱长度不能超过256个字符")
            .When(x => !string.IsNullOrEmpty(x.EMail));
    }
}

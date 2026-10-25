using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace iOSClub.Data.Validation;

/// <summary>
/// 校验字符串属于某个静态字符串数组（例如 <see cref="ShowModels.SignRecord.Academies"/>）。
/// <para>
/// DataAnnotations 的属性参数只能是编译期常量，没法直接传数组，所以这里接收数组所在的
/// 类型和成员名，在特性实例化时读取一次。空值直接放行，由 <see cref="RequiredAttribute"/>
/// 负责"必填"语义 —— 这样同一个特性也能用在校验宽松的 Update DTO 上（空值表示不修改）。
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class AllowedValuesFromAttribute(Type sourceType, string memberName) : ValidationAttribute
{
    private readonly string[] _values = ReadValues(sourceType, memberName);

    public override bool IsValid(object? value)
    {
        if (value is not string text || string.IsNullOrEmpty(text)) return true;
        return _values.Contains(text);
    }

    private static string[] ReadValues(Type sourceType, string memberName)
    {
        var member = sourceType
            .GetMember(memberName, BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault();

        return member switch
        {
            FieldInfo field when field.GetValue(null) is string[] values => values,
            PropertyInfo property when property.GetValue(null) is string[] values => values,
            _ => throw new InvalidOperationException(
                $"{sourceType.Name}.{memberName} 必须是公开的静态 string[]，供 AllowedValuesFromAttribute 读取")
        };
    }
}

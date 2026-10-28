using iOSClub.WebAPI.Common.Security;

namespace iOSClub.Tests.SecurityTests;

/// <summary>
/// 身份层级规则的单测。所有授予/管理身份的接口都依赖这里，
/// 规则一旦被改松，C2 的越权路径会立刻回来。
/// </summary>
public class RoleHierarchyTests
{
    [Theory]
    // 不能越级授予
    [InlineData("Minister", "President", false)]
    [InlineData("Minister", "Founder", false)]
    [InlineData("President", "Founder", false)]
    [InlineData("Department", "Minister", false)]
    // 可以授予不高于自己的身份
    [InlineData("Minister", "Minister", true)]
    [InlineData("Minister", "Department", true)]
    [InlineData("President", "President", true)]
    [InlineData("President", "Minister", true)]
    [InlineData("Founder", "Founder", true)]
    [InlineData("Founder", "Member", true)]
    // 未知身份一律拒绝
    [InlineData("Minister", "", false)]
    [InlineData("", "Member", false)]
    public void CanAssign_FollowsRoleHierarchy(string actor, string target, bool expected)
        => Assert.Equal(expected, RoleHierarchy.CanAssign(actor, target));

    [Theory]
    // 普通维护允许同级
    [InlineData("Minister", "President", false)]
    [InlineData("Minister", "Minister", true)]
    [InlineData("President", "President", true)]
    [InlineData("President", "Minister", true)]
    [InlineData("Minister", "Department", true)]
    [InlineData("Founder", "President", true)]
    [InlineData("Founder", "Founder", true)]
    [InlineData("Department", "Member", true)]
    public void CanManage_AllowsSameOrLowerRole(string actor, string target, bool expected)
        => Assert.Equal(expected, RoleHierarchy.CanManage(actor, target));

    [Theory]
    // 接管登录凭据必须严格高于目标
    [InlineData("Minister", "President", false)]
    [InlineData("Minister", "Minister", false)]
    [InlineData("President", "President", false)]
    [InlineData("President", "Minister", true)]
    [InlineData("Minister", "Department", true)]
    [InlineData("Founder", "President", true)]
    [InlineData("Founder", "Founder", true)]
    [InlineData("Department", "Member", true)]
    public void CanTakeOver_RequiresStrictlyLowerRole(string actor, string target, bool expected)
        => Assert.Equal(expected, RoleHierarchy.CanTakeOver(actor, target));
}

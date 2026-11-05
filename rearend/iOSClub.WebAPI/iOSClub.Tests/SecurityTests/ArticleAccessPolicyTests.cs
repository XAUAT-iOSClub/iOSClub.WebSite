using iOSClub.Data.DataObjects;
using iOSClub.DataApi.Repositories;

namespace iOSClub.Tests.SecurityTests;

/// <summary>
/// 文章访问策略的单元测试。
/// <para>
/// 回归背景：读接口按职级/部门过滤，但 update/delete 直接按路径取文章，
/// 导致部员能修改"部长仅见"的文章。读写必须共用同一套判定。
/// </para>
/// </summary>
public class ArticleAccessPolicyTests
{
    private static ArticleDO Article(string? identity, string? visibleToDepartment = null)
        => new()
        {
            Path = "test-article",
            Title = "测试文章",
            Content = "测试内容测试内容",
            Identity = identity,
            VisibleToDepartment = visibleToDepartment
        };

    [Theory]
    [InlineData("Department", "Minister", false)]
    [InlineData("Department", "Department", true)]
    [InlineData("Department", "Member", true)]
    [InlineData("Department", null, true)]
    [InlineData("Minister", "Minister", true)]
    [InlineData("Minister", "President", false)]
    [InlineData("President", "Founder", false)]
    [InlineData("Founder", "Founder", true)]
    public void CanAccess_EnforcesIdentityLevels(
        string viewerIdentity,
        string? articleIdentity,
        bool expected)
    {
        var actual = ArticleAccessPolicy.CanAccess(
            new ArticleAccessScope(viewerIdentity),
            Article(articleIdentity));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Department", "宣传部", "科技部", false)]
    [InlineData("Minister", "宣传部", "科技部", false)]
    [InlineData("Department", "科技部", "科技部", true)]
    [InlineData("President", "宣传部", "科技部", true)]
    [InlineData("Founder", "宣传部", "科技部", true)]
    public void CanAccess_EnforcesDepartmentScope(
        string viewerIdentity,
        string visibleToDepartment,
        string viewerDepartment,
        bool expected)
    {
        var actual = ArticleAccessPolicy.CanAccess(
            new ArticleAccessScope(viewerIdentity, viewerDepartment),
            Article("Department", visibleToDepartment));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Department", "Department", true)]
    [InlineData("Department", "Member", true)]
    [InlineData("Department", null, true)]
    [InlineData("Department", "Minister", false)]
    [InlineData("Department", "President", false)]
    [InlineData("Department", "Founder", false)]
    [InlineData("Minister", "Minister", true)]
    [InlineData("Minister", "President", false)]
    [InlineData("President", "Founder", false)]
    [InlineData("Founder", "Founder", true)]
    public void CanAssignIdentity_DoesNotAllowEscalatingRestriction(
        string viewerIdentity,
        string? identity,
        bool expected)
    {
        var actual = ArticleAccessPolicy.CanAssignIdentity(
            new ArticleAccessScope(viewerIdentity),
            identity);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperUser")]
    public void CanAssignIdentity_RejectsUnknownIdentity(string identity)
    {
        Assert.False(ArticleAccessPolicy.CanAssignIdentity(new ArticleAccessScope("Founder"), identity));
    }
}

using iOSClub.Data;
using iOSClub.WebAPI.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.Tests.SecurityTests;

/// <summary>
/// Founder 首次初始化的输入校验。
/// 这里拒绝固定学号/弱密码，是为了避免公开注册抢注和默认口令登录。
/// </summary>
public class FounderBootstrapTests
{
    [Theory]
    [InlineData("0000000000", true)]
    [InlineData("2023000001", true)]
    [InlineData("123", false)]
    [InlineData("202300000A", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidUserId_RequiresTenDigits(string? userId, bool expected)
        => Assert.Equal(expected, FounderBootstrap.IsValidUserId(userId));

    [Theory]
    [InlineData("iosclub123", false)] // 只有 10 位
    [InlineData("123456789012", false)] // 没有字母
    [InlineData("abcdefghijkl", false)] // 没有数字
    [InlineData("StrongPass123", true)]
    [InlineData(null, false)]
    public void IsValidPassword_RequiresLengthLettersAndDigits(string? password, bool expected)
        => Assert.Equal(expected, FounderBootstrap.IsValidPassword(password));

    [Fact]
    public async Task CreateFounder_WritesStaffAndStudentTogether()
    {
        var options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"FounderBootstrap-{Guid.NewGuid()}")
            .Options;

        await using var context = new ClubContext(options);
        FounderBootstrap.CreateFounder(context, "root", "0000000001", "StrongPass123");
        await context.SaveChangesAsync();

        var staff = await context.Staffs.SingleAsync();
        Assert.Equal("Founder", staff.Identity);
        Assert.Equal("0000000001", staff.UserId);

        var student = await context.Students.SingleAsync();
        Assert.Equal(staff.UserId, student.UserId);
        Assert.True(DataTool.IsOk("StrongPass123", student.PasswordHash));
    }
}

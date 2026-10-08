using System.Security.Claims;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.DataApi.Repositories;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.Controllers;
using iOSClub.Data.VOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace iOSClub.Tests.IntegrationTests;

/// <summary>
/// <c>GET /User/data</c> 的回归测试。
/// <para>
/// 回归背景：GetData() 曾经对 Founder 直接返回 <c>TokenHelper.GetUser()</c> 拼出来的
/// MemberVO 存根，而那个存根只有 UserId + Identity。于是 Founder 在「概览」页看不到姓名，
/// 「个人档案」页整张表单是空的 —— 前端 userName/academy/className/phoneNum 都是 required，
/// 校验永远不过，保存按钮彻底失效。
/// </para>
/// </summary>
public class UserProfileDataTests
{
    private readonly DbContextOptions<ClubContext> _options;
    private readonly StudentRepository _studentRepository;
    private readonly StaffRepository _staffRepository;

    public UserProfileDataTests()
    {
        // 每个测试实例一个独立库名：xUnit 会并行跑不同测试类，共用一个库名会互相踩数据。
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase(databaseName: $"UserProfileData_{Guid.NewGuid()}")
            .Options;

        _studentRepository = new StudentRepository(new TestDbContextFactory(_options));
        _staffRepository = new StaffRepository(new TestDbContextFactory(_options));
    }

    private UserController CreateController(string userId, string identity)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, identity),
        ], "JWT"));

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext { User = principal });

        return new UserController(_studentRepository, _staffRepository, accessor.Object);
    }

    /// <summary>
    /// 控制器都用 <c>Ok(...)</c> 返回，此时 <c>ActionResult.Value</c> 是 null ——
    /// 载荷挂在 <c>Result</c> 上，得从 OkObjectResult 里取。
    /// </summary>
    private static ApiResponse<MemberVO> Unwrap(ActionResult<ApiResponse<MemberVO>> response)
    {
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        return Assert.IsType<ApiResponse<MemberVO>>(ok.Value);
    }

    private async Task SeedAsync(params StudentDO[] students)
    {
        await using var context = new ClubContext(_options);
        await context.Students.AddRangeAsync(students);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Founder_ReturnsPersistedProfileInsteadOfJwtStub()
    {
        // Arrange：Founder 一定有学生档案，档案里的姓名/学院/班级必须能读到。
        const string userId = "0000000000";
        await SeedAsync(new StudentDO
        {
            UserId = userId,
            UserName = "LuckyFish",
            Academy = "建筑学院",
            ClassName = "计科2101",
            Gender = "男",
            PoliticalLandscape = "群众",
            PhoneNum = "13800000000",
            EMail = "iosclub@example.com",
        });

        var controller = CreateController(userId, "Founder");

        // Act
        var response = await controller.GetData();

        // Assert
        var payload = Unwrap(response);
        Assert.Equal(ErrorCode.Success, payload.ErrorCode);
        Assert.NotNull(payload.Data);
        // 修复前这里是空串 —— 因为返回的是 claims 存根。
        Assert.Equal("LuckyFish", payload.Data!.UserName);
        Assert.Equal("建筑学院", payload.Data.Academy);
        Assert.Equal("计科2101", payload.Data.ClassName);
        // Identity 仍然取自 token，而不是学生档案。
        Assert.Equal("Founder", payload.Data.Identity);
    }

    [Fact]
    public async Task Member_StillReturnsPersistedProfile()
    {
        // Arrange
        const string userId = "2021000001";
        await SeedAsync(new StudentDO
        {
            UserId = userId,
            UserName = "张三",
            Academy = "计算机学院",
            ClassName = "软工2102",
            Gender = "女",
            PhoneNum = "13900000000",
        });

        var controller = CreateController(userId, "Member");

        // Act
        var response = await controller.GetData();

        // Assert
        var payload = Unwrap(response);
        Assert.Equal(ErrorCode.Success, payload.ErrorCode);
        Assert.Equal("张三", payload.Data!.UserName);
        Assert.Equal("计算机学院", payload.Data.Academy);
        Assert.Equal("Member", payload.Data.Identity);
    }

    [Fact]
    public async Task UnknownUser_ReturnsUserNotFound()
    {
        // Arrange：token 有效但库里没有这个人。
        var controller = CreateController("9999999999", "Member");

        // Act
        var response = await controller.GetData();

        // Assert
        var payload = Unwrap(response);
        Assert.Equal(ErrorCode.UserNotFound, payload.ErrorCode);
        Assert.Null(payload.Data);
    }
}

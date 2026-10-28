using iOSClub.DataApi.Repositories;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace iOSClub.Tests.SecurityTests;

/// <summary>
/// H4 回归：密码重置申请接口不能通过响应区分"用户是否存在"。
/// </summary>
public class PasswordResetEnumerationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RequestPasswordReset_AlwaysReturnsTheSameGenericSuccess(bool codeWasSent)
    {
        var loginService = new Mock<ILoginService>();
        loginService.Setup(s => s.RequestPasswordResetCode("2024000001")).ReturnsAsync(codeWasSent);

        var controller = new AuthController(
            Mock.Of<IStudentRepository>(),
            Mock.Of<IStaffRepository>(),
            loginService.Object,
            Mock.Of<ITokenGenerator>());

        var result = await controller.RequestPasswordReset("2024000001");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<bool>>(ok.Value);
        Assert.Equal(0, body.ErrorCode);
        Assert.Equal("如果该账号已绑定邮箱，验证码已发送", body.Message);
    }
}

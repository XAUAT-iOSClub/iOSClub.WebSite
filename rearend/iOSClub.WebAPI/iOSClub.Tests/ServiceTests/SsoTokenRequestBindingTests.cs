using System.Text;
using System.Text.Json;
using iOSClub.DataApi.Repositories;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common.Security;
using iOSClub.WebAPI.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using StackExchange.Redis;

namespace iOSClub.Tests.ServiceTests;

/// <summary>
/// SSO /token 端点同时支持 JSON 与表单。模型上标注的是 Newtonsoft 的
/// JsonProperty，但请求体实际由 System.Text.Json 反序列化，snake_case
/// 字段因此无法绑定。这里锁定两种 Content-Type 的字段解析结果一致。
/// </summary>
public class SsoTokenRequestBindingTests
{
    private static SSOController CreateController(HttpContext httpContext)
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(Mock.Of<IDatabase>());

        var controller = new SSOController(
            Mock.Of<ILoginService>(),
            Mock.Of<IStudentRepository>(),
            Mock.Of<IStaffRepository>(),
            Mock.Of<IClientApplicationRepository>(),
            redis.Object,
            new ConfigurationBuilder().Build(),
            NullLogger<SSOController>.Instance,
            null!);

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static async Task<string?> PostTokenAsync(HttpContext httpContext)
    {
        var result = await CreateController(httpContext).Token();
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var payload = JsonSerializer.Serialize(badRequest.Value);
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("error_description").GetString();
    }

    [Fact]
    public async Task Token_JsonSnakeCaseBody_IsBoundLikeFormBody()
    {
        var jsonContext = new DefaultHttpContext();
        jsonContext.Request.ContentType = "application/json";
        jsonContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(
            """{"grant_type":"authorization_code","code":"","client_id":"demo","redirect_uri":"https://app.example/cb"}"""));

        var formContext = new DefaultHttpContext();
        formContext.Request.ContentType = "application/x-www-form-urlencoded";
        formContext.Features.Set<IFormFeature>(new FormFeature(new FormCollection(
            new Dictionary<string, StringValues>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = "",
                ["client_id"] = "demo",
                ["redirect_uri"] = "https://app.example/cb"
            })));

        var jsonMessage = await PostTokenAsync(jsonContext);
        var formMessage = await PostTokenAsync(formContext);

        // 修复前 JSON 分支会报 "缺少必需参数: grant_type"；表单分支才是 "缺少必需参数: code"。
        Assert.Equal(formMessage, jsonMessage);
        Assert.Contains("code", jsonMessage);
        Assert.DoesNotContain("grant_type", jsonMessage);
    }
}

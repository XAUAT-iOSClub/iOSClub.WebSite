using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using iOSClub.Data;
using iOSClub.Data.VOs;
using iOSClub.WebAPI.Common.Security;
using iOSClub.DataApi.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;

namespace iOSClub.Tests.IntegrationTests;

public class HttpRequestContractTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly Mock<ILoginService> _login = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _token;
    private const string UserId = "2024000001";
    private const string Secret = "Password123&+#?中文";

    public HttpRequestContractTests(WebApplicationFactory<Program> factory)
    {
        var options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"HttpContract-{Guid.NewGuid()}").Options;
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(Mock.Of<IDatabase>());
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing").ConfigureServices(services =>
        {
            services.Remove(services.Single(d => d.ServiceType == typeof(IDbContextFactory<ClubContext>)));
            services.AddSingleton<IDbContextFactory<ClubContext>>(new TestDbContextFactory(options));
            services.AddSingleton(redis.Object);
            services.AddSingleton(Mock.Of<IEmailService>());
            services.AddSingleton(_login.Object);
            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                o.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        }));
        _client = _factory.CreateClient();
        _token = _factory.Services.GetRequiredService<JwtService>().GenerateTokens(
            new MemberVO { UserId = UserId, Identity = "Founder" }).AccessToken;
        _login.Setup(s => s.ValidateToken(UserId, _token, "")).ReturnsAsync(true);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        _client.DefaultRequestHeaders.Add("X-Test-UserId", UserId);
    }

    [Theory]
    [InlineData("/Staff/Delete/2024000002")]
    [InlineData("/Department/Delete/test")]
    [InlineData("/Category/Delete/test")]
    [InlineData("/SSO/from_main_jwt")]
    public async Task LegacyGet_IsRejected(string path)
    {
        var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_BindsJsonBody()
    {
        _login.Setup(s => s.ValidateToken(UserId, _token)).ReturnsAsync(true);
        _login.Setup(s => s.ChangePassword(UserId, Secret, Secret + "new")).ReturnsAsync(true);
        var response = await _client.PutAsJsonAsync("/Auth/change-password",
            new { userId = UserId, oldPassword = Secret, newPassword = Secret + "new" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _login.Verify(s => s.ChangePassword(UserId, Secret, Secret + "new"), Times.Once);
    }

    [Fact]
    public async Task RefreshToken_BindsJsonBodyAndReturnsRotatedToken()
    {
        _login.Setup(s => s.RefreshToken(UserId, Secret, "demo", "profile")).ReturnsAsync("new-access");
        _login.Setup(s => s.GetRefreshToken(UserId, "demo")).ReturnsAsync("new-refresh");
        var response = await _client.PostAsJsonAsync("/Auth/refresh-token",
            new { userId = UserId, refreshToken = Secret, clientId = "demo", scope = "profile" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("new-refresh", Assert.Single(response.Headers.GetValues("X-Refresh-Token")));
        _login.Verify(s => s.RefreshToken(UserId, Secret, "demo", "profile"), Times.Once);
    }

    [Fact]
    public async Task MainJwtSession_BindsJsonBody()
    {
        _login.Setup(s => s.LoginThirdPartyFromMainJwt(UserId, "demo", _token, "profile")).ReturnsAsync("third-party-token");
        var response = await _client.PostAsJsonAsync("/SSO/from_main_jwt", new { clientId = "demo", scope = "profile" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("third-party-token", await response.Content.ReadAsStringAsync());
        _login.Verify(s => s.LoginThirdPartyFromMainJwt(UserId, "demo", _token, "profile"), Times.Once);
    }

    [Fact]
    public async Task PasswordReset_BindsJsonBody()
    {
        _login.Setup(s => s.ResetPasswordWithCode(UserId, "123456", Secret)).ReturnsAsync(true);
        var response = await _client.PostAsJsonAsync("/Auth/reset-password",
            new { userId = UserId, code = "123456", newPassword = Secret });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _login.Verify(s => s.ResetPasswordWithCode(UserId, "123456", Secret), Times.Once);
    }

    [Theory]
    [InlineData("POST", "/Auth/reset-password?userId=2024000001&code=123456&newPassword=Password123")]
    [InlineData("POST", "/Auth/request-password-reset?userId=2024000001")]
    [InlineData("POST", "/Auth/refresh-token?userId=2024000001&refreshToken=old")]
    [InlineData("PUT", "/Auth/change-password?userId=2024000001&oldPassword=old&newPassword=new")]
    [InlineData("POST", "/SSO/from_main_jwt?client_id=demo&scope=profile")]
    public async Task QueryParametersWithoutBody_AreRejected(string method, string path)
    {
        var response = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new StringContent("", System.Text.Encoding.UTF8, "application/json")
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.All(_login.Invocations, call => Assert.Equal(nameof(ILoginService.ValidateToken), call.Method.Name));
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

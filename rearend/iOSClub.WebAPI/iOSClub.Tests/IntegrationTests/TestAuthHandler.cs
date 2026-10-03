using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iOSClub.Tests.IntegrationTests;

/// <summary>
/// 集成测试用的认证方案：不校验真实 JWT，直接按请求头给出身份。
/// <list type="bullet">
/// <item>默认：Founder / 0000000000</item>
/// <item><c>X-Test-Identity</c> / <c>X-Test-UserId</c>：指定身份与学号</item>
/// <item><c>X-Test-Anonymous: true</c>：返回未认证，用于验证 401</item>
/// </list>
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.TryGetValue("X-Test-Anonymous", out var anonymous) && anonymous == "true")
            return Task.FromResult(AuthenticateResult.NoResult());

        var userId = Request.Headers.TryGetValue("X-Test-UserId", out var id) ? id.ToString() : "0000000000";
        var role = Request.Headers.TryGetValue("X-Test-Identity", out var identity) ? identity.ToString() : "Founder";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

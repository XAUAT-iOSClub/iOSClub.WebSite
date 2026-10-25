using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using iOSClub.Data.DTOs;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.IdentityModels;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using StackExchange.Redis;

namespace iOSClub.WebAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class OAuthController(ILoginService loginService, IConnectionMultiplexer redis, IConfiguration configuration)
    : ControllerBase
{
    private readonly IDatabase _db = redis.GetDatabase();

    [HttpGet("login")]
    public IActionResult Login(string provider = "OAuth2", string? returnUrl = "/", bool rememberMe = false)
    {
        // 检查是否配置了OAuth
        var clientId = configuration["OAuth2:ClientId"];
        if (string.IsNullOrEmpty(clientId))
        {
            return Ok(ApiResponse<string>.Fail(ErrorCode.ExternalServiceNotConfigured, "OAuth2 未正确配置"));
        }

        var redirectUrl = Url.Action(nameof(Callback), "OAuth", new { returnUrl, rememberMe });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, provider);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(string? returnUrl = "/", bool rememberMe = false)
    {
        // 检查是否配置了OAuth
        var clientId = configuration["OAuth2:ClientId"];
        if (string.IsNullOrEmpty(clientId))
        {
            return BadRequest("OAuth2 未正确配置");
        }

        var authenticateResult = await HttpContext.AuthenticateAsync("OAuth2");

        if (!authenticateResult.Succeeded)
            return BadRequest("OAuth2 认证失败");

        var claims = authenticateResult.Principal?.Claims;
        if (claims == null)
            return BadRequest("无法获取用户信息");

        // 获取用户标识信息
        var enumerable = claims as Claim[] ?? claims.ToArray();
        var userId = enumerable.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        // var userName = enumerable.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(userId))
            return BadRequest("无法获取用户ID");

        var token = _db.StringGet($"token:{userId}").ToString();

        // 如果登录仍然失败，返回错误
        if (string.IsNullOrEmpty(token))
            return BadRequest("用户登录失败");

        // 返回 token 给客户端
        return Ok(new { token, returnUrl });
    }

    [HttpPost("exchange")]
    public async Task<ActionResult<ApiResponse<object>>> ExchangeToken([FromBody] OAuthExchangeRequest request)
    {
        // 如果你有一个外部 OAuth 提供商的访问令牌，
        // 可以在这里验证并交换为内部 JWT Token

        // 这是一个示例，实际实现会根据你的 OAuth 提供商而有所不同
        // 通常你需要调用 OAuth 提供商的 API 来验证访问令牌

        // 使用 loginService 验证用户并生成 JWT Token
        if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.UserName))
            return Ok(ApiResponse<object>.Fail(ErrorCode.ParameterEmpty, "用户ID和用户名不能为空"));

        // 创建LoginDTO用于传递RememberMe参数
        var loginModel = new LoginDTO
        {
            UserId = request.UserId,
            Password = request.Password ?? request.UserId, // 如果没有提供密码，则使用UserId作为默认密码
            RememberMe = request.RememberMe
        };

        // 首先尝试普通用户登录
        var token = await loginService.Login(loginModel);

        // 如果登录仍然失败，返回错误
        if (string.IsNullOrEmpty(token))
            return Ok(ApiResponse<object>.Fail(ErrorCode.UserNotFound, "用户登录失败"));

        return Ok(ApiResponse<object>.Success(new { Token = token }, "获取令牌成功"));
    }

    [HttpPost("logout")]
    [HttpGet("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(string? returnUrl = "/")
    {
        // 清除认证Cookie
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var user = HttpContext.User.GetUser();

        if (user != null)
        {
            await loginService.Logout(user.UserId);
        }
        else
        {
            return BadRequest("JWT解析失败");
        }

        var clientAppUrl = GetClientAppUrl();
        var logoutUrl = $"{clientAppUrl}/logout";

        if (!string.IsNullOrEmpty(returnUrl))
        {
            logoutUrl += $"?returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        return Redirect(logoutUrl);
    }

    [HttpGet("access-denied")]
    public IActionResult AccessDenied()
    {
        var clientAppUrl = GetClientAppUrl();
        var accessDeniedUrl = $"{clientAppUrl}/access-denied";
        return Redirect(accessDeniedUrl);
    }

    private string GetClientAppUrl()
    {
        var clientAppUrl = Environment.GetEnvironmentVariable("CLIENTAPPURL");

        if (string.IsNullOrEmpty(clientAppUrl))
        {
            clientAppUrl = configuration["ClientAppUrl"] ?? "http://localhost:5173";
        }

        return clientAppUrl.TrimEnd('/');
    }
}

[Serializable]
public class OAuthExchangeRequest
{
    public string? AccessToken { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public bool RememberMe { get; set; }
    public string? Password { get; set; }
}

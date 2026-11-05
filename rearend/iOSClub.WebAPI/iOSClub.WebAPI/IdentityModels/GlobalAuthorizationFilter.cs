using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.Common.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace iOSClub.WebAPI.IdentityModels;

public class GlobalAuthorizationFilter(
    ILoginService loginService,
    JwtService jwtService,
    ILogger<GlobalAuthorizationFilter> logger) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var endpointMetadata = context.ActionDescriptor.EndpointMetadata;
        var hasAuthorize = endpointMetadata?.Any(m =>
            m is Microsoft.AspNetCore.Authorization.AuthorizeAttribute
                or Microsoft.AspNetCore.Authorization.IAuthorizeData) == true;
        var hasAllowAnonymous =
            endpointMetadata?.Any(m => m is Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute) == true;

        var bearer = context.HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (!AuthenticationHeaderValue.TryParse(bearer, out var authorization) ||
            !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        if (hasAuthorize && !hasAllowAnonymous)
        {
            context.Result = new UnauthorizedObjectResult(ApiResponse.Fail(ErrorCode.InvalidToken, "登录会话已失效，请重新登录"));
        }

        try
        {
            var token = authorization.Parameter;
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            var (isValid, claims) = jwtService.ValidateAccessToken(token);

            if (!isValid)
            {
                return;
            }

            var enumerable = claims as Claim[] ?? claims.ToArray();
            var userId = enumerable.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value ??
                         enumerable.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var clientId = enumerable.FirstOrDefault(c => c.Type == "client_id")?.Value ?? "";

            if (string.IsNullOrEmpty(userId) || !await loginService.ValidateToken(userId, token, clientId))
            {
                return;
            }

            var claimsIdentity = new ClaimsIdentity(enumerable, "JWT");
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            context.HttpContext.User = claimsPrincipal;
            context.Result = null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "登录会话验证失败");
        }
    }
}

public static class TokenHelper
{
    public static MemberVO? GetUser(this ClaimsPrincipal? claimsPrincipal)
    {
        var claimId = claimsPrincipal?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        var claimRole = claimsPrincipal?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        if (claimId.IsNull() || claimRole.IsNull())
        {
            return null;
        }

        return new MemberVO()
        {
            UserId = claimId!.Value,
            Identity = claimRole!.Value,
        };
    }

    public static string? GetJwt(this HttpContext context)
    {
        var bearer = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(bearer) || !bearer.Contains("Bearer")) return null;
        var jwt = bearer.Split(' ');
        return jwt.Length != 2 ? null : jwt[1];
    }

    private static bool IsNull(this Claim? claim)
        => claim == null || string.IsNullOrEmpty(claim.Value);
}

using System.Security.Claims;
using iOSClub.Data.DataObjects;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.Services;

namespace iOSClub.WebAPI.Common.Middleware;

/// <summary>
/// 财务模块的角色准入与越权留痕。
/// <para>
/// [Authorize] 会在进入控制器前返回 403，但那里已经没法写业务日志；
/// 所以在授权中间件之前先判断一次角色：非 Founder / President / Minister
/// 直接返回统一信封，并写一条"越权访问尝试"日志。
/// </para>
/// </summary>
public class FinanceAccessGuardMiddleware(RequestDelegate next, ILogger<FinanceAccessGuardMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, FinanceAuditService audit)
    {
        if (!context.Request.Path.StartsWithSegments("/Finance") ||
            HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
            if (!FinanceRoles.IsAllowed(role))
            {
                logger.LogWarning("财务模块越权访问：{UserId} 角色 {Role} 请求 {Path}",
                    context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, role, context.Request.Path);

                await audit.WriteAsync(FinanceActions.UnauthorizedAttempt, null, context.Request.Path.Value,
                    null, null, "failure", $"角色 {role} 无权访问财务模块");

                var body = ApiResponse.Fail(ErrorCode.InsufficientPermission, "无权访问财务管理");
                context.Response.StatusCode = body.Code;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(body);
                return;
            }
        }

        await next(context);
    }
}

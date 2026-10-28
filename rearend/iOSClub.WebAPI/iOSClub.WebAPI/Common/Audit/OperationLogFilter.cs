using System.Diagnostics;
using System.Security.Claims;
using iOSClub.Data.DataObjects;
using iOSClub.DataApi.Exceptions;
using iOSClub.DataApi.Repositories;
using iOSClub.WebAPI.IdentityModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace iOSClub.WebAPI.Common.Audit;

/// <summary>
/// 全局操作日志采集器。
/// <para>
/// 选用 <see cref="IAsyncResourceFilter"/>：授权过滤器先执行（用户已就绪），模型绑定尚未执行
/// （请求体仍可读取），并且包住整个 action + result 管道（能拿到 <see cref="IApiResponse"/>
/// 和最终状态码，也能在模型校验短路/异常时记录失败）。
/// </para>
/// <para>
/// 审计写入是 best-effort：任何异常都只记 Warning，绝不影响业务响应。
/// </para>
/// </summary>
public sealed class OperationLogFilter(
    IOperationLogRepository repository,
    ILogger<OperationLogFilter> logger) : IAsyncResourceFilter
{
    private static readonly HashSet<string> ExcludedControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Auth", "SSO", "OperationLogs"
    };

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (!ShouldLog(context, out var descriptor, out var operatorId, out var operatorRole))
        {
            await next();
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        OperationLogSnapshot snapshot;
        try
        {
            snapshot = await OperationLogRequestSnapshot.CaptureAsync(
                context.HttpContext.Request, context.RouteData.Values);
        }
        catch (Exception ex)
        {
            // 快照失败同样不能影响业务请求。
            logger.LogWarning(ex, "操作日志请求快照失败：{Controller}.{Action}",
                descriptor.ControllerName, descriptor.MethodInfo.Name);
            await next();
            return;
        }

        ResourceExecutedContext? executed = null;
        Exception? thrown = null;

        try
        {
            executed = await next();
        }
        catch (Exception ex)
        {
            thrown = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            await SafeWriteAsync(
                context, descriptor, snapshot, operatorId, operatorRole,
                executed, thrown, stopwatch.ElapsedMilliseconds);
        }
    }

    private static bool ShouldLog(
        ResourceExecutingContext context,
        out ControllerActionDescriptor descriptor,
        out string operatorId,
        out string operatorRole)
    {
        descriptor = null!;
        operatorId = "";
        operatorRole = "";

        if (context.ActionDescriptor is not ControllerActionDescriptor cad)
        {
            return false;
        }

        if (ExcludedControllers.Contains(cad.ControllerName))
        {
            return false;
        }

        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            return false;
        }

        var user = context.HttpContext.User.GetUser();
        if (user is null)
        {
            return false;
        }

        var method = context.HttpContext.Request.Method;
        var isWrite = HttpMethods.IsPost(method)
                      || HttpMethods.IsPut(method)
                      || HttpMethods.IsPatch(method)
                      || HttpMethods.IsDelete(method);

        if (!isWrite && !OperationLogCatalog.IsMutatingGet(cad.ControllerName, cad.MethodInfo.Name))
        {
            return false;
        }

        descriptor = cad;
        operatorId = user.UserId;
        operatorRole = user.Identity;
        return true;
    }

    private async Task SafeWriteAsync(
        ResourceExecutingContext context,
        ControllerActionDescriptor descriptor,
        OperationLogSnapshot snapshot,
        string operatorId,
        string operatorRole,
        ResourceExecutedContext? executed,
        Exception? thrown,
        long durationMs)
    {
        try
        {
            var (module, action) = OperationLogCatalog.Resolve(
                descriptor.ControllerName, descriptor.MethodInfo.Name);

            var exception = thrown ?? executed?.Exception;
            bool success;
            int status;
            string? message = null;
            string? errorMessage = null;

            if (exception is not null)
            {
                success = false;
                status = exception is CustomException custom && custom.HttpStatusCode > 0
                    ? custom.HttpStatusCode
                    : StatusCodes.Status500InternalServerError;
                errorMessage = OperationLogSanitizer.Cap(
                    OperationLogSanitizer.SanitizeText($"{exception.GetType().Name}: {exception.Message}"),
                    2000);
            }
            else if (executed?.Result is ObjectResult { Value: IApiResponse body } result)
            {
                success = body.ErrorCode == 0;
                message = OperationLogSanitizer.Cap(
                    OperationLogSanitizer.SanitizeText(body.Message), 1000);
                status = (result as IStatusCodeActionResult)?.StatusCode
                         ?? (body.Code > 0 ? body.Code : context.HttpContext.Response.StatusCode);
            }
            else
            {
                status = (executed?.Result as IStatusCodeActionResult)?.StatusCode
                         ?? context.HttpContext.Response.StatusCode;
                success = status is >= StatusCodes.Status200OK and < StatusCodes.Status400BadRequest;
            }

            var target = OperationLogSanitizer.SanitizeText(BuildTarget(snapshot.RouteValues) ?? "");
            var summary = string.IsNullOrWhiteSpace(target) ? action : $"{action}：{target}";

            var entry = new OperationLogDO
            {
                OperatorId = OperationLogSanitizer.Cap(operatorId, 36),
                OperatorRole = OperationLogSanitizer.Cap(operatorRole, 20),
                OperatorName = OperationLogSanitizer.Cap(GetOperatorName(context), 50),
                Module = OperationLogSanitizer.Cap(module, 50) ?? "",
                Action = OperationLogSanitizer.Cap(action, 100) ?? "",
                ControllerName = OperationLogSanitizer.Cap(descriptor.ControllerName, 50) ?? "",
                ActionName = OperationLogSanitizer.Cap(descriptor.MethodInfo.Name, 100) ?? "",
                Method = OperationLogSanitizer.Cap(snapshot.Method, 10) ?? "",
                Path = OperationLogSanitizer.Cap(
                    OperationLogSanitizer.SanitizeText(snapshot.Path), 512),
                Summary = OperationLogSanitizer.Cap(summary, 500),
                Target = OperationLogSanitizer.Cap(target, 200),
                ChangeContent = OperationLogSanitizer.BuildChangeContent(
                    snapshot.RouteValues, snapshot.Query, snapshot.Body,
                    snapshot.BodyIsJson, snapshot.BodyOmitted),
                Message = message,
                ErrorMessage = errorMessage,
                Success = success,
                HttpStatus = status,
                DurationMs = durationMs,
                TraceId = OperationLogSanitizer.Cap(snapshot.TraceId, 64),
                IpAddress = OperationLogSanitizer.Cap(snapshot.IpAddress, 45),
                UserAgent = OperationLogSanitizer.Cap(snapshot.UserAgent, 512),
                CreatedAt = DateTime.UtcNow
            };

            await WriteWithRetryAsync(entry);
        }
        catch (Exception ex)
        {
            // 审计失败不能影响业务请求，也不能把请求体写进日志。
            logger.LogWarning(ex, "操作日志写入失败：{Controller}.{Action}",
                descriptor.ControllerName, descriptor.MethodInfo.Name);
        }
    }

    private async Task WriteWithRetryAsync(OperationLogDO entry)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await repository.AddAsync(entry, cts.Token);
                return;
            }
            catch (Exception) when (attempt < 2)
            {
                // SQLite 单写者下偶发锁竞争，重试一次。
                await Task.Delay(50);
            }
        }
    }

    private static string? GetOperatorName(ResourceExecutingContext context)
    {
        var name = context.HttpContext.User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = context.HttpContext.User.FindFirst(ClaimTypes.Name)?.Value;
        }

        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private static string? BuildTarget(IReadOnlyDictionary<string, string?> route)
    {
        if (route.Count == 0)
        {
            return null;
        }

        string[] preferred =
        [
            "id", "userId", "name", "path", "clientId", "participantId", "historyId", "departmentName"
        ];

        foreach (var key in preferred)
        {
            if (route.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return $"{key}={value}";
            }
        }

        foreach (var (key, value) in route)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return $"{key}={value}";
            }
        }

        return null;
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.DataApi.Repositories;
using iOSClub.DataApi.Services;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.WebAPI.Services;

/// <summary>
/// 财务模块的操作人与审计日志。身份完全来自网站 JWT，不新建账号、不做登录。
/// 日志写入失败只记录警告，不影响业务请求。
/// </summary>
public class FinanceAuditService(
    IHttpContextAccessor accessor,
    IFinanceRepository repository,
    IDbContextFactory<ClubContext> factory,
    ILogger<FinanceAuditService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task<FinanceActor> GetActorAsync(CancellationToken ct = default)
    {
        var context = accessor.HttpContext;
        var user = context?.User;
        var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "";
        var name = user?.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
                   ?? user?.FindFirst(ClaimTypes.Name)?.Value;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            try
            {
                await using var db = await factory.CreateDbContextAsync(ct);
                var staff = await db.Staffs.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);
                if (!string.IsNullOrWhiteSpace(staff?.Name)) name = staff.Name;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "查询财务操作人姓名失败：{UserId}", userId);
            }
        }

        return new FinanceActor(
            userId,
            string.IsNullOrWhiteSpace(name) ? userId ?? "未知用户" : name!,
            role,
            context?.Connection.RemoteIpAddress?.ToString(),
            context?.Request.Headers.UserAgent.ToString());
    }

    public async Task WriteAsync(string action, string? targetType, string? targetId, object? before,
        object? after, string result = "success", string? message = null, FinanceActor? actor = null,
        CancellationToken ct = default)
    {
        try
        {
            actor ??= await GetActorAsync(ct);
            var log = new FinanceLogDO
            {
                OperatorId = actor.UserId,
                OperatorName = actor.Name,
                Role = actor.Role,
                Ip = actor.Ip,
                UserAgent = Truncate(actor.UserAgent, 300),
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                BeforeJson = Serialize(before),
                AfterJson = Serialize(after),
                Result = result,
                Message = Truncate(message, 500),
                CreatedAt = DateTime.UtcNow
            };
            await repository.AddLogAsync(log, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "写入财务操作日志失败：{Action}", action);
        }
    }

    private static string? Serialize(object? value)
    {
        if (value == null) return null;
        try
        {
            return Truncate(JsonSerializer.Serialize(value, JsonOptions), 4000);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];
}

using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.DataApi.Repositories;

/// <summary>
/// 全局操作日志的写入与查询。仅由 <c>OperationLogFilter</c> 写入，
/// 由 <c>OperationLogsController</c>（Founder / President）读取。
/// </summary>
public interface IOperationLogRepository
{
    Task AddAsync(OperationLogDO entry, CancellationToken ct = default);

    Task<OperationLogPageVO> QueryAsync(OperationLogQueryDTO query, CancellationToken ct = default);

    Task<List<string>> GetModulesAsync(CancellationToken ct = default);

    Task<List<OperationLogOperatorVO>> GetOperatorsAsync(int take = 500, CancellationToken ct = default);
}

public class OperationLogRepository(IDbContextFactory<ClubContext> factory) : IOperationLogRepository
{
    public async Task AddAsync(OperationLogDO entry, CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);

        if (string.IsNullOrWhiteSpace(entry.OperatorName) && !string.IsNullOrWhiteSpace(entry.OperatorId))
        {
            // 操作人姓名做写入时快照，避免成员改名后历史日志失真。
            entry.OperatorName = await ctx.Staffs.AsNoTracking()
                .Where(s => s.UserId == entry.OperatorId)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(ct);
        }

        entry.CreatedAt = entry.CreatedAt.Kind == DateTimeKind.Utc
            ? entry.CreatedAt
            : entry.CreatedAt.ToUniversalTime();

        ctx.OperationLogs.Add(entry);
        await ctx.SaveChangesAsync(ct);
    }

    public async Task<OperationLogPageVO> QueryAsync(OperationLogQueryDTO query, CancellationToken ct = default)
    {
        var pageIndex = Math.Max(1, query.PageIndex);
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);

        await using var ctx = await factory.CreateDbContextAsync(ct);
        var logs = ctx.OperationLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            var module = query.Module.Trim();
            logs = logs.Where(x => x.Module == module);
        }

        if (!string.IsNullOrWhiteSpace(query.OperatorId))
        {
            var operatorId = query.OperatorId.Trim();
            logs = logs.Where(x => x.OperatorId == operatorId);
        }

        if (query.Success is { } success)
        {
            logs = logs.Where(x => x.Success == success);
        }

        if (query.StartTime is { } start)
        {
            var startUtc = start.UtcDateTime;
            logs = logs.Where(x => x.CreatedAt >= startUtc);
        }

        if (query.EndTime is { } end)
        {
            var endUtc = end.UtcDateTime;
            logs = logs.Where(x => x.CreatedAt <= endUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            if (keyword.Length > 100) keyword = keyword[..100];
            var lowered = keyword.ToLower();
            logs = logs.Where(x =>
                (x.Summary ?? "").ToLower().Contains(lowered) ||
                (x.ChangeContent ?? "").ToLower().Contains(lowered) ||
                (x.Message ?? "").ToLower().Contains(lowered) ||
                (x.Path ?? "").ToLower().Contains(lowered) ||
                (x.OperatorName ?? "").ToLower().Contains(lowered) ||
                (x.OperatorId ?? "").ToLower().Contains(lowered));
        }

        var total = await logs.CountAsync(ct);

        var entities = await logs
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // SQLite 读回的 DateTime 可能是 Unspecified；统一标记为 UTC，
        // 保证前端拿到带 Z 的 ISO 时间，不会按本地时区再偏移一次。
        var items = entities.Select(x => new OperationLogVO
        {
            Id = x.Id,
            OperatorId = x.OperatorId,
            OperatorName = x.OperatorName,
            OperatorRole = x.OperatorRole,
            Module = x.Module,
            Action = x.Action,
            ControllerName = x.ControllerName,
            ActionName = x.ActionName,
            Method = x.Method,
            Path = x.Path,
            Summary = x.Summary,
            Target = x.Target,
            ChangeContent = x.ChangeContent,
            Message = x.Message,
            ErrorMessage = x.ErrorMessage,
            Success = x.Success,
            HttpStatus = x.HttpStatus,
            DurationMs = x.DurationMs,
            TraceId = x.TraceId,
            IpAddress = x.IpAddress,
            UserAgent = x.UserAgent,
            CreatedAt = DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc)
        }).ToList();

        return new OperationLogPageVO
        {
            Items = items,
            TotalCount = total,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    public async Task<List<string>> GetModulesAsync(CancellationToken ct = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        return await ctx.OperationLogs.AsNoTracking()
            .Where(x => x.Module != "")
            .Select(x => x.Module)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);
    }

    public async Task<List<OperationLogOperatorVO>> GetOperatorsAsync(int take = 500, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 500);

        await using var ctx = await factory.CreateDbContextAsync(ct);

        // 先投影到匿名类型再 Distinct：OperationLogOperatorVO 是普通类，
        // 直接对类做 Distinct 会退化成引用比较，InMemory 下不会去重。
        var rows = await ctx.OperationLogs.AsNoTracking()
            .Where(x => x.OperatorId != null && x.OperatorId != "")
            .Select(x => new { x.OperatorId, x.OperatorName })
            .Distinct()
            .OrderBy(x => x.OperatorId)
            .Take(take)
            .ToListAsync(ct);

        return rows
            .Select(x => new OperationLogOperatorVO { UserId = x.OperatorId!, Name = x.OperatorName })
            .ToList();
    }
}

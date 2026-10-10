using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.VOs;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.DataApi.Repositories;

public interface IActivityOperationLogRepository
{
    Task LogAsync(string? activityId, string? operatorId, string action, string? detail = null);
    Task<List<ActivityOperationLogVO>> GetByActivityAsync(string activityId, int take = 100);
    Task<List<ActivityOperationLogVO>> GetRecentAsync(int take = 200);
}

public class ActivityOperationLogRepository(IDbContextFactory<ClubContext> factory) : IActivityOperationLogRepository
{
    public async Task LogAsync(string? activityId, string? operatorId, string action, string? detail = null)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        ctx.ActivityOperationLogs.Add(new ActivityOperationLogDO
        {
            ActivityId = activityId,
            OperatorId = operatorId,
            Action = action,
            Detail = detail,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
    }

    public async Task<List<ActivityOperationLogVO>> GetByActivityAsync(string activityId, int take = 100)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.ActivityOperationLogs.AsNoTracking()
            .Where(l => l.ActivityId == activityId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(take)
            .Select(l => new ActivityOperationLogVO
            {
                Id = l.Id, ActivityId = l.ActivityId, OperatorId = l.OperatorId,
                Action = l.Action, Detail = l.Detail, CreatedAt = l.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<List<ActivityOperationLogVO>> GetRecentAsync(int take = 200)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.ActivityOperationLogs.AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .Take(take)
            .Select(l => new ActivityOperationLogVO
            {
                Id = l.Id, ActivityId = l.ActivityId, OperatorId = l.OperatorId,
                Action = l.Action, Detail = l.Detail, CreatedAt = l.CreatedAt
            })
            .ToListAsync();
    }
}
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.VOs;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.DataApi.Repositories;

public interface IActivityRepository
{
    Task<List<ActivityVO>> GetAllAsync(string? status = null, string? keyword = null);
    Task<ActivityVO?> GetByIdAsync(string id);
    Task<ActivityDO?> GetByIdDOAsync(string id);
    Task<ActivityDO?> CreateAsync(ActivityDO model);
    Task<bool> UpdateAsync(ActivityDO model);
    Task<bool> UpdateStatusAsync(string id, string status);
    Task<bool> SetSelfRegistrationAsync(string id, bool enabled);
    Task<bool> DeleteAsync(string id);
    Task<List<ActivityDO>> GetAutoTransitionCandidatesAsync(DateTime now);
    Task<bool> ApplyAutoTransitionAsync(string id, string expectedStatus, string targetStatus);
}

public class ActivityRepository(IDbContextFactory<ClubContext> factory) : IActivityRepository
{
    private static IQueryable<ActivityVO> Project(IQueryable<ActivityDO> q) =>
        q.Select(a => new ActivityVO
        {
            Id = a.Id,
            Name = a.Name,
            StartTime = a.StartTime,
            EndTime = a.EndTime,
            Location = a.Location,
            Description = a.Description,
            Status = a.Status,
            SelfRegistrationEnabled = a.SelfRegistrationEnabled,
            CreatedBy = a.CreatedBy,
            ParticipantCount = a.Participants.Count,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        });

    public async Task<List<ActivityVO>> GetAllAsync(string? status = null, string? keyword = null)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var query = ctx.Activities.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status);
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(a => a.Name.Contains(keyword));
        return await Project(query)
            .OrderByDescending(a => a.StartTime)
            .ToListAsync();
    }

    public async Task<ActivityVO?> GetByIdAsync(string id)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await Project(ctx.Activities.AsNoTracking().Where(a => a.Id == id)).FirstOrDefaultAsync();
    }

    public async Task<ActivityDO?> GetByIdDOAsync(string id)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.Activities.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<ActivityDO?> CreateAsync(ActivityDO model)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        ctx.Activities.Add(model);
        return await ctx.SaveChangesAsync() > 0 ? model : null;
    }

    public async Task<bool> UpdateAsync(ActivityDO model)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var entity = await ctx.Activities.FirstOrDefaultAsync(a => a.Id == model.Id);
        if (entity == null) return false;
        entity.Name = model.Name;
        entity.StartTime = model.StartTime;
        entity.EndTime = model.EndTime;
        entity.Location = model.Location;
        entity.Description = model.Description;
        entity.UpdatedAt = DateTime.UtcNow;
        return await ctx.SaveChangesAsync() > 0;
    }

    /// <summary>变更为任意状态（管理员手动），并按规则同步自助登记通道。</summary>
    public async Task<bool> UpdateStatusAsync(string id, string status)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var entity = await ctx.Activities.FirstOrDefaultAsync(a => a.Id == id);
        if (entity == null) return false;
        entity.Status = status;
        // 进行中开启自助登记；其它状态关闭
        entity.SelfRegistrationEnabled = status == ActivityStatus.Ongoing;
        entity.UpdatedAt = DateTime.UtcNow;
        return await ctx.SaveChangesAsync() > 0;
    }

    public async Task<bool> SetSelfRegistrationAsync(string id, bool enabled)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var entity = await ctx.Activities.FirstOrDefaultAsync(a => a.Id == id);
        if (entity == null) return false;
        entity.SelfRegistrationEnabled = enabled;
        entity.UpdatedAt = DateTime.UtcNow;
        return await ctx.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var entity = await ctx.Activities.FirstOrDefaultAsync(a => a.Id == id);
        if (entity == null) return false;
        ctx.Activities.Remove(entity);
        return await ctx.SaveChangesAsync() > 0;
    }

    /// <summary>定时任务：按当前状态和时间条件找出需要自动流转的活动。</summary>
    public async Task<List<ActivityDO>> GetAutoTransitionCandidatesAsync(DateTime now)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.Activities
            .Where(a => (a.Status == ActivityStatus.Upcoming && a.StartTime <= now) ||
                        (a.Status == ActivityStatus.Ongoing && a.EndTime <= now))
            .ToListAsync();
    }

    /// <summary>定时任务：仅在当前状态仍符合预期时执行自动流转，避免覆盖管理员刚做的修改。</summary>
    public async Task<bool> ApplyAutoTransitionAsync(string id, string expectedStatus, string targetStatus)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var entity = await ctx.Activities.FirstOrDefaultAsync(a => a.Id == id && a.Status == expectedStatus);
        if (entity == null) return false;
        entity.Status = targetStatus;
        // 自动开始 -> 打开自助登记；自动结束 -> 关闭
        entity.SelfRegistrationEnabled = targetStatus == ActivityStatus.Ongoing;
        entity.UpdatedAt = DateTime.UtcNow;
        return await ctx.SaveChangesAsync() > 0;
    }
}
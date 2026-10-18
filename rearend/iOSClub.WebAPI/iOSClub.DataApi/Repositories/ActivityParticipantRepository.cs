using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.VOs;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.DataApi.Repositories;

public interface IActivityParticipantRepository
{
    Task<ActivityParticipantPageVO> GetByActivityAsync(string activityId, string? search = null, string? academy = null, int page = 1, int pageSize = 20);
    Task<List<ActivityParticipantDO>> GetAllByActivityAsync(string activityId);
    Task<ActivityParticipantDO?> GetByIdAsync(string id);
    Task<List<ActivityParticipantDO>> GetByIdsAsync(List<string> ids);
    Task<bool> ExistsAsync(string activityId, string studentId);
    Task<HashSet<string>> GetExistingStudentIdsAsync(string activityId);
    Task<ActivityParticipantDO?> CreateAsync(ActivityParticipantDO model);
    Task<bool> UpdateAsync(ActivityParticipantDO model);
    Task<bool> DeleteAsync(string id);
    Task<int> DeleteManyAsync(string activityId, List<string> ids);
}

public class ActivityParticipantRepository(IDbContextFactory<ClubContext> factory) : IActivityParticipantRepository
{
    private static ActivityParticipantVO ToVO(ActivityParticipantDO p) => new()
    {
        Id = p.Id, ActivityId = p.ActivityId, Name = p.Name, StudentId = p.StudentId,
        Academy = p.Academy, ClassName = p.ClassName, Source = p.Source,
        CreatedAt = p.CreatedAt, UpdatedAt = p.UpdatedAt
    };

    public async Task<ActivityParticipantPageVO> GetByActivityAsync(string activityId, string? search = null, string? academy = null, int page = 1, int pageSize = 20)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var query = ctx.ActivityParticipants.AsNoTracking().Where(p => p.ActivityId == activityId);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.StudentId.Contains(search));
        if (!string.IsNullOrWhiteSpace(academy))
            query = query.Where(p => p.Academy == academy);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreatedAt).ThenBy(p => p.StudentId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new ActivityParticipantVO
            {
                Id = p.Id, ActivityId = p.ActivityId, Name = p.Name, StudentId = p.StudentId,
                Academy = p.Academy, ClassName = p.ClassName, Source = p.Source,
                CreatedAt = p.CreatedAt, UpdatedAt = p.UpdatedAt
            })
            .ToListAsync();

        return new ActivityParticipantPageVO { Items = items, Total = total };
    }

    public async Task<List<ActivityParticipantDO>> GetAllByActivityAsync(string activityId)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.ActivityParticipants.AsNoTracking()
            .Where(p => p.ActivityId == activityId)
            .OrderBy(p => p.CreatedAt).ToListAsync();
    }

    public async Task<ActivityParticipantDO?> GetByIdAsync(string id)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.ActivityParticipants.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<ActivityParticipantDO>> GetByIdsAsync(List<string> ids)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.ActivityParticipants.Where(p => ids.Contains(p.Id)).ToListAsync();
    }

    public async Task<bool> ExistsAsync(string activityId, string studentId)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.ActivityParticipants.AnyAsync(p => p.ActivityId == activityId && p.StudentId == studentId);
    }

    public async Task<HashSet<string>> GetExistingStudentIdsAsync(string activityId)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var ids = await ctx.ActivityParticipants.AsNoTracking()
            .Where(p => p.ActivityId == activityId).Select(p => p.StudentId).ToListAsync();
        return new HashSet<string>(ids);
    }

    public async Task<ActivityParticipantDO?> CreateAsync(ActivityParticipantDO model)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        ctx.ActivityParticipants.Add(model);
        return await ctx.SaveChangesAsync() > 0 ? model : null;
    }

    public async Task<bool> UpdateAsync(ActivityParticipantDO model)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var entity = await ctx.ActivityParticipants.FirstOrDefaultAsync(p => p.Id == model.Id);
        if (entity == null) return false;
        entity.Name = model.Name;
        entity.StudentId = model.StudentId;
        entity.Academy = model.Academy;
        entity.ClassName = model.ClassName;
        entity.UpdatedAt = DateTime.UtcNow;
        return await ctx.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var entity = await ctx.ActivityParticipants.FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) return false;
        ctx.ActivityParticipants.Remove(entity);
        return await ctx.SaveChangesAsync() > 0;
    }

    public async Task<int> DeleteManyAsync(string activityId, List<string> ids)
    {
        if (ids.Count == 0) return 0;
        await using var ctx = await factory.CreateDbContextAsync();
        var items = await ctx.ActivityParticipants.Where(p => p.ActivityId == activityId && ids.Contains(p.Id)).ToListAsync();
        if (items.Count == 0) return 0;
        ctx.ActivityParticipants.RemoveRange(items);
        await ctx.SaveChangesAsync();
        return items.Count;
    }
}
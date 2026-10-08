using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.DataApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.Tests.RepositoryTests;

public class ActivityRepositoryTests
{
    private static DbContextOptions<ClubContext> BuildOptions() =>
        new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"ActivityRepositoryTestDatabase-{Guid.NewGuid()}")
            .Options;

    private static ActivityDO NewActivity(string status, DateTime start, DateTime end) => new()
    {
        Name = "状态流转测试活动",
        Status = status,
        StartTime = start,
        EndTime = end,
        Location = "测试地点"
    };

    [Fact]
    public async Task GetAutoTransitionCandidates_DoesNotSkipLegacyManualFlag()
    {
        var options = BuildOptions();
        await using var context = new ClubContext(options);
        var now = DateTime.UtcNow;
        var activity = NewActivity(ActivityStatus.Upcoming, now.AddMinutes(-5), now.AddMinutes(30));
        activity.IsManualStatus = true; // 旧数据可能保留 true，但新逻辑必须按当前状态判断
        context.Activities.Add(activity);
        await context.SaveChangesAsync();

        var repository = new ActivityRepository(new TestDbContextFactory(options));
        var candidates = await repository.GetAutoTransitionCandidatesAsync(now);

        Assert.Contains(candidates, a => a.Id == activity.Id);
    }

    [Fact]
    public async Task ApplyAutoTransition_DoesNotOverwriteStatusChangedAfterCandidateWasRead()
    {
        var options = BuildOptions();
        await using var context = new ClubContext(options);
        var now = DateTime.UtcNow;
        var activity = NewActivity(ActivityStatus.Upcoming, now.AddMinutes(-5), now.AddMinutes(30));
        context.Activities.Add(activity);
        await context.SaveChangesAsync();

        var repository = new ActivityRepository(new TestDbContextFactory(options));
        var candidate = Assert.Single(await repository.GetAutoTransitionCandidatesAsync(now));

        Assert.True(await repository.UpdateStatusAsync(activity.Id, ActivityStatus.Finished));
        var applied = await repository.ApplyAutoTransitionAsync(
            candidate.Id, candidate.Status, ActivityStatus.Ongoing);

        Assert.False(applied);
        await using var verifyContext = new ClubContext(options);
        var stored = await verifyContext.Activities.SingleAsync(a => a.Id == activity.Id);
        Assert.Equal(ActivityStatus.Finished, stored.Status);
        Assert.False(stored.SelfRegistrationEnabled);
    }

    [Fact]
    public async Task UpdateStatus_SyncsRegistrationAndKeepsParticipants()
    {
        var options = BuildOptions();
        await using var context = new ClubContext(options);
        var now = DateTime.UtcNow;
        var activity = NewActivity(ActivityStatus.Upcoming, now, now.AddHours(1));
        context.Activities.Add(activity);
        context.ActivityParticipants.AddRange(
            new ActivityParticipantDO
            {
                ActivityId = activity.Id, Name = "测试同学一", StudentId = "2024000001",
                Academy = "计算机和信息工程学院", ClassName = "计科2301"
            },
            new ActivityParticipantDO
            {
                ActivityId = activity.Id, Name = "测试同学二", StudentId = "2024000002",
                Academy = "计算机和信息工程学院", ClassName = "计科2301"
            });
        await context.SaveChangesAsync();

        var repository = new ActivityRepository(new TestDbContextFactory(options));
        Assert.True(await repository.UpdateStatusAsync(activity.Id, ActivityStatus.Ongoing));

        await using (var ongoingContext = new ClubContext(options))
        {
            var ongoing = await ongoingContext.Activities.SingleAsync(a => a.Id == activity.Id);
            Assert.True(ongoing.SelfRegistrationEnabled);
            Assert.Equal(2, await ongoingContext.ActivityParticipants.CountAsync());
        }

        Assert.True(await repository.UpdateStatusAsync(activity.Id, ActivityStatus.Upcoming));

        await using var verifyContext = new ClubContext(options);
        var stored = await verifyContext.Activities.SingleAsync(a => a.Id == activity.Id);
        Assert.False(stored.SelfRegistrationEnabled);
        Assert.Equal(2, await verifyContext.ActivityParticipants.CountAsync());
    }
}
using iOSClub.Data.DataObjects;
using iOSClub.DataApi.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace iOSClub.DataApi.Services;

/// <summary>
/// 定时任务：按时间自动开始/结束活动。
/// 严格单向流转（待开始→进行中→已结束）。每次触发都按当前状态判断，手动改回前置状态后会自动恢复流转。
/// </summary>
public class ActivityAutoStatusService(
    IServiceScopeFactory scopeFactory,
    ILogger<ActivityAutoStatusService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();
                var now = DateTime.UtcNow;
                var candidates = await repo.GetAutoTransitionCandidatesAsync(now);

                foreach (var activity in candidates)
                {
                    var target = activity.Status == ActivityStatus.Upcoming
                        ? ActivityStatus.Ongoing
                        : ActivityStatus.Finished;

                    if (await repo.ApplyAutoTransitionAsync(activity.Id, activity.Status, target))
                    {
                        logger.LogInformation("活动 {Name}({Id}) 自动流转：{From} → {To}",
                            activity.Name, activity.Id, activity.Status, target);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "活动自动流转任务执行出错");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
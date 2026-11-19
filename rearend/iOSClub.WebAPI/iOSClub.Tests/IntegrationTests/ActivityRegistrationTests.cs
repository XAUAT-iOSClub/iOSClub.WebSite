using System.Net;
using System.Net.Http.Json;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.DataApi.Repositories;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace iOSClub.Tests.IntegrationTests;

public class ActivityRegistrationTests
{
    [Fact]
    public async Task AnonymousRegistrationClosesAtEndTimeBeforeStatusChanges()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers(options => options.Filters.Add<ApiResponseResultFilter>())
            .AddApplicationPart(typeof(ActivityController).Assembly);
        builder.Services.AddDbContextFactory<ClubContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        builder.Services.AddScoped<IActivityRepository, ActivityRepository>();
        builder.Services.AddScoped<IActivityParticipantRepository, ActivityParticipantRepository>();
        builder.Services.AddScoped<IActivityOperationLogRepository, ActivityOperationLogRepository>();
        builder.Services.AddScoped<IActivityExcelService, ActivityExcelService>();

        await using var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        using var client = app.GetTestClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<ClubContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var activity = new ActivityDO
        {
            Name = "测试活动",
            Location = "测试地点",
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = ActivityStatus.Ongoing,
            SelfRegistrationEnabled = true
        };
        db.Activities.Add(activity);
        await db.SaveChangesAsync();
        var path = $"/Activity/{activity.Id}/register";
        var participant = new ActivityParticipantCreateDTO
        {
            Name = "测试同学", StudentId = "2024000001", Academy = "测试学院", ClassName = "测试班级"
        };

        using var openCheck = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, openCheck.StatusCode);
        Assert.True(openCheck.Headers.CacheControl?.NoStore);
        using var accepted = await client.PostAsJsonAsync(path, participant);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        activity.EndTime = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        participant.StudentId = "2024000002";

        using var closedCheck = await client.GetAsync(path);
        using var rejected = await client.PostAsJsonAsync(path, participant);
        foreach (var response in new[] { closedCheck, rejected })
        {
            var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
            Assert.NotNull(body);
            Assert.Equal(ErrorCode.InvalidStatusForOperation, body.ErrorCode);
            Assert.Equal("活动已结束", body.Message);
        }

        Assert.Equal(1, await db.ActivityParticipants.CountAsync());
    }
}

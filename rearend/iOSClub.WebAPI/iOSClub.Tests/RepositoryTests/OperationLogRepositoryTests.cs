using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.DataApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.Tests.RepositoryTests;

public class OperationLogRepositoryTests
{
    private readonly DbContextOptions<ClubContext> _options;
    private readonly OperationLogRepository _repository;

    public OperationLogRepositoryTests()
    {
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"OperationLogRepository-{Guid.NewGuid()}")
            .Options;
        _repository = new OperationLogRepository(new TestDbContextFactory(_options));
    }

    private async Task<ClubContext> CreateContextAsync()
    {
        var context = new ClubContext(_options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static OperationLogDO Log(
        string module = "成员管理",
        string action = "新增成员",
        string? operatorId = "2023000001",
        bool success = true,
        DateTime? createdAt = null,
        string? summary = null,
        string? content = null)
        => new()
        {
            Module = module,
            Action = action,
            OperatorId = operatorId,
            Success = success,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            Summary = summary,
            ChangeContent = content
        };

    [Fact]
    public async Task AddAsync_ResolvesOperatorNameFromStaff()
    {
        await using (var context = await CreateContextAsync())
        {
            context.Staffs.Add(new StaffDO
            {
                UserId = "2023000001",
                Name = "社长",
                Identity = "President"
            });
            await context.SaveChangesAsync();
        }

        await _repository.AddAsync(Log());

        await using var verify = await CreateContextAsync();
        var saved = await verify.OperationLogs.SingleAsync();
        Assert.Equal("社长", saved.OperatorName);
        Assert.Equal(DateTimeKind.Utc, saved.CreatedAt.Kind);
    }

    [Fact]
    public async Task QueryAsync_FiltersByModuleOperatorSuccessAndKeyword()
    {
        var now = DateTime.UtcNow;
        await using (var context = await CreateContextAsync())
        {
            context.OperationLogs.AddRange(
                Log(operatorId: "A", createdAt: now.AddMinutes(-3), summary: "新增成员 2023000001"),
                Log(module: "文章管理", action: "编辑文章", operatorId: "B", createdAt: now.AddMinutes(-2), summary: "编辑文章 hello"),
                Log(module: "文章管理", action: "删除文章", operatorId: "B", success: false, createdAt: now.AddMinutes(-1), summary: "删除文章 bye"));
            await context.SaveChangesAsync();
        }

        var byModule = await _repository.QueryAsync(new OperationLogQueryDTO { Module = "文章管理" });
        Assert.Equal(2, byModule.TotalCount);

        var byOperator = await _repository.QueryAsync(new OperationLogQueryDTO { OperatorId = "A" });
        Assert.Equal(1, byOperator.TotalCount);

        var failed = await _repository.QueryAsync(new OperationLogQueryDTO { Success = false });
        Assert.Equal(1, failed.TotalCount);
        Assert.Equal("删除文章", failed.Items[0].Action);

        var byKeyword = await _repository.QueryAsync(new OperationLogQueryDTO { Keyword = "hello" });
        Assert.Equal(1, byKeyword.TotalCount);
        Assert.Equal("编辑文章", byKeyword.Items[0].Action);
    }

    [Fact]
    public async Task QueryAsync_OrdersDescendingAndPaginates()
    {
        var now = DateTime.UtcNow;
        await using (var context = await CreateContextAsync())
        {
            for (var i = 0; i < 5; i++)
            {
                context.OperationLogs.Add(Log(
                    action: $"操作{i}",
                    operatorId: "A",
                    createdAt: now.AddMinutes(i)));
            }

            await context.SaveChangesAsync();
        }

        var page = await _repository.QueryAsync(new OperationLogQueryDTO { PageIndex = 2, PageSize = 2 });

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal("操作2", page.Items[0].Action);
        Assert.Equal("操作1", page.Items[1].Action);
    }

    [Fact]
    public async Task QueryAsync_ClampsPageSize()
    {
        await using (var context = await CreateContextAsync())
        {
            context.OperationLogs.Add(Log());
            await context.SaveChangesAsync();
        }

        var page = await _repository.QueryAsync(new OperationLogQueryDTO { PageSize = 1000 });

        Assert.Equal(100, page.PageSize);
    }

    [Fact]
    public async Task GetModulesAndOperators_AreDistinct()
    {
        await using (var context = await CreateContextAsync())
        {
            context.OperationLogs.AddRange(
                Log(operatorId: "A"),
                Log(operatorId: "A"),
                Log(module: "文章管理", operatorId: "B"));
            await context.SaveChangesAsync();
        }

        var modules = await _repository.GetModulesAsync();
        var operators = await _repository.GetOperatorsAsync();

        Assert.Equal(2, modules.Count);
        Assert.Contains("成员管理", modules);
        Assert.Contains("文章管理", modules);
        Assert.Equal(2, operators.Count);
        Assert.Contains(operators, x => x.UserId == "A");
        Assert.Contains(operators, x => x.UserId == "B");
    }
}

using System.Text.Json;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.DataApi.Exceptions;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.Tests.ServiceTests;

public class DepartmentImportFounderSecurityTests
{
    private readonly TestDbContextFactory _factory = new(new DbContextOptionsBuilder<ClubContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    [Theory]
    [InlineData(null)]
    [InlineData("其他部门")]
    [InlineData("技术部")]
    public async Task ImportRosterAsync_ExistingFounder_RejectsWithoutChangingData(string? founderDepartment)
    {
        await SeedAsync(founderDepartment);
        var before = await ReadStateAsync();
        var service = new DepartmentImportService(_factory);
        var dto = new DepartmentImportDTO { Members = IncomingMembers(), FileName = "名单.json" };

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            service.ImportRosterAsync("技术部", dto, "9999999999", "操作员"));

        Assert.Equal(ErrorCode.InvalidStatusForOperation, error.ErrorCode);
        Assert.Equal(before, await ReadStateAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("其他部门")]
    [InlineData("技术部")]
    public async Task RollbackAsync_SnapshotMemberIsNowFounder_RejectsWithoutChangingData(string? founderDepartment)
    {
        await SeedAsync(founderDepartment);
        var history = new ImportHistoryDO
        {
            DepartmentName = "技术部",
            SnapshotJson = JsonSerializer.Serialize(IncomingMembers()),
            FileName = "旧名单.json"
        };
        await using (var seed = _factory.CreateDbContext())
        {
            seed.ImportHistories.Add(history);
            await seed.SaveChangesAsync();
        }

        var before = await ReadStateAsync();
        var service = new DepartmentImportService(_factory);

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            service.RollbackAsync("技术部", history.Id, "9999999999", "操作员"));

        Assert.Equal(ErrorCode.InvalidStatusForOperation, error.ErrorCode);
        Assert.Equal(before, await ReadStateAsync());
    }

    [Fact]
    public async Task ImportRosterAsync_EmptyRosterWouldRemoveFounder_RejectsWithoutChangingData()
    {
        await SeedAsync("技术部");
        var before = await ReadStateAsync();
        var service = new DepartmentImportService(_factory);

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            service.ImportRosterAsync("技术部", new DepartmentImportDTO(), "9999999999", "操作员"));

        Assert.Equal(ErrorCode.InvalidStatusForOperation, error.ErrorCode);
        Assert.Equal(before, await ReadStateAsync());
    }

    private static List<DepartmentImportMemberDTO> IncomingMembers() =>
    [
        new()
        {
            UserId = "0000000001", Name = "部员", Identity = "Minister", Academy = "新学院"
        },
        new()
        {
            UserId = " 0000000000 ", Name = "创始人", Identity = "Department", Academy = "新学院"
        }
    ];

    private async Task SeedAsync(string? founderDepartment)
    {
        await using var context = _factory.CreateDbContext();
        var department = new DepartmentDO { Key = "tech", Name = "技术部" };
        var other = new DepartmentDO { Key = "other", Name = "其他部门" };
        context.Departments.AddRange(department, other);
        context.Staffs.AddRange(
            new StaffDO
            {
                UserId = "0000000001", Name = "部员", Identity = "Department", Department = department
            },
            new StaffDO
            {
                UserId = "0000000002", Name = "原部员", Identity = "Department", Department = department
            },
            new StaffDO
            {
                UserId = "0000000000", Name = "创始人", Identity = "Founder",
                Department = founderDepartment == "技术部" ? department : founderDepartment == "其他部门" ? other : null
            });
        context.Students.AddRange(
            new StudentDO
            {
                UserId = "0000000001", UserName = "部员", Academy = "原学院", PasswordHash = "member-hash"
            },
            new StudentDO
            {
                UserId = "0000000000", UserName = "创始人", Academy = "原学院", PasswordHash = "founder-hash"
            });
        await context.SaveChangesAsync();
    }

    private async Task<string> ReadStateAsync()
    {
        await using var context = _factory.CreateDbContext();
        var staffs = await context.Staffs.OrderBy(s => s.UserId)
            .Select(s => new { s.UserId, s.Name, s.Identity, Department = s.Department == null ? null : s.Department.Name })
            .ToListAsync();
        var students = await context.Students.OrderBy(s => s.UserId).ToListAsync();
        var histories = await context.ImportHistories.OrderBy(h => h.Id).ToListAsync();
        return JsonSerializer.Serialize(new { staffs, students, histories });
    }
}

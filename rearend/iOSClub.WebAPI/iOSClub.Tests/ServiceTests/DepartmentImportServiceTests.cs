using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.DataApi.Exceptions;
using iOSClub.WebAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.Tests.ServiceTests;

public class DepartmentImportServiceTests
{
    private readonly DbContextOptions<ClubContext> _options;
    private readonly TestDbContextFactory _contextFactory;
    private readonly DepartmentImportService _service;

    public DepartmentImportServiceTests()
    {
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase(databaseName: "DepartmentImportServiceTestDatabase")
            .Options;

        _contextFactory = new TestDbContextFactory(_options);
        _service = new DepartmentImportService(_contextFactory);
    }

    private async Task SeedDepartmentAsync()
    {
        await using var context = _contextFactory.CreateDbContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var department = new DepartmentDO { Key = "tech", Name = "技术部", Description = "技术部" };
        context.Departments.Add(department);
        context.Staffs.AddRange(
            new StaffDO { UserId = "0000000001", Name = "旧部长", Identity = "Minister", Department = department },
            new StaffDO { UserId = "0000000002", Name = "旧部员", Identity = "Department", Department = department });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task ImportRosterAsync_OverwritesRoster_BacksUpAndRecordsHistory()
    {
        await SeedDepartmentAsync();

        var dto = new DepartmentImportDTO
        {
            FileName = "技术部名单.xlsx",
            Members =
            [
                new DepartmentImportMemberDTO { UserId = "0000000001", Name = "新部长", Identity = "Minister" },
                new DepartmentImportMemberDTO { UserId = "0000000003", Name = "新部员", Identity = "Department" }
            ]
        };

        var result = await _service.ImportRosterAsync("技术部", dto, "9999999999", "操作员");

        Assert.Equal(2, result.BeforeCount);
        Assert.Equal(2, result.AfterCount);
        Assert.Equal(2, result.Backup.Count);
        Assert.Contains(result.Backup, m => m.UserId == "0000000002");
        Assert.Equal(1, result.RoleChanges["Minister"][0]);
        Assert.Equal(1, result.RoleChanges["Department"][0]);
        Assert.Equal(1, result.RoleChanges["Department"][1]);

        await using var context = _contextFactory.CreateDbContext();
        var staff = await context.Staffs.Include(s => s.Department).ToListAsync();
        Assert.Equal(2, staff.Count);
        Assert.DoesNotContain(staff, s => s.UserId == "0000000002");
        Assert.Equal("新部长", staff.Single(s => s.UserId == "0000000001").Name);
        var added = staff.Single(s => s.UserId == "0000000003");
        Assert.Equal("技术部", added.Department!.Name);

        var history = await context.ImportHistories.SingleAsync();
        Assert.Equal("技术部", history.DepartmentName);
        Assert.Equal(2, history.MemberCount);
        Assert.Equal("操作员", history.OperatorName);
        Assert.Equal("技术部名单.xlsx", history.FileName);
    }

    [Fact]
    public async Task ImportRosterAsync_DuplicateUserId_Throws()
    {
        await SeedDepartmentAsync();

        var dto = new DepartmentImportDTO
        {
            Members =
            [
                new DepartmentImportMemberDTO { UserId = "0000000003", Name = "甲", Identity = "Department" },
                new DepartmentImportMemberDTO { UserId = "0000000003", Name = "乙", Identity = "Department" }
            ]
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ImportRosterAsync("技术部", dto, "9999999999", "操作员"));
        Assert.Equal(2000, ex.ErrorCode);
    }

    [Fact]
    public async Task ImportRosterAsync_UpsertsStudentProfileFields()
    {
        await SeedDepartmentAsync();

        // 旧成员先有一个学生档案，用于验证字段更新
        await using (var seed = _contextFactory.CreateDbContext())
        {
            seed.Students.Add(new StudentDO
            {
                UserId = "0000000001", UserName = "旧部长", Academy = "旧学院",
                ClassName = "旧班", PhoneNum = "13900000000", PoliticalLandscape = "群众", Gender = "男"
            });
            await seed.SaveChangesAsync();
        }

        var dto = new DepartmentImportDTO
        {
            Members =
            [
                new DepartmentImportMemberDTO
                {
                    UserId = "0000000001", Name = "新部长", Identity = "Minister",
                    Academy = "计算机学院", PhoneNum = "13800138000",
                    PoliticalLandscape = "共青团员", Gender = "男"
                },
                new DepartmentImportMemberDTO
                {
                    UserId = "0000000003", Name = "新部员", Identity = "Department",
                    Academy = "软件学院", ClassName = "软工2301", PhoneNum = "13700137000",
                    Gender = "女", PoliticalLandscape = "群众", EMail = "member@example.com"
                }
            ]
        };

        await _service.ImportRosterAsync("技术部", dto, "9999999999", "操作员");

        await using var context = _contextFactory.CreateDbContext();

        var updated = await context.Students.SingleAsync(s => s.UserId == "0000000001");
        Assert.Equal("新部长", updated.UserName);
        Assert.Equal("计算机学院", updated.Academy);
        Assert.Equal("13800138000", updated.PhoneNum);
        Assert.Equal("共青团员", updated.PoliticalLandscape);

        var created = await context.Students.SingleAsync(s => s.UserId == "0000000003");
        Assert.Equal("软件学院", created.Academy);
        Assert.Equal("软工2301", created.ClassName);
        Assert.Equal("member@example.com", created.EMail);
        Assert.False(string.IsNullOrEmpty(created.PasswordHash));
        // 新学生默认密码 = 手机号
        Assert.True(DataTool.IsOk("13700137000", created.PasswordHash));
    }

    [Fact]
    public async Task ImportRosterAsync_InvalidPhone_Throws()
    {
        await SeedDepartmentAsync();

        var dto = new DepartmentImportDTO
        {
            Members =
            [
                new DepartmentImportMemberDTO
                {
                    UserId = "0000000003", Name = "甲", Identity = "Department", PhoneNum = "123"
                }
            ]
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ImportRosterAsync("技术部", dto, "9999999999", "操作员"));
        Assert.Equal(1003, ex.ErrorCode);
    }

    [Fact]
    public async Task ImportRosterAsync_DepartmentNotFound_Throws()
    {
        await SeedDepartmentAsync();

        var dto = new DepartmentImportDTO
        {
            Members = [new DepartmentImportMemberDTO { UserId = "0000000003", Name = "甲", Identity = "Department" }]
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ImportRosterAsync("不存在的部门", dto, "9999999999", "操作员"));
        Assert.Equal(4000, ex.ErrorCode);
    }

    [Fact]
    public async Task ImportRosterAsync_EmptyRoster_ClearsDepartmentAndBacksUp()
    {
        await SeedDepartmentAsync();

        var result = await _service.ImportRosterAsync("技术部", new DepartmentImportDTO(), "9999999999", "操作员");

        Assert.Equal(2, result.BeforeCount);
        Assert.Equal(0, result.AfterCount);
        Assert.Equal(2, result.Backup.Count);

        await using var context = _contextFactory.CreateDbContext();
        Assert.Empty(await context.Staffs.Include(s => s.Department)
            .Where(s => s.Department != null && s.Department.Name == "技术部").ToListAsync());
        Assert.Equal(1, await context.ImportHistories.CountAsync());
    }

    [Fact]
    public async Task ImportRosterAsync_InvalidIdentity_Throws()
    {
        await SeedDepartmentAsync();

        var dto = new DepartmentImportDTO
        {
            Members =
            [
                new DepartmentImportMemberDTO { UserId = "0000000003", Name = "甲", Identity = "Founder" }
            ]
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ImportRosterAsync("技术部", dto, "9999999999", "操作员"));
        Assert.Equal(1003, ex.ErrorCode);
    }

    [Fact]
    public async Task ImportRosterAsync_InvalidEmail_Throws()
    {
        await SeedDepartmentAsync();

        var dto = new DepartmentImportDTO
        {
            Members =
            [
                new DepartmentImportMemberDTO
                {
                    UserId = "0000000003", Name = "甲", Identity = "Department", EMail = "not-an-email"
                }
            ]
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ImportRosterAsync("技术部", dto, "9999999999", "操作员"));
        Assert.Equal(1003, ex.ErrorCode);
    }

    [Fact]
    public async Task ImportRosterAsync_InvalidGender_Throws()
    {
        await SeedDepartmentAsync();

        var dto = new DepartmentImportDTO
        {
            Members =
            [
                new DepartmentImportMemberDTO
                {
                    UserId = "0000000003", Name = "甲", Identity = "Department", Gender = "未知"
                }
            ]
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ImportRosterAsync("技术部", dto, "9999999999", "操作员"));
        Assert.Equal(1003, ex.ErrorCode);
    }

    [Fact]
    public async Task ImportRosterAsync_NewStudentWithoutPhone_UsesUserIdAsDefaultPassword()
    {
        await SeedDepartmentAsync();

        var dto = new DepartmentImportDTO
        {
            Members =
            [
                new DepartmentImportMemberDTO { UserId = "0000000003", Name = "甲", Identity = "Department" }
            ]
        };

        await _service.ImportRosterAsync("技术部", dto, "9999999999", "操作员");

        await using var context = _contextFactory.CreateDbContext();
        var created = await context.Students.SingleAsync(s => s.UserId == "0000000003");
        Assert.True(DataTool.IsOk("0000000003", created.PasswordHash));
    }

    [Fact]
    public async Task ImportRosterAsync_BackupIncludesStudentProfileFields()
    {
        await SeedDepartmentAsync();

        await using (var seed = _contextFactory.CreateDbContext())
        {
            seed.Students.Add(new StudentDO
            {
                UserId = "0000000002", UserName = "旧部员", Academy = "信息学院", ClassName = "信管2301",
                PhoneNum = "13800138000", PoliticalLandscape = "共青团员", Gender = "女", EMail = "old@example.com"
            });
            await seed.SaveChangesAsync();
        }

        // 新名单只保留 0000000001，0000000002 会被移除并进入备份
        var dto = new DepartmentImportDTO
        {
            Members =
            [
                new DepartmentImportMemberDTO { UserId = "0000000001", Name = "旧部长", Identity = "Minister" }
            ]
        };

        var result = await _service.ImportRosterAsync("技术部", dto, "9999999999", "操作员");

        var backedUp = Assert.Single(result.Backup, m => m.UserId == "0000000002");
        Assert.Equal("信息学院", backedUp.Academy);
        Assert.Equal("信管2301", backedUp.ClassName);
        Assert.Equal("13800138000", backedUp.PhoneNum);
        Assert.Equal("共青团员", backedUp.PoliticalLandscape);
        Assert.Equal("女", backedUp.Gender);
        Assert.Equal("old@example.com", backedUp.EMail);
    }

    [Fact]
    public async Task ImportRosterAsync_EmptyProfileFieldsDoNotWipeExistingStudent()
    {
        await SeedDepartmentAsync();

        await using (var seed = _contextFactory.CreateDbContext())
        {
            seed.Students.Add(new StudentDO
            {
                UserId = "0000000001", UserName = "旧部长", Academy = "计算机学院", ClassName = "计科2301",
                PhoneNum = "13800138000", PoliticalLandscape = "共青团员", Gender = "男", EMail = "keep@example.com"
            });
            await seed.SaveChangesAsync();
        }

        // 名单只给 姓名/学号/职位，档案字段全空 —— 不应抹掉已有档案
        var dto = new DepartmentImportDTO
        {
            Members =
            [
                new DepartmentImportMemberDTO { UserId = "0000000001", Name = "新名字", Identity = "Minister" }
            ]
        };

        await _service.ImportRosterAsync("技术部", dto, "9999999999", "操作员");

        await using var context = _contextFactory.CreateDbContext();
        var student = await context.Students.SingleAsync(s => s.UserId == "0000000001");
        Assert.Equal("新名字", student.UserName);
        Assert.Equal("计算机学院", student.Academy);
        Assert.Equal("计科2301", student.ClassName);
        Assert.Equal("13800138000", student.PhoneNum);
        Assert.Equal("共青团员", student.PoliticalLandscape);
        Assert.Equal("男", student.Gender);
        Assert.Equal("keep@example.com", student.EMail);
    }

    [Fact]
    public async Task ImportRosterAsync_RemovedMemberKeepsStudentProfile()
    {
        await SeedDepartmentAsync();

        await using (var seed = _contextFactory.CreateDbContext())
        {
            seed.Students.Add(new StudentDO { UserId = "0000000002", UserName = "旧部员", Academy = "信息学院" });
            await seed.SaveChangesAsync();
        }

        // 覆盖名单时移除 0000000002
        await _service.ImportRosterAsync("技术部", new DepartmentImportDTO
        {
            Members = [new DepartmentImportMemberDTO { UserId = "0000000001", Name = "旧部长", Identity = "Minister" }]
        }, "9999999999", "操作员");

        await using var context = _contextFactory.CreateDbContext();
        // Staff 解除部门关系（这里直接删除），但学生档案保留
        Assert.DoesNotContain(await context.Staffs.ToListAsync(), s => s.UserId == "0000000002");
        Assert.True(await context.Students.AnyAsync(s => s.UserId == "0000000002"));
    }
}

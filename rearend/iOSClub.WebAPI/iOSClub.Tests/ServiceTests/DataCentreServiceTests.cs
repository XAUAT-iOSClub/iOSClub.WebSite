using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.DataApi.Services;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.Tests.ServiceTests;

public class DataCentreServiceTests
{
    private readonly DbContextOptions<ClubContext> _options;
    private readonly TestDbContextFactory _contextFactory;
    private readonly DataCentreService _dataCentreService;

    public DataCentreServiceTests()
    {
        // 使用内存数据库进行测试
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase(databaseName: "DataCentreServiceTestDatabase")
            .Options;

        _contextFactory = new TestDbContextFactory(_options);
        _dataCentreService = new DataCentreService(_contextFactory);
    }

    [Fact]
    public async Task GetYearDataAsync_ReturnsCorrectYearCountVOs()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成测试数据
        var students = new List<StudentDO>
        {
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "20123456")
                .RuleFor(s => s.JoinTime, new DateTime(2020, 9, 1))
                .RuleFor(s => s.Academy, "Computer")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "21123456")
                .RuleFor(s => s.JoinTime, new DateTime(2021, 9, 1))
                .RuleFor(s => s.Academy, "Computer")
                .RuleFor(s => s.PoliticalLandscape, "中共党员")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "21234567")
                .RuleFor(s => s.JoinTime, new DateTime(2021, 9, 1))
                .RuleFor(s => s.Academy, "Information")
                .RuleFor(s => s.Gender, "女")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "22123456")
                .RuleFor(s => s.JoinTime, new DateTime(2022, 9, 1))
                .RuleFor(s => s.Academy, "Computer")
                .RuleFor(s => s.Gender, "女")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "22234567")
                .RuleFor(s => s.JoinTime, new DateTime(2022, 9, 1))
                .RuleFor(s => s.Academy, "Information")
                .RuleFor(s => s.PoliticalLandscape, "群众")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "22345678")
                .RuleFor(s => s.JoinTime, new DateTime(2022, 9, 1))
                .RuleFor(s => s.Academy, "Mathematics")
                .Generate(),
        };

        await context.Students.AddRangeAsync(students);
        await context.SaveChangesAsync();

        // Act
        var result = await _dataCentreService.GetYearDataAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains(result,
            yc => yc.Year == "2019学年" || yc.Year == "2020学年" || yc.Year == "2021学年" || yc.Year == "2022学年");
    }

    [Fact]
    public async Task GetCollegeDataAsync_ReturnsCorrectAcademyCountVOs()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成测试数据
        var students = new List<StudentDO>
        {
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "20123456")
                .RuleFor(s => s.JoinTime, new DateTime(2020, 9, 1))
                .RuleFor(s => s.Academy, "Computer")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "21123456")
                .RuleFor(s => s.JoinTime, new DateTime(2021, 9, 1))
                .RuleFor(s => s.Academy, "Computer")
                .RuleFor(s => s.PoliticalLandscape, "中共党员")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "21234567")
                .RuleFor(s => s.JoinTime, new DateTime(2021, 9, 1))
                .RuleFor(s => s.Academy, "Information")
                .RuleFor(s => s.Gender, "女")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "22123456")
                .RuleFor(s => s.JoinTime, new DateTime(2022, 9, 1))
                .RuleFor(s => s.Academy, "Computer")
                .RuleFor(s => s.Gender, "女")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "22234567")
                .RuleFor(s => s.JoinTime, new DateTime(2022, 9, 1))
                .RuleFor(s => s.Academy, "Information")
                .RuleFor(s => s.PoliticalLandscape, "群众")
                .Generate(),
        };

        await context.Students.AddRangeAsync(students);
        await context.SaveChangesAsync();

        // Act
        var result = await _dataCentreService.GetCollegeDataAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, ac => ac is { Type: "Computer", Value: 3 });
        Assert.Contains(result, ac => ac is { Type: "Information", Value: 2 });
    }

    [Fact]
    public async Task GetGradeDataAsync_ReturnsCorrectGradeCountVOs()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成测试数据
        var students = new List<StudentDO>
        {
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "20123456")
                .RuleFor(s => s.JoinTime, new DateTime(2020, 9, 1))
                .RuleFor(s => s.Academy, "Computer")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "21123456")
                .RuleFor(s => s.JoinTime, new DateTime(2021, 9, 1))
                .RuleFor(s => s.Academy, "Computer")
                .RuleFor(s => s.PoliticalLandscape, "中共党员")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "21234567")
                .RuleFor(s => s.JoinTime, new DateTime(2021, 9, 1))
                .RuleFor(s => s.Academy, "Information")
                .RuleFor(s => s.Gender, "女")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "22123456")
                .RuleFor(s => s.JoinTime, new DateTime(2022, 9, 1))
                .RuleFor(s => s.Academy, "Computer")
                .RuleFor(s => s.Gender, "女")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "22234567")
                .RuleFor(s => s.JoinTime, new DateTime(2022, 9, 1))
                .RuleFor(s => s.Academy, "Information")
                .RuleFor(s => s.PoliticalLandscape, "群众")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "22345678")
                .RuleFor(s => s.JoinTime, new DateTime(2022, 9, 1))
                .RuleFor(s => s.Academy, "Mathematics")
                .Generate(),
        };

        await context.Students.AddRangeAsync(students);
        await context.SaveChangesAsync();

        // Act
        var result = await _dataCentreService.GetGradeDataAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal(3, result.Count);
        Assert.Contains(result, gc => gc is { Grade: "20级", Value: 1 });
        Assert.Contains(result, gc => gc is { Grade: "21级", Value: 2 });
        Assert.Contains(result, gc => gc is { Grade: "22级", Value: 3 });
    }

    [Fact]
    public async Task GetLandscapeDataAsync_ReturnsCorrectPoliticalLandscapeCountVOs()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成测试数据
        var students = new List<StudentDO>
        {
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "20123456")
                .RuleFor(s => s.JoinTime, new DateTime(2020, 9, 1))
                .RuleFor(s => s.Academy, "Computer")
                .RuleFor(s => s.PoliticalLandscape, "共青团员")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "21123456")
                .RuleFor(s => s.JoinTime, new DateTime(2021, 9, 1))
                .RuleFor(s => s.Academy, "Computer")
                .RuleFor(s => s.PoliticalLandscape, "中共党员")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "21234567")
                .RuleFor(s => s.JoinTime, new DateTime(2021, 9, 1))
                .RuleFor(s => s.Academy, "Information")
                .RuleFor(s => s.Gender, "女")
                .RuleFor(s => s.PoliticalLandscape, "共青团员")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "22123456")
                .RuleFor(s => s.JoinTime, new DateTime(2022, 9, 1))
                .RuleFor(s => s.Academy, "Computer")
                .RuleFor(s => s.Gender, "女")
                .RuleFor(s => s.PoliticalLandscape, "共青团员")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "22234567")
                .RuleFor(s => s.JoinTime, new DateTime(2022, 9, 1))
                .RuleFor(s => s.Academy, "Information")
                .RuleFor(s => s.PoliticalLandscape, "群众")
                .Generate(),
        };

        await context.Students.AddRangeAsync(students);
        await context.SaveChangesAsync();

        // Act
        var result = await _dataCentreService.GetLandscapeDataAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal(3, result.Count);
        Assert.Contains(result, lc => lc is { Type: "共青团员", Value: 3 });
        Assert.Contains(result, lc => lc is { Type: "中共党员", Value: 1 });
        Assert.Contains(result, lc => lc is { Type: "群众", Value: 1 });
    }

    [Fact]
    public async Task GetGenderDataAsync_ReturnsCorrectGenderCountVOs()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成测试数据，确保性别分布符合预期
        var students = new List<StudentDO>
        {
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "20123456")
                .RuleFor(s => s.JoinTime, new DateTime(2020, 9, 1))
                .RuleFor(s => s.Academy, "Computer")
                .RuleFor(s => s.Gender, "男")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "21123456")
                .RuleFor(s => s.JoinTime, new DateTime(2021, 9, 1))
                .RuleFor(s => s.Academy, "Computer")
                .RuleFor(s => s.Gender, "男")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "21234567")
                .RuleFor(s => s.JoinTime, new DateTime(2021, 9, 1))
                .RuleFor(s => s.Academy, "Information")
                .RuleFor(s => s.Gender, "女")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "22123456")
                .RuleFor(s => s.JoinTime, new DateTime(2022, 9, 1))
                .RuleFor(s => s.Academy, "Computer")
                .RuleFor(s => s.Gender, "女")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "22234567")
                .RuleFor(s => s.JoinTime, new DateTime(2022, 9, 1))
                .RuleFor(s => s.Academy, "Information")
                .RuleFor(s => s.Gender, "男")
                .Generate(),
        };

        await context.Students.AddRangeAsync(students);
        await context.SaveChangesAsync();

        // Act
        var result = await _dataCentreService.GetGenderDataAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, gc => gc is { Type: "男", Value: 3 });
        Assert.Contains(result, gc => gc is { Type: "女", Value: 2 });
    }

    [Fact]
    public async Task GetYearDataAsync_WithEmptyDatabase_ReturnsDefaultYearData()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // Act
        var result = await _dataCentreService.GetYearDataAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.True(result.Count >= 4); // 至少返回2019-2022学年的数据
    }

    [Fact]
    public async Task GetGradeDataAsync_WithEmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // Act
        var result = await _dataCentreService.GetGradeDataAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // 把导出的备份再传回来时，库里的学号一定已经存在；旧实现直接 AddRange，会撞 PK_Students
    [Fact]
    public async Task ImportAllDataAsync_WithExistingStudents_UpdatesInPlaceAndOnlyInsertsNewOnes()
    {
        // Arrange
        await using (var seed = new ClubContext(_options))
        {
            await seed.Database.EnsureDeletedAsync();
            await seed.Database.EnsureCreatedAsync();

            var existing = BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.UserId, "20123456")
                .RuleFor(s => s.UserName, "旧名字")
                .RuleFor(s => s.PhoneNum, "13800000000")
                .Generate();
            existing.PasswordHash = "原有密码哈希";

            await seed.Students.AddAsync(existing);
            await seed.SaveChangesAsync();
        }

        var data = new AllDataImportDTO
        {
            Students =
            [
                new StudentDO { UserId = "20123456", UserName = "新名字", PhoneNum = "13800000000" },
                new StudentDO { UserId = "21123456", UserName = "新同学", PhoneNum = "13900000000" }
            ]
        };

        // Act
        var result = await _dataCentreService.ImportAllDataAsync(data);

        // Assert
        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Updated);

        await using var context = new ClubContext(_options);
        var students = await context.Students.OrderBy(s => s.UserId).ToListAsync();
        Assert.Equal(2, students.Count);

        var updated = students.Single(s => s.UserId == "20123456");
        Assert.Equal("新名字", updated.UserName);
        // 备份导入不应该覆盖库里已有的密码和入社时间
        Assert.Equal("原有密码哈希", updated.PasswordHash);
    }

    // 同一份文件里重复出现的学号在 EF 里是"同一实体的两个实例"，跟踪时就会抛错，必须在入库前合并
    [Fact]
    public async Task ImportAllDataAsync_WithDuplicateRowsInSameFile_MergesThemIntoOneRow()
    {
        // Arrange
        await using (var seed = new ClubContext(_options))
        {
            await seed.Database.EnsureDeletedAsync();
            await seed.Database.EnsureCreatedAsync();
        }

        var data = new AllDataImportDTO
        {
            Students =
            [
                new StudentDO { UserId = "20123456", UserName = "先出现", PhoneNum = "13800000000" },
                new StudentDO { UserId = "20123456", UserName = "后出现", PhoneNum = "13800000000" }
            ]
        };

        // Act
        var result = await _dataCentreService.ImportAllDataAsync(data);

        // Assert
        Assert.Equal(1, result.Added);

        await using var context = new ClubContext(_options);
        var student = await context.Students.SingleAsync();
        Assert.Equal("后出现", student.UserName);
    }

    [Fact]
    public async Task ImportAllDataAsync_WithMissingPrimaryKey_SkipsTheRow()
    {
        // Arrange
        await using (var seed = new ClubContext(_options))
        {
            await seed.Database.EnsureDeletedAsync();
            await seed.Database.EnsureCreatedAsync();
        }

        var data = new AllDataImportDTO
        {
            Students = [new StudentDO { UserId = "  ", UserName = "没有学号", PhoneNum = "13800000000" }]
        };

        // Act
        var result = await _dataCentreService.ImportAllDataAsync(data);

        // Assert
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Added);

        await using var context = new ClubContext(_options);
        Assert.Empty(await context.Students.ToListAsync());
    }

    // 备份里的 Founder 记录的可能是旧身份，导入不能把现任创始人降级成社长
    [Fact]
    public async Task ImportAllDataAsync_WithFounderInPresidents_KeepsFounderIdentity()
    {
        // Arrange
        await using (var seed = new ClubContext(_options))
        {
            await seed.Database.EnsureDeletedAsync();
            await seed.Database.EnsureCreatedAsync();

            await seed.Staffs.AddAsync(new StaffDO { UserId = "0000000000", Name = "root", Identity = "Founder" });
            await seed.SaveChangesAsync();
        }

        var data = new AllDataImportDTO
        {
            Presidents =
            [
                new StaffDO { UserId = "0000000000", Name = "root", Identity = "President" },
                new StaffDO { UserId = "20123456", Name = "新社长", Identity = "President" }
            ]
        };

        // Act
        var result = await _dataCentreService.ImportAllDataAsync(data);

        // Assert
        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Updated);

        await using var context = new ClubContext(_options);
        Assert.Equal("Founder", (await context.Staffs.SingleAsync(s => s.UserId == "0000000000")).Identity);
        Assert.Equal("President", (await context.Staffs.SingleAsync(s => s.UserId == "20123456")).Identity);
    }

    // 部门里夹带的部员列表如果直接入库，会级联插入并再次撞上 Staffs 主键
    [Fact]
    public async Task ImportAllDataAsync_WithNestedStaffsInDepartment_DoesNotCascadeInsertThem()
    {
        // Arrange
        await using (var seed = new ClubContext(_options))
        {
            await seed.Database.EnsureDeletedAsync();
            await seed.Database.EnsureCreatedAsync();

            await seed.Staffs.AddAsync(new StaffDO { UserId = "20123456", Name = "已有部员", Identity = "Department" });
            await seed.SaveChangesAsync();
        }

        var data = new AllDataImportDTO
        {
            Departments =
            [
                new DepartmentDO
                {
                    Name = "技术部",
                    Key = "tech",
                    Staffs = [new StaffDO { UserId = "20123456", Name = "已有部员", Identity = "Department" }]
                }
            ]
        };

        // Act
        var result = await _dataCentreService.ImportAllDataAsync(data);

        // Assert
        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Skipped);

        await using var context = new ClubContext(_options);
        Assert.Equal("技术部", (await context.Departments.SingleAsync()).Name);
        Assert.Single(await context.Staffs.ToListAsync());
    }
}
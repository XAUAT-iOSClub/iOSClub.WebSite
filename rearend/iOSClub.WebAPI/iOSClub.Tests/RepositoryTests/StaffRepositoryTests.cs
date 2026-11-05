using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.DataApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.Tests.RepositoryTests;

public class StaffRepositoryTests
{
    private readonly DbContextOptions<ClubContext> _options;
    private readonly StaffRepository _staffRepository;

    public StaffRepositoryTests()
    {
        // 使用内存数据库进行测试
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase(databaseName: "StaffRepositoryTestDatabase")
            .Options;
        
        var contextFactory = new TestDbContextFactory(_options);
        _staffRepository = new StaffRepository(contextFactory);
    }

    [Fact]
    public async Task GetAllStaffAsync_ReturnsAllStaffMembers()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成2个员工
        var staffs = BogusDataGenerator.GenerateStaffs(2);
        await context.Staffs.AddRangeAsync(staffs);
        await context.SaveChangesAsync();

        // Act
        var result = await _staffRepository.GetAllStaffAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task GetStaffByIdAsync_ReturnsCorrectStaff()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成1个员工
        var staff = BogusDataGenerator.StaffFaker.Generate();
        await context.Staffs.AddAsync(staff);
        await context.SaveChangesAsync();

        // Act
        var result = await _staffRepository.GetStaffByIdAsync(staff.UserId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(staff.UserId, result.UserId);
        Assert.Equal(staff.Name, result.Name);
    }

    [Fact]
    public async Task GetStaffByIdWithoutOtherData_ReturnsCorrectStaff()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成1个员工
        var staff = BogusDataGenerator.StaffFaker.Generate();
        await context.Staffs.AddAsync(staff);
        await context.SaveChangesAsync();

        // Act
        var result = await _staffRepository.GetStaffByIdWithoutOtherData(staff.UserId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(staff.UserId, result.UserId);
        Assert.Equal(staff.Name, result.Name);
    }

    [Fact]
    public async Task StaffExistsAsync_ReturnsTrue_WhenStaffExists()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成1个员工
        var staff = BogusDataGenerator.StaffFaker.Generate();
        await context.Staffs.AddAsync(staff);
        await context.SaveChangesAsync();

        // Act
        var result = await _staffRepository.StaffExistsAsync(staff.UserId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task StaffExistsAsync_ReturnsFalse_WhenStaffDoesNotExist()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // Act
        var result = await _staffRepository.StaffExistsAsync("non-existent-staff");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CreateStaffAsync_CreatesNewStaff()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        var staff = BogusDataGenerator.StaffFaker.Generate();
        staff.Identity = "President";

        // Act
        var result = await _staffRepository.CreateStaffAsync(staff);
        
        // 创建新的context实例来查询更新后的结果
        await using var newContext = new ClubContext(_options);
        var savedStaff = await newContext.Staffs.FirstOrDefaultAsync(s => s.UserId == staff.UserId);

        // Assert
        Assert.True(result);
        Assert.NotNull(savedStaff);
        Assert.Equal(staff.UserId, savedStaff.UserId);
    }

    [Fact]
    public async Task UpdateStaffAsync_UpdatesExistingStaff()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成1个员工
        var staff = BogusDataGenerator.StaffFaker.Generate();
        staff.Identity = "Department";
        await context.Staffs.AddAsync(staff);
        await context.SaveChangesAsync();
        
        // 修改员工信息
        staff.Name = "Updated Name";
        staff.Identity = "Minister";

        // Act
        var result = await _staffRepository.UpdateStaffAsync(staff);
        
        // 创建新的context实例来查询更新后的结果
        await using var newContext = new ClubContext(_options);
        var updatedStaff = await newContext.Staffs.FirstOrDefaultAsync(s => s.UserId == staff.UserId);

        // Assert
        Assert.True(result);
        Assert.NotNull(updatedStaff);
        Assert.Equal("Updated Name", updatedStaff.Name);
        Assert.Equal("Minister", updatedStaff.Identity);
    }

    [Fact]
    public async Task DeleteStaffAsync_RemovesStaff()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成1个员工
        var staff = BogusDataGenerator.StaffFaker.Generate();
        staff.Identity = "Department";
        await context.Staffs.AddAsync(staff);
        await context.SaveChangesAsync();

        // Act
        var result = await _staffRepository.DeleteStaffAsync(staff.UserId);
        
        // 创建新的context实例来查询更新后的结果
        await using var newContext = new ClubContext(_options);
        var deletedStaff = await newContext.Staffs.FirstOrDefaultAsync(s => s.UserId == staff.UserId);

        // Assert
        Assert.True(result);
        Assert.Null(deletedStaff);
    }

    [Fact]
    public async Task ChangeStaffDepartmentAsync_ChangesStaffDepartment()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成2个部门
        var departments = BogusDataGenerator.GenerateDepartments(2);
        await context.Departments.AddRangeAsync(departments);
        
        // 使用Bogus生成1个员工，并关联到第一个部门
        var staff = BogusDataGenerator.StaffFaker.Generate();
        staff.Identity = "Department";
        staff.Department = departments[0];
        await context.Staffs.AddAsync(staff);
        await context.SaveChangesAsync();

        // Act
        var result = await _staffRepository.ChangeStaffDepartmentAsync(staff.UserId, departments[1].Name);
        
        // 创建新的context实例来查询更新后的结果
        await using var newContext = new ClubContext(_options);
        var updatedStaff = await newContext.Staffs.Include(s => s.Department).FirstOrDefaultAsync(s => s.UserId == staff.UserId);

        // Assert
        Assert.True(result);
        Assert.NotNull(updatedStaff);
        Assert.NotNull(updatedStaff.Department);
        Assert.Equal(departments[1].Name, updatedStaff.Department.Name);
    }

    [Fact]
    public async Task GetStaffsByIdentitiesAsync_ReturnsStaffsWithMatchingIdentities()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // 使用Bogus生成3个员工，分别设置不同身份
        var presidentStaff = BogusDataGenerator.StaffFaker.Generate();
        presidentStaff.Identity = "President";
        
        var ministerStaff = BogusDataGenerator.StaffFaker.Generate();
        ministerStaff.Identity = "Minister";
        
        var memberStaff = BogusDataGenerator.StaffFaker.Generate();
        memberStaff.Identity = "Member";
        
        var staffs = new List<StaffDO> { presidentStaff, ministerStaff, memberStaff };
        await context.Staffs.AddRangeAsync(staffs);
        await context.SaveChangesAsync();

        // Act
        var result = await _staffRepository.GetStaffsByIdentitiesAsync("President", "Minister");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.All(result, s => Assert.Contains(s.Identity, new[] { "President", "Minister" }));
    }

    [Fact]
    public async Task AssignRoleAsync_PromotesDepartmentMemberToMinister_KeepsDepartment()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var department = new DepartmentDO { Key = "tech", Name = "技术部", Description = "技术" };
        await context.Departments.AddAsync(department);
        await context.Staffs.AddAsync(new StaffDO
        {
            UserId = "0000000001", Name = "部员甲", Identity = "Department", Department = department
        });
        await context.SaveChangesAsync();

        // Act
        var (success, error) = await _staffRepository.AssignRoleAsync("0000000001", "Minister", "技术部");

        // Assert
        Assert.True(success, error);
        await using var verify = new ClubContext(_options);
        var staff = await verify.Staffs.Include(s => s.Department).FirstAsync(s => s.UserId == "0000000001");
        Assert.Equal("Minister", staff.Identity);
        Assert.Equal("技术部", staff.Department!.Name);
    }

    [Fact]
    public async Task AssignRoleAsync_PromotesToPresident_ClearsDepartment()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var department = new DepartmentDO { Key = "tech", Name = "技术部", Description = "技术" };
        await context.Departments.AddAsync(department);
        await context.Staffs.AddAsync(new StaffDO
        {
            UserId = "0000000002", Name = "部长乙", Identity = "Minister", Department = department
        });
        await context.SaveChangesAsync();

        // Act
        var (success, error) = await _staffRepository.AssignRoleAsync("0000000002", "President", null);

        // Assert
        Assert.True(success, error);
        await using var verify = new ClubContext(_options);
        var staff = await verify.Staffs.Include(s => s.Department).FirstAsync(s => s.UserId == "0000000002");
        Assert.Equal("President", staff.Identity);
        Assert.Null(staff.Department);
    }

    [Fact]
    public async Task AssignRoleAsync_RejectsFounder()
    {
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        await context.Staffs.AddAsync(new StaffDO { UserId = "0000000000", Name = "创始人", Identity = "Founder" });
        await context.SaveChangesAsync();

        var (success, error) = await _staffRepository.AssignRoleAsync("0000000000", "President", null);

        Assert.False(success);
        Assert.Contains("创始人", error);
    }

    [Fact]
    public async Task AssignRoleAsync_RejectsMinisterWithoutDepartment()
    {
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        await context.Staffs.AddAsync(new StaffDO { UserId = "0000000004", Name = "无部门", Identity = "Department" });
        await context.SaveChangesAsync();

        var (success, _) = await _staffRepository.AssignRoleAsync("0000000004", "Minister", null);

        Assert.False(success);
    }

    [Fact]
    public async Task GetAllStaffIdentity_ExcludesFounder()
    {
        // Arrange
        await using var context = new ClubContext(_options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var department = new DepartmentDO { Key = "tech", Name = "技术部", Description = "技术" };
        await context.Departments.AddAsync(department);
        await context.Staffs.AddRangeAsync(
            new StaffDO { UserId = "0000000001", Name = "创始人", Identity = "Founder" },
            new StaffDO { UserId = "0000000003", Name = "部员", Identity = "Department", Department = department });
        await context.Students.AddRangeAsync(
            new StudentDO { UserId = "0000000001", UserName = "创始人" },
            new StudentDO { UserId = "0000000003", UserName = "部员" });
        await context.SaveChangesAsync();

        // Act —— 汇总名单导出不应包含 Founder
        var result = (await _staffRepository.GetAllStaffIdentity()).ToList();

        // Assert
        Assert.DoesNotContain(result, m => m.UserId == "0000000001");
        Assert.Contains(result, m => m.UserId == "0000000003");
    }

    // ==== C23 / C27：社长不隶属任何部门，更新接口必须真正落库部门 ====

    [Fact]
    public async Task UpdateStaffAsync_WithoutDepartmentName_KeepsExistingDepartment()
    {
        // C27：缺省 DepartmentName 表示“不修改部门”。清空部门走独立的调部门接口，
        // 否则仅改姓名的调用方会静默把部门抹掉。
        var (repository, options) = CreateIsolatedRepository();
        await SeedStaffAsync(options, "Department", "技术部");

        var result = await repository.UpdateStaffAsync(new StaffDO
        {
            UserId = "0000000001",
            Name = "只改姓名",
            Identity = "Department"
        });

        Assert.True(result);
        await using var verify = new ClubContext(options);
        var saved = await verify.Staffs.Include(s => s.Department).SingleAsync(s => s.UserId == "0000000001");
        Assert.Equal("只改姓名", saved.Name);
        Assert.Equal("技术部", saved.Department!.Name);
    }

    /// <summary>
    /// 每条规则用独立的 InMemory 库，避免与既有用例互相污染。
    /// </summary>
    private static (StaffRepository Repository, DbContextOptions<ClubContext> Options) CreateIsolatedRepository()
    {
        var options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"StaffRepositoryRules-{Guid.NewGuid()}")
            .Options;
        return (new StaffRepository(new TestDbContextFactory(options)), options);
    }

    private static async Task SeedStaffAsync(DbContextOptions<ClubContext> options, string identity,
        string? departmentName)
    {
        await using var context = new ClubContext(options);
        var department = new DepartmentDO { Key = "tech", Name = "技术部", Description = "技术" };
        var other = new DepartmentDO { Key = "design", Name = "设计部", Description = "设计" };
        context.Departments.AddRange(department, other);
        context.Staffs.Add(new StaffDO
        {
            UserId = "0000000001",
            Name = "成员",
            Identity = identity,
            Department = departmentName == "技术部" ? department : departmentName == "设计部" ? other : null
        });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateRoleAsync_PromotesToPresident_ClearsDepartment()
    {
        // Arrange —— 部长身上带着“技术部”，Founder 权限接口把它提为社长时不能留下部门
        var (repository, options) = CreateIsolatedRepository();
        await SeedStaffAsync(options, "Minister", "技术部");

        // Act
        var (success, error) = await repository.UpdateRoleAsync("0000000001", "President", "技术部", "9999999999");

        // Assert
        Assert.True(success, error);
        await using var verify = new ClubContext(options);
        var saved = await verify.Staffs.Include(s => s.Department).SingleAsync(s => s.UserId == "0000000001");
        Assert.Equal("President", saved.Identity);
        Assert.Null(saved.Department);
    }

    [Fact]
    public async Task UpdateRoleAsync_DemotingLastFounder_IsStillRejected()
    {
        // 既有 Founder 保护不能被 C23 的改动削弱
        var (repository, options) = CreateIsolatedRepository();
        await SeedStaffAsync(options, "Founder", null);

        var (success, error) = await repository.UpdateRoleAsync("0000000001", "President", null, "9999999999");

        Assert.False(success);
        Assert.Contains("Founder", error);
        await using var verify = new ClubContext(options);
        Assert.Equal("Founder", (await verify.Staffs.SingleAsync(s => s.UserId == "0000000001")).Identity);
    }

    [Fact]
    public async Task AssignRoleAsync_PresidentWithDepartmentName_StillClearsDepartment()
    {
        // 调用方即使误传部门，社长的部门也必须为空
        var (repository, options) = CreateIsolatedRepository();
        await SeedStaffAsync(options, "Minister", "技术部");

        var (success, error) = await repository.AssignRoleAsync("0000000001", "President", "技术部");

        Assert.True(success, error);
        await using var verify = new ClubContext(options);
        var saved = await verify.Staffs.Include(s => s.Department).SingleAsync(s => s.UserId == "0000000001");
        Assert.Equal("President", saved.Identity);
        Assert.Null(saved.Department);
    }

    [Fact]
    public async Task UpdateStaffAsync_ChangesDepartmentByName()
    {
        // C27：Staff/Update 的 DepartmentName 不能再被静默忽略
        var (repository, options) = CreateIsolatedRepository();
        await SeedStaffAsync(options, "Minister", "技术部");

        var result = await repository.UpdateStaffAsync(new StaffDO
        {
            UserId = "0000000001", Name = "改名后的部长", Identity = "Minister",
            Department = new DepartmentDO { Name = "设计部" }
        });

        Assert.True(result);
        await using var verify = new ClubContext(options);
        var saved = await verify.Staffs.Include(s => s.Department).SingleAsync(s => s.UserId == "0000000001");
        Assert.Equal("改名后的部长", saved.Name);
        Assert.Equal("Minister", saved.Identity);
        Assert.Equal("设计部", saved.Department!.Name);
    }

    [Fact]
    public async Task UpdateStaffAsync_UnknownDepartment_ReturnsFalseAndKeepsExistingData()
    {
        var (repository, options) = CreateIsolatedRepository();
        await SeedStaffAsync(options, "Minister", "技术部");

        var result = await repository.UpdateStaffAsync(new StaffDO
        {
            UserId = "0000000001", Name = "不应该写入的名字", Identity = "Minister",
            Department = new DepartmentDO { Name = "不存在的部门" }
        });

        Assert.False(result);
        await using var verify = new ClubContext(options);
        var saved = await verify.Staffs.Include(s => s.Department).SingleAsync(s => s.UserId == "0000000001");
        Assert.Equal("成员", saved.Name);
        Assert.Equal("技术部", saved.Department!.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("技术部")]
    public async Task UpdateStaffAsync_PromotingToPresident_ClearsDepartment(string? departmentName)
    {
        // C23：无论请求带不带部门，社长落库后都不能有部门
        var (repository, options) = CreateIsolatedRepository();
        await SeedStaffAsync(options, "Minister", "技术部");

        var result = await repository.UpdateStaffAsync(new StaffDO
        {
            UserId = "0000000001", Name = "新社长", Identity = "President",
            Department = departmentName == null ? null : new DepartmentDO { Name = departmentName }
        });

        Assert.True(result);
        await using var verify = new ClubContext(options);
        var saved = await verify.Staffs.Include(s => s.Department).SingleAsync(s => s.UserId == "0000000001");
        Assert.Equal("President", saved.Identity);
        Assert.Null(saved.Department);
    }

    [Fact]
    public async Task UpdateStaffAsync_DemotingPresidentToMinister_AssignsDepartment()
    {
        // 反向人事调动同样要落库部门，否则社长回到部门时部门丢失
        var (repository, options) = CreateIsolatedRepository();
        await SeedStaffAsync(options, "President", null);

        var result = await repository.UpdateStaffAsync(new StaffDO
        {
            UserId = "0000000001", Name = "回到部门", Identity = "Minister",
            Department = new DepartmentDO { Name = "技术部" }
        });

        Assert.True(result);
        await using var verify = new ClubContext(options);
        var saved = await verify.Staffs.Include(s => s.Department).SingleAsync(s => s.UserId == "0000000001");
        Assert.Equal("Minister", saved.Identity);
        Assert.Equal("技术部", saved.Department!.Name);
    }

    [Fact]
    public async Task ChangeStaffDepartmentAsync_AssigningDepartmentToPresident_IsRejected()
    {
        // C23：社长是社团层面身份，独立调部门接口也不能给他挂部门
        var (repository, options) = CreateIsolatedRepository();
        await SeedStaffAsync(options, "President", "技术部");

        var result = await repository.ChangeStaffDepartmentAsync("0000000001", "设计部");

        Assert.False(result);
        await using var verify = new ClubContext(options);
        var saved = await verify.Staffs.Include(s => s.Department).SingleAsync(s => s.UserId == "0000000001");
        Assert.Equal("技术部", saved.Department!.Name);
    }

    [Fact]
    public async Task ChangeStaffDepartmentAsync_ClearingPresidentDepartment_RemainsAllowed()
    {
        // 清理历史脏数据（社长身上残留部门）必须仍然可用
        var (repository, options) = CreateIsolatedRepository();
        await SeedStaffAsync(options, "President", "技术部");

        var result = await repository.ChangeStaffDepartmentAsync("0000000001", null);

        Assert.True(result);
        await using var verify = new ClubContext(options);
        var saved = await verify.Staffs.Include(s => s.Department).SingleAsync(s => s.UserId == "0000000001");
        Assert.Null(saved.Department);
    }

    [Fact]
    public async Task OrdinaryWrites_CannotCreatePromoteModifyDeleteOrMoveFounder()
    {
        await using (var context = new ClubContext(_options))
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
            context.Departments.Add(new DepartmentDO { Key = "tech", Name = "技术部" });
            context.Staffs.AddRange(
                new StaffDO { UserId = "0000000000", Name = "创始人", Identity = "Founder" },
                new StaffDO { UserId = "0000000001", Name = "社长", Identity = "President" });
            await context.SaveChangesAsync();
        }

        Assert.False(await _staffRepository.CreateStaffAsync(new StaffDO
        {
            UserId = "0000000002", Name = "另一个创始人", Identity = "Founder"
        }));
        Assert.False(await _staffRepository.UpdateStaffAsync(new StaffDO
        {
            UserId = "0000000001", Name = "已提权", Identity = "Founder"
        }));
        Assert.False(await _staffRepository.UpdateStaffAsync(new StaffDO
        {
            UserId = "0000000000", Name = "已篡改", Identity = "President"
        }));
        Assert.False(await _staffRepository.DeleteStaffAsync("0000000000"));
        Assert.False(await _staffRepository.ChangeStaffDepartmentAsync("0000000000", "技术部"));

        await using var verify = new ClubContext(_options);
        var founder = await verify.Staffs.Include(s => s.Department).SingleAsync(s => s.UserId == "0000000000");
        Assert.Equal("Founder", founder.Identity);
        Assert.Equal("创始人", founder.Name);
        Assert.Null(founder.Department);
        Assert.Equal("President", (await verify.Staffs.SingleAsync(s => s.UserId == "0000000001")).Identity);
        Assert.False(await verify.Staffs.AnyAsync(s => s.UserId == "0000000002"));
    }
}

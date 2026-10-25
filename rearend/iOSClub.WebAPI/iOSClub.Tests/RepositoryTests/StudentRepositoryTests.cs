using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.DataApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.Tests.RepositoryTests;

public class StudentRepositoryTests
{
    private readonly DbContextOptions<ClubContext> _options;
    private readonly TestDbContextFactory _contextFactory;
    private readonly StudentRepository _studentRepository;

    public StudentRepositoryTests()
    {
        // 使用内存数据库进行测试
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase(databaseName: "StudentRepositoryTestDatabase")
            .Options;

        _contextFactory = new TestDbContextFactory(_options);
        _studentRepository = new StudentRepository(_contextFactory);
    }

    [Fact]
    public async Task GetAll_ReturnsAllStudents()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var students = BogusDataGenerator.StudentFaker.Generate(2);
        await context.Students.AddRangeAsync(students);
        await context.SaveChangesAsync();

        // Act
        var result = await _studentRepository.GetAll();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectStudent()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var student = BogusDataGenerator.StudentFaker.Generate();
        await context.Students.AddAsync(student);
        await context.SaveChangesAsync();

        // Act
        var result = await _studentRepository.GetByIdAsync(student.UserId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(student.UserName, result.UserName);
        Assert.Equal(student.PhoneNum, result.PhoneNum);
    }

    [Fact]
    public async Task Create_ReturnsCreatedStudent()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var student = BogusDataGenerator.StudentFaker.Generate();

        // Act
        var result = await _studentRepository.Create(student);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(student.UserId, result.UserId);
        Assert.Equal(student.UserName, result.UserName);
    }

    [Fact]
    public async Task UpdateProfileAsync_UpdatesExistingStudent()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var student = BogusDataGenerator.StudentFaker.Generate();
        await context.Students.AddAsync(student);
        await context.SaveChangesAsync();

        student.UserName = "Updated Name";
        student.PhoneNum = "13800138002";

        // Act
        var result = await _studentRepository.UpdateProfileAsync(student);
        var updatedStudent = await _studentRepository.GetByIdAsync(student.UserId);

        // Assert
        Assert.True(result);
        Assert.NotNull(updatedStudent);
        Assert.Equal("Updated Name", updatedStudent.UserName);
        Assert.Equal("13800138002", updatedStudent.PhoneNum);
    }

    [Fact]
    public async Task UpdateProfileAsync_DoesNotTouchPasswordHash()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange：建一条真实密码的记录
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var student = BogusDataGenerator.StudentFaker.Generate();
        student.PasswordHash = DataTool.StringToHash("MySecretPwd");
        await context.Students.AddAsync(student);
        await context.SaveChangesAsync();

        var hashBefore = student.PasswordHash;

        // Act：模拟"档案 DTO 转 DO"—— 对象里没有密码，PasswordHash 是默认空串。
        // 这正是 /User/profile 与 /MemberManagement/update 送到仓库层的形状。
        var profileOnly = new StudentDO
        {
            UserId = student.UserId,
            UserName = "只改了名字",
            PhoneNum = student.PhoneNum
        };
        var result = await _studentRepository.UpdateProfileAsync(profileOnly);

        // Assert：档案更新成功，且密码一个字节都没动
        var updatedStudent = await _studentRepository.GetByIdAsync(student.UserId);
        Assert.True(result);
        Assert.NotNull(updatedStudent);
        Assert.Equal("只改了名字", updatedStudent.UserName);
        Assert.Equal(hashBefore, updatedStudent.PasswordHash);
        Assert.True(DataTool.IsOk("MySecretPwd", updatedStudent.PasswordHash));
    }

    [Fact]
    public async Task SetPasswordAsync_RotatesPassword()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var student = BogusDataGenerator.StudentFaker.Generate();
        student.PasswordHash = DataTool.StringToHash("OldPwd");
        await context.Students.AddAsync(student);
        await context.SaveChangesAsync();

        // Act
        var result = await _studentRepository.SetPasswordAsync(student.UserId, "NewPwd");

        // Assert：新密码可用、旧密码失效
        var updatedStudent = await _studentRepository.GetByIdAsync(student.UserId);
        Assert.True(result);
        Assert.NotNull(updatedStudent);
        Assert.True(DataTool.IsOk("NewPwd", updatedStudent.PasswordHash));
        Assert.False(DataTool.IsOk("OldPwd", updatedStudent.PasswordHash));
        Assert.True(await _studentRepository.Login(student.UserId, "NewPwd"));
        Assert.False(await _studentRepository.Login(student.UserId, "OldPwd"));
    }

    [Fact]
    public async Task SetPasswordAsync_UnknownStudent_ReturnsFalse()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        Assert.False(await _studentRepository.SetPasswordAsync("9999999999", "NewPwd"));
    }

    [Fact]
    public async Task Login_EmptyPasswordHash_RejectsEvenMatchingPhoneNumber()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange：历史遗留数据，哈希为空但手机号存在
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var student = BogusDataGenerator.StudentFaker.Generate();
        student.PasswordHash = "";
        await context.Students.AddAsync(student);
        await context.SaveChangesAsync();

        // Act & Assert：以前这里会把手机号当密码放行，现在必须直接拒绝
        Assert.False(await _studentRepository.Login(student.UserId, student.PhoneNum));
        Assert.Null(await _studentRepository.LoginAndGetStudentAsync(student.UserId, student.PhoneNum));
    }

    [Fact]
    public async Task DeleteAsync_RemovesStudent()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var student = BogusDataGenerator.StudentFaker.Generate();
        await context.Students.AddAsync(student);
        await context.SaveChangesAsync();

        // Act
        var result = await _studentRepository.DeleteAsync(student.UserId);
        var deletedStudent = await _studentRepository.GetByIdAsync(student.UserId);

        // Assert
        Assert.True(result);
        Assert.Null(deletedStudent);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTrue()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var student = BogusDataGenerator.StudentFaker.Generate();
        var password = "password";
        student.PasswordHash = DataTool.StringToHash(password);
        await context.Students.AddAsync(student);
        await context.SaveChangesAsync();

        // Act
        var result = await _studentRepository.Login(student.UserId, password);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsFalse()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var student = BogusDataGenerator.StudentFaker.Generate();
        student.PasswordHash = DataTool.StringToHash("password");
        await context.Students.AddAsync(student);
        await context.SaveChangesAsync();

        // Act
        var result = await _studentRepository.Login(student.UserId, "wrongpassword");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateManyAsync_AddsNewStudents()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var students = BogusDataGenerator.StudentFaker.Generate(2);

        // Act
        var result = await _studentRepository.UpdateManyAsync(students);
        var allStudents = await _studentRepository.GetAll();

        // Assert
        Assert.True(result);
        Assert.Equal(2, allStudents.Count);
    }

    [Fact]
    public async Task GetMembersPagedAsync_ReturnsCorrectPage()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成5个学生
        var students = BogusDataGenerator.StudentFaker.Generate(5);
        await context.Students.AddRangeAsync(students);
        await context.SaveChangesAsync();

        // Act
        var (members, totalCount) = await _studentRepository.GetMembersPagedAsync(1, 2);

        // Assert
        Assert.NotNull(members);
        Assert.Equal(2, members.Count);
        Assert.Equal(5, totalCount);
    }

    [Fact]
    public async Task Search_ReturnsMatchingStudents()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成学生，并确保有两个学生的Academy是"Computer Science"
        var students = new List<StudentDO>
        {
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.Academy, "Computer Science")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.Academy, "Mathematics")
                .Generate(),
            BogusDataGenerator.StudentFaker.Clone()
                .RuleFor(s => s.Academy, "Computer Science")
                .Generate()
        };
        await context.Students.AddRangeAsync(students);
        await context.SaveChangesAsync();

        // Act
        var result = await _studentRepository.Search("Computer Science", "academy");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, student => Assert.Equal("Computer Science", student.Academy));
    }

    [Fact]
    public async Task GetAllMembersAsync_ReturnsAllMembersWithCorrectIdentity()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用Bogus生成学生
        var student = BogusDataGenerator.StudentFaker.Generate();
        await context.Students.AddAsync(student);

        // 使用Bogus生成员工
        var staff = BogusDataGenerator.StaffFaker.Clone()
            .RuleFor(s => s.UserId, student.UserId)
            .RuleFor(s => s.Identity, "President")
            .Generate();
        await context.Staffs.AddAsync(staff);

        await context.SaveChangesAsync();

        // Act
        var result = await _studentRepository.GetAllMembersAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("President", result[0].Identity);
    }
}

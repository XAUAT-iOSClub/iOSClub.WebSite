using iOSClub.Data;
using iOSClub.DataApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.Tests.RepositoryTests;

public class StudentRepositoryBogusTests
{
    private readonly DbContextOptions<ClubContext> _options;
    private readonly TestDbContextFactory _contextFactory;
    private readonly StudentRepository _studentRepository;

    public StudentRepositoryBogusTests()
    {
        // 使用内存数据库进行测试
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase(databaseName: "StudentRepositoryBogusTestDatabase")
            .Options;

        _contextFactory = new TestDbContextFactory(_options);
        _studentRepository = new StudentRepository(_contextFactory);
    }

    [Fact]
    public async Task GetAll_ReturnsAllStudents_WithBogusData()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用集中式Bogus生成器生成10个学生
        var students = BogusDataGenerator.StudentFaker.Generate(10);
        await context.Students.AddRangeAsync(students);
        await context.SaveChangesAsync();

        // Act
        var result = await _studentRepository.GetAll();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Count);
    }

    [Fact]
    public async Task GetMembersPagedAsync_ReturnsCorrectPage_WithBogusData()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用集中式Bogus生成器生成20个学生
        var students = BogusDataGenerator.StudentFaker.Generate(20);
        await context.Students.AddRangeAsync(students);
        await context.SaveChangesAsync();

        // Act
        var (members, totalCount) = await _studentRepository.GetMembersPagedAsync(1, 5);

        // Assert
        Assert.NotNull(members);
        Assert.Equal(5, members.Count);
        Assert.Equal(20, totalCount);
    }

    [Fact]
    public async Task Search_ReturnsMatchingStudents_WithBogusData()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用集中式Bogus生成器生成10个学生
        var students = BogusDataGenerator.StudentFaker.Generate(10);
        
        // 确保有一些学生来自计算机学院
        foreach (var student in students.Take(5))
        {
            student.Academy = "计算机学院";
        }
        
        await context.Students.AddRangeAsync(students);
        await context.SaveChangesAsync();

        // Act
        var result = await _studentRepository.Search("计算机学院", "academy");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 5); // 至少有5个学生来自计算机学院
        Assert.All(result, student => Assert.Equal("计算机学院", student.Academy));
    }

    [Fact]
    public async Task UpdateManyAsync_AddsNewStudents_WithBogusData()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用集中式Bogus生成器生成15个学生，BogusDataGenerator已确保UserId唯一
        var students = BogusDataGenerator.StudentFaker.Generate(15);

        // Act
        var result = await _studentRepository.UpdateManyAsync(students);
        var allStudents = await _studentRepository.GetAll();

        // Assert：返回值改为逐行结果，15 名新学生都计入新增
        Assert.Equal(15, result.Created);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(15, allStudents.Count);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTrue_WithBogusData()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Arrange
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 使用集中式Bogus生成器生成一个学生
        var student = BogusDataGenerator.StudentFaker.Generate();
        var password = "password123";
        student.PasswordHash = DataTool.StringToHash(password);
        
        await context.Students.AddAsync(student);
        await context.SaveChangesAsync();

        // Act
        var result = await _studentRepository.Login(student.UserId, password);

        // Assert
        Assert.True(result);
    }
}

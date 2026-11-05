using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.Tests.RepositoryTests;

/// <summary>
/// C28 回归测试：批量更新必须逐行给出新增、更新、跳过和失败的结果。
/// <para>
/// 回归背景：旧实现先用"学号非空 + 手机号格式正确"过滤掉不合格行，再只返回一个 bool。
/// 当整批行都被过滤掉时既不写库也不报错，接口就返回"批量更新学生成功"，
/// 调用方无法知道有多少行真的写入、哪些行被丢弃以及丢弃原因。
/// 这里锁定：空输入返回空结果且不写库、逐行分类正确、全部不合格时跳过数真实可见。
/// </para>
/// </summary>
public class BulkUpdateResultTests
{
    private readonly DbContextOptions<ClubContext> _options;
    private readonly TestDbContextFactory _contextFactory;
    private readonly StudentRepository _studentRepository;

    public BulkUpdateResultTests()
    {
        // 每个测试实例使用独立内存库，避免与其他测试类共享固定库名。
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase(databaseName: $"BulkUpdateResult-{Guid.NewGuid()}")
            .Options;

        _contextFactory = new TestDbContextFactory(_options);
        _studentRepository = new StudentRepository(_contextFactory);
    }

    private static StudentDO ValidStudent(string userId, string phoneNum = "13800138000", string userName = "测试成员")
    {
        return new StudentDO
        {
            UserId = userId,
            UserName = userName,
            Academy = "计算机与信息工程学院",
            PoliticalLandscape = "群众",
            Gender = "男",
            ClassName = "计科2301",
            PhoneNum = phoneNum
        };
    }

    [Fact]
    public async Task UpdateManyAsync_空列表_返回空结果且不写库()
    {
        // Act
        var result = await _studentRepository.UpdateManyAsync([]);
        var allStudents = await _studentRepository.GetAll();

        // Assert：没有任何输入行，也就没有可报告的结果，且不得产生写入。
        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.Issues);
        Assert.Empty(allStudents);
    }

    [Fact]
    public async Task UpdateManyAsync_新学生_计入新增并写入默认密码()
    {
        // Arrange：新学号，手机号合法，密码为空，仓库层应按手机号生成默认密码。
        var student = ValidStudent("2024000001", "13800138000");

        // Act
        var result = await _studentRepository.UpdateManyAsync([student]);
        var allStudents = await _studentRepository.GetAll();

        // Assert
        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.Issues);
        Assert.Single(allStudents);
        Assert.True(await _studentRepository.Login("2024000001", "13800138000"));
    }

    [Fact]
    public async Task UpdateManyAsync_已存在学生_计入更新且不覆盖密码()
    {
        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var existing = ValidStudent("2024000002", "13800138002", "原姓名");
            existing.PasswordHash = DataTool.StringToHash("OldPass123");
            context.Students.Add(existing);
            await context.SaveChangesAsync();
        }

        // Act：只改姓名，密码保持原值。
        var result = await _studentRepository.UpdateManyAsync(
            [ValidStudent("2024000002", "13800138002", "新姓名")]);

        // Assert
        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);
        var updated = await _studentRepository.GetByIdAsync("2024000002");
        Assert.NotNull(updated);
        Assert.Equal("新姓名", updated.UserName);
        Assert.True(await _studentRepository.Login("2024000002", "OldPass123"));
    }

    [Fact]
    public async Task UpdateManyAsync_学号为空_跳过并给出原因()
    {
        // Arrange：学号为空的行在写库前就被过滤，应单独报告而不是混进更新结果。
        var invalid = ValidStudent("", "13800138003");
        var valid = ValidStudent("2024000003", "13800138004");

        // Act
        var result = await _studentRepository.UpdateManyAsync([invalid, valid]);
        var allStudents = await _studentRepository.GetAll();

        // Assert
        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Failed);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(BulkUpdateIssueVO.KindSkipped, issue.Kind);
        Assert.Equal("", issue.UserId);
        Assert.Contains("学号", issue.Reason);
        Assert.Single(allStudents);
    }

    [Fact]
    public async Task UpdateManyAsync_手机号不合法_跳过并给出原因()
    {
        var invalid = ValidStudent("2024000005", "12345");

        var result = await _studentRepository.UpdateManyAsync([invalid]);
        var allStudents = await _studentRepository.GetAll();

        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Failed);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(BulkUpdateIssueVO.KindSkipped, issue.Kind);
        Assert.Equal("2024000005", issue.UserId);
        Assert.Contains("手机号", issue.Reason);
        Assert.Empty(allStudents);
    }

    [Fact]
    public async Task UpdateManyAsync_全部不合格_零写入时也返回逐行跳过原因()
    {
        // Arrange：这是旧实现最容易误报成功的场景——所有行都被过滤，库没有任何变化。
        var invalidRows = new List<StudentDO>
        {
            ValidStudent("", "13800138006"),
            ValidStudent("2024000006", "not-a-phone"),
            ValidStudent("2024000007", "")
        };

        // Act
        var result = await _studentRepository.UpdateManyAsync(invalidRows);
        var allStudents = await _studentRepository.GetAll();

        // Assert：零新增、零更新、三次跳过，每条原因都能对应到输入行。
        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(3, result.Skipped);
        Assert.Equal(0, result.Failed);
        Assert.Equal(3, result.Issues.Count);
        Assert.All(result.Issues, issue => Assert.Equal(BulkUpdateIssueVO.KindSkipped, issue.Kind));
        Assert.Contains(result.Issues, issue => issue.UserId == "2024000006" && issue.Reason.Contains("手机号"));
        Assert.Empty(allStudents);
    }

    [Fact]
    public async Task UpdateManyAsync_混合行_分别统计新增更新与跳过()
    {
        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var existing = ValidStudent("2024000009", "13800138009", "原姓名");
            existing.PasswordHash = DataTool.StringToHash("OldPass123");
            context.Students.Add(existing);
            await context.SaveChangesAsync();
        }

        var rows = new List<StudentDO>
        {
            ValidStudent("2024000008", "13800138008", "新增成员"),
            ValidStudent("2024000009", "13800138009", "更新成员"),
            ValidStudent("2024000010", "", "跳过成员")
        };

        // Act
        var result = await _studentRepository.UpdateManyAsync(rows);

        // Assert
        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Failed);
        Assert.Equal(2, (await _studentRepository.GetAll()).Count);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("2024000010", issue.UserId);
        Assert.Equal(BulkUpdateIssueVO.KindSkipped, issue.Kind);
        Assert.Contains("手机号", issue.Reason);
    }
}

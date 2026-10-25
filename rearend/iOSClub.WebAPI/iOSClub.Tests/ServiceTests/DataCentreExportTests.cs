using System.Text;
using System.Text.Json;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace iOSClub.Tests.ServiceTests;

/// <summary>
/// C12：导出 JSON 必须覆盖导入结构已经支持的实体。此前只写 Students / Departments /
/// Presidents / Resources，文章、分类和普通部门部员都不在文件里，无法在空库恢复。
/// </summary>
public class DataCentreExportTests
{
    private readonly DbContextOptions<ClubContext> _options;
    private readonly DataCentreController _controller;

    public DataCentreExportTests()
    {
        _options = new DbContextOptionsBuilder<ClubContext>()
            .UseInMemoryDatabase($"DataCentreExport-{Guid.NewGuid()}")
            .Options;
        var factory = new TestDbContextFactory(_options);
        _controller = new DataCentreController(factory, new DataCentreService(factory),
            NullLogger<DataCentreController>.Instance);
    }

    [Fact]
    public async Task ExportJson_IncludesStaffDepartmentCategoriesAndArticles()
    {
        await using (var context = new ClubContext(_options))
        {
            await context.Database.EnsureCreatedAsync();
            var department = new DepartmentDO { Name = "技术部", Key = "tech" };
            context.Departments.Add(department);
            await context.SaveChangesAsync();
            context.Staffs.Add(new StaffDO
            {
                UserId = "20210001", Name = "部员甲", Identity = "Department",
                Department = department
            });
            context.Categories.Add(new CategoryDO { Id = "cat-1", Name = "公告", Order = 1 });
            context.Articles.Add(new ArticleDO
            {
                Path = "hello", Title = "你好", Content = "正文", CategoryId = "cat-1",
                Identity = "Member", ArticleOrder = 3
            });
            await context.SaveChangesAsync();
        }

        var result = await _controller.ExportJson();

        var file = Assert.IsType<FileContentResult>(result);
        var json = Encoding.UTF8.GetString(file.FileContents);
        var data = JsonSerializer.Deserialize<AllDataImportDTO>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(data);
        Assert.Contains(data.Staffs, staff => staff.UserId == "20210001" && staff.Identity == "Department");
        Assert.Contains(data.Staffs, staff => staff.Department?.Name == "技术部");
        Assert.Contains(data.Categories, category => category.Id == "cat-1");
        Assert.Contains(data.Articles, article => article.Path == "hello" && article.CategoryId == "cat-1");
    }
}

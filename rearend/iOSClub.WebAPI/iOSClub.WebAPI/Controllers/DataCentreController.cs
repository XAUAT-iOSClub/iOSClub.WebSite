using System.Text;
using iOSClub.Data;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using Microsoft.AspNetCore.Authorization;

namespace iOSClub.WebAPI.Controllers;

[ApiController]
[Authorize(Roles = "Founder, President, Minister")]
[Route("[controller]")] // 使用C#推荐的API路径格式
public class DataCentreController(
    IDbContextFactory<ClubContext> dbContextFactory,
    IDataCentreService dataCentreService,
    ILogger<DataCentreController> logger)
    : ControllerBase
{
    [HttpGet("year")]
    public async Task<ActionResult<ApiResponse<List<YearCountVO>>>> GetYearData()
    {
        var yearData = await dataCentreService.GetYearDataAsync();
        return Ok(ApiResponse<List<YearCountVO>>.Success(yearData));
    }

    [HttpGet("college")]
    public async Task<ActionResult<ApiResponse<List<AcademyCountVO>>>> GetCollegeData()
    {
        var collegeData = await dataCentreService.GetCollegeDataAsync();
        return Ok(ApiResponse<List<AcademyCountVO>>.Success(collegeData));
    }

    [HttpGet("grade")]
    public async Task<ActionResult<ApiResponse<List<GradeCountVO>>>> GetGradeData()
    {
        var gradeData = await dataCentreService.GetGradeDataAsync();
        return Ok(ApiResponse<List<GradeCountVO>>.Success(gradeData));
    }

    [HttpGet("landscape")]
    public async Task<ActionResult<ApiResponse<List<LandscapeCountVO>>>> GetLandscapeData()
    {
        var landscapeData = await dataCentreService.GetLandscapeDataAsync();
        return Ok(ApiResponse<List<LandscapeCountVO>>.Success(landscapeData));
    }

    [HttpGet("gender")]
    public async Task<ActionResult<ApiResponse<List<GenderCountVO>>>> GetGenderData()
    {
        var genderData = await dataCentreService.GetGenderDataAsync();
        return Ok(ApiResponse<List<GenderCountVO>>.Success(genderData));
    }


    [HttpPost("update-from-json")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateDataFromJson(IFormFile? file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return Ok(ApiResponse<object>.Fail(ErrorCode.ParameterEmpty, "文件为空"));
            }

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(ApiResponse<object>.Fail(ErrorCode.ParameterFormatError, "请上传JSON文件"));
            }

            // 读取文件内容
            using var reader = new StreamReader(file.OpenReadStream());
            var jsonContent = await reader.ReadToEndAsync();

            // 反序列化JSON到AllDataImportDTO
            var allData = JsonSerializer.Deserialize<AllDataImportDTO>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (allData == null)
            {
                return Ok(ApiResponse<object>.Fail(ErrorCode.ParameterFormatError, "JSON文件格式不正确"));
            }

            // 处理学生数据，与现有方法保持一致
            foreach (var student in allData.Students)
            {
                student.JoinTime =
                    DateTime.SpecifyKind(student.JoinTime.Year == 0 ? DateTime.Parse("2023-10-18") : student.JoinTime,
                        DateTimeKind.Utc);
            }

            foreach (var article in allData.Articles.Where(article =>
                         article.LastWriteTime.Kind == DateTimeKind.Unspecified))
            {
                article.LastWriteTime = DateTime.SpecifyKind(article.LastWriteTime, DateTimeKind.Utc);
            }

            // 按主键合并导入：库中没有的插入，已存在的就地更新
            var result = await dataCentreService.ImportAllDataAsync(allData);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("数据导入完成：{Result}", result);
            }

            return Ok(ApiResponse<object>.Success(result, $"数据更新成功：{result}"));
        }
        catch (JsonException ex)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(ex, "JSON解析错误: {Message}", ex.Message);
            }

            return Ok(ApiResponse<object>.Fail(ErrorCode.ParameterFormatError, $"JSON解析错误: {ex.Message}"));
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(ex, "数据更新失败");
            }

            return Ok(ApiResponse<object>.Fail(ErrorCode.InternalServerError, "数据更新失败"));
        }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> GetCentreData()
    {
        await using var context = await dbContextFactory.CreateDbContextAsync();
        var data = new
        {
            Members = await context.Students.CountAsync(),
            Departments = await context.Departments.CountAsync(),
            Staffs = await context.Staffs.Where(staff => staff.Identity != "Founder").CountAsync(),
            Resources = await context.Resources.CountAsync(),
        };
        return Ok(ApiResponse<object>.Success(data));
    }

    [HttpGet("export-json")]
    public async Task<IActionResult> ExportJson()
    {
        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();
            var allData = new AllDataImportDTO
            {
                Students = await context.Students.ToListAsync(),
                Departments = await context.Departments.ToListAsync(),
                Presidents = await context.Staffs.Where(staff => staff.Identity == "President").ToListAsync(),
                Resources = await context.Resources.ToListAsync(),
            };

            var options = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
                WriteIndented = true
            };

            return File(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(allData, options)), "application/json",
                "all-data.json");
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(ex, "导出数据失败");
            }

            // 对于文件下载，保持原有的IActionResult返回类型
            return StatusCode(500, "导出数据失败");
        }
    }
}

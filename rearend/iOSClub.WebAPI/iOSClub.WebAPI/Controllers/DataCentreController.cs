using System.Text;
using iOSClub.Data;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.WebAPI.Common.Security;
using iOSClub.WebAPI.IdentityModels;
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

            // 全量导入会直接写 Staffs.Identity 和 Students.PasswordHash，等同于批量发角色/发密码。
            // 必须先按调用者职级校验，否则任何 Minister 都能导入一份含 Founder 的文件。
            var actor = HttpContext.User.GetUser();
            if (actor == null)
            {
                return Ok(ApiResponse<object>.Fail(ErrorCode.Unauthorized, "用户未认证"));
            }

            var authorizationError = await ValidateImportAuthorizationAsync(allData, actor.Identity);
            if (authorizationError != null)
            {
                return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, authorizationError));
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

    /// <summary>
    /// 校验全量导入是否越权。
    /// <para>
    /// 导入文件可以携带任意 <c>StaffDO.Identity</c> 和 <c>StudentDO.PasswordHash</c>，
    /// 因此必须满足两条：
    /// 1. 不能导入高于调用者职级的员工身份（尤其不能新建 Founder）；
    /// 2. 不能为高于调用者职级的现有员工补建 Student 档案（那等于替对方设置登录密码）。
    /// </para>
    /// </summary>
    private async Task<string?> ValidateImportAuthorizationAsync(AllDataImportDTO data, string actorRole)
    {
        var incomingStaff = data.Staffs.Concat(data.Presidents).ToList();

        // 1) 不能授予高于自己的身份（尤其不能新建 Founder）。
        var unauthorizedRole = incomingStaff
            .FirstOrDefault(s => !RoleHierarchy.CanAssign(actorRole, s.Identity));
        if (unauthorizedRole != null)
        {
            return $"导入文件包含无权设置的身份 {unauthorizedRole.Identity}（学号 {unauthorizedRole.UserId}）";
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();
        var existingStudentIds = await context.Students.Select(s => s.UserId).ToHashSetAsync();
        var staffRoles = await context.Staffs.ToDictionaryAsync(s => s.UserId, s => s.Identity);

        // 2) 修改既有员工的身份属于权限变更，要求严格高于其当前职级；
        //    否则 Minister 可以在导入文件里把 President 降成 Department。
        var unauthorizedChange = incomingStaff
            .Where(s => staffRoles.TryGetValue(s.UserId, out var current) &&
                        !string.Equals(current, s.Identity, StringComparison.Ordinal) &&
                        !RoleHierarchy.CanTakeOver(actorRole, current))
            .Select(s => s.UserId)
            .FirstOrDefault();
        if (unauthorizedChange != null)
        {
            return $"导入文件包含无权调整身份的现有员工（学号 {unauthorizedChange}）";
        }

        // 3) 不能为高于调用者职级的现有员工补建 Student 档案（那等于替对方设置登录密码）。
        var unauthorizedStudent = data.Students
            .Where(s => !existingStudentIds.Contains(s.UserId))
            .FirstOrDefault(s => staffRoles.TryGetValue(s.UserId, out var role) &&
                                 !RoleHierarchy.CanTakeOver(actorRole, role));

        return unauthorizedStudent == null
            ? null
            : $"导入文件包含无权为其创建登录档案的成员（学号 {unauthorizedStudent.UserId}）";
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
            // 部员与部门的关系保存在 StaffDO.Department 导航上；不 Include 就导出成无部门。
            var staffs = await context.Staffs.Include(staff => staff.Department).ToListAsync();
            // 只保留部门标识信息，避免把部门对象下的完整部员列表再级联导出一次。
            foreach (var staff in staffs) staff.OutputWhenOtherList();

            // 文章和分类互为导航属性，EF 会同时补齐两侧；导出前断开，
            // 否则序列化会沿 Category.Articles.Category… 形成对象环。
            var articles = await context.Articles.ToListAsync();
            foreach (var article in articles) article.Category = null;

            var categories = await context.Categories.ToListAsync();
            foreach (var category in categories) category.Articles.Clear();

            var departments = await context.Departments.ToListAsync();
            foreach (var department in departments) department.Staffs.Clear();

            var allData = new AllDataImportDTO
            {
                Students = await context.Students.ToListAsync(),
                Departments = departments,
                Staffs = staffs,
                Categories = categories,
                Articles = articles,
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

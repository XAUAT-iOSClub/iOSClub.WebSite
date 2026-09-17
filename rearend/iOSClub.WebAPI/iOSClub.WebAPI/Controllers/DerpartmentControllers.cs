using Mapster;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.IdentityModels;
using iOSClub.WebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace iOSClub.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")] // 使用C#推荐的API路径格式
public class DepartmentController(
    IDepartmentRepository departmentRepository,
    IStaffRepository staffRepository,
    IDepartmentImportService departmentImportService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DepartmentController> logger)
    : ControllerBase
{
    /// <summary>
    /// 获取部门信息
    /// </summary>
    [HttpGet("{name?}")]
    public async Task<ActionResult<ApiResponse<DepartmentVO>>> GetDepartment(string? name)
    {
        var userJwt = httpContextAccessor.HttpContext?.User.GetUser();
        if (userJwt == null)
            return Ok(ApiResponse<DepartmentVO>.Fail(ErrorCode.Unauthorized, "用户未认证"));

        // 检查用户权限
        if (!IsAuthorizedUser(userJwt.Identity))
            return Ok(ApiResponse<DepartmentVO>.Fail(ErrorCode.InsufficientPermission, "权限不足"));

        var staff = await staffRepository.GetStaffByIdAsync(userJwt.UserId);
        if (staff == null) return Ok(ApiResponse<DepartmentVO>.Fail(ErrorCode.Unauthorized, "用户未找到或权限不足"));
        // 如果没有指定部门名称，返回用户所在部门
        if (string.IsNullOrEmpty(name))
        {
            var userDepartment = await departmentRepository.GetDepartmentByNameAsync(staff.Department?.Name ?? "");
            if (userDepartment == null)
                return Ok(ApiResponse<DepartmentVO>.Fail(ErrorCode.ResourceNotFound, "用户未分配部门"));

            // 普通用户只能查看自己部门
            if (!IsAdminUser(userJwt.Identity) && userDepartment.Name != staff.Department?.Name)
                return Ok(ApiResponse<DepartmentVO>.Fail(ErrorCode.InsufficientPermission, "只能查看自己部门的信息"));

            return Ok(ApiResponse<DepartmentVO>.Success(userDepartment.Adapt<DepartmentVO>()));
        }

        // 管理员可以查看任何部门
        if (IsAdminUser(userJwt.Identity))
        {
            var department = await departmentRepository.GetDepartmentByNameAsync(name);

            if (department == null)
                return Ok(ApiResponse<DepartmentVO>.Fail(ErrorCode.ResourceNotFound, "部门不存在"));

            return Ok(ApiResponse<DepartmentVO>.Success(department.Adapt<DepartmentVO>()));
        }

        // 普通用户只能查看自己部门
        if (staff.Department == null || staff.Department.Name != name)
            return Ok(ApiResponse<DepartmentVO>.Fail(ErrorCode.InsufficientPermission, "只能查看自己部门的信息"));

        var userDept = await departmentRepository.GetDepartmentByNameAsync(name);
        if (userDept == null)
        {
            return NotFound(ApiResponse<DepartmentVO>.Fail(ErrorCode.ResourceNotFound, "部门不存在"));
        }

        return Ok(ApiResponse<DepartmentVO>.Success(userDept.Adapt<DepartmentVO>()));
    }

    /// <summary>
    /// 获取所有部门（仅管理员）
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = "Founder,President,Minister")]
    public async Task<ActionResult<ApiResponse<List<DepartmentVO>>>> GetAllDepartments()
    {
        var departments = await departmentRepository.GetAllDepartmentsAsync();
        return Ok(ApiResponse<List<DepartmentVO>>.Success(departments.Adapt<List<DepartmentVO>>()));
    }

    /// <summary>
    /// 更新部门信息
    /// </summary>
    [HttpPost("Update")]
    [Authorize(Roles = "Founder,President,Minister")]
    public async Task<ActionResult<ApiResponse<string>>> UpdateDepartment([FromBody] DepartmentCreateUpdateDTO model)
    {
        var department = await departmentRepository.GetDepartmentByNameAsync(model.Name);

        if (department == null)
            return Ok(ApiResponse<string>.Fail(ErrorCode.ResourceNotFound, "部门不存在"));

        // 更新可修改的字段
        department.Description = model.Description;
        department.Key = model.Key; // 如果需要更新Key

        var result = await departmentRepository.UpdateDepartmentAsync(department);
        if (!result)
            return Ok(ApiResponse<string>.Fail(ErrorCode.OperationFailed, "更新失败"));

        return Ok(ApiResponse<string>.Success("部门信息更新成功"));
    }

    /// <summary>
    /// 创建新部门
    /// </summary>
    [HttpPost("Create")]
    [Authorize(Roles = "Founder,President")]
    public async Task<ActionResult<ApiResponse<DepartmentVO>>> CreateDepartment([FromBody] DepartmentCreateUpdateDTO model)
    {
        // 检查部门是否已存在
        var existingDept = await departmentRepository.GetDepartmentByNameAsync(model.Name);
        if (existingDept != null)
            return Ok(ApiResponse<DepartmentVO>.Fail(ErrorCode.ResourceAlreadyExists, "部门已存在"));

        var entity = model.Adapt<DepartmentDO>();
        var result = await departmentRepository.AddDepartmentAsync(entity);
        if (!result)
            return Ok(ApiResponse<DepartmentVO>.Fail(ErrorCode.OperationFailed, "创建失败"));

        return CreatedAtAction(nameof(GetDepartment), new { name = model.Name },
            ApiResponse<DepartmentVO>.Success(entity.Adapt<DepartmentVO>(), "部门创建成功"));
    }

    /// <summary>
    /// 删除部门
    /// </summary>
    [HttpGet("Delete/{name}")]
    [Authorize(Roles = "Founder,President")]
    public async Task<ActionResult<ApiResponse<string>>> DeleteDepartment(string name)
    {
        var department = await departmentRepository.GetDepartmentByNameAsync(name);

        if (department == null)
            return Ok(ApiResponse<string>.Fail(ErrorCode.ResourceNotFound, "部门不存在"));

        // 包含成员的部门不能直接删除
        if (department.Staffs.Count != 0)
            return Ok(ApiResponse<string>.Fail(ErrorCode.InvalidStatusForOperation, "无法删除包含成员的部门"));

        var result = await departmentRepository.DeleteDepartmentAsync(name);
        if (!result)
            return Ok(ApiResponse<string>.Fail(ErrorCode.OperationFailed, "删除失败"));
        return Ok(ApiResponse<string>.Success("部门删除成功"));
    }

    /// <summary>
    /// 导出所有部员数据为JSON文件
    /// </summary>
    [Authorize(Roles = "Founder,President,Minister")]
    [HttpGet("export-json")]
    public async Task<IActionResult> ExportJson()
    {
        try
        {
            var members = await staffRepository.GetAllStaffIdentity();

            var jsonOptions = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(members, jsonOptions);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            return File(bytes, "application/json", "members.json");
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(ex, "导出部员数据失败");
            }

            return StatusCode(500, $"导出失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 导入并覆盖指定部门的成员名单（导入前自动备份原名单）
    /// </summary>
    [HttpPost("{name}/import")]
    [Authorize(Roles = "Founder,President,Minister")]
    public async Task<ActionResult<ApiResponse<DepartmentImportResultVO>>> ImportDepartmentRoster(
        string name, [FromBody] DepartmentImportDTO model)
    {
        var userJwt = httpContextAccessor.HttpContext?.User.GetUser();
        if (userJwt == null)
            return Ok(ApiResponse<DepartmentImportResultVO>.Fail(ErrorCode.Unauthorized, "用户未认证"));

        if (!IsAdminUser(userJwt.Identity))
            return Ok(ApiResponse<DepartmentImportResultVO>.Fail(ErrorCode.InsufficientPermission, "权限不足"));

        var operatorStaff = await staffRepository.GetStaffByIdAsync(userJwt.UserId);
        var result = await departmentImportService.ImportRosterAsync(name, model, userJwt.UserId,
            operatorStaff?.Name ?? "");
        return Ok(ApiResponse<DepartmentImportResultVO>.Success(result, "导入成功"));
    }

    /// <summary>
    /// 获取指定部门的导入历史
    /// </summary>
    [HttpGet("{name}/import-history")]
    [Authorize(Roles = "Founder,President,Minister")]
    public async Task<ActionResult<ApiResponse<List<ImportHistoryVO>>>> GetImportHistory(string name)
    {
        var history = await departmentImportService.GetHistoryAsync(name);
        return Ok(ApiResponse<List<ImportHistoryVO>>.Success(history, "获取导入历史成功"));
    }

    /// <summary>
    /// 下载某次导入前的名单备份
    /// </summary>
    [HttpGet("import-history/{id}/backup")]
    [Authorize(Roles = "Founder,President,Minister")]
    public async Task<IActionResult> DownloadImportBackup(string id)
    {
        var record = await departmentImportService.GetHistoryByIdAsync(id);
        if (record == null)
            return Ok(ApiResponse<string>.Fail(ErrorCode.ResourceNotFound, "导入记录不存在"));

        var bytes = Encoding.UTF8.GetBytes(record.BackupJson);
        var fileName = $"{record.DepartmentName}-backup-{record.ImportedAt:yyyyMMddHHmmss}.json";
        return File(bytes, "application/json", fileName);
    }

    /// <summary>
    /// 检查用户是否有权限访问部门信息
    /// </summary>
    /// <param name="identity">用户身份</param>
    /// <returns>如果用户有权限则返回true，否则返回false</returns>
    private static bool IsAuthorizedUser(string? identity)
    {
        return identity is "Founder" or "President" or "Minister" or "Department";
    }

    /// <summary>
    /// 检查用户是否是管理员（创始人、社长或部长）
    /// </summary>
    /// <param name="identity">用户身份</param>
    /// <returns>如果用户是管理员则返回true，否则返回false</returns>
    private static bool IsAdminUser(string? identity)
    {
        return identity is "Founder" or "President" or "Minister";
    }
}

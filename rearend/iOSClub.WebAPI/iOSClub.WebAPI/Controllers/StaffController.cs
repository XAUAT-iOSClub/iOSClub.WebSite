using Mapster;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.Common.Security;
using iOSClub.WebAPI.IdentityModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace iOSClub.WebAPI.Controllers;

[Authorize(Roles = "Founder, President, Minister")]
[ApiController]
[Route("[controller]")] // 使用C#推荐的API路径格式
public class StaffController(
    IStaffRepository staffRepository,
    IStudentRepository studentRepository,
    ILogger<StaffController> logger)
    : ControllerBase
{
    /// <summary>
    /// 获取所有员工列表
    /// </summary>
    /// <returns>员工列表</returns>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<StaffVO>>>> GetAllStaff()
    {
        var staffs = await staffRepository.GetAllStaffAsync();
        var staffModels = staffs as StaffDO[] ?? staffs.ToArray();
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("获取所有员工列表成功，员工数量: {Count}", staffModels.Length);
        }

        return Ok(ApiResponse<IEnumerable<StaffVO>>.Success(staffModels.Adapt<List<StaffVO>>(), "获取所有员工列表成功"));
    }

    [HttpGet("members")]
    public async Task<ActionResult<ApiResponse<IEnumerable<MemberVO>>>> GetAllStaffToMembers()
    {
        var staffs = await staffRepository.GetAllStaffToMembers();
        var memberModels = staffs as MemberVO[] ?? staffs.ToArray();
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("获取所有成员列表成功，成员数量: {Count}", memberModels.Length);
        }

        return Ok(ApiResponse<IEnumerable<MemberVO>>.Success(memberModels, "获取所有成员列表成功"));
    }

    /// <summary>
    /// 根据用户ID获取员工信息
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>员工信息</returns>
    [HttpGet("{userId}")]
    public async Task<ActionResult<ApiResponse<StaffVO>>> GetStaff(string userId)
    {
        var staff = await staffRepository.GetStaffByIdAsync(userId);
        if (staff == null)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("获取员工信息失败，员工不存在，ID: {UserId}", userId);
            }

            return Ok(ApiResponse<StaffVO>.Fail(ErrorCode.UserNotFound, "员工不存在"));
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("获取员工信息成功，ID: {UserId}", userId);
        }

        return Ok(ApiResponse<StaffVO>.Success(staff.Adapt<StaffVO>(), "获取员工信息成功"));
    }

    /// <summary>
    /// 创建新员工
    /// </summary>
    /// <param name="staff">员工信息模型</param>
    /// <returns>创建结果</returns>
    [HttpPost("Create")]
    public async Task<ActionResult<ApiResponse>> CreateStaff([FromBody] StaffCreateDTO staff)
    {
        var actor = User.GetUser();
        if (actor == null)
            return Ok(ApiResponse.Fail(ErrorCode.Unauthorized, "用户未认证"));

        if (staff.Identity == "Founder")
            return Ok(ApiResponse.Fail(ErrorCode.InsufficientPermission, "创始人权限只能通过专用权限接口设置"));

        // 低职级不能创建高职级：历史上 Minister 可以在这里直接造出 President。
        if (!RoleHierarchy.CanAssign(actor.Identity, staff.Identity))
            return Ok(ApiResponse.Fail(ErrorCode.InsufficientPermission, $"无权授予 {staff.Identity} 身份"));

        // 防抢注：公开注册可能先占住同号学生档案，再用别人的姓名创建员工身份。
        // 与部门导入保持同一条规则：学号 + 姓名都一致才视为同一个人。
        var existingStudent = await studentRepository.GetByIdAsync(staff.UserId);
        if (existingStudent != null &&
            !string.Equals(existingStudent.UserName?.Trim(), staff.Name.Trim(), StringComparison.Ordinal))
        {
            return Ok(ApiResponse.Fail(ErrorCode.InsufficientPermission,
                $"学号 {staff.UserId} 已有学生档案且姓名不一致，请核对后再创建员工身份"));
        }

        var result = await staffRepository.CreateStaffAsync(staff.Adapt<StaffDO>());
        if (!result)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("创建成员失败，ID: {UserId}, 名称: {Name}", staff.UserId, staff.Name);
            }

            return Ok(ApiResponse<StaffVO>.Fail(ErrorCode.OperationFailed, "创建成员失败"));
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("创建成员成功，ID: {UserId}, 名称: {Name}", staff.UserId, staff.Name);
        }

        return ApiResponse.Success("创建成员成功");
    }

    /// <summary>
    /// 更新员工信息
    /// </summary>
    /// <param name="staff">更新后的员工信息</param>
    /// <returns>更新结果</returns>
    [HttpPost("Update")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateStaff([FromBody] StaffCreateDTO staff)
    {
        // 获取目标成员信息
        var targetStaff = await staffRepository.GetStaffByIdAsync(staff.UserId);
        if (targetStaff == null)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("更新成员失败，员工不存在，ID: {UserId}", staff.UserId);
            }

            return Ok(ApiResponse<object>.Fail(ErrorCode.UserNotFound, "员工不存在"));
        }

        var actor = User.GetUser();
        if (actor == null)
            return Ok(ApiResponse<object>.Fail(ErrorCode.Unauthorized, "用户未认证"));

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isSelf = staff.UserId == currentUserId;

        // 自己的普通字段由下面的 C27 规则处理（不能改身份、不能改部门）。
        // 对他人：改身份属于权限变更，要求严格高于目标；只改姓名/部门则允许同级。
        if (!isSelf)
        {
            var identityChanged = !string.Equals(staff.Identity, targetStaff.Identity, StringComparison.Ordinal);
            var canTouchTarget = identityChanged
                ? RoleHierarchy.CanTakeOver(actor.Identity, targetStaff.Identity)
                : RoleHierarchy.CanManage(actor.Identity, targetStaff.Identity);

            if (!canTouchTarget)
                return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, "无权调整该成员的身份"));

            if (!RoleHierarchy.CanAssign(actor.Identity, staff.Identity))
                return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, $"无权授予 {staff.Identity} 身份"));
        }

        if (targetStaff.Identity == "Founder" || staff.Identity == "Founder")
            return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, "创始人权限只能通过专用权限接口设置"));

        if (isSelf)
        {
            if (staff.Identity != targetStaff.Identity)
                return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, "不能修改自己的权限"));

            // C27：与 change-department 接口一致，本人不能调整自己的部门；
            // 缺省 DepartmentName 视为“部门不变”，改姓名等普通字段仍然放行。
            var requestedDepartment = string.IsNullOrWhiteSpace(staff.DepartmentName)
                ? targetStaff.Department?.Name
                : staff.DepartmentName;
            if (!string.Equals(requestedDepartment, targetStaff.Department?.Name, StringComparison.Ordinal))
                return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, "不能修改自己的部门"));
        }

        var result = await staffRepository.UpdateStaffAsync(staff.Adapt<StaffDO>());
        if (!result)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("更新成员失败，ID: {UserId}, 名称: {Name}", staff.UserId, staff.Name);
            }

            return Ok(ApiResponse<object>.Fail(ErrorCode.OperationFailed, "更新成员失败"));
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("更新成员成功，ID: {UserId}, 名称: {Name}", staff.UserId, staff.Name);
        }

        return Ok(ApiResponse.Success("更新成员成功"));
    }

    /// <summary>
    /// 删除员工
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>删除结果</returns>
    [HttpGet("Delete/{userId}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteStaff(string userId)
    {
        // 获取目标成员信息
        var targetStaff = await staffRepository.GetStaffByIdAsync(userId);
        if (targetStaff == null)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("删除成员失败，员工不存在，ID: {UserId}", userId);
            }

            return Ok(ApiResponse<object>.Fail(ErrorCode.UserNotFound, "员工不存在"));
        }

        if (targetStaff.Identity == "Founder" || userId == User.FindFirstValue(ClaimTypes.NameIdentifier))
            return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, "不能移除创始人或自己的权限"));

        var actor = User.GetUser();
        if (actor == null)
            return Ok(ApiResponse<object>.Fail(ErrorCode.Unauthorized, "用户未认证"));

        if (!RoleHierarchy.CanManage(actor.Identity, targetStaff.Identity))
            return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, "无权移除该成员"));

        var result = await staffRepository.DeleteStaffAsync(userId);
        if (!result)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("删除成员失败，ID: {UserId}", userId);
            }

            return Ok(ApiResponse<object>.Fail(ErrorCode.OperationFailed, "删除成员失败"));
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("删除成员成功，ID: {UserId}", userId);
        }

        return Ok(ApiResponse.Success("删除成员成功"));
    }

    /// <summary>
    /// 根据身份获取员工列表
    /// </summary>
    /// <param name="identity">员工身份</param>
    /// <returns>符合条件的员工列表</returns>
    [HttpGet("by-identity/{identity}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<StaffVO>>>> GetStaffsByIdentity(string identity)
    {
        var staffs = await staffRepository.GetStaffsByIdentitiesAsync(identity);
        var staffModels = staffs as StaffDO[] ?? staffs.ToArray();
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("根据身份获取员工列表成功，身份: {Identity}, 员工数量: {Count}", identity, staffModels.Length);
        }

        return Ok(ApiResponse<IEnumerable<StaffVO>>.Success(staffModels.Adapt<List<StaffVO>>(), "根据身份获取员工列表成功"));
    }

    /// <summary>
    /// 修改员工所属部门
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="departmentName">部门名称，为null时表示移除部门</param>
    /// <returns>修改结果</returns>
    [HttpPost("change-department/{userId}")]
    public async Task<ActionResult<ApiResponse<object>>> ChangeStaffDepartment(string userId,
        [FromQuery] string? departmentName)
    {
        var targetStaff = await staffRepository.GetStaffByIdAsync(userId);
        if (targetStaff?.Identity == "Founder" || userId == User.FindFirstValue(ClaimTypes.NameIdentifier))
            return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, "不能调整创始人或自己的部门权限"));

        var actor = User.GetUser();
        if (actor == null)
            return Ok(ApiResponse<object>.Fail(ErrorCode.Unauthorized, "用户未认证"));

        if (targetStaff != null && !RoleHierarchy.CanManage(actor.Identity, targetStaff.Identity))
            return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, "无权调整该成员的部门"));

        var result = await staffRepository.ChangeStaffDepartmentAsync(userId, departmentName);
        if (!result)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("修改部门失败，ID: {UserId}, 部门名称: {DepartmentName}", userId, departmentName);
            }

            return Ok(ApiResponse<object>.Fail(ErrorCode.OperationFailed, "修改部门失败"));
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("修改部门成功，ID: {UserId}, 部门名称: {DepartmentName}", userId, departmentName);
        }

        return Ok(ApiResponse.Success("修改部门成功"));
    }

    /// <summary>
    /// 人事调动：从现有成员中提拔为部长、或将部员/部长提拔为社长，
    /// 也支持把领导调整回某个部门。仅更新已存在的成员，不新增。
    /// </summary>
    /// <param name="model">调动请求</param>
    /// <returns>操作结果</returns>
    [HttpPost("assign-role")]
    public async Task<ActionResult<ApiResponse<object>>> AssignRole([FromBody] StaffRoleAssignDTO model)
    {
        if (model.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier))
            return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, "不能修改自己的权限"));

        var targetStaff = await staffRepository.GetStaffByIdWithoutOtherData(model.UserId);
        if (targetStaff == null)
            return Ok(ApiResponse<object>.Fail(ErrorCode.UserNotFound, "成员不存在"));

        var actor = User.GetUser();
        if (actor == null)
            return Ok(ApiResponse<object>.Fail(ErrorCode.Unauthorized, "用户未认证"));

        // 调动身份会改变权限归属，要求严格高于目标；新身份也不能高于自己。
        if (!RoleHierarchy.CanTakeOver(actor.Identity, targetStaff.Identity))
            return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, "无权调整该成员的身份"));

        if (!RoleHierarchy.CanAssign(actor.Identity, model.Identity))
            return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, $"无权授予 {model.Identity} 身份"));

        var (success, error) = await staffRepository.AssignRoleAsync(model.UserId, model.Identity, model.DepartmentName);
        if (!success)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("人事调动失败，ID: {UserId}, 目标身份: {Identity}, 原因: {Error}", model.UserId,
                    model.Identity, error);
            }

            return Ok(ApiResponse<object>.Fail(ErrorCode.InvalidStatusForOperation, error));
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("人事调动成功，ID: {UserId}, 目标身份: {Identity}", model.UserId, model.Identity);
        }

        return Ok(ApiResponse.Success("身份调整成功"));
    }
}

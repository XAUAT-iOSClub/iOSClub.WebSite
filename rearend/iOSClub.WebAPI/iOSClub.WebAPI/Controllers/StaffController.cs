using Mapster;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iOSClub.WebAPI.Controllers;

[Authorize(Roles = "Founder, President, Minister")]
[ApiController]
[Route("[controller]")] // 使用C#推荐的API路径格式
public class StaffController(IStaffRepository staffRepository, ILogger<StaffController> logger)
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
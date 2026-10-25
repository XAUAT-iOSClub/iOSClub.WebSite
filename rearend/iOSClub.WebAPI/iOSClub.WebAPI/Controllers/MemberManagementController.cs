using Mapster;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iOSClub.WebAPI.Controllers;

/// <summary>
/// 成员管理控制器 - 负责成员的删除、更新等管理功能
/// </summary>
[Authorize(Roles = "Founder, President, Minister")]
[ApiController]
[Route("[controller]")]
public class MemberManagementController(
    IStudentRepository studentRepository,
    ILogger<MemberManagementController> logger) : ControllerBase
{
    /// <summary>
    /// 删除学生成员
    /// </summary>
    /// <param name="id">学生ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("delete/{id}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string id)
    {
        var result = await studentRepository.DeleteAsync(id);
        if (!result)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("删除学生失败，学生不存在，ID: {Id}", id);
            }

            return Ok(ApiResponse<object>.Fail(ErrorCode.UserNotFound, "学生不存在"));
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("删除学生成功，ID: {Id}", id);
        }

        return Ok(ApiResponse.Success("删除学生成功"));
    }

    /// <summary>
    /// 批量更新或添加学生成员
    /// </summary>
    /// <param name="list">学生列表</param>
    /// <returns>更新后的学生列表</returns>
    [HttpPost("update-many")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateMany(List<StudentUpdateDTO> list)
    {
        var doList = list.Adapt<List<StudentDO>>();
        var result = await studentRepository.UpdateManyAsync(doList);
        if (result)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("批量更新学生成功，更新数量: {Count}", list.Count);
            }

            return Ok(ApiResponse<bool>.Success(true, "批量更新学生成功"));
        }
        else
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("批量更新学生失败，更新数量: {Count}", list.Count);
            }

            return Ok(ApiResponse<bool>.Fail(ErrorCode.OperationFailed, "批量更新学生失败"));
        }
    }

    /// <summary>
    /// 新增学生成员（管理角色专用）
    /// <para>
    /// 与 <see cref="Update"/> 分开：update 以学号为定位键，学号不存在时必然失败，
    /// 因此不能复用为创建。初始密码由管理员在此提交，仓库层负责 BCrypt 哈希；
    /// 响应只返回不含密码字段的 <see cref="StudentVO"/>。
    /// </para>
    /// <para>
    /// 只创建学生档案，不自动创建 Staff 档案，身份按现有规则回落为 Member。
    /// </para>
    /// </summary>
    /// <param name="model">学生创建模型，包含管理员设置的初始密码</param>
    /// <returns>新建的学生信息</returns>
    [HttpPost("create")]
    public async Task<ActionResult<ApiResponse<StudentVO>>> Create([FromBody] StudentCreateDTO model)
    {
        // 先查重再创建，避免把"学号已存在"和"参数不合法"混成同一个失败提示。
        var existing = await studentRepository.GetByIdAsync(model.UserId);
        if (existing != null)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("新增学生失败，学号已存在，ID: {Id}", model.UserId);
            }

            return Ok(ApiResponse<StudentVO>.Fail(ErrorCode.ResourceAlreadyExists, "学号已存在，不能重复创建"));
        }

        // Mapster 全局配置负责把 StudentCreateDTO.Password 映射为 BCrypt PasswordHash。
        var created = await studentRepository.Create(model.Adapt<StudentDO>());
        if (created == null)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("新增学生失败，ID: {Id}", model.UserId);
            }

            return Ok(ApiResponse<StudentVO>.Fail(ErrorCode.OperationFailed, "新增学生失败"));
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("新增学生成功，ID: {Id}", created.UserId);
        }

        return Ok(ApiResponse<StudentVO>.Success(
            created.Adapt<StudentVO>(),
            "成员创建成功，初始密码由管理员设置，请告知本人"));
    }

    /// <summary>
    /// 更新单个成员信息
    /// </summary>
    /// <param name="model">成员模型</param>
    /// <returns>操作结果</returns>
    [HttpPost("update")]
    public async Task<ActionResult<ApiResponse<object>>> Update([FromBody] StudentUpdateDTO model)
    {
        var result = await studentRepository.UpdateProfileAsync(model.Adapt<StudentDO>());
        if (!result)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("更新学生信息失败，学生不存在，ID: {Id}", model.UserId);
            }

            return Ok(ApiResponse<object>.Fail(ErrorCode.UserNotFound, "学生不存在"));
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("更新学生信息成功，ID: {Id}", model.UserId);
        }

        return Ok(ApiResponse.Success("更新学生信息成功"));
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(ResetPasswordData data)
    {
        var student = await studentRepository.GetByIdAsync(data.UserId);
        if (student == null)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("重置密码失败，学生不存在，ID: {Id}", data.UserId);
            }

            return Ok(ApiResponse<object>.Fail(ErrorCode.UserNotFound, "学生不存在"));
        }

        // 明文密码交给仓库层的 SetPasswordAsync 去哈希，控制器不碰哈希细节。
        var result = await studentRepository.SetPasswordAsync(data.UserId, data.NewPassword);
        if (!result)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("重置密码失败，ID: {Id}", data.UserId);
            }

            return Ok(ApiResponse<object>.Fail(ErrorCode.OperationFailed, "重置密码失败"));
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("重置密码成功，ID: {Id}", data.UserId);
        }

        return Ok(ApiResponse.Success("重置密码成功"));
    }
}

public record ResetPasswordData(string UserId, string NewPassword);
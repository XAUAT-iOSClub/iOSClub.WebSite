using Mapster;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.Common.Security;
using iOSClub.WebAPI.IdentityModels;
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
    IStaffRepository staffRepository,
    ILogger<MemberManagementController> logger) : ControllerBase
{
    /// <summary>
    /// 调用者是否可以对目标学号做普通维护（改资料、删除）。
    /// <para>
    /// 目标没有 Staff 记录时按普通 Member 处理；有 Staff 记录时按
    /// <see cref="RoleHierarchy.CanManage"/> 判断（允许同级，禁止越级）。
    /// </para>
    /// </summary>
    private async Task<bool> CanManageTargetAsync(string userId)
    {
        var actor = User.GetUser();
        if (actor == null) return false;
        if (actor.Identity == RoleHierarchy.Founder) return true;

        var target = await staffRepository.GetStaffByIdWithoutOtherData(userId);
        return RoleHierarchy.CanManage(actor.Identity, target?.Identity ?? RoleHierarchy.Member);
    }

    /// <summary>
    /// 调用者是否可以接管目标学号的登录凭据（重置密码、创建 Student 档案）。
    /// 要求严格高于目标职级：同级之间互相重置密码等于互相接管。
    /// </summary>
    private async Task<bool> CanTakeOverTargetAsync(string userId)
    {
        var actor = User.GetUser();
        if (actor == null) return false;
        if (actor.Identity == RoleHierarchy.Founder) return true;

        var target = await staffRepository.GetStaffByIdWithoutOtherData(userId);
        return RoleHierarchy.CanTakeOver(actor.Identity, target?.Identity ?? RoleHierarchy.Member);
    }

    /// <summary>
    /// 删除学生成员
    /// </summary>
    /// <param name="id">学生ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("delete/{id}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string id)
    {
        if (!await CanManageTargetAsync(id))
            return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, "无权删除该成员"));

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
    /// <para>
    /// 响应逐行给出新增、更新、跳过和失败的数量及原因。零写入且存在被跳过或失败的行时，
    /// 业务码为 <see cref="ErrorCode.OperationFailed"/>，但 <c>data</c> 仍保留完整结果，
    /// 避免"整批被过滤却提示成功"。
    /// </para>
    /// </summary>
    /// <param name="list">学生列表</param>
    /// <returns>逐行更新结果</returns>
    [HttpPost("update-many")]
    public async Task<ActionResult<ApiResponse<BulkUpdateResultVO>>> UpdateMany(List<StudentUpdateDTO> list)
    {
        // 批量写库前统一校验：
        // - 已存在的 Student 只是改资料，按普通维护（同级可操作）判断；
        // - 不存在的 Student 会被创建并写入默认密码，等于发放登录凭据，必须严格高于目标职级。
        var forbidden = new List<string>();
        foreach (var item in list)
        {
            var existing = await studentRepository.GetByIdAsync(item.UserId);
            var allowed = existing != null
                ? await CanManageTargetAsync(item.UserId)
                : await CanTakeOverTargetAsync(item.UserId);

            if (!allowed)
                forbidden.Add(item.UserId);
        }

        if (forbidden.Count > 0)
        {
            return Ok(ApiResponse<BulkUpdateResultVO>.Fail(
                ErrorCode.InsufficientPermission,
                $"无权修改以下学号：{string.Join(", ", forbidden.Distinct())}"));
        }

        var doList = list.Adapt<List<StudentDO>>();
        var result = await studentRepository.UpdateManyAsync(doList);
        var message = $"新增 {result.Created}，更新 {result.Updated}，跳过 {result.Skipped}，失败 {result.Failed}";

        // 零写入 + 有未写入行：接口必须失败，但逐行原因仍要能展示
        if (result.Created + result.Updated == 0 && result.Skipped + result.Failed > 0)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("批量更新学生未写入任何记录：{Message}", message);
            }

            return Ok(ApiResponse<BulkUpdateResultVO>.FailWithData(ErrorCode.OperationFailed, result, message));
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("批量更新学生完成：{Message}", message);
        }

        return Ok(ApiResponse<BulkUpdateResultVO>.Success(result, message));
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
        if (!await CanTakeOverTargetAsync(model.UserId))
            return Ok(ApiResponse<StudentVO>.Fail(ErrorCode.InsufficientPermission, "无权为该学号创建账号"));

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
        if (!await CanManageTargetAsync(model.UserId))
            return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, "无权修改该成员"));

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
        // 重置密码等于直接接管账号：禁止 Minister/President 对同级或更高级别账号操作，
        // 尤其是 Founder。Founder 仍可重置任何人的密码。
        if (!await CanTakeOverTargetAsync(data.UserId))
            return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, "无权重置该成员的密码"));

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
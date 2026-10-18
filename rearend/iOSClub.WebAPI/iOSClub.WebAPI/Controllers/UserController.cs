using Mapster;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.IdentityModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iOSClub.WebAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class UserController(
    IStudentRepository studentRepository,
    IStaffRepository staffRepository,
    IHttpContextAccessor httpContextAccessor)
    : ControllerBase
{
    [Authorize]
    [HttpGet("data")]
    public async Task<ActionResult<ApiResponse<MemberVO>>> GetData()
    {
        var member = httpContextAccessor.HttpContext?.User.GetUser();
        if (member == null)
            return Ok(ApiResponse<MemberVO>.Fail(ErrorCode.Unauthorized, "用户未认证"));

        // 优先返回学生档案（含姓名、学院、班级等完整信息）
        var student = await studentRepository.GetByIdAsync(member.UserId);
        if (student != null)
        {
            var result = student.Adapt<MemberVO>();
            result.Identity = member.Identity;
            return Ok(ApiResponse<MemberVO>.Success(result, "获取用户信息成功"));
        }

        // 没有学生档案的账号（例如创始人 / 干部 Staff）：回退到 Staff 信息。
        // 至少要能返回 UserId、姓名和身份，否则概览页拿不到资料会清空令牌把用户踢出去。
        var staff = await staffRepository.GetStaffByIdWithoutOtherData(member.UserId);
        if (staff != null)
        {
            return Ok(ApiResponse<MemberVO>.Success(new MemberVO
            {
                UserId = staff.UserId,
                UserName = staff.Name,
                Identity = staff.Identity,
                Academy = "",
                PoliticalLandscape = "",
                Gender = "",
                ClassName = "",
                PhoneNum = "",
                JoinTime = DateTime.UtcNow
            }, "获取用户信息成功"));
        }

        return Ok(ApiResponse<MemberVO>.Fail(ErrorCode.UserNotFound, "用户不存在"));
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProfile([FromBody] StudentUpdateDTO dto)
    {
        var member = httpContextAccessor.HttpContext?.User.GetUser();
        if (member == null || member.UserId != dto.UserId)
            return Ok(ApiResponse<object>.Fail(ErrorCode.InsufficientPermission, "权限不足"));

        // 走 UpdateProfileAsync 而不是通用更新：这个接口是用户改自己的资料，
        // 不该有任何路径能碰到 PasswordHash（改密码在 Auth/change-password）。
        var result = await studentRepository.UpdateProfileAsync(dto.Adapt<StudentDO>());
        return result
            ? Ok(ApiResponse.Success("更新用户资料成功"))
            : Ok(ApiResponse<object>.Fail(ErrorCode.OperationFailed, "更新用户资料失败"));
    }
}
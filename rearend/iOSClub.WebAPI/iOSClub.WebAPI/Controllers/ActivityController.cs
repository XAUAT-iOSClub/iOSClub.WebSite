using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.IdentityModels;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iOSClub.WebAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class ActivityController(
    IActivityRepository activityRepository,
    IActivityParticipantRepository participantRepository,
    IActivityOperationLogRepository logRepository,
    IActivityExcelService excelService) : ControllerBase
{
    private const string AdminRoles = "Founder,President,Minister";

    private string? CurrentUserId() => HttpContext.User.GetUser()?.UserId;

    // ===== 活动 CRUD =====

    [HttpGet]
    [Authorize(Roles = AdminRoles)]
    public async Task<ActionResult<ApiResponse<List<ActivityVO>>>> GetAll(string? status = null, string? keyword = null)
        => Ok(ApiResponse<List<ActivityVO>>.Success(await activityRepository.GetAllAsync(status, keyword)));

    [HttpGet("{id}")]
    [Authorize(Roles = AdminRoles)]
    public async Task<ActionResult<ApiResponse<ActivityVO>>> Get(string id)
    {
        var vo = await activityRepository.GetByIdAsync(id);
        return vo == null
            ? Ok(ApiResponse<ActivityVO>.Fail(ErrorCode.ResourceNotFound, "活动不存在"))
            : Ok(ApiResponse<ActivityVO>.Success(vo));
    }

    [HttpPost]
    [Authorize(Roles = AdminRoles)]
    public async Task<ActionResult<ApiResponse<ActivityVO>>> Create([FromBody] ActivityCreateDTO dto)
    {
        var err = ValidateActivity(dto);
        if (err != null) return Ok(ApiResponse<ActivityVO>.Fail(ErrorCode.ParameterValidationFailed, err));

        var model = dto.Adapt<ActivityDO>();
        model.CreatedBy = CurrentUserId();
        model.StartTime = ToUtc(model.StartTime);
        model.EndTime = ToUtc(model.EndTime);
        var created = await activityRepository.CreateAsync(model);
        if (created == null) return Ok(ApiResponse<ActivityVO>.Fail(ErrorCode.OperationFailed, "创建失败"));

        await logRepository.LogAsync(created.Id, CurrentUserId(), "创建活动", created.Name);
        return Ok(ApiResponse<ActivityVO>.Success(created.Adapt<ActivityVO>(), "创建成功"));
    }

    /// <summary>编辑活动（所有状态下均可）。</summary>
    [HttpPut("{id}")]
    [Authorize(Roles = AdminRoles)]
    public async Task<ActionResult<ApiResponse>> Update(string id, [FromBody] ActivityUpdateDTO dto)
    {
        var existing = await activityRepository.GetByIdAsync(id);
        if (existing == null) return Ok(ApiResponse.Fail(ErrorCode.ResourceNotFound, "活动不存在"));

        var err = ValidateActivity(dto);
        if (err != null) return Ok(ApiResponse.Fail(ErrorCode.ParameterValidationFailed, err));

        var model = dto.Adapt<ActivityDO>();
        model.Id = id;
        model.StartTime = ToUtc(model.StartTime);
        model.EndTime = ToUtc(model.EndTime);
        if (!await activityRepository.UpdateAsync(model))
            return Ok(ApiResponse.Fail(ErrorCode.OperationFailed, "更新失败"));

        await logRepository.LogAsync(id, CurrentUserId(), "编辑活动", model.Name);
        return Ok(ApiResponse.Success("更新成功"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = AdminRoles)]
    public async Task<ActionResult<ApiResponse>> Delete(string id)
    {
        var activity = await activityRepository.GetByIdAsync(id);
        if (activity == null) return Ok(ApiResponse.Fail(ErrorCode.ResourceNotFound, "活动不存在"));

        if (!await activityRepository.DeleteAsync(id))
            return Ok(ApiResponse.Fail(ErrorCode.OperationFailed, "删除失败"));

        await logRepository.LogAsync(null, CurrentUserId(), "删除活动", $"{activity.Name}（含 {activity.ParticipantCount} 名参与者）");
        return Ok(ApiResponse.Success("删除成功"));
    }

    // ===== 状态与自助登记 =====

    /// <summary>管理员手动变更状态：任意状态 → 任意状态。</summary>
    [HttpPost("{id}/status")]
    [Authorize(Roles = AdminRoles)]
    public async Task<ActionResult<ApiResponse>> ChangeStatus(string id, [FromBody] ActivityStatusUpdateDTO dto)
    {
        if (!ActivityStatus.IsValid(dto.Status))
            return Ok(ApiResponse.Fail(ErrorCode.ParameterValidationFailed, "状态只能是 Upcoming / Ongoing / Finished"));

        var activity = await activityRepository.GetByIdAsync(id);
        if (activity == null) return Ok(ApiResponse.Fail(ErrorCode.ResourceNotFound, "活动不存在"));

        if (!await activityRepository.UpdateStatusAsync(id, dto.Status))
            return Ok(ApiResponse.Fail(ErrorCode.OperationFailed, "状态变更失败"));

        await logRepository.LogAsync(id, CurrentUserId(), "变更状态", $"{activity.Status} → {dto.Status}");
        return Ok(ApiResponse.Success("状态已更新"));
    }

    [HttpPost("{id}/self-registration")]
    [Authorize(Roles = AdminRoles)]
    public async Task<ActionResult<ApiResponse>> SetSelfRegistration(string id, [FromBody] ActivitySelfRegistrationDTO dto)
    {
        var activity = await activityRepository.GetByIdAsync(id);
        if (activity == null) return Ok(ApiResponse.Fail(ErrorCode.ResourceNotFound, "活动不存在"));

        if (!await activityRepository.SetSelfRegistrationAsync(id, dto.Enabled))
            return Ok(ApiResponse.Fail(ErrorCode.OperationFailed, "操作失败"));

        await logRepository.LogAsync(id, CurrentUserId(), "自助登记开关", dto.Enabled ? "开启" : "关闭");
        return Ok(ApiResponse.Success(dto.Enabled ? "已开启自助登记" : "已关闭自助登记"));
    }

    // ===== 参与者管理 =====

    [HttpGet("{id}/participants")]
    [Authorize(Roles = AdminRoles)]
    public async Task<ActionResult<ApiResponse<ActivityParticipantPageVO>>> Participants(
        string id, string? search = null, string? academy = null, int page = 1, int pageSize = 20)
        => Ok(ApiResponse<ActivityParticipantPageVO>.Success(
            await participantRepository.GetByActivityAsync(id, search, academy, page, pageSize)));

    [HttpPost("{id}/participants")]
    [Authorize(Roles = AdminRoles)]
    public async Task<ActionResult<ApiResponse<ActivityParticipantVO>>> AddParticipant(string id, [FromBody] ActivityParticipantCreateDTO dto)
    {
        var (model, err) = BuildParticipant(id, dto, ParticipantSource.Manual);
        if (err != null) return Ok(ApiResponse<ActivityParticipantVO>.Fail(ErrorCode.ParameterValidationFailed, err));
        if (await participantRepository.ExistsAsync(id, dto.StudentId))
            return Ok(ApiResponse<ActivityParticipantVO>.Fail(ErrorCode.ResourceAlreadyExists, "该学号已在此活动中登记"));

        var created = await participantRepository.CreateAsync(model!);
        if (created == null) return Ok(ApiResponse<ActivityParticipantVO>.Fail(ErrorCode.OperationFailed, "添加失败"));

        await logRepository.LogAsync(id, CurrentUserId(), "添加参与者", $"{dto.Name}/{dto.StudentId}");
        return Ok(ApiResponse<ActivityParticipantVO>.Success(created.Adapt<ActivityParticipantVO>(), "添加成功"));
    }

    [HttpPut("participants/{participantId}")]
    [Authorize(Roles = AdminRoles)]
    public async Task<ActionResult<ApiResponse>> UpdateParticipant(string participantId, [FromBody] ActivityParticipantCreateDTO dto)
    {
        var existing = await participantRepository.GetByIdAsync(participantId);
        if (existing == null) return Ok(ApiResponse.Fail(ErrorCode.ResourceNotFound, "参与者不存在"));

        var (_, err) = BuildParticipant(existing.ActivityId, dto, existing.Source);
        if (err != null) return Ok(ApiResponse.Fail(ErrorCode.ParameterValidationFailed, err));

        if (existing.StudentId != dto.StudentId && await participantRepository.ExistsAsync(existing.ActivityId, dto.StudentId))
            return Ok(ApiResponse.Fail(ErrorCode.ResourceAlreadyExists, "该学号已在此活动中登记"));

        var model = dto.Adapt<ActivityParticipantDO>();
        model.Id = participantId;
        model.ActivityId = existing.ActivityId;
        model.Source = existing.Source;
        return Ok(await participantRepository.UpdateAsync(model)
            ? ApiResponse.Success("更新成功")
            : ApiResponse.Fail(ErrorCode.OperationFailed, "更新失败"));
    }

    [HttpDelete("participants/{participantId}")]
    [Authorize(Roles = AdminRoles)]
    public async Task<ActionResult<ApiResponse>> DeleteParticipant(string participantId)
        => Ok(await participantRepository.DeleteAsync(participantId)
            ? ApiResponse.Success("删除成功")
            : ApiResponse.Fail(ErrorCode.ResourceNotFound, "参与者不存在或删除失败"));

    [HttpPost("{id}/participants/bulk-delete")]
    [Authorize(Roles = AdminRoles)]
    public async Task<ActionResult<ApiResponse<object>>> BulkDeleteParticipants(string id, [FromBody] BulkDeleteParticipantsDTO dto)
    {
        if (dto.Ids.Count == 0) return Ok(ApiResponse<object>.Fail(ErrorCode.ParameterEmpty, "请选择要删除的参与者"));
        var count = await participantRepository.DeleteManyAsync(id, dto.Ids);
        await logRepository.LogAsync(id, CurrentUserId(), "批量删除参与者", $"删除 {count} 条");
        return Ok(ApiResponse<object>.Success(new { deleted = count }, $"已删除 {count} 条"));
    }

    // ===== 自助登记（公开） =====

    [HttpPost("{id}/register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ActivityParticipantVO>>> Register(string id, [FromBody] ActivityParticipantCreateDTO dto)
    {
        var activity = await activityRepository.GetByIdAsync(id);
        if (activity == null) return Ok(ApiResponse<ActivityParticipantVO>.Fail(ErrorCode.ResourceNotFound, "活动不存在"));
        if (!activity.SelfRegistrationEnabled)
            return Ok(ApiResponse<ActivityParticipantVO>.Fail(ErrorCode.InvalidStatusForOperation, "登记通道已关闭"));

        var (model, err) = BuildParticipant(id, dto, ParticipantSource.Self);
        if (err != null) return Ok(ApiResponse<ActivityParticipantVO>.Fail(ErrorCode.ParameterValidationFailed, err));
        if (await participantRepository.ExistsAsync(id, dto.StudentId))
            return Ok(ApiResponse<ActivityParticipantVO>.Fail(ErrorCode.ResourceAlreadyExists, "该学号已登记，请勿重复提交"));

        var created = await participantRepository.CreateAsync(model!);
        return created == null
            ? Ok(ApiResponse<ActivityParticipantVO>.Fail(ErrorCode.OperationFailed, "登记失败"))
            : Ok(ApiResponse<ActivityParticipantVO>.Success(created.Adapt<ActivityParticipantVO>(), "登记成功"));
    }

    // ===== Excel =====

    /// <summary>导出参与者名单。backup=true 时文件名加“_备份”后缀。</summary>
    [HttpGet("{id}/export")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> Export(string id, bool backup = false)
    {
        var activity = await activityRepository.GetByIdAsync(id);
        if (activity == null) return NotFound();

        var participants = await participantRepository.GetAllByActivityAsync(id);
        var bytes = excelService.Export(participants);
        var suffix = backup ? "_备份" : "";
        var fileName = $"{activity.Name}_参与者名单{suffix}.xlsx";

        await logRepository.LogAsync(id, CurrentUserId(), backup ? "删除前备份导出" : "导出参与者名单", $"{participants.Count} 条");
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("import-template")]
    [Authorize(Roles = AdminRoles)]
    public IActionResult DownloadTemplate()
    {
        var bytes = excelService.BuildTemplate();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ActivityExcelService.TemplateFileName);
    }

    /// <summary>导入参与者。preview=true 只校验、不写入。</summary>
    [HttpPost("{id}/import")]
    [Authorize(Roles = AdminRoles)]
    public async Task<ActionResult<ApiResponse<ActivityImportResultVO>>> Import(string id, IFormFile? file, bool preview = false)
    {
        var activity = await activityRepository.GetByIdAsync(id);
        if (activity == null) return Ok(ApiResponse<ActivityImportResultVO>.Fail(ErrorCode.ResourceNotFound, "活动不存在"));

        var (result, validRows, _, fatal) = await ParseImportAsync(id, file);
        if (fatal != null) return Ok(ApiResponse<ActivityImportResultVO>.Fail(ErrorCode.ParameterValidationFailed, fatal));

        if (preview) return Ok(ApiResponse<ActivityImportResultVO>.Success(result, "校验完成"));

        var success = 0;
        foreach (var dto in validRows)
        {
            var model = dto.Adapt<ActivityParticipantDO>();
            model.ActivityId = id;
            model.Source = ParticipantSource.Import;
            if (await participantRepository.CreateAsync(model) != null) success++;
        }

        var finalResult = new ActivityImportResultVO
        {
            TotalRows = result.TotalRows,
            SuccessCount = success,
            FailCount = result.FailCount,
            Errors = result.Errors
        };
        await logRepository.LogAsync(id, CurrentUserId(), "Excel 导入", $"成功 {success} 条，失败 {result.FailCount} 条");
        return Ok(ApiResponse<ActivityImportResultVO>.Success(finalResult, "导入完成"));
    }

    /// <summary>生成导入错误报告（上传原文件，返回错误 xlsx）。</summary>
    [HttpPost("{id}/import-error-report")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> ImportErrorReport(string id, IFormFile? file)
    {
        var (_, _, errorList, fatal) = await ParseImportAsync(id, file);
        if (fatal != null)
            return BadRequest(ApiResponse.Fail(ErrorCode.ParameterValidationFailed, fatal));
        var bytes = excelService.BuildErrorReport(errorList);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"导入错误报告_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    // ===== 操作日志 =====

    [HttpGet("{id}/logs")]
    [Authorize(Roles = AdminRoles)]
    public async Task<ActionResult<ApiResponse<List<ActivityOperationLogVO>>>> Logs(string id)
        => Ok(ApiResponse<List<ActivityOperationLogVO>>.Success(await logRepository.GetByActivityAsync(id)));

    // ===== 私有工具 =====

    /// <summary>把传入时间统一成 UTC：Npgsql 的 timestamptz 只接受 Utc/Local，
    /// 带时区偏移的字符串反序列化后可能得到非 Utc 的 Kind，直接保存会抛 DbUpdateException。</summary>
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string? ValidateActivity(ActivityCreateDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length < 1 || dto.Name.Length > 50)
            return "活动名称长度需在 1-50 个字符之间";
        if (string.IsNullOrWhiteSpace(dto.Location) || dto.Location.Length < 1 || dto.Location.Length > 15)
            return "活动地点长度需在 1-15 个字符之间";
        if (dto.EndTime <= dto.StartTime)
            return "结束时间必须晚于开始时间";
        if (dto.Description is { Length: > 500 })
            return "活动描述不能超过 500 个字符";
        return null;
    }

    private static (ActivityParticipantDO? Model, string? Error) BuildParticipant(string activityId, ActivityParticipantCreateDTO dto, string source)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length < 2 || dto.Name.Length > 20)
            return (null, "姓名长度需在 2-20 个字符之间");
        if (string.IsNullOrWhiteSpace(dto.StudentId) || dto.StudentId.Length != 10 || !dto.StudentId.All(char.IsDigit))
            return (null, "学号必须是 10 位数字");
        if (string.IsNullOrWhiteSpace(dto.Academy))
            return (null, "学院不能为空");
        if (string.IsNullOrWhiteSpace(dto.ClassName) || dto.ClassName.Length < 2 || dto.ClassName.Length > 30)
            return (null, "班级长度需在 2-30 个字符之间");

        var model = dto.Adapt<ActivityParticipantDO>();
        model.ActivityId = activityId;
        model.Source = source;
        return (model, null);
    }

    /// <summary>解析并校验 Excel，返回（结果、可导入行、致命错误）。</summary>
    private async Task<(ActivityImportResultVO Result, List<ActivityParticipantCreateDTO> ValidRows, List<ActivityImportErrorVO> ErrorList, string? Fatal)> ParseImportAsync(string activityId, IFormFile? file)
    {
        var result = new ActivityImportResultVO();
        var validRows = new List<ActivityParticipantCreateDTO>();
        var errorList = new List<ActivityImportErrorVO>();
        if (file == null || file.Length == 0) return (result, validRows, errorList, "请选择要导入的文件");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls") return (result, validRows, errorList, "仅支持 .xlsx 或 .xls 格式");
        if (file.Length > 10 * 1024 * 1024) return (result, validRows, errorList, "文件大小不能超过 10MB");

        List<ActivityParticipantCreateDTO> rows;
        List<ActivityImportErrorVO> errors;
        try
        {
            (rows, errors) = excelService.Import(file.OpenReadStream(), file.FileName);
        }
        catch (InvalidDataException ex)
        {
            return (result, validRows, errorList, ex.Message);
        }
        errorList.AddRange(errors);
        var existing = await participantRepository.GetExistingStudentIdsAsync(activityId);
        var seen = new HashSet<string>();

        foreach (var dto in rows)
        {
            if (!seen.Add(dto.StudentId))
            {
                errorList.Add(new ActivityImportErrorVO { Name = dto.Name, StudentId = dto.StudentId, Academy = dto.Academy, ClassName = dto.ClassName, Reason = "文件内学号重复" });
                continue;
            }
            if (existing.Contains(dto.StudentId))
            {
                errorList.Add(new ActivityImportErrorVO { Name = dto.Name, StudentId = dto.StudentId, Academy = dto.Academy, ClassName = dto.ClassName, Reason = "与活动中已有参与者学号重复" });
                continue;
            }
            validRows.Add(dto);
        }

        result.TotalRows = rows.Count + errors.Count;
        result.SuccessCount = validRows.Count;
        result.FailCount = errorList.Count;
        result.Errors = errorList
            .Select(e => string.IsNullOrEmpty(e.Name) && string.IsNullOrEmpty(e.StudentId)
                ? e.Reason
                : $"{(e.RowNumber > 0 ? $"第{e.RowNumber}行 " : "")}{e.Name}/{e.StudentId}：{e.Reason}")
            .ToList();
        result.PreviewItems = validRows.Select(r => new ActivityParticipantVO
        {
            Name = r.Name, StudentId = r.StudentId, Academy = r.Academy, ClassName = r.ClassName
        }).ToList();
        return (result, validRows, errorList, null);
    }
}

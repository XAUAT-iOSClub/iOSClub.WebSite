using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.Common.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iOSClub.WebAPI.Controllers;

/// <summary>
/// 全局后台操作日志。仅 Founder / President 可读；日志由
/// <see cref="OperationLogFilter"/> 自动写入，接口不提供删除或清理能力。
/// </summary>
[ApiController]
[Authorize(Roles = "Founder, President")]
[Route("[controller]")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class OperationLogsController(IOperationLogRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<OperationLogPageVO>>> GetLogs(
        int pageIndex = 1,
        int pageSize = 20,
        string? module = null,
        string? operatorId = null,
        string? keyword = null,
        bool? success = null,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null)
    {
        if (startTime is not null && endTime is not null && startTime > endTime)
        {
            return Ok(ApiResponse<OperationLogPageVO>.Fail(
                ErrorCode.ParameterOutOfRange, "开始时间不能晚于结束时间"));
        }

        if (keyword is { Length: > 100 })
        {
            keyword = keyword[..100];
        }

        var query = new OperationLogQueryDTO
        {
            PageIndex = pageIndex < 1 ? 1 : pageIndex,
            PageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100),
            Module = module,
            OperatorId = operatorId,
            Keyword = keyword,
            Success = success,
            StartTime = startTime,
            EndTime = endTime
        };

        var page = await repository.QueryAsync(query);
        return Ok(ApiResponse<OperationLogPageVO>.Success(page));
    }

    [HttpGet("options")]
    public async Task<ActionResult<ApiResponse<OperationLogOptionsVO>>> GetOptions()
    {
        var storedModules = await repository.GetModulesAsync();
        var modules = OperationLogCatalog.ModuleLabels
            .Concat(storedModules)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var operators = await repository.GetOperatorsAsync();

        return Ok(ApiResponse<OperationLogOptionsVO>.Success(new OperationLogOptionsVO
        {
            Modules = modules,
            Operators = operators
        }));
    }
}

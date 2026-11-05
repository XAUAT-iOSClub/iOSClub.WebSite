namespace iOSClub.Data.DTOs;

/// <summary>操作日志列表查询参数。</summary>
public class OperationLogQueryDTO
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Module { get; set; }
    public string? OperatorId { get; set; }
    public string? Keyword { get; set; }
    public bool? Success { get; set; }
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
}

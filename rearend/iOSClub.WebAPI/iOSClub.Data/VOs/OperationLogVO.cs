namespace iOSClub.Data.VOs;

/// <summary>操作日志列表/详情项。</summary>
public class OperationLogVO
{
    public long Id { get; set; }
    public string? OperatorId { get; set; }
    public string? OperatorName { get; set; }
    public string? OperatorRole { get; set; }
    public string Module { get; set; } = "";
    public string Action { get; set; } = "";
    public string ControllerName { get; set; } = "";
    public string ActionName { get; set; } = "";
    public string Method { get; set; } = "";
    public string? Path { get; set; }
    public string? Summary { get; set; }
    public string? Target { get; set; }
    public string? ChangeContent { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public bool Success { get; set; }
    public int HttpStatus { get; set; }
    public long DurationMs { get; set; }
    public string? TraceId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>操作日志分页结果。</summary>
public class OperationLogPageVO
{
    public List<OperationLogVO> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>筛选下拉用的操作人。</summary>
public class OperationLogOperatorVO
{
    public string UserId { get; set; } = "";
    public string? Name { get; set; }
}

/// <summary>操作日志筛选下拉选项。</summary>
public class OperationLogOptionsVO
{
    public List<string> Modules { get; set; } = [];
    public List<OperationLogOperatorVO> Operators { get; set; } = [];
}

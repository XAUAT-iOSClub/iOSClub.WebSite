using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace iOSClub.Data.DataObjects;

/// <summary>
/// 全局后台操作日志。记录谁在什么时间、从哪个 IP 对哪个模块执行了什么写操作，
/// 以及脱敏后的请求变更内容。仅 Founder / President 可读取。
/// </summary>
[Table("OperationLogs")]
public class OperationLogDO
{
    [Key]
    public long Id { get; set; }

    [MaxLength(36)] public string? OperatorId { get; set; }

    /// <summary>写入时快照，避免成员改名后历史日志失真。</summary>
    [MaxLength(50)] public string? OperatorName { get; set; }

    [MaxLength(20)] public string? OperatorRole { get; set; }

    /// <summary>中文模块名，如“成员管理”。</summary>
    [MaxLength(50)] public string Module { get; set; } = "";

    /// <summary>中文操作名，如“删除成员”。</summary>
    [MaxLength(100)] public string Action { get; set; } = "";

    /// <summary>原始控制器名，便于在目录调整后追溯。</summary>
    [MaxLength(50)] public string ControllerName { get; set; } = "";

    /// <summary>原始 action 方法名。</summary>
    [MaxLength(100)] public string ActionName { get; set; } = "";

    [MaxLength(10)] public string Method { get; set; } = "";

    [MaxLength(512)] public string? Path { get; set; }

    /// <summary>列表页摘要，如“删除成员：id=2023000001”。</summary>
    [MaxLength(500)] public string? Summary { get; set; }

    /// <summary>主路由参数，如 id=2023000001。</summary>
    [MaxLength(200)] public string? Target { get; set; }

    /// <summary>{ route, query, body } 脱敏后的 JSON。</summary>
    [MaxLength(8000)] public string? ChangeContent { get; set; }

    /// <summary>业务响应 message。</summary>
    [MaxLength(1000)] public string? Message { get; set; }

    /// <summary>异常信息（脱敏、截断）。</summary>
    [MaxLength(2000)] public string? ErrorMessage { get; set; }

    public bool Success { get; set; }

    public int HttpStatus { get; set; }

    public long DurationMs { get; set; }

    /// <summary>HttpContext.TraceIdentifier，便于与系统日志关联。</summary>
    [MaxLength(64)] public string? TraceId { get; set; }

    [MaxLength(45)] public string? IpAddress { get; set; }

    [MaxLength(512)] public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

using System.ComponentModel.DataAnnotations;

namespace iOSClub.Data.DTOs;

/// <summary>新建 / 编辑报销材料的请求体。</summary>
public class FinanceMaterialUpsertDTO
{
    [MaxLength(50, ErrorMessage = "活动名称不能超过 50 个字符")]
    public string? ActivityName { get; set; }

    public DateTime? ActivityDate { get; set; }

    [MaxLength(20)] public string? ActivityType { get; set; }

    [MaxLength(50)] public string? Location { get; set; }

    [Range(0, 100000, ErrorMessage = "参加人数需在 0-100000 之间")]
    public int? Headcount { get; set; }

    [MaxLength(20)] public string? Owner { get; set; }

    [MaxLength(14)] public string? Phone { get; set; }

    [MaxLength(200, ErrorMessage = "活动简介不能超过 200 个字符")]
    public string? Summary { get; set; }

    [Range(0, 100000000, ErrorMessage = "活动经费需在 0-100000000 之间")]
    public decimal ActivityBudget { get; set; }

    [MaxLength(200)] public string? Remark { get; set; }
}

/// <summary>保存发票校对结果。</summary>
public class FinanceInvoiceUpdateDTO
{
    [MaxLength(20)] public string? Type { get; set; }

    [MaxLength(20)] public string? Code { get; set; }

    [MaxLength(20)] public string? Number { get; set; }

    public DateTime? IssueDate { get; set; }

    [MaxLength(100)] public string? Buyer { get; set; }

    [MaxLength(100)] public string? Seller { get; set; }

    [MaxLength(100)] public string? Item { get; set; }

    [Range(0, 100000000)] public decimal? Qty { get; set; }

    [Range(0, 100000000)] public decimal? Price { get; set; }

    [MaxLength(10)] public string? Unit { get; set; }

    [Range(0, 100000000)] public decimal Amount { get; set; }

    [Range(0, 100000000)] public decimal Tax { get; set; }

    [Range(0, 100000000)] public decimal Total { get; set; }

    /// <summary>为 true 时把发票标记为"已确认"，可用于生成表单与报销。</summary>
    public bool Confirm { get; set; }
}

/// <summary>归集 / 解除归集发票。</summary>
public class FinanceAssignInvoicesDTO
{
    [Required, MinLength(1, ErrorMessage = "请选择发票")]
    public List<string> InvoiceIds { get; set; } = [];

    /// <summary>assign 归集；remove 解除归集。</summary>
    public string Action { get; set; } = "assign";
}

/// <summary>生成表单。kind：apply 经费申请表 / reimburse 经费报销单。</summary>
public class FinanceGenerateFormDTO
{
    [Required] public string Kind { get; set; } = "reimburse";
}

/// <summary>撤销报销，必须填写原因。</summary>
public class FinanceRevokeDTO
{
    [Required(ErrorMessage = "请填写撤销原因")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "撤销原因需在 2-200 个字符之间")]
    public string Reason { get; set; } = "";
}

/// <summary>设置学年经费。</summary>
public class FinanceAccountUpdateDTO
{
    [Required] public string AcademicYear { get; set; } = "";

    [Range(0, 100000000, ErrorMessage = "年度经费需在 0-100000000 之间")]
    public decimal BudgetAmount { get; set; }

    [MaxLength(200)] public string? Remark { get; set; }
}

/// <summary>批量导出筛选条件。Ids 为空时按其余条件筛选。</summary>
public class FinanceBatchExportDTO
{
    public List<string>? Ids { get; set; }

    [MaxLength(32)] public string? MaterialNo { get; set; }

    [MaxLength(50)] public string? ActivityName { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    [MaxLength(20)] public string? Status { get; set; }

    /// <summary>apply / reimburse，默认 reimburse。</summary>
    public string Kind { get; set; } = "reimburse";
}

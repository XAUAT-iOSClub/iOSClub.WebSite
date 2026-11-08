namespace iOSClub.Data.VOs;

/// <summary>通用分页结果。</summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

/// <summary>发票视图模型。金额单位为元，数据库内仍以分运算。</summary>
public class FinanceInvoiceVO
{
    public string Id { get; set; } = "";
    public string? MaterialId { get; set; }
    public string? MaterialNo { get; set; }
    public string? ActivityName { get; set; }

    /// <summary>所属报销材料的状态；未归集时为空。</summary>
    public string? MaterialStatus { get; set; }
    public string? MaterialStatusLabel { get; set; }

    public string? Type { get; set; }
    public string? Code { get; set; }
    public string? Number { get; set; }
    public DateTime? IssueDate { get; set; }
    public string? Buyer { get; set; }
    public string? Seller { get; set; }
    public string? Item { get; set; }
    public decimal? Qty { get; set; }
    public decimal? Price { get; set; }
    public string? Unit { get; set; }
    public decimal Amount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }

    /// <summary>字段名 → 置信度（0-1）。低于阈值的字段由前端标黄。</summary>
    public Dictionary<string, double> Confidence { get; set; } = new();

    public string Status { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public bool Confirmed { get; set; }
    public bool HasFile { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public string? RecognitionError { get; set; }
    public DateTime? RecognizedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>报销材料视图模型。</summary>
public class FinanceMaterialVO
{
    public string Id { get; set; } = "";
    public string? ActivityName { get; set; }
    public DateTime? ActivityDate { get; set; }
    public string? ActivityType { get; set; }
    public string? Location { get; set; }
    public int? Headcount { get; set; }
    public string? Owner { get; set; }
    public string? Phone { get; set; }
    public string? Summary { get; set; }
    public decimal ActivityBudget { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public string? Remark { get; set; }
    public string AcademicYear { get; set; } = "";
    public int FormVersion { get; set; }
    public bool HasApplyForm { get; set; }
    public bool HasReimburseForm { get; set; }
    public DateTime? GeneratedAt { get; set; }
    public int InvoiceCount { get; set; }
    public int ConfirmedInvoiceCount { get; set; }
    public decimal UnconfirmedAmount { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool CanRestore { get; set; }
    public string? RevokeReason { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>材料详情：材料信息 + 发票明细。</summary>
public class FinanceMaterialDetailVO : FinanceMaterialVO
{
    public List<FinanceInvoiceVO> Invoices { get; set; } = [];

    /// <summary>本次报销的预计扣减额 D = max(0, 合计 - 活动经费)。</summary>
    public decimal EstimatedDeduction { get; set; }
}

/// <summary>经费流水。</summary>
public class FinanceFlowVO
{
    public string Id { get; set; } = "";
    public string MaterialId { get; set; } = "";
    public string MaterialNo { get; set; } = "";
    public string? ActivityName { get; set; }
    public string AcademicYear { get; set; } = "";
    public decimal Amount { get; set; }
    public string Type { get; set; } = "";
    public string TypeLabel { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Reason { get; set; }
    public string? OperatorId { get; set; }
    public string? OperatorName { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>操作日志。</summary>
public class FinanceLogVO
{
    public string Id { get; set; } = "";
    public string? OperatorId { get; set; }
    public string? OperatorName { get; set; }
    public string? Role { get; set; }
    public string? RoleLabel { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public string Action { get; set; } = "";
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
    public string Result { get; set; } = "";
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>学年经费。</summary>
public class FinanceAccountVO
{
    public string Id { get; set; } = "";
    public string AcademicYear { get; set; } = "";
    public decimal BudgetAmount { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool Editable { get; set; }
}

/// <summary>经费看板。</summary>
public class FinanceFundSummaryVO
{
    public string AcademicYear { get; set; } = "";
    public decimal BudgetAmount { get; set; }
    public decimal UsedAmount { get; set; }
    public decimal InTransitAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal UsagePercent { get; set; }
    public string WarningLevel { get; set; } = "normal";
    public bool AccountExists { get; set; }
    public bool Editable { get; set; }
    public int ReimbursedCount { get; set; }
    public int PendingCount { get; set; }
    public List<FinanceFlowVO> RecentFlows { get; set; } = [];
}

/// <summary>表单模板。</summary>
public class FinanceTemplateVO
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string KindLabel { get; set; } = "";
    public int Version { get; set; }
    public string? OriginalFileName { get; set; }
    public bool Enabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>发票校验结果。Errors 阻断保存，Warnings 只提示。</summary>
public class FinanceValidationVO
{
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public bool IsBlocked => Errors.Count > 0;
}

/// <summary>发票保存结果：保存后的发票 + 校验提示。</summary>
public class FinanceInvoiceSaveResultVO
{
    public FinanceInvoiceVO? Invoice { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public bool Saved => Invoice != null && Errors.Count == 0;
}

/// <summary>前端启动时需要的财务模块元信息。</summary>
public class FinanceMetaVO
{
    public string ClubName { get; set; } = "";
    public string CurrentAcademicYear { get; set; } = "";
    public List<string> Years { get; set; } = [];
    public List<string> ActivityTypes { get; set; } = [];
    public double LowConfidenceThreshold { get; set; } = 0.8;
    public bool OcrConfigured { get; set; }
    public bool PdfConfigured { get; set; }
    public int MaxFileSizeMb { get; set; } = 10;
    public int MaxFilesPerUpload { get; set; } = 20;
    public List<string> SupportedExtensions { get; set; } = [];
}

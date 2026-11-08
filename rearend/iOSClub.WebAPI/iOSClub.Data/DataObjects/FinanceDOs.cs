using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace iOSClub.Data.DataObjects;

/// <summary>
/// 报销材料。一份材料 = 一次报销，主键即材料编号（如 CL-2026-0001）。
/// </summary>
[Table("finance_material")]
public class FinanceMaterialDO
{
    [Key, MaxLength(32), Column("id")]
    public string Id { get; set; } = "";

    [MaxLength(50), Column("activity_name")]
    public string? ActivityName { get; set; }

    [Column("activity_date")]
    public DateTime? ActivityDate { get; set; }

    [MaxLength(20), Column("activity_type")]
    public string? ActivityType { get; set; }

    [MaxLength(50), Column("location")]
    public string? Location { get; set; }

    [Column("headcount")]
    public int? Headcount { get; set; }

    [MaxLength(20), Column("owner")]
    public string? Owner { get; set; }

    [MaxLength(14), Column("phone")]
    public string? Phone { get; set; }

    [MaxLength(200), Column("summary")]
    public string? Summary { get; set; }

    /// <summary>活动经费（分）。确认报销时先抵扣本次开销，差额才从社团经费扣减。</summary>
    [Column("activity_budget_cents")]
    public long ActivityBudgetCents { get; set; }

    /// <summary>材料合计（分），由已确认发票明细汇总，不接受手工修改。</summary>
    [Column("total_amount_cents")]
    public long TotalAmountCents { get; set; }

    [MaxLength(20), Column("status")]
    public string Status { get; set; } = FinanceMaterialStatus.Pending;

    [MaxLength(200), Column("remark")]
    public string? Remark { get; set; }

    /// <summary>所属学年（按活动时间，缺省按创建时间）。用于经费看板与流水归集。</summary>
    [MaxLength(9), Column("academic_year")]
    public string AcademicYear { get; set; } = "";

    /// <summary>表单生成版本，每次生成 +1。</summary>
    [Column("form_version")]
    public int FormVersion { get; set; }

    [MaxLength(512), Column("apply_form_path")]
    public string? ApplyFormPath { get; set; }

    [MaxLength(512), Column("reimburse_form_path")]
    public string? ReimburseFormPath { get; set; }

    [Column("generated_at")]
    public DateTime? GeneratedAt { get; set; }

    [Column("confirmed_at")]
    public DateTime? ConfirmedAt { get; set; }

    [MaxLength(36), Column("confirmed_by")]
    public string? ConfirmedBy { get; set; }

    [MaxLength(200), Column("revoke_reason")]
    public string? RevokeReason { get; set; }

    [MaxLength(36), Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    public List<FinanceInvoiceDO> Invoices { get; set; } = [];
}

/// <summary>发票。MaterialId 为空表示仍在待归档池。</summary>
[Table("finance_invoice")]
public class FinanceInvoiceDO
{
    [Key, MaxLength(36), Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [MaxLength(32), Column("material_id")]
    public string? MaterialId { get; set; }

    [MaxLength(20), Column("type")]
    public string? Type { get; set; }

    [MaxLength(20), Column("code")]
    public string? Code { get; set; }

    [MaxLength(20), Column("number")]
    public string? Number { get; set; }

    [Column("issue_date")]
    public DateTime? IssueDate { get; set; }

    [MaxLength(100), Column("buyer")]
    public string? Buyer { get; set; }

    [MaxLength(100), Column("seller")]
    public string? Seller { get; set; }

    /// <summary>项目名称，对应报销单"物品名称"。</summary>
    [MaxLength(100), Column("item")]
    public string? Item { get; set; }

    [Column("qty")]
    public decimal? Qty { get; set; }

    /// <summary>单价（分）。</summary>
    [Column("price_cents")]
    public long? PriceCents { get; set; }

    [MaxLength(10), Column("unit")]
    public string? Unit { get; set; }

    /// <summary>金额（不含税，分）。</summary>
    [Column("amount_cents")]
    public long AmountCents { get; set; }

    /// <summary>税额（分）。</summary>
    [Column("tax_cents")]
    public long TaxCents { get; set; }

    /// <summary>价税合计（分）。</summary>
    [Column("total_cents")]
    public long TotalCents { get; set; }

    /// <summary>各字段置信度 JSON，键为字段名，值为 0-1。</summary>
    [MaxLength(4000), Column("confidence_json")]
    public string? ConfidenceJson { get; set; }

    [MaxLength(512), Column("file_path")]
    public string? FilePath { get; set; }

    [MaxLength(255), Column("original_file_name")]
    public string? OriginalFileName { get; set; }

    [MaxLength(100), Column("content_type")]
    public string? ContentType { get; set; }

    [Column("file_size")]
    public long FileSize { get; set; }

    [MaxLength(20), Column("status")]
    public string Status { get; set; } = FinanceInvoiceStatus.Pending;

    [MaxLength(500), Column("recognition_error")]
    public string? RecognitionError { get; set; }

    [Column("recognized_at")]
    public DateTime? RecognizedAt { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [MaxLength(36), Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public FinanceMaterialDO? Material { get; set; }
}

/// <summary>学年经费。同一学年一条记录，历史学年只读。</summary>
[Table("finance_account")]
public class FinanceAccountDO
{
    [Key, MaxLength(36), Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>学年，如 2026-2027。</summary>
    [MaxLength(9), Column("academic_year")]
    public string AcademicYear { get; set; } = "";

    [Column("budget_amount_cents")]
    public long BudgetAmountCents { get; set; }

    [MaxLength(200), Column("remark")]
    public string? Remark { get; set; }

    [MaxLength(36), Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>经费流水。扣减与回滚都留痕；撤销时原扣减流水作废。</summary>
[Table("finance_flow")]
public class FinanceFlowDO
{
    [Key, MaxLength(36), Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [MaxLength(32), Column("material_id")]
    public string MaterialId { get; set; } = "";

    [MaxLength(32), Column("material_no")]
    public string MaterialNo { get; set; } = "";

    [MaxLength(50), Column("activity_name")]
    public string? ActivityName { get; set; }

    [MaxLength(9), Column("academic_year")]
    public string AcademicYear { get; set; } = "";

    /// <summary>流水金额（分），恒为正数；方向由 Type 表达。</summary>
    [Column("amount_cents")]
    public long AmountCents { get; set; }

    [MaxLength(10), Column("type")]
    public string Type { get; set; } = FinanceFlowType.Deduct;

    [MaxLength(10), Column("status")]
    public string Status { get; set; } = FinanceFlowStatus.Active;

    [MaxLength(200), Column("reason")]
    public string? Reason { get; set; }

    [MaxLength(36), Column("operator_id")]
    public string? OperatorId { get; set; }

    [MaxLength(50), Column("operator_name")]
    public string? OperatorName { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>财务模块操作日志，只追加，不提供修改与删除接口。</summary>
[Table("finance_log")]
public class FinanceLogDO
{
    [Key, MaxLength(36), Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [MaxLength(36), Column("operator_id")]
    public string? OperatorId { get; set; }

    [MaxLength(50), Column("operator_name")]
    public string? OperatorName { get; set; }

    [MaxLength(20), Column("role")]
    public string? Role { get; set; }

    [MaxLength(64), Column("ip")]
    public string? Ip { get; set; }

    [MaxLength(300), Column("user_agent")]
    public string? UserAgent { get; set; }

    [MaxLength(50), Column("action")]
    public string Action { get; set; } = "";

    [MaxLength(20), Column("target_type")]
    public string? TargetType { get; set; }

    [MaxLength(64), Column("target_id")]
    public string? TargetId { get; set; }

    [MaxLength(4000), Column("before_json")]
    public string? BeforeJson { get; set; }

    [MaxLength(4000), Column("after_json")]
    public string? AfterJson { get; set; }

    [MaxLength(20), Column("result")]
    public string Result { get; set; } = "success";

    [MaxLength(500), Column("message")]
    public string? Message { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>两份学校表单的 docx 模板。同一类型只有一个启用版本。</summary>
[Table("finance_template")]
public class FinanceTemplateDO
{
    [Key, MaxLength(36), Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>apply / reimburse</summary>
    [MaxLength(20), Column("kind")]
    public string Kind { get; set; } = FinanceFormKind.Apply;

    [Column("version")]
    public int Version { get; set; }

    [MaxLength(512), Column("file_path")]
    public string FilePath { get; set; } = "";

    [MaxLength(255), Column("original_file_name")]
    public string? OriginalFileName { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; }

    [MaxLength(36), Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

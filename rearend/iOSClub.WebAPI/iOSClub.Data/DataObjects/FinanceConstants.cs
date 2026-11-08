namespace iOSClub.Data.DataObjects;

/// <summary>
/// 财务模块允许的角色。财务模块不新建身份，直接复用网站 JWT 中的 role 声明。
/// </summary>
public static class FinanceRoles
{
    public const string Allowed = "Founder,President,Minister";

    public static bool IsAllowed(string? role) => role is "Founder" or "President" or "Minister";

    public static string Label(string? role) => role switch
    {
        "Founder" => "创始人",
        "President" => "社长",
        "Minister" => "部长",
        _ => "其他"
    };
}

/// <summary>报销材料状态。财务模块只有待报销 / 已报销 / 已删除三种状态。</summary>
public static class FinanceMaterialStatus
{
    public const string Pending = "pending";
    public const string Reimbursed = "reimbursed";
    public const string Deleted = "deleted";

    public static bool IsValid(string? status) => status is Pending or Reimbursed or Deleted;

    public static string Label(string? status) => status switch
    {
        Pending => "待报销",
        Reimbursed => "已报销",
        Deleted => "已删除",
        _ => status ?? ""
    };
}

/// <summary>发票状态：待识别 → 已识别（待归档）→ 已确认；识别失败保留原件转手工录入。</summary>
public static class FinanceInvoiceStatus
{
    public const string Pending = "pending";
    public const string Recognized = "recognized";
    public const string Confirmed = "confirmed";
    public const string Failed = "failed";

    public static bool IsValid(string? status) => status is Pending or Recognized or Confirmed or Failed;

    public static string Label(string? status) => status switch
    {
        Pending => "待识别",
        Recognized => "待归档",
        Confirmed => "已确认",
        Failed => "识别失败",
        _ => status ?? ""
    };
}

/// <summary>经费流水类型。</summary>
public static class FinanceFlowType
{
    public const string Deduct = "deduct";
    public const string Rollback = "rollback";

    public static string Label(string? type) => type == Rollback ? "回滚" : "扣减";
}

/// <summary>经费流水状态。撤销报销时原扣减流水作废，并补一条回滚流水留存。</summary>
public static class FinanceFlowStatus
{
    public const string Active = "active";
    public const string Voided = "voided";
}

/// <summary>两份固定表单的类型。</summary>
public static class FinanceFormKind
{
    public const string Apply = "apply";
    public const string Reimburse = "reimburse";

    public static bool IsValid(string? kind) => kind is Apply or Reimburse;

    public static string Label(string? kind) => kind == Apply ? "经费申请表" : "经费报销单";

    public static string FileTag(string? kind) => kind == Apply ? "经费申请表" : "经费报销单";
}

/// <summary>操作日志的对象类型。</summary>
public static class FinanceTargetType
{
    public const string Material = "material";
    public const string Invoice = "invoice";
    public const string Account = "account";
    public const string Template = "template";
    public const string Flow = "flow";
}

/// <summary>关键操作动作名。集中定义，避免日志查询与写入出现拼写漂移。</summary>
public static class FinanceActions
{
    public const string UploadInvoice = "上传发票";
    public const string RecognizeInvoice = "识别发票";
    public const string UpdateInvoice = "校对发票";
    public const string ConfirmInvoice = "确认发票";
    public const string DeleteInvoice = "删除发票";
    public const string CreateMaterial = "新建材料";
    public const string UpdateMaterial = "编辑材料";
    public const string AssignInvoice = "归集发票";
    public const string RemoveInvoice = "解除归集";
    public const string GenerateForm = "生成表单";
    public const string ConfirmMaterial = "确认已报销";
    public const string RevokeMaterial = "撤销报销";
    public const string DeleteMaterial = "删除材料";
    public const string RestoreMaterial = "恢复材料";
    public const string ExportMaterial = "导出材料";
    public const string BatchExport = "批量导出";
    public const string UpdateAccount = "修改年度经费";
    public const string UploadTemplate = "上传模板";
    public const string EnableTemplate = "启用模板";
    public const string DeleteTemplate = "删除模板";
    public const string UnauthorizedAttempt = "越权访问尝试";
}

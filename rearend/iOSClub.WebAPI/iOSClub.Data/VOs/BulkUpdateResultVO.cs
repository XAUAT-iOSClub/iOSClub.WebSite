namespace iOSClub.Data.VOs;

/// <summary>
/// 批量更新成员的结果：逐行区分新增、更新、跳过与失败。
/// <para>
/// 旧实现只返回一个 bool，被校验规则过滤掉的行与真正写入的行无法区分，
/// 整批都被过滤时调用方仍会收到"成功"。这里把每类结果与未写入行的原因都显式带回，
/// 前端按 <see cref="Issues"/> 展示被跳过或失败的具体学号。
/// </para>
/// </summary>
[Serializable]
public class BulkUpdateResultVO
{
    /// <summary>
    /// 本次新增的学生数
    /// </summary>
    public int Created { get; set; }

    /// <summary>
    /// 本次更新已有学生档案的数量
    /// </summary>
    public int Updated { get; set; }

    /// <summary>
    /// 在校验阶段被过滤、没有进入写库流程的行数
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// 进入写库流程但保存失败的行数
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// 未写入行的明细：被跳过的校验原因或保存失败原因
    /// </summary>
    public List<BulkUpdateIssueVO> Issues { get; set; } = [];
}

/// <summary>
/// 批量更新中未写入的一行及其原因
/// </summary>
[Serializable]
public class BulkUpdateIssueVO
{
    /// <summary>
    /// 校验阶段被过滤（学号为空、手机号格式不正确等）
    /// </summary>
    public const string KindSkipped = "skipped";

    /// <summary>
    /// 已通过校验但保存到数据库失败
    /// </summary>
    public const string KindFailed = "failed";

    /// <summary>
    /// 该行的学号（被校验过滤时保持请求里的原值）
    /// </summary>
    public string UserId { get; set; } = "";

    /// <summary>
    /// 未写入类型：<see cref="KindSkipped"/> 或 <see cref="KindFailed"/>
    /// </summary>
    public string Kind { get; set; } = KindSkipped;

    /// <summary>
    /// 未写入原因，可直接展示给管理员
    /// </summary>
    public string Reason { get; set; } = "";
}

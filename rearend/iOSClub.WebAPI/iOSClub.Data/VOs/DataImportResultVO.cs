namespace iOSClub.Data.VOs;

/// <summary>
/// 备份数据导入的结果统计
/// </summary>
[Serializable]
public class DataImportResultVO
{
    /// <summary>
    /// 库中原本没有、本次插入的记录数
    /// </summary>
    public int Added { get; set; }

    /// <summary>
    /// 库中已存在、本次就地更新的记录数
    /// </summary>
    public int Updated { get; set; }

    /// <summary>
    /// 跳过的记录数（缺少主键，或本接口不导入的内容）
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// 累加另一批数据的导入统计
    /// </summary>
    /// <param name="other">另一批数据的导入统计</param>
    public void Add(DataImportResultVO other)
    {
        Added += other.Added;
        Updated += other.Updated;
        Skipped += other.Skipped;
    }

    public override string ToString()
    {
        var text = $"新增 {Added} 条，更新 {Updated} 条";
        return Skipped > 0 ? $"{text}，跳过 {Skipped} 条" : text;
    }
}

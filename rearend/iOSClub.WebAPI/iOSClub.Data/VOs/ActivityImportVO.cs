namespace iOSClub.Data.VOs;

/// <summary>Excel 导入中的一条失败记录（用于预览与错误报告）。</summary>
public class ActivityImportErrorVO
{
    public int RowNumber { get; set; }
    public string Name { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string Academy { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string Reason { get; set; } = "";
}
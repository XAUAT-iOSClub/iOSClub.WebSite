namespace iOSClub.Data.VOs;

public class ActivityParticipantVO
{
    public string Id { get; set; } = "";
    public string ActivityId { get; set; } = "";
    public string Name { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string Academy { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ActivityParticipantPageVO
{
    public List<ActivityParticipantVO> Items { get; set; } = [];
    public int Total { get; set; }
}

/// <summary>Excel 导入预览/结果。</summary>
public class ActivityImportResultVO
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public List<string> Errors { get; set; } = [];
    /// <summary>校验通过、可导入的行（预览时用）。</summary>
    public List<ActivityParticipantVO> PreviewItems { get; set; } = [];
}
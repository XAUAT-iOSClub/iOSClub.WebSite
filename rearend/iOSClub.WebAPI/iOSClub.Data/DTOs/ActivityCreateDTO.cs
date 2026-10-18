namespace iOSClub.Data.DTOs;

public class ActivityCreateDTO
{
    public string Name { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Location { get; set; } = "";
    public string? Description { get; set; }
}

/// <summary>更新字段与创建一致，Id 走路由。</summary>
public class ActivityUpdateDTO : ActivityCreateDTO { }
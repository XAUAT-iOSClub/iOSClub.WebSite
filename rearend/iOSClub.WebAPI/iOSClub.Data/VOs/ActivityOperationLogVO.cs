namespace iOSClub.Data.VOs;

public class ActivityOperationLogVO
{
    public long Id { get; set; }
    public string? ActivityId { get; set; }
    public string? OperatorId { get; set; }
    public string Action { get; set; } = "";
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; }
}
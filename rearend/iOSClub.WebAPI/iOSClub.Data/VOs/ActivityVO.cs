namespace iOSClub.Data.VOs;

public class ActivityVO
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Location { get; set; } = "";
    public string? Description { get; set; }
    public string Status { get; set; } = "";
    public bool SelfRegistrationEnabled { get; set; }
    public string? CreatedBy { get; set; }
    public int ParticipantCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
using System.ComponentModel.DataAnnotations;

namespace iOSClub.Data.DTOs;

public class StaffCreateDTO
{
    [Required][MaxLength(10)] public string UserId { get; set; } = "";
    [Required][MaxLength(50)] public string Name { get; set; } = "";
    [MaxLength(20)]
    [AllowedValues("Member", "Department", "Minister", "President", "Founder")]
    public string Identity { get; set; } = "Member";
    [MaxLength(20)] public string? DepartmentName { get; set; }
}

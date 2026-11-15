using System.ComponentModel.DataAnnotations;

namespace iOSClub.Data.DataObjects;

public enum ApiKeyPermission
{
    ReadOnly = 1,
    ReadWrite = 2
}

public sealed class ApiKeyDO
{
    public Guid Id { get; init; }
    [MaxLength(100)]
    public required string Name { get; init; }
    [MaxLength(32)]
    public required byte[] SecretHash { get; init; }
    public ApiKeyPermission Permission { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

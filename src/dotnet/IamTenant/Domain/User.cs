using Shared.Entity;
namespace IamTenant.Domain;

public class User : TenantAuditableEntity
{
    public string CognitoSub { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    // Optional properties for specific staff/users
    public string? StaffCode { get; set; }
    public string? Department { get; set; }

    public Enums.UserType UserType { get; set; } = Enums.UserType.TenantStaff;
    public Enums.UserStatus Status { get; set; } = Enums.UserStatus.Invited;
    public Enums.StaffType StaffType {get; set;} = Enums.StaffType.Normal;

    public int PermissionVersion { get; set; } = 1;

    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsDeleted { get; private set; }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }

    public Tenant? Tenant { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = [];
}

using shared.Entity;
namespace iam_tennant.Domain;

public class User : IAuditableEntity, ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // Nullable for System Admin (who don't belong to a specific tenant but manage them all)
    public Guid TenantId { get; set; }
    
    public string CognitoSub { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    
    // Optional properties for specific staff/users
    public string? StaffCode { get; set; } 
    public string? Department { get; set; }

    public string UserType { get; set; } = string.Empty; // SYSTEM_ADMIN, TENANT_ADMIN, TENANT_STAFF
    public string Status { get; set; } = "INVITED"; // INVITED, ACTIVE, SUSPENDED, BLOCKED

    public bool IsDeleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}


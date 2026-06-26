using Shared.Entity;
namespace IamTenant.Domain;

public class Role : TenantAuditableEntity
{

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public bool IsSystemRole { get; set; } // If true, tenant admins cannot edit/delete this role


    public Tenant? Tenant { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}


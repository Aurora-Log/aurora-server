namespace IamTenant.Domain;

using Shared.Entity;

public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty; // e.g. "users:create", "routes:view"
    public string Module { get; set; } = string.Empty; // e.g. "IAM", "Routing"
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

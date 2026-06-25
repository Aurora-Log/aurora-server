namespace iam_tennant.Domain;

public class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty; // e.g. "users:create", "routes:view"
    public string Module { get; set; } = string.Empty; // e.g. "IAM", "Routing"
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

namespace IamTenant.Application.DTOs.Roles;

public class RoleDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public List<string> PermissionIds { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}

public class PermissionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UserPermissionsDto
{
    public Guid UserId { get; set; }
    public List<string> RoleIds { get; set; } = [];
    public List<PermissionDto> Permissions { get; set; } = [];
    public int Version { get; set; }
    public bool FromCache { get; set; }
}

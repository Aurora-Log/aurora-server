using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Roles;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Cache;

namespace IamTenant.Application.Commands.Permissions;

// ─────────────────────────────────────────────────────────────────────────────
// ASSIGN PERMISSIONS TO ROLE
// Sau khi assign, tăng permission_version cho tất cả user có role này
// và invalidate Redis cache của họ.
// ─────────────────────────────────────────────────────────────────────────────
public record AssignPermissionsToRoleCommand(Guid RoleId, List<Guid> PermissionIds) : IRequest<RoleDto>;

public class AssignPermissionsToRoleHandler(
    IamTenantDbContext context,
    IPermissionCacheService permissionCache)
    : IRequestHandler<AssignPermissionsToRoleCommand, RoleDto>
{
    public async Task<RoleDto> Handle(AssignPermissionsToRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new Exception("Role not found.");

        // Replace all current permissions
        context.RolePermissions.RemoveRange(role.RolePermissions);

        foreach (var permId in request.PermissionIds)
        {
            var permExists = await context.Permissions.AnyAsync(p => p.Id == permId, cancellationToken);
            if (!permExists) throw new Exception($"Permission {permId} not found.");

            role.RolePermissions.Add(new IamTenant.Domain.RolePermission
            {
                RoleId = role.Id,
                PermissionId = permId
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        // Invalidate Redis cache cho tất cả user có role này
        var affectedUserIds = await context.UserRoles
            .Where(ur => ur.RoleId == request.RoleId)
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);

        foreach (var userId in affectedUserIds)
        {
            await permissionCache.InvalidateAsync(userId, cancellationToken);
        }

        return new RoleDto
        {
            Id = role.Id,
            TenantId = role.TenantId,
            Code = role.Code,
            Name = role.Name,
            Description = role.Description,
            IsSystemRole = role.IsSystemRole,
            PermissionIds = role.RolePermissions.Select(rp => rp.PermissionId.ToString()).ToList(),
            CreatedAt = role.CreatedAt
        };
    }
}

using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Cache;
using Shared.Security;

namespace IamTenant.Application.Commands.Users;

// ─────────────────────────────────────────────────────────────────────────────
// ASSIGN ROLES TO USER
// Sau khi assign, invalidate Redis cache của user đó.
// ─────────────────────────────────────────────────────────────────────────────
public record AssignRolesCommand(Guid UserId, List<Guid> RoleIds) : IRequest<StaffDto>;

public class AssignRolesHandler(
    IamTenantDbContext context,
    IPermissionCacheService permissionCache)
    : IRequestHandler<AssignRolesCommand, StaffDto>
{
    public async Task<StaffDto> Handle(AssignRolesCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new Exception("User not found.");

        // Remove existing roles
        var existingUserRoles = context.UserRoles.Where(ur => ur.UserId == user.Id);
        context.UserRoles.RemoveRange(existingUserRoles);

        foreach (var roleId in request.RoleIds)
        {
            var roleExists = await context.Roles.AnyAsync(r => r.Id == roleId, cancellationToken);
            if (!roleExists) throw new Exception($"Role {roleId} not found.");

            context.UserRoles.Add(new Domain.UserRole
            {
                UserId = user.Id,
                RoleId = roleId
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        // Invalidate Redis — next request will re-build from DB
        await permissionCache.InvalidateAsync(user.Id, cancellationToken);

        return new StaffDto
        {
            Id = user.Id,
            TenantId = user.TenantId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserType = user.UserType,
            Status = user.Status,
            CreatedAt = user.CreatedAt
        };
    }
}

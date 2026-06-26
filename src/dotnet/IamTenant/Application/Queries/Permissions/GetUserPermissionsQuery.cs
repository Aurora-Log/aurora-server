using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Roles;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Cache;
using Shared.Security;

namespace IamTenant.Application.Queries.Permissions;

// ─────────────────────────────────────────────────────────────────────────────
// GET USER PERMISSIONS — Redis-first, DB fallback, version check
// ─────────────────────────────────────────────────────────────────────────────
public record GetUserPermissionsQuery(Guid UserId, int? JwtPermissionVersion = null) : IRequest<UserPermissionsDto>;

public class GetUserPermissionsHandler(
    IamTenantDbContext context,
    IPermissionCacheService permissionCache)
    : IRequestHandler<GetUserPermissionsQuery, UserPermissionsDto>
{
    public async Task<UserPermissionsDto> Handle(GetUserPermissionsQuery request, CancellationToken cancellationToken)
    {
        // 1. Thử đọc từ Redis
        var cached = await permissionCache.GetAsync(request.UserId, cancellationToken);

        if (cached is not null && (request.JwtPermissionVersion is null || cached.Version == request.JwtPermissionVersion))
        {
            return new UserPermissionsDto
            {
                UserId = request.UserId,
                RoleIds = cached.RoleIds,
                Permissions = [.. cached.Permissions.Select(p => new PermissionDto { Code = p })],
                Version = cached.Version,
                FromCache = true
            };
        }

        // 2. Cache miss hoặc version lệch — query từ DB
        var user = await context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null) throw new Shared.Exceptions.NotFoundException("User not found");

        var roleIds = user.UserRoles.Select(ur => ur.RoleId.ToString()).ToList();
        var permissions = user.UserRoles
            .Where(ur => ur.Role != null)
            .SelectMany(ur => ur.Role!.RolePermissions.Select(rp => rp.Permission))
            .Where(p => p != null)
            .DistinctBy(p => p!.Id)
            .ToList();

        // 3. Update Redis
        var newCache = new UserPermissionCache
        {
            Version = user.PermissionVersion,
            RoleIds = roleIds,
            Permissions = permissions.Select(p => p!.Code).ToList()
        };
        await permissionCache.SetAsync(request.UserId, newCache, cancellationToken);

        return new UserPermissionsDto
        {
            UserId = request.UserId,
            RoleIds = roleIds,
            Permissions = [.. permissions.Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Module = p.Module,
                Description = p.Description
            })],
            Version = user.PermissionVersion,
            FromCache = false
        };
    }
}

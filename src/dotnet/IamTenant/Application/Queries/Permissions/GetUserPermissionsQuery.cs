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
                RoleCodes = cached.RoleCodes,
                Permissions = [.. cached.Permissions.Select(p => new PermissionDto { Code = p })],
                Version = cached.Version,
                FromCache = true
            };
        }

        // 2. Cache miss hoặc version lệch — query từ DB
        var user = await context.Users
            .Where(u => u.Id == request.UserId)
            .Select(u => new
            {
                u.PermissionVersion,
                RoleIds = u.UserRoles.Select(ur => ur.RoleId.ToString()).ToList(),
                RoleCodes = u.UserRoles.Select(ur => ur.Role!.Code).ToList(),
                Permissions = u.UserRoles
                    .SelectMany(ur => ur.Role!.RolePermissions.Select(rp => rp.Permission))
                    .Where(p => p != null)
                    .Select(p => new PermissionDto
                    {
                        Id = p!.Id,
                        Code = p.Code,
                        Module = p.Module,
                        Description = p.Description
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new Shared.Exceptions.NotFoundException("User not found");
        
        var permissions = user.Permissions.DistinctBy(p => p.Id).ToList();

        // 3. Update Redis
        var newCache = new UserPermissionCache
        {
            Version = user.PermissionVersion,
            RoleIds = user.RoleIds,
            RoleCodes = user.RoleCodes,
            Permissions = [.. permissions.Select(p => p.Code)]
        };
        await permissionCache.SetAsync(request.UserId, newCache, cancellationToken);

        return new UserPermissionsDto
        {
            UserId = request.UserId,
            RoleIds = user.RoleIds,
            RoleCodes = user.RoleCodes,
            Permissions = permissions,
            Version = user.PermissionVersion,
            FromCache = false
        };
    }
}

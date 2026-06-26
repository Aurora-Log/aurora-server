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
                Permissions = cached.Permissions.Select(p => new PermissionDto { Code = p }).ToList(),
                Version = cached.Version,
                FromCache = true
            };
        }

        // 2. Cache miss hoặc version lệch — query từ DB
        var userRoles = await context.UserRoles
            .Where(ur => ur.UserId == request.UserId)
            .Include(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .ToListAsync(cancellationToken);

        var roleIds = userRoles.Select(ur => ur.RoleId.ToString()).ToList();
        var permissions = userRoles
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission))
            .DistinctBy(p => p.Id)
            .ToList();

        // Tính version mới = tổng hash đơn giản (thực tế nên lưu riêng trong bảng User)
        var newVersion = (cached?.Version ?? 0) + 1;

        // 3. Update Redis
        var newCache = new UserPermissionCache
        {
            Version = newVersion,
            RoleIds = roleIds,
            Permissions = permissions.Select(p => p.Code).ToList()
        };
        await permissionCache.SetAsync(request.UserId, newCache, cancellationToken);

        return new UserPermissionsDto
        {
            UserId = request.UserId,
            RoleIds = roleIds,
            Permissions = permissions.Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Module = p.Module,
                Description = p.Description
            }).ToList(),
            Version = newVersion,
            FromCache = false
        };
    }
}

using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Roles;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Pagination;

namespace IamTenant.Application.Queries.Roles;

// ─────────────────────────────────────────────────────────────────────────────
// GET ONE ROLE
// ─────────────────────────────────────────────────────────────────────────────
public record GetRoleQuery(Guid Id) : IRequest<RoleDto>;

public class GetRoleHandler(IamTenantDbContext context) : IRequestHandler<GetRoleQuery, RoleDto>
{
    public async Task<RoleDto> Handle(GetRoleQuery request, CancellationToken cancellationToken)
    {
        var role = await context.Roles
            .AsNoTracking()
            .Where(r => r.Id == request.Id)
            .Select(r => new RoleDto
            {
                Id = r.Id,
                TenantId = r.TenantId,
                Code = r.Code,
                Name = r.Name,
                Description = r.Description,
                IsSystemRole = r.IsSystemRole,
                PermissionIds = r.RolePermissions.Select(rp => rp.PermissionId.ToString()).ToList(),
                CreatedAt = r.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new Exception("Role not found.");

        return role;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// LIST ROLES (PAGINATED)
// Global Query Filter đã lọc theo TenantId — không cần .Where thêm
// ─────────────────────────────────────────────────────────────────────────────
public class ListRolesQuery : PagedRequest, IRequest<PagedResult<RoleDto>> { }

public class ListRolesHandler(IamTenantDbContext context) : IRequestHandler<ListRolesQuery, PagedResult<RoleDto>>
{
    public async Task<PagedResult<RoleDto>> Handle(ListRolesQuery request, CancellationToken cancellationToken)
    {
        var query = context.Roles
            .OrderBy(r => r.IsSystemRole ? 0 : 1)
            .ThenBy(r => r.Name)
            .Select(r => new RoleDto
            {
                Id = r.Id,
                TenantId = r.TenantId,
                Code = r.Code,
                Name = r.Name,
                Description = r.Description,
                IsSystemRole = r.IsSystemRole,
                PermissionIds = r.RolePermissions.Select(rp => rp.PermissionId.ToString()).ToList(),
                CreatedAt = r.CreatedAt
            });

        return await query.ToPagedResultAsync(request, cancellationToken);
    }
}

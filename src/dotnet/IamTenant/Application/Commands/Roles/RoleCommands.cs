using IamTenant.Domain;
using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Roles;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Security;

namespace IamTenant.Application.Commands.Roles;

// ─────────────────────────────────────────────────────────────────────────────
// CREATE CUSTOM ROLE
// ─────────────────────────────────────────────────────────────────────────────
public record CreateCustomRoleCommand(string Code, string Name, string? Description) : IRequest<RoleDto>;

public class CreateCustomRoleHandler(IamTenantDbContext context, ICurrentUserService currentUser)
    : IRequestHandler<CreateCustomRoleCommand, RoleDto>
{
    public async Task<RoleDto> Handle(CreateCustomRoleCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue)
            throw new UnauthorizedAccessException("TenantId is required to create a custom role.");

        var exists = await context.Roles.AnyAsync(
            r => r.Code == request.Code && r.TenantId == currentUser.TenantId, cancellationToken);
        if (exists)
            throw new Exception($"Role '{request.Code}' already exists in this tenant.");

        var role = new Role
        {
            TenantId = currentUser.TenantId.Value,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            IsSystemRole = false
        };

        context.Roles.Add(role);
        await context.SaveChangesAsync(cancellationToken);

        return MapToDto(role, []);
    }

    private static RoleDto MapToDto(Role r, List<string> permIds) => new()
    {
        Id = r.Id,
        TenantId = r.TenantId,
        Code = r.Code,
        Name = r.Name,
        Description = r.Description,
        IsSystemRole = r.IsSystemRole,
        PermissionIds = permIds,
        CreatedAt = r.CreatedAt
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// UPDATE ROLE
// ─────────────────────────────────────────────────────────────────────────────
public record UpdateRoleCommand(Guid Id, string Name, string? Description) : IRequest<RoleDto>;

public class UpdateRoleHandler(IamTenantDbContext context )
    : IRequestHandler<UpdateRoleCommand, RoleDto>
{
    public async Task<RoleDto> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new Exception("Role not found.");

        if (role.IsSystemRole)
            throw new Exception("System roles cannot be modified.");

        role.Name = request.Name;
        role.Description = request.Description;

        await context.SaveChangesAsync(cancellationToken);

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

// ─────────────────────────────────────────────────────────────────────────────
// DELETE ROLE
// ─────────────────────────────────────────────────────────────────────────────
public record DeleteRoleCommand(Guid Id) : IRequest;

public class DeleteRoleHandler(IamTenantDbContext context)
    : IRequestHandler<DeleteRoleCommand>
{
    public async Task Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await context.Roles.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new Exception("Role not found.");

        if (role.IsSystemRole)
            throw new Exception("System roles cannot be deleted.");

        context.Roles.Remove(role);
        await context.SaveChangesAsync(cancellationToken);
    }
}

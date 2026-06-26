using IamTenant.Domain;
using IamTenant.Domain.Constants;
using IamTenant.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;

namespace IamTenant.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedPermissionsAndRolesAsync(IamTenantDbContext context, Guid tenantId)
    {
        // 1. Seed All Possible Permissions into the DB
        var allPermissionCodes = PermissionConstants.GetAllPermissions();

        foreach (var code in allPermissionCodes)
        {
            var exists = await context.Permissions.AnyAsync(p => p.Code == code);
            if (!exists)
            {
                var parts = code.Split(':');
                context.Permissions.Add(new Permission
                {
                    Id = Guid.CreateVersion7(),
                    Code = code,
                    Module = parts[0],
                    Description = $"Allows {parts[1]} operation on {parts[0]}"
                });
            }
        }
        await context.SaveChangesAsync();

        // 2. Setup Default Roles for Tenant
        var allPermissionsDb = await context.Permissions.ToListAsync();

        // Tenant Admin Role (Has all permissions)
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Code == "TENANT_ADMIN");
        if (adminRole == null)
        {
            adminRole = new Role
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                Code = "TENANT_ADMIN",
                Name = "Tenant Admin",
                Description = "Administrator with full access to all services",
                IsSystemRole = true
            };
            context.Roles.Add(adminRole);

            // Assign ALL permissions
            foreach (var perm in allPermissionsDb)
            {
                context.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionId = perm.Id });
            }
        }

        // Default Staff Role (Has only create and read permissions)
        var staffRole = await context.Roles.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Code == "STAFF");
        if (staffRole == null)
        {
            staffRole = new Role
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                Code = "STAFF",
                Name = "Default Staff",
                Description = "Standard staff role with view and create permissions",
                IsSystemRole = true
            };
            context.Roles.Add(staffRole);

            // Assign only default staff permissions (create, read)
            var defaultStaffCodes = PermissionConstants.GetDefaultStaffPermissions();
            var staffPermissions = allPermissionsDb.Where(p => defaultStaffCodes.Contains(p.Code)).ToList();

            foreach (var perm in staffPermissions)
            {
                context.RolePermissions.Add(new RolePermission { RoleId = staffRole.Id, PermissionId = perm.Id });
            }
        }

        await context.SaveChangesAsync();
    }
}

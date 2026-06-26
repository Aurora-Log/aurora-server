using Shared.Entity;
using Shared.Interceptors;
using Shared.Security;
using IamTenant.Domain;
using Microsoft.EntityFrameworkCore;

namespace IamTenant.Infrastructure.Persistences;

public class IamTenantDbContext : DbContext
{
    private readonly ICurrentUserService _currentUser;
    private readonly AuditSaveChangesInterceptor _auditInterceptor;

    public IamTenantDbContext(
        DbContextOptions<IamTenantDbContext> options,
        ICurrentUserService currentUser,
        AuditSaveChangesInterceptor auditInterceptor) : base(options)
    {
        _currentUser = currentUser;
        _auditInterceptor = auditInterceptor;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_auditInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ============================================================
        // GLOBAL QUERY FILTERS — tham chiếu trực tiếp qua _currentUser
        // (KHÔNG dùng local variable - EF Core sẽ capture đúng per-request)
        // ============================================================

        modelBuilder.Entity<User>(e =>
        {
            // Filter: chỉ lấy User thuộc TenantId hiện tại (nếu có) và chưa bị xoá
            e.HasQueryFilter(u =>
                (!_currentUser.TenantId.HasValue || u.TenantId == _currentUser.TenantId)
                && !u.IsDeleted);

            e.HasIndex(u => u.CognitoSub).IsUnique();
            e.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();

            // MaxLength để tránh Postgres tạo ra cột 'text'
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.FirstName).HasMaxLength(100);
            e.Property(u => u.LastName).HasMaxLength(100);
            e.Property(u => u.CognitoSub).HasMaxLength(128);
            e.Property(u => u.UserType).HasConversion<string>().HasMaxLength(50);
            e.Property(u => u.Status).HasConversion<string>().HasMaxLength(50);
        });

        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasQueryFilter(t => !t.IsDeleted);
            e.HasIndex(t => t.Code).IsUnique();
            e.HasIndex(t => t.CompanyDomain).IsUnique();

            e.Property(t => t.Name).HasMaxLength(200).IsRequired();
            e.Property(t => t.Code).HasMaxLength(50).IsRequired();
            e.Property(t => t.CompanyDomain).HasMaxLength(100).IsRequired();
            e.Property(t => t.TaxCode).HasMaxLength(50);
            e.Property(t => t.PlanType).HasMaxLength(50);
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(50);
        });

        modelBuilder.Entity<Role>(e =>
        {
            // Filter: Role hệ thống (IsSystemRole = true) hiện ra cho mọi tenant
            e.HasQueryFilter(r =>
                r.IsSystemRole
                || !_currentUser.TenantId.HasValue
                || r.TenantId == _currentUser.TenantId);

            e.HasIndex(r => new { r.TenantId, r.Code }).IsUnique();
            e.Property(r => r.Code).HasMaxLength(100).IsRequired();
            e.Property(r => r.Name).HasMaxLength(200).IsRequired();
            e.Property(r => r.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
            e.Property(p => p.Code).HasMaxLength(100).IsRequired();
            e.Property(p => p.Module).HasMaxLength(100).IsRequired();
            e.Property(p => p.Description).HasMaxLength(500);
        });

        // Composite PKs cho junction tables
        modelBuilder.Entity<UserRole>().HasKey(ur => new { ur.UserId, ur.RoleId });
        modelBuilder.Entity<RolePermission>().HasKey(rp => new { rp.RoleId, rp.PermissionId });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.Property(o => o.EventType).HasMaxLength(256).IsRequired();
            e.Property(o => o.Payload).IsRequired();
        });

        // ============================================================
        // SEED: System Roles & Permissions mặc định
        // ============================================================
        SeedSystemData(modelBuilder);
    }

    private static void SeedSystemData(ModelBuilder modelBuilder)
    {
        // System Tenant (tenantId = Guid.Empty = system scope)
        var systemTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // --- Roles ---
        var sysAdminRoleId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var tenantAdminRoleId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var tenantStaffRoleId = Guid.Parse("10000000-0000-0000-0000-000000000003");

        modelBuilder.Entity<Role>().HasData(
            new Role { Id = sysAdminRoleId, TenantId = systemTenantId, Code = "SYSTEM_ADMIN", Name = "System Administrator", IsSystemRole = true, CreatedAt = DateTimeOffset.UnixEpoch, CreatedBy = "system" },
            new Role { Id = tenantAdminRoleId, TenantId = systemTenantId, Code = "TENANT_ADMIN", Name = "Tenant Administrator", IsSystemRole = true, CreatedAt = DateTimeOffset.UnixEpoch, CreatedBy = "system" },
            new Role { Id = tenantStaffRoleId, TenantId = systemTenantId, Code = "TENANT_STAFF", Name = "Tenant Staff", IsSystemRole = true, CreatedAt = DateTimeOffset.UnixEpoch, CreatedBy = "system" }
        );

        // --- Permissions ---
        var perms = new[]
        {
            ("20000000-0000-0000-0000-000000000001", "tenant:create", "IAM"),
            ("20000000-0000-0000-0000-000000000002", "tenant:read", "IAM"),
            ("20000000-0000-0000-0000-000000000003", "tenant:update", "IAM"),
            ("20000000-0000-0000-0000-000000000004", "tenant:delete", "IAM"),
            ("20000000-0000-0000-0000-000000000011", "staff:create", "IAM"),
            ("20000000-0000-0000-0000-000000000012", "staff:read", "IAM"),
            ("20000000-0000-0000-0000-000000000013", "staff:update", "IAM"),
            ("20000000-0000-0000-0000-000000000014", "staff:delete", "IAM"),
            ("20000000-0000-0000-0000-000000000015", "staff:activate", "IAM"),
            ("20000000-0000-0000-0000-000000000016", "staff:reset_password", "IAM"),
            ("20000000-0000-0000-0000-000000000021", "role:create", "IAM"),
            ("20000000-0000-0000-0000-000000000022", "role:read", "IAM"),
            ("20000000-0000-0000-0000-000000000023", "role:update", "IAM"),
            ("20000000-0000-0000-0000-000000000024", "role:delete", "IAM"),
            ("20000000-0000-0000-0000-000000000025", "permission:assign", "IAM"),
        };

        modelBuilder.Entity<Permission>().HasData(
            perms.Select(p => new Permission
            {
                Id = Guid.Parse(p.Item1),
                Code = p.Item2,
                Module = p.Item3
            }).ToArray()
        );

        // --- RolePermissions: SYSTEM_ADMIN gets ALL ---
        var sysAdminPerms = perms.Select(p => new RolePermission
        {
            RoleId = sysAdminRoleId,
            PermissionId = Guid.Parse(p.Item1)
        }).ToArray();

        // TENANT_ADMIN: manage staff, roles within tenant
        var tenantAdminPermIds = new[]
        {
            "20000000-0000-0000-0000-000000000011",
            "20000000-0000-0000-0000-000000000012",
            "20000000-0000-0000-0000-000000000013",
            "20000000-0000-0000-0000-000000000014",
            "20000000-0000-0000-0000-000000000015",
            "20000000-0000-0000-0000-000000000016",
            "20000000-0000-0000-0000-000000000022",
            "20000000-0000-0000-0000-000000000023",
            "20000000-0000-0000-0000-000000000025",
        };

        var tenantAdminPerms = tenantAdminPermIds.Select(id => new RolePermission
        {
            RoleId = tenantAdminRoleId,
            PermissionId = Guid.Parse(id)
        }).ToArray();

        // TENANT_STAFF: read only
        var staffPermIds = new[]
        {
            "20000000-0000-0000-0000-000000000012",
            "20000000-0000-0000-0000-000000000022",
        };

        var tenantStaffPerms = staffPermIds.Select(id => new RolePermission
        {
            RoleId = tenantStaffRoleId,
            PermissionId = Guid.Parse(id)
        }).ToArray();

        modelBuilder.Entity<RolePermission>().HasData(
            [.. sysAdminPerms, .. tenantAdminPerms, .. tenantStaffPerms]);
    }
}

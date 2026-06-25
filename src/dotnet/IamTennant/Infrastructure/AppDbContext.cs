using shared.Entity;
using iam_tennant.Domain;
using iam_tennant.Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace iam_tennant.Infrastructure;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUserService currentUserService,
    AuditInterceptor auditInterceptor) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(auditInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global Query Filters
        var tenantId = currentUserService.TenantId;

        // User
        modelBuilder.Entity<User>()
            .HasQueryFilter(e => (!tenantId.HasValue || e.TenantId == tenantId) && !e.IsDeleted)
            .HasIndex(e => e.CognitoSub)
            .IsUnique();
        
        modelBuilder.Entity<User>()
            .HasIndex(e => new { e.TenantId, e.Email })
            .IsUnique();

        // Tenant
        modelBuilder.Entity<Tenant>()
            .HasQueryFilter(e => !e.IsDeleted)
            .HasIndex(e => e.Code)
            .IsUnique();

        // Role
        modelBuilder.Entity<Role>()
            .HasQueryFilter(e => !tenantId.HasValue || e.TenantId == tenantId);

        // Many-to-Many Relationships
        modelBuilder.Entity<UserRole>()
            .HasKey(ur => new { ur.UserId, ur.RoleId });

        modelBuilder.Entity<RolePermission>()
            .HasKey(rp => new { rp.RoleId, rp.PermissionId });
    }
}


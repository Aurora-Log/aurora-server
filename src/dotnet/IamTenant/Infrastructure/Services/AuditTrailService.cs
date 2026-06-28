using System.Text.Json;
using IamTenant.Application.Interfaces;
using IamTenant.Domain;
using IamTenant.Infrastructure.Persistences;
using Microsoft.Extensions.Logging;
using Shared.Security;

namespace IamTenant.Infrastructure.Services;

public class AuditTrailService(
    IamTenantDbContext context,
    ICurrentUserService currentUser,
    ILogger<AuditTrailService> logger) : IAuditTrailService
{
    public Task LogAsync(string action, string resource, object details, CancellationToken cancellationToken = default)
    {
        try
        {
            var auditLog = new AuditLog
            {
                TenantId = currentUser.TenantId,
                Actor = currentUser.UserId?.ToString() ?? "SYSTEM",
                Action = action,
                Resource = resource,
                Details = JsonSerializer.Serialize(details),
                CorrelationId = currentUser.TraceId ?? Guid.NewGuid().ToString(),
                CreatedAt = DateTimeOffset.UtcNow
            };

            context.AuditLogs.Add(auditLog);
            // LƯU Ý: Không gọi SaveChangesAsync ở đây. 
            // Command Handler sẽ chịu trách nhiệm gọi SaveChangesAsync của DbContext trong cùng 1 transaction.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write audit log for action {Action} on resource {Resource}", action, resource);
        }
        
        return Task.CompletedTask;
    }
}

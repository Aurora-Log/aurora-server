using IamTenant.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IamTenant.Infrastructure.BackgroundJobs;

public class SoftDeleteCleanupWorker(
    IServiceProvider serviceProvider,
    ILogger<SoftDeleteCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SoftDeleteCleanupWorker is starting.");

        // Chạy mỗi 24 giờ
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

        try
        {
            // Thực thi ngay lần đầu khởi động (hoặc có thể bỏ qua dòng này nếu chỉ muốn đợi sau 24h)
            await DoWorkAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await DoWorkAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("SoftDeleteCleanupWorker is stopping.");
        }
    }

    private async Task DoWorkAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("SoftDeleteCleanupWorker is doing background work.");

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IamTenantDbContext>();

        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-40);

        try
        {
            // 1. Hard Delete Users đã soft-delete quá 40 ngày
            var deletedUsersCount = await context.Users
                .IgnoreQueryFilters()
                .Where(u => u.IsDeleted && u.DeletedAt <= cutoffDate)
                .ExecuteDeleteAsync(cancellationToken);

            logger.LogInformation("Hard deleted {Count} old soft-deleted users.", deletedUsersCount);

            // 2. Hard Delete AuditLogs quá hạn (ví dụ 90 ngày)
            var auditLogCutoffDate = DateTimeOffset.UtcNow.AddDays(-90);
            var deletedAuditLogsCount = await context.AuditLogs
                .Where(a => a.CreatedAt <= auditLogCutoffDate)
                .ExecuteDeleteAsync(cancellationToken);

            logger.LogInformation("Hard deleted {Count} old audit logs.", deletedAuditLogsCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred executing cleanup worker.");
        }
    }
}

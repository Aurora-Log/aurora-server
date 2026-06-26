using System.Text.Json;
using IamTenant.Infrastructure.Persistences;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Events;

namespace IamTenant.Infrastructure.BackgroundJobs;

public class OutboxProcessorBackgroundService(
    IServiceProvider serviceProvider,
    ILogger<OutboxProcessorBackgroundService> logger) : BackgroundService
{
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while processing outbox messages.");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IamTenantDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var messages = await context.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(stoppingToken);

        foreach (var message in messages)
        {
            try
            {
                // Find event type
                Type? eventType = GetEventType(message.EventType);
                if (eventType != null)
                {
                    var eventObject = JsonSerializer.Deserialize(message.Payload, eventType);
                    if (eventObject != null)
                    {
                        await publishEndpoint.Publish(eventObject, eventType, stoppingToken);
                    }
                }

                message.ProcessedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish outbox message {MessageId}", message.Id);
                message.RetryCount++;
                message.Error = ex.Message;
            }
        }

        if (messages.Any())
        {
            await context.SaveChangesAsync(stoppingToken);
        }
    }

    private Type? GetEventType(string typeName)
    {
        return typeName switch
        {
            nameof(TenantAdminCreatedEvent) => typeof(TenantAdminCreatedEvent),
            nameof(TenantStaffCreatedEvent) => typeof(TenantStaffCreatedEvent),
            nameof(TenantStaffPasswordResetEvent) => typeof(TenantStaffPasswordResetEvent),
            _ => null
        };
    }
}

using IamTenant.Infrastructure.Persistences;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Events;

namespace IamTenant.Application.Commands.Tenants;

/// <summary>
/// TenantId không cần truyền vào — Global Query Filter đã đảm bảo
/// chỉ trả về Staff thuộc TenantId của người gọi.
/// </summary>
public record ResetStaffPasswordCommand(Guid UserId) : IRequest;

public class ResetStaffPasswordHandler(IamTenantDbContext context, IPublishEndpoint publishEndpoint)
    : IRequestHandler<ResetStaffPasswordCommand>
{
    public async Task Handle(ResetStaffPasswordCommand request, CancellationToken cancellationToken)
    {
        // Global Query Filter đảm bảo user này phải thuộc TenantId hiện tại
        var staffUser = await context.Users.FirstOrDefaultAsync(
            u => u.Id == request.UserId, cancellationToken)
            ?? throw new Exception("Staff not found.");

        var resetToken = Guid.CreateVersion7().ToString("N");

        await publishEndpoint.Publish(new TenantStaffPasswordResetEvent
        {
            TenantId = staffUser.TenantId,
            UserId = staffUser.Id,
            Email = staffUser.Email,
            ResetToken = resetToken
        }, cancellationToken);
    }
}

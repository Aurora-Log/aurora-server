using IamTenant.Domain;
using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Events;
using Shared.Security;

namespace IamTenant.Application.Commands.Tenants;

/// <summary>
/// TenantId không cần truyền vào — được resolve từ ICurrentUserService
/// (đã được populate bởi AuthInterceptor từ gRPC metadata).
/// Global Query Filter trên DbContext cũng đảm bảo không có data cross-tenant.
/// </summary>
public record CreateStaffCommand(string Email, string FirstName, string LastName) : IRequest<StaffDto>;

public class CreateStaffHandler(
    IamTenantDbContext context,
    IPublishEndpoint publishEndpoint,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateStaffCommand, StaffDto>
{
    public async Task<StaffDto> Handle(CreateStaffCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue)
            throw new UnauthorizedAccessException("TenantId is required.");

        var tenant = await context.Tenants.FirstOrDefaultAsync(cancellationToken: cancellationToken)
            ?? throw new Exception("Tenant not found.");

        // Validate Email Domain — BẮT BUỘC theo yêu cầu nghiệp vụ
        if (!request.Email.EndsWith($"@{tenant.CompanyDomain}", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"Staff Email must belong to the Company Domain: {tenant.CompanyDomain}");

        var staffUser = new User
        {
            TenantId = currentUser.TenantId.Value,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            UserType = Domain.Enums.UserType.TenantStaff,
            Status = Domain.Enums.UserStatus.Invited,
        };

        context.Users.Add(staffUser);
        await context.SaveChangesAsync(cancellationToken);

        // Publish invitation event (snake-case name → NestJS consumer)
        await publishEndpoint.Publish(new TenantStaffCreatedEvent
        {
            TenantId = tenant.Id,
            UserId = staffUser.Id,
            Email = staffUser.Email,
            FirstName = staffUser.FirstName,
            LastName = staffUser.LastName,
        }, cancellationToken);

        return new StaffDto
        {
            Id = staffUser.Id,
            TenantId = staffUser.TenantId,
            Email = staffUser.Email,
            FirstName = staffUser.FirstName,
            LastName = staffUser.LastName,
            UserType = staffUser.UserType.ToString(),
            Status = staffUser.Status.ToString(),
            StaffType = staffUser.StaffType.ToString(),
            CreatedAt = staffUser.CreatedAt
        };
    }
}

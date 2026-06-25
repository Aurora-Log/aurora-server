using iam_tennant.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace iam_tennant.Application.Queries.Auth;

public record IdentifyUserResult(bool Exists, string? TenantCode, string? UserType);

public record IdentifyUserQuery(string Email) : IRequest<IdentifyUserResult>;

public class IdentifyUserQueryHandler(AppDbContext context) : IRequestHandler<IdentifyUserQuery, IdentifyUserResult>
{
    public async Task<IdentifyUserResult> Handle(IdentifyUserQuery request, CancellationToken cancellationToken)
    {
        // Global query filter takes care of IsDeleted, but here we query by email across tenants, 
        // so we must use IgnoreQueryFilters() if the current context tenantId is set to something else.
        // Assuming IdentifyUser is called by BFF before login, context might not have TenantId yet.
        var user = await context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted, cancellationToken);

        if (user == null)
        {
            return new IdentifyUserResult(false, null, null);
        }

        return new IdentifyUserResult(true, user.Tenant?.Code ?? "SYSTEM", user.UserType);
    }
}

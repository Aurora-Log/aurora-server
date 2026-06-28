namespace IamTenant.Application.Interfaces;

public interface IAuditTrailService
{
    Task LogAsync(string action, string resource, object details, CancellationToken cancellationToken = default);
}

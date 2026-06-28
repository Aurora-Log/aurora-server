using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BFF.Behaviors;

/// <summary>
/// Log toàn bộ Commands/Queries đi qua BFF CQRS pipeline.
/// </summary>
public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName}: {@Request}", requestName, request);

        var response = await next();

        logger.LogInformation("Handled {RequestName}", requestName);
        return response;
    }
}

/// <summary>
/// Log warning nếu request xử lý quá lâu (> 500ms).
/// </summary>
public class PerformanceBehavior<TRequest, TResponse>(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private const long WarningThresholdMs = 500;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > WarningThresholdMs)
        {
            logger.LogWarning("Slow request {RequestName} took {ElapsedMs}ms. Request: {@Request}",
                typeof(TRequest).Name, sw.ElapsedMilliseconds, request);
        }

        return response;
    }
}

/// <summary>
/// Validate request trước khi đến Handler (FluentValidation).
/// </summary>
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<FluentValidation.IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();

        var context = new FluentValidation.ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f != null).ToList();

        if (failures.Count > 0)
            throw new Shared.Exceptions.DomainException(
                string.Join("; ", failures.Select(f => f.ErrorMessage)));

        return await next();
    }
}

/// <summary>
/// Kiểm tra quyền truy cập cho các Command/Query yêu cầu Authorization.
/// </summary>
public class AuthorizationBehavior<TRequest, TResponse>(
    Shared.Security.ICurrentUserService currentUser,
    ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Nếu Request implement IRequirePermission, kiểm tra quyền
        if (request is IRequirePermission permReq)
        {
            if (currentUser.UserId is null)
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            if (!currentUser.Permissions.Contains(permReq.RequiredPermission))
            {
                logger.LogWarning("User {UserId} denied access. Required: {Permission}",
                    currentUser.UserId, permReq.RequiredPermission);
                throw new Shared.Exceptions.ForbiddenException($"Missing permission: {permReq.RequiredPermission}");
            }
        }

        return await next();
    }
}

/// <summary>
/// Marker interface cho các Command/Query cần kiểm tra quyền cụ thể.
/// </summary>
public interface IRequirePermission
{
    string RequiredPermission { get; }
}

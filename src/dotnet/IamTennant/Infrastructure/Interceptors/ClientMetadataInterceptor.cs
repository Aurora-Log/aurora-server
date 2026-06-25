using Grpc.Core;
using Grpc.Core.Interceptors;
using shared.Entity;

namespace iam_tennant.Infrastructure.Interceptors;

public class ClientMetadataInterceptor(ICurrentUserService currentUserService) : Interceptor
{
    private void AddCallerMetadata<TRequest, TResponse>(ref ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        var headers = context.Options.Headers ?? new Metadata();

        if (!string.IsNullOrEmpty(currentUserService.TraceId))
        {
            // gRPC headers should generally be lowercase
            if (headers.Get("x-trace-id") == null)
            {
                headers.Add("x-trace-id", currentUserService.TraceId);
            }
        }

        if (currentUserService.TenantId.HasValue)
        {
            if (headers.Get("x-tenant-id") == null)
            {
                headers.Add("x-tenant-id", currentUserService.TenantId.Value.ToString());
            }
        }

        if (currentUserService.UserId.HasValue)
        {
            if (headers.Get("x-user-id") == null)
            {
                headers.Add("x-user-id", currentUserService.UserId.Value.ToString());
            }
        }

        if (currentUserService.Permissions != null && currentUserService.Permissions.Any())
        {
            if (headers.Get("x-user-permissions") == null)
            {
                headers.Add("x-user-permissions", string.Join(",", currentUserService.Permissions));
            }
        }

        var newOptions = context.Options.WithHeaders(headers);
        context = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, newOptions);
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        AddCallerMetadata(ref context);
        return continuation(request, context);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request, 
        ClientInterceptorContext<TRequest, TResponse> context, 
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        AddCallerMetadata(ref context);
        return continuation(request, context);
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context, 
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        AddCallerMetadata(ref context);
        return continuation(context);
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context, 
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        AddCallerMetadata(ref context);
        return continuation(context);
    }
}

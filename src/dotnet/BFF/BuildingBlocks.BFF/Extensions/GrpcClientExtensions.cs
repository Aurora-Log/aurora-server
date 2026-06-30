namespace BuildingBlocks.BFF.Extensions;
public static class GrpcClientExtensions
{
    /// <summary>
    /// Đăng ký tất cả gRPC clients với Resilience Pipelines (retry + circuit breaker).
    /// Mỗi client có deadline riêng được set per-call qua GrpcDeadlines.
    /// </summary>
    public static IServiceCollection AddBffGrpcClients(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Clients should be registered in the specific Micro-BFF projects.
        // For example, Admin.Bff registers IamServiceClient.
        return services;
    }

    // ── Resilience profiles ───────────────────────────────────────────────────

    /// <summary>IAM: retry nhanh hơn (auth critical path).</summary>
    private static void ConfigureIamResilience(
        Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions r)
    {
        r.Retry.MaxRetryAttempts = 2;
        r.Retry.Delay = TimeSpan.FromMilliseconds(200);
        r.Retry.UseJitter = true;
        r.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        r.CircuitBreaker.FailureRatio = 0.5;
        r.CircuitBreaker.MinimumThroughput = 5;
        r.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        r.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
    }

    /// <summary>Business services: timeout rộng hơn cho heavy operations.</summary>
    private static void ConfigureBusinessResilience(
        Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions r)
    {
        r.Retry.MaxRetryAttempts = 2;
        r.Retry.Delay = TimeSpan.FromMilliseconds(200);
        r.Retry.UseJitter = true;
        r.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        r.CircuitBreaker.FailureRatio = 0.5;
        r.CircuitBreaker.MinimumThroughput = 5;
        r.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        r.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
    }
}

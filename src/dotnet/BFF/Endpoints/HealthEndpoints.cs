namespace BFF.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        // /healthz — liveness probe (Kubernetes + Prometheus scrape)
        app.MapGet("/healthz", () => Results.Ok(new
        {
            status = "healthy",
            service = "bff-gateway",
            timestamp = DateTimeOffset.UtcNow
        })).AllowAnonymous();

        // /readyz — readiness probe
        app.MapGet("/readyz", async (IServiceProvider sp) =>
        {
            // Có thể check downstream services (Redis ping, IAM gRPC ping)
            // Hiện tại return OK đơn giản
            await Task.CompletedTask;
            return Results.Ok(new
            {
                status = "ready",
                service = "bff-gateway",
                timestamp = DateTimeOffset.UtcNow
            });
        }).AllowAnonymous();
    }
}

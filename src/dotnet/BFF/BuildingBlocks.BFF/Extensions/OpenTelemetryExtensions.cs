using OpenTelemetry.Instrumentation.GrpcNetClient;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BuildingBlocks.BFF.Extensions;

public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Cấu hình OpenTelemetry: Tracing (ASP.NET + HttpClient + gRPC) + Metrics (Prometheus).
    /// Serilog được cấu hình riêng trong SerilogExtensions vì cần UseSerilog() trên Host.
    /// </summary>
    public static IServiceCollection AddBffOpenTelemetry(
        this IServiceCollection services,
        IConfiguration config)
    {
        var serviceName = config["OpenTelemetry:ServiceName"] ?? "bff-gateway";
        var otlpEndpoint = config["OpenTelemetry:OtlpEndpoint"];

        services
            .AddOpenTelemetry()
            .ConfigureResource(res => res.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(opts =>
                    {
                        // Exclude high-frequency, low-value paths khỏi trace noise
                        opts.Filter = httpCtx =>
                            !httpCtx.Request.Path.StartsWithSegments("/healthz") &&
                            !httpCtx.Request.Path.StartsWithSegments("/readyz") &&
                            !httpCtx.Request.Path.StartsWithSegments("/metrics");
                        opts.RecordException = true;
                    })
                    .AddHttpClientInstrumentation()
                    .AddGrpcClientInstrumentation(); // BFF → IAM gRPC traces

                if (!string.IsNullOrEmpty(otlpEndpoint))
                    tracing.AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("BFF.Gateway");
                metrics.AddPrometheusExporter();
            });

        return services;
    }
}

using Serilog;

namespace BuildingBlocks.BFF.Extensions;

public static class SerilogExtensions
{
    /// <summary>
    /// Cấu hình Serilog structured JSON logging cho Loki.
    /// Enrich mỗi log entry với: MachineName, EnvironmentName, ServiceName.
    /// Sink: Console (JSON) + OpenTelemetry (nếu OtlpEndpoint được set).
    /// </summary>
    public static IHostBuilder AddBffSerilog(this IHostBuilder host)
    {
        host.UseSerilog((ctx, services, loggerConfig) =>
        {
            var serviceName = ctx.Configuration["OpenTelemetry:ServiceName"] ?? "bff-gateway";
            var otlpEndpoint = ctx.Configuration["OpenTelemetry:OtlpEndpoint"];

            loggerConfig
                .ReadFrom.Configuration(ctx.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                // .Enrich.WithMachineName()
                // .Enrich.WithEnvironmentName()
                .Enrich.WithProperty("ServiceName", serviceName)
                .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());

            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                loggerConfig.WriteTo.OpenTelemetry(opts =>
                {
                    opts.Endpoint = otlpEndpoint;
                    opts.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
                    opts.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = serviceName
                    };
                });
            }
        });

        return host;
    }
}

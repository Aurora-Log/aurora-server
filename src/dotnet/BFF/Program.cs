using BFF.Behaviors;
using BFF.Endpoints;
using BFF.Middleware;
using BFF.RateLimiting;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Shared.Cache;
using Shared.Security;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ── CORS ─────────────────────────────────────────────────────────────────────
var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()); // Required for HttpOnly Cookie
});

// ── Cookie Authentication + JWT Bearer (đọc từ Cookie) ───────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.Authority = config["Auth:Jwt:Authority"];
        opts.Audience = config["Auth:Jwt:Audience"];
        opts.RequireHttpsMetadata = config.GetValue("Auth:CookieSecure", true);

        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // Đọc token từ HttpOnly Cookie thay vì Authorization header
        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Cookies["access_token"];
                if (!string.IsNullOrWhiteSpace(token))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── ICurrentUserService (Shared) ─────────────────────────────────────────────
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<CurrentUserService>());
builder.Services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserService>());

// ── Redis Permission Cache ────────────────────────────────────────────────────
var redisConn = config["Redis:ConnectionString"]
    ?? throw new InvalidOperationException("Redis:ConnectionString is required");
builder.Services.AddStackExchangeRedisCache(opts => opts.Configuration = redisConn);
builder.Services.AddScoped<IPermissionCacheService, PermissionCacheService>();

// ── Rate Limiting ─────────────────────────────────────────────────────────────
builder.Services.AddBffRateLimiting(config);

// ── MediatR + Behaviors (BFF internal CQRS) ──────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ── gRPC Clients ──────────────────────────────────────────────────────────────
var iamUrl = config["GrpcServices:IamTenant:Url"]
    ?? throw new InvalidOperationException("GrpcServices:IamTenant:Url is required");

builder.Services.AddGrpcClient<Auth.Grpc.AuthService.AuthServiceClient>(opts =>
{
    opts.Address = new Uri(iamUrl);
}).ConfigureChannel(ch =>
{
    // HTTP/2 không TLS trên internal k8s network
    ch.UnsafeUseInsecureChannelCallCredentials = true;
});

builder.Services.AddGrpcClient<IamTenant.Grpc.IamService.IamServiceClient>(opts =>
{
    opts.Address = new Uri(iamUrl);
}).ConfigureChannel(ch =>
{
    ch.UnsafeUseInsecureChannelCallCredentials = true;
});

// ── YARP Reverse Proxy ────────────────────────────────────────────────────────
builder.Services.AddReverseProxy()
    .LoadFromConfig(config.GetSection("ReverseProxy"));

// ── OpenTelemetry ─────────────────────────────────────────────────────────────
var otlpEndpoint = config["OpenTelemetry:OtlpEndpoint"];
var serviceName = config["OpenTelemetry:ServiceName"] ?? "bff-gateway";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(res => res.AddService(serviceName))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation();
        if (!string.IsNullOrEmpty(otlpEndpoint))
            tracing.AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
               .AddRuntimeInstrumentation();
        // Prometheus scrape endpoint /metrics (kube-prometheus-stack)
        metrics.AddPrometheusExporter();
    });

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Middleware Pipeline (thứ tự quan trọng) ───────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();       // 1. Catch tất cả errors
app.UseMiddleware<CorrelationIdMiddleware>();           // 2. Trace ID
app.UseMiddleware<SecurityHeadersMiddleware>();         // 3. Security headers

app.UseCors();                                         // 4. CORS

app.UseRateLimiter();                                  // 5. Rate Limiting

app.UseAuthentication();                               // 6. JWT validate (từ Cookie)

app.UseMiddleware<CurrentUserContextMiddleware>();      // 7. Populate ICurrentUserService
app.UseMiddleware<TenantResolutionMiddleware>();        // 8. Resolve TenantId (from JWT claim)
app.UseMiddleware<PermissionVersionMiddleware>();       // 9. Version check vs Redis
app.UseMiddleware<GrpcMetadataPropagationMiddleware>(); // 10. Gắn x-headers cho YARP

app.UseAuthorization();                                // 11. Policy-based authorization

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapAuthEndpoints();    // /auth/login, /auth/logout, /auth/refresh
app.MapHealthEndpoints();  // /healthz, /readyz

// Prometheus metrics endpoint (kube-prometheus-stack scrape)
app.MapPrometheusScrapingEndpoint("/metrics");

// ── YARP Proxy ────────────────────────────────────────────────────────────────
app.MapReverseProxy();

app.Run();

// FluentValidation registration helper
static class ServiceCollectionExtensions
{
    public static IServiceCollection AddValidatorsFromAssemblyContaining<T>(
        this IServiceCollection services) =>
        FluentValidation.ServiceCollectionExtensions.AddValidatorsFromAssemblyContaining<T>(services);
}

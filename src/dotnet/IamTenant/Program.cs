using Amazon.CognitoIdentityProvider;
using IamTenant.Application.Interfaces;
using IamTenant.GrpcServices;
using IamTenant.Infrastructure.Auth.Cognito;
using IamTenant.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Shared.Extensions;
using Shared.Interceptors;

var builder = WebApplication.CreateBuilder(args);

// ── gRPC với AuthInterceptor từ Shared ───────────────────────────────────────
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<AuthInterceptor>(); // Populate ICurrentUserService từ metadata
});

// ── Shared Services: CurrentUserService, Redis, Interceptors, MassTransit ────
builder.Services.AddSharedServices(builder.Configuration);
builder.Services.AddSharedMassTransit(builder.Configuration);

// ── MediatR ───────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// ── AWS Cognito ───────────────────────────────────────────────────────────────
builder.Services.Configure<CognitoOptions>(builder.Configuration.GetSection("Cognito"));
builder.Services.AddAWSService<IAmazonCognitoIdentityProvider>();
builder.Services.AddScoped<ICognitoAuthService, CognitoAuthService>();

// ── PostgreSQL — Database per Service ────────────────────────────────────────
builder.Services.AddDbContext<IamTenantDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsAssembly("IamTenant")));

var app = builder.Build();

// ── gRPC Endpoints ────────────────────────────────────────────────────────────
app.MapGrpcService<IamGrpcService>();
app.MapGrpcService<AuthGrpcService>();

app.MapGet("/", () => "IAM Tenant gRPC Service — use a gRPC client to connect.");

app.Run();

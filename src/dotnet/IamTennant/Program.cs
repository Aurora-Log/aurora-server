using Amazon.CognitoIdentityProvider;
using iam_tennant.Application.Interfaces;
using iam_tennant.GrpcServices;
using iam_tennant.Infrastructure;
using iam_tennant.Infrastructure.Auth.Cognito;
using iam_tennant.Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<MetadataInterceptor>();
});

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// AWS Cognito
builder.Services.Configure<CognitoOptions>(builder.Configuration.GetSection("Cognito"));
builder.Services.AddAWSService<IAmazonCognitoIdentityProvider>();
builder.Services.AddScoped<ICognitoAuthService, CognitoAuthService>();

// EF Core & Interceptors
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddScoped<MetadataInterceptor>();
builder.Services.AddTransient<ClientMetadataInterceptor>();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    // Use Npgsql for PostgreSQL
    // options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.UseInMemoryDatabase("IamTenantDb"); // Placeholder for compilation
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<IamGrpcService>();
app.MapGrpcService<AuthGrpcService>();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();

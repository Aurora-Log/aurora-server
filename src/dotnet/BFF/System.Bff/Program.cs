using BuildingBlocks.BFF.Extensions;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddControllers();
// builder.Services.AddCustomSwagger("System BFF API");

// Thêm các logic cấu hình từ BuildingBlocks (VD: Authentication, gRPC Clients)
// builder.Services.AddBffAuthentication(config);
// builder.Services.AddBffGrpcClients(config);

var app = builder.Build();

// app.UseCustomSwagger("System BFF API");
app.UseRouting();

// app.UseAuthentication();
// app.UseAuthorization();

app.MapControllers();

app.Run();

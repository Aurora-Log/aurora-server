using BuildingBlocks.BFF.Extensions;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddControllers();
// builder.Services.AddCustomSwagger("Staff BFF API");

// builder.Services.AddBffAuthentication(config);
// builder.Services.AddBffGrpcClients(config);

var app = builder.Build();

// app.UseCustomSwagger("Staff BFF API");
app.UseRouting();

// app.UseAuthentication();
// app.UseAuthorization();

app.MapControllers();

app.Run();

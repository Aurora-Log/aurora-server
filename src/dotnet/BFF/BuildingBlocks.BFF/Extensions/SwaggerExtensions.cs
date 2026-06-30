// using Microsoft.OpenApi;
// using Microsoft.OpenApi.Models;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.AspNetCore.Builder;
// using Microsoft.AspNetCore.Http;

// namespace BuildingBlocks.BFF.Extensions;

// public static class SwaggerExtensions
// {
//     public static IServiceCollection AddCustomSwagger(this IServiceCollection services, string apiTitle)
//     {
//         services.AddSwaggerGen(options =>
//         {
//             options.SwaggerDoc("v1", new OpenApiInfo { Title = apiTitle, Version = "v1" });

//             // Tránh trùng lặp DTO model (bắt chước logic E-Verland)
//             options.CustomSchemaIds(type =>
//             {
//                 static string PrefixByModule(Type t, string baseName)
//                 {
//                     var ns = t.Namespace ?? "";
//                     if (ns.Contains("IamTenant")) return "Iam_" + baseName;
//                     if (ns.Contains("RoutePlanning")) return "RoutePlanning_" + baseName;
//                     if (ns.Contains("System")) return "System_" + baseName;
//                     if (ns.Contains("Admin")) return "Admin_" + baseName;
//                     if (ns.Contains("Staff")) return "Staff_" + baseName;
//                     return baseName;
//                 }

//                 if (!type.IsGenericType)
//                 {
//                     return PrefixByModule(type, type.Name);
//                 }

//                 var genericName = type.GetGenericTypeDefinition().Name.Split('`')[0];
//                 genericName = PrefixByModule(type, genericName);

//                 var args = string.Join("_",
//                     type.GetGenericArguments().Select(a =>
//                     {
//                         var argBase = a.IsGenericType
//                             ? a.GetGenericTypeDefinition().Name.Split('`')[0]
//                             : a.Name;
//                         return PrefixByModule(a, argBase);
//                     }));

//                 return $"{genericName}_{args}";
//             });

//             // Sử dụng Cookie Authentication thay vì Bearer Header
//             // Swagger UI khi gọi "Try it out" sẽ tự động gửi HttpOnly cookie (access_token) của trình duyệt.
//             // Để hiển thị biểu tượng khoá bảo mật trên Swagger UI, chúng ta định nghĩa ApiKeySecurityScheme In Cookie.
//             options.AddSecurityDefinition("cookieAuth", new OpenApiSecurityScheme
//             {
//                 Name = "access_token",
//                 Type = SecuritySchemeType.ApiKey,
//                 In = ParameterLocation.Cookie,
//                 Description = "Sử dụng Cookie access_token để xác thực. Khi bạn đăng nhập thành công, trình duyệt tự động lưu HttpOnly cookie và Swagger UI sẽ tự động đính kèm cookie này vào các request tiếp theo."
//             });

//             options.AddSecurityRequirement(new OpenApiSecurityRequirement
//             {
//                 {
//                     new OpenApiSecurityScheme
//                     {
//                         Reference = new OpenApiReference
//                         {
//                             Type = ReferenceType.SecurityScheme,
//                             Id = "cookieAuth"
//                         }
//                     },
//                     Array.Empty<string>()
//                 }
//             });
//         });

//         return services;
//     }

//     public static WebApplication UseCustomSwagger(this WebApplication app, string apiTitle)
//     {
//         app.MapGet("/swagger-download/v1", async (HttpContext context) =>
//         {
//             var url = $"{context.Request.Scheme}://{context.Request.Host}/swagger/v1/swagger.json";

//             using var httpClient = new HttpClient();
//             var json = await httpClient.GetStringAsync(url);

//             return Results.File(
//                 System.Text.Encoding.UTF8.GetBytes(json),
//                 "application/json",
//                 "swagger-v1.json");
//         });

//         app.UseSwagger();

//         app.UseSwaggerUI(options =>
//         {
//             options.SwaggerEndpoint("/swagger/v1/swagger.json", apiTitle);
//             options.RoutePrefix = "api-docs";
//             // Kích hoạt tính năng gửi credential (Cookie) cho "Try it out"
//             options.ConfigObject.AdditionalItems["withCredentials"] = true;
//         });

//         return app;
//     }
// }

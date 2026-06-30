namespace BuildingBlocks.BFF.Extensions;

public static class MvcExtensions
{
    /// <summary>
    /// Đăng ký Controllers và các cấu hình liên quan (JSON, v.v.)
    /// </summary>
    public static IServiceCollection AddBffControllers(this IServiceCollection services)
    {
        services.AddControllers(options =>
        {
            // Có thể thêm global filters ở đây nếu cần
            // Ví dụ: options.Filters.Add<GlobalExceptionFilter>();
        })
        .AddJsonOptions(options =>
        {
            // Thiết lập serialization JSON mặc định (camelCase)
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });

        return services;
    }
}

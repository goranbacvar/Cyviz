using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Cyviz.Infrastructure;

public class ApiKeyValidator
{
    private readonly string _apiKey;
    public ApiKeyValidator(IConfiguration config)
    {
        _apiKey = config.GetValue<string>("ApiKey") ?? "local-dev-key";
    }
    public bool Validate(string? key) => !string.IsNullOrEmpty(key) && key == _apiKey;
}

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeyValidator _validator;
    public ApiKeyMiddleware(RequestDelegate next, ApiKeyValidator validator)
    {
        _next = next; _validator = validator;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/devicehub"))
        {
            var key = context.Request.Headers["X-Api-Key"].FirstOrDefault();
            if (!_validator.Validate(key))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid API key");
                return;
            }
        }
        await _next(context);
    }
}

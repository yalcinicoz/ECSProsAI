using System.Net;
using System.Text.Json;

namespace ECSPros.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Yetkisiz erişim."),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Kayıt bulunamadı."),
            ArgumentException e => (HttpStatusCode.BadRequest, e.Message),
            InvalidOperationException e => (HttpStatusCode.BadRequest, e.Message),
            _ => (HttpStatusCode.InternalServerError, "Beklenmeyen bir hata oluştu.")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = JsonSerializer.Serialize(new
        {
            success = false,
            error = message
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        return context.Response.WriteAsync(response);
    }
}

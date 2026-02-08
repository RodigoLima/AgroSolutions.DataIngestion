using System.Diagnostics;
using AgroSolutions.DataIngestion.Api.Configuration;
using AgroSolutions.DataIngestion.Application.Interfaces;
using AgroSolutions.DataIngestion.Application.Telemetry;
using Microsoft.Extensions.Options;

namespace AgroSolutions.DataIngestion.Api.Middlewares;

public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
    private readonly ApiKeySettings _apiKeySettings;
    private readonly ISensorMetrics _metrics;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyAuthenticationMiddleware> logger,
        IOptions<ApiKeySettings> apiKeySettings,
        ISensorMetrics metrics)
    {
        _next = next;
        _logger = logger;
        _apiKeySettings = apiKeySettings.Value;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Criar trace para autenticação
        using var activity = SensorActivitySource.Source.StartActivity(
            SensorActivitySource.Authentication,
            ActivityKind.Server);

        var path = context.Request.Path.Value?.ToLower() ?? string.Empty;
        
        activity?.SetTag("http.path", path);
        activity?.SetTag("http.method", context.Request.Method);

        // Permitir acesso público a endpoints específicos
        if (path.Contains("/swagger") || 
            path.Contains("/health") || 
            path == "/" ||
            path == "")
        {
            activity?.SetTag("auth.required", false);
            activity?.SetTag("auth.result", "public_endpoint");
            await _next(context);
            return;
        }

        activity?.SetTag("auth.required", true);

        // Verificar se o header da API Key está presente
        if (!context.Request.Headers.TryGetValue(_apiKeySettings.HeaderName, out var extractedApiKey))
        {
            _logger.LogWarning(
                "API Key não fornecida. Path: {Path}, IP: {RemoteIp}",
                path,
                context.Connection.RemoteIpAddress);

            // Registrar métrica de falha de autenticação
            _metrics.RecordAuthenticationAttempt(success: false);
            
            activity?.SetTag("auth.result", "missing_key");
            activity?.SetStatus(ActivityStatusCode.Error, "API Key not provided");
            activity?.AddEvent(new ActivityEvent("Authentication failed: API Key not provided"));
            
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Unauthorized",
                message = $"API Key é obrigatória. Forneça no header '{_apiKeySettings.HeaderName}'"
            });
            return;
        }

        // Validar a API Key
        if (!_apiKeySettings.ApiKey.Equals(extractedApiKey))
        {
            _logger.LogWarning(
                "API Key inválida fornecida. Path: {Path}, IP: {RemoteIp}, Key fornecida: {ProvidedKey}", 
                path,
                context.Connection.RemoteIpAddress,
                extractedApiKey.ToString().Substring(0, Math.Min(4, extractedApiKey.ToString().Length)) + "***");

            // Registrar métrica de falha de autenticação
            _metrics.RecordAuthenticationAttempt(success: false);
            
            activity?.SetTag("auth.result", "invalid_key");
            activity?.SetStatus(ActivityStatusCode.Error, "Invalid API Key");
            activity?.AddEvent(new ActivityEvent("Authentication failed: Invalid API Key"));
            
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Unauthorized",
                message = "API Key inválida"
            });
            return;
        }

        // API Key válida - continuar com a requisição
        _logger.LogInformation(
            "Requisição autenticada com sucesso. Path: {Path}, IP: {RemoteIp}",
            path,
            context.Connection.RemoteIpAddress);

        // Registrar métrica de sucesso de autenticação
        _metrics.RecordAuthenticationAttempt(success: true);

        activity?.SetTag("auth.result", "success");
        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.AddEvent(new ActivityEvent("Authentication successful"));

        await _next(context);
    }
}

// Extension method para facilitar o uso
public static class ApiKeyAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }
}

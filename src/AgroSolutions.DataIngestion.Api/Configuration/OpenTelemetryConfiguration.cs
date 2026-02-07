using AgroSolutions.DataIngestion.Application.Telemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AgroSolutions.DataIngestion.Api.Configuration;

public static class OpenTelemetryConfiguration
{
    public static IServiceCollection AddOpenTelemetryConfiguration(
        this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: SensorActivitySource.ServiceName,
                    serviceVersion: SensorActivitySource.ServiceVersion,
                    serviceInstanceId: Environment.MachineName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = (httpContext) =>
                    {
                        // Não rastrear health check
                        return !httpContext.Request.Path.Value?.Contains("/health") ?? true;
                    };
                })
                .AddHttpClientInstrumentation()
                .AddSource("MassTransit")
                .AddSource("SensorDataIngestion.*")
                .AddOtlpExporter())
                // .AddConsoleExporter()) // Para debug local
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("MassTransit")
                .AddMeter("SensorDataIngestion.*")
                .AddOtlpExporter());
                // .AddConsoleExporter()); // Para debug local

        return services;
    }

    public static ILoggingBuilder AddOpenTelemetryLogging(
        this ILoggingBuilder logging,
        IConfiguration configuration)
    {
        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";

        logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            
            options.AddOtlpExporter();
            
            // options.AddConsoleExporter(); // Para debug local
        });

        return logging;
    }
}

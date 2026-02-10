using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Prometheus;
using Serilog;
using AgroSolutions.DataIngestion.Application.DTOs;
using AgroSolutions.DataIngestion.Application.Interfaces;
using AgroSolutions.DataIngestion.Application.Validators;
using AgroSolutions.DataIngestion.Application.Services;
using AgroSolutions.DataIngestion.Api.Middlewares;
using AgroSolutions.DataIngestion.Api.Configuration;
using AgroSolutions.DataIngestion.Application.Telemetry;
using Serilog.Sinks.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// Configurar Serilog
// ========================================
var otelEndpoint =
    Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
    ?? "http://localhost:4318";


Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "SensorDataIngestionAPI")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.OpenTelemetry(op =>
    {
        op.Endpoint = otelEndpoint;
        op.Protocol = OtlpProtocol.HttpProtobuf;
    })
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Host.UseSerilog();

// ========================================
// Configurar OpenTelemetry (Metrics, Traces, Logs)
// ========================================
builder.Services.AddOpenTelemetryConfiguration();
// builder.Logging.AddOpenTelemetryLogging();

// ========================================
// Registrar Métricas Customizadas
// ========================================
builder.Services.AddSingleton<ISensorMetrics, SensorMetrics>();

// ========================================
// Configurar Serviços
// ========================================

// API Key Settings
builder.Services.Configure<ApiKeySettings>(
    builder.Configuration.GetSection("ApiKey"));

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Sensor Data Ingestion API",
        Version = "v1",
        Description = "API para ingestão de dados de sensores agrícolas com autenticação via API Key e OpenTelemetry"
    });

    c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "API Key necessária para acessar os endpoints. Header: X-API-Key",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Name = "X-API-Key",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Scheme = "ApiKeyScheme"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<SensorDataRequestValidator>();

// Application Services
builder.Services.AddScoped<ISensorDataIngestionService, SensorDataIngestionService>();

// MassTransit com RabbitMQ
var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitPort = builder.Configuration.GetValue<int>("RabbitMQ:Port", 5672);
var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
var rabbitVHost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/";

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, (ushort)rabbitPort, rabbitVHost, h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        cfg.ConfigureEndpoints(context);
        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
    });
});

// Health Checks
var rabbitConnectionString = $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}:{rabbitPort}/{rabbitVHost}";
builder.Services.AddHealthChecks()
    .AddRabbitMQ(
        rabbitConnectionString: rabbitConnectionString,
        name: "rabbitmq",
        tags: new[] { "ready", "messaging" });

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ========================================
// Configurar Pipeline
// ========================================

// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI(c =>
//     {
//         c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sensor Data Ingestion API v1");
//     });
// }

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sensor Data Ingestion API v1");
});

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress);
    };
});

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseMetricServer();
app.UseHttpMetrics();

app.UseApiKeyAuthentication();

// ========================================
// Endpoints Minimal API
// ========================================

// Health Check (público)
app.MapHealthChecks("/health");

// GET /api/sensordata/status - Status do serviço (público)
app.MapGet("/api/sensordata/status", () =>
{
    return Results.Ok(new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        service = "Sensor Data Ingestion API",
        version = "1.0.0",
        authentication = "API Key Required (X-API-Key header)",
        telemetry = new
        {
            traces = "enabled",
            metrics = "enabled",
            logs = "enabled",
            exporter = "OTLP"
        }
    });
})
.WithName("GetStatus")
.WithTags("Health")
.WithOpenApi(operation =>
{
    operation.Summary = "Verifica o status do serviço";
    operation.Description = "Endpoint público para verificar se o serviço está operacional";
    return operation;
});

// POST /api/sensordata - Ingerir um dado de sensor (REQUER AUTENTICAÇÃO)
app.MapPost("/api/sensordata", async (
    [FromBody] SensorDataRequest request,
    [FromServices] ISensorDataIngestionService ingestionService,
    [FromServices] IValidator<SensorDataRequest> validator,
    [FromServices] ILogger<Program> logger,
    [FromServices] ISensorMetrics metrics,
    CancellationToken cancellationToken) =>
{
    metrics.IncrementActiveRequests();
    
    try
    {
        // Validar
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            logger.LogWarning("Validação falhou para TalhaoId: {TalhaoId}", request.TalhaoId);
            
            return Results.BadRequest(new
            {
                message = "Erro de validação",
                errors = validationResult.Errors.Select(e => new
                {
                    field = e.PropertyName,
                    error = e.ErrorMessage
                })
            });
        }

        // Processar
        await ingestionService.IngestAsync(request, cancellationToken);

        return Results.Accepted("/api/sensordata/status", new
        {
            message = "Dados recebidos e enviados para processamento",
            talhaoId = request.TalhaoId,
            tipo = request.Tipo.ToString()
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erro ao ingerir dados do sensor");
        
        return Results.Problem(
            title: "Erro ao processar requisição",
            detail: "Ocorreu um erro interno ao processar os dados do sensor",
            statusCode: 500
        );
    }
    finally
    {
        metrics.DecrementActiveRequests();
    }
})
.WithName("IngestSensorData")
.WithTags("Ingestion")
.WithOpenApi(operation =>
{
    operation.Summary = "Ingere dados de um único sensor";
    operation.Description = "Recebe dados de telemetria de um sensor e os envia para processamento via fila. **Requer autenticação via API Key no header X-API-Key**";
    return operation;
});

// POST /api/sensordata/batch - Ingerir múltiplos dados (REQUER AUTENTICAÇÃO)
app.MapPost("/api/sensordata/batch", async (
    [FromBody] List<SensorDataRequest> requests,
    [FromServices] ISensorDataIngestionService ingestionService,
    [FromServices] ILogger<Program> logger,
    [FromServices] ISensorMetrics metrics,
    CancellationToken cancellationToken) =>
{
    metrics.IncrementActiveRequests();
    
    try
    {
        if (requests == null || !requests.Any())
        {
            return Results.BadRequest(new 
            { 
                message = "A lista de dados não pode estar vazia" 
            });
        }

        await ingestionService.IngestBatchAsync(requests, cancellationToken);

        return Results.Accepted("/api/sensordata/status", new
        {
            message = "Lote de dados recebido e enviado para processamento",
            count = requests.Count
        });
    }
    catch (ValidationException ex)
    {
        logger.LogWarning(ex, "Erro de validação ao ingerir lote de dados");
        
        return Results.BadRequest(new
        {
            message = "Erro de validação em um ou mais registros",
            errors = ex.Errors.Select(e => new
            {
                field = e.PropertyName,
                error = e.ErrorMessage
            })
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erro ao ingerir lote de dados");
        
        return Results.Problem(
            title: "Erro ao processar requisição",
            detail: "Ocorreu um erro interno ao processar o lote de dados",
            statusCode: 500
        );
    }
    finally
    {
        metrics.DecrementActiveRequests();
    }
})
.WithName("IngestBatchSensorData")
.WithTags("Ingestion")
.WithOpenApi(operation =>
{
    operation.Summary = "Ingere dados de múltiplos sensores em lote";
    operation.Description = "Recebe múltiplos registros de telemetria e os envia para processamento via fila. **Requer autenticação via API Key no header X-API-Key**";
    return operation;
});

// ========================================
// Iniciar Aplicação
// ========================================

try
{
    Log.Information("Iniciando Sensor Data Ingestion API com OpenTelemetry");
    Log.Information("OpenTelemetry Endpoint: {Endpoint}", builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Aplicação falhou ao iniciar");
}
finally
{
    Log.CloseAndFlush();
}
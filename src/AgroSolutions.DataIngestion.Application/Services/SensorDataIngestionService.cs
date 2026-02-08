using System.Diagnostics;
using AgroSolutions.Contracts;
using AgroSolutions.DataIngestion.Application.DTOs;
using AgroSolutions.DataIngestion.Application.Interfaces;
using AgroSolutions.DataIngestion.Application.Telemetry;
using FluentValidation;
using MassTransit;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace AgroSolutions.DataIngestion.Application.Services;

public class SensorDataIngestionService : ISensorDataIngestionService
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IValidator<SensorDataRequest> _validator;
    private readonly ILogger<SensorDataIngestionService> _logger;
    private readonly ISensorMetrics _metrics;

    public SensorDataIngestionService(
        IPublishEndpoint publishEndpoint,
        IValidator<SensorDataRequest> validator,
        ILogger<SensorDataIngestionService> logger,
        ISensorMetrics metrics)
    {
        _publishEndpoint = publishEndpoint;
        _validator = validator;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task IngestAsync(SensorDataRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Criar trace para ingestão
        using var activity = SensorActivitySource.Source.StartActivity(
            SensorActivitySource.SensorDataIngestion,
            ActivityKind.Server);

        activity?.SetTag("sensor.talhao_id", request.TalhaoId.ToString());
        activity?.SetTag("sensor.type", request.Tipo.ToString());
        activity?.SetTag("sensor.value", request.Valor);
        activity?.SetTag("sensor.measurement_date", request.DataMedicao.ToString("O"));

        try
        {
            _logger.LogInformation(
                "Iniciando ingestão de dados do sensor. TalhaoId: {TalhaoId}, Tipo: {Tipo}, Valor: {Valor}",
                request.TalhaoId,
                request.Tipo,
                request.Valor);

            // Registrar métrica de dado recebido
            _metrics.RecordSensorDataReceived(request.Tipo.ToString(), request.TalhaoId.ToString());

            // Validação com trace
            using (var validationActivity = SensorActivitySource.Source.StartActivity(
                SensorActivitySource.SensorDataValidation,
                ActivityKind.Internal))
            {
                validationActivity?.SetTag("sensor.type", request.Tipo.ToString());

                var validationResult = await _validator.ValidateAsync(request, cancellationToken);
                
                if (!validationResult.IsValid)
                {
                    var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                    
                    _logger.LogWarning(
                        "Validação falhou para TalhaoId: {TalhaoId}. Erros: {Errors}",
                        request.TalhaoId,
                        errors);

                    // Registrar métrica de falha
                    _metrics.RecordSensorDataFailed(request.Tipo.ToString(), "validation_error");
                    
                    validationActivity?.SetStatus(ActivityStatusCode.Error, "Validation failed");
                    validationActivity?.SetTag("validation.errors", errors);
                    validationActivity?.AddEvent(new ActivityEvent("Validation failed", 
                        tags: new ActivityTagsCollection { { "errors", errors } }));

                    activity?.SetStatus(ActivityStatusCode.Error, "Validation failed");
                    
                    stopwatch.Stop();
                    _metrics.RecordProcessingDuration(stopwatch.Elapsed.TotalMilliseconds, request.Tipo.ToString(), false);
                    
                    throw new ValidationException(validationResult.Errors);
                }

                validationActivity?.SetStatus(ActivityStatusCode.Ok);
                validationActivity?.AddEvent(new ActivityEvent("Validation successful"));
            }

            // Mapear para message
            var message = new SensorDataMessage(
                request.TalhaoId,
                request.DataMedicao,
                request.Tipo,
                request.Valor);

            // Publicar na fila com trace
            using (var publishActivity = SensorActivitySource.Source.StartActivity(
                SensorActivitySource.SensorDataPublishing,
                ActivityKind.Producer))
            {
                publishActivity?.SetTag("messaging.system", "rabbitmq");
                publishActivity?.SetTag("messaging.destination", "sensor-data-queue");
                publishActivity?.SetTag("sensor.type", request.Tipo.ToString());

                await _publishEndpoint.Publish(message, cancellationToken);

                publishActivity?.SetStatus(ActivityStatusCode.Ok);
                publishActivity?.AddEvent(new ActivityEvent("Message published to queue"));
            }

            // Registrar métrica de sucesso
            _metrics.RecordSensorDataPublished(request.Tipo.ToString());

            _logger.LogInformation(
                "Dados do sensor publicados com sucesso. TalhaoId: {TalhaoId}, Tipo: {Tipo}",
                request.TalhaoId,
                request.Tipo);

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.AddEvent(new ActivityEvent("Sensor data ingested successfully"));

            stopwatch.Stop();
            _metrics.RecordProcessingDuration(stopwatch.Elapsed.TotalMilliseconds, request.Tipo.ToString(), true);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Erro ao processar dados do sensor. TalhaoId: {TalhaoId}, Tipo: {Tipo}",
                request.TalhaoId,
                request.Tipo);

            // Registrar métrica de falha
            _metrics.RecordSensorDataFailed(request.Tipo.ToString(), "processing_error");

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);

            stopwatch.Stop();
            _metrics.RecordProcessingDuration(stopwatch.Elapsed.TotalMilliseconds, request.Tipo.ToString(), false);

            throw;
        }
    }

    public async Task IngestBatchAsync(IEnumerable<SensorDataRequest> requests, CancellationToken cancellationToken = default)
    {
        var requestsList = requests.ToList();
        
        using var activity = SensorActivitySource.Source.StartActivity(
            "sensor.data.batch.ingestion",
            ActivityKind.Server);

        activity?.SetTag("batch.size", requestsList.Count);
        
        _logger.LogInformation(
            "Iniciando ingestão em lote de {Count} registros",
            requestsList.Count);

        var tasks = requestsList.Select(request => IngestAsync(request, cancellationToken));
        
        try
        {
            await Task.WhenAll(tasks);
            
            _logger.LogInformation(
                "Ingestão em lote concluída com sucesso. Total: {Count} registros",
                requestsList.Count);

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.AddEvent(new ActivityEvent("Batch ingestion completed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante ingestão em lote");
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            
            throw;
        }
    }
}

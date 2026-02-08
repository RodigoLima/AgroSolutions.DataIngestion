using System.Diagnostics.Metrics;
using AgroSolutions.DataIngestion.Application.Interfaces;

namespace AgroSolutions.DataIngestion.Application.Telemetry;

/// <summary>
/// Métricas customizadas para o serviço de ingestão de dados de sensores
/// </summary>
public class SensorMetrics : ISensorMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _sensorDataReceivedCounter;
    private readonly Counter<long> _sensorDataPublishedCounter;
    private readonly Counter<long> _sensorDataFailedCounter;
    private readonly Histogram<double> _sensorDataProcessingDuration;
    private readonly Counter<long> _authenticationAttemptsCounter;
    private readonly Counter<long> _authenticationFailuresCounter;
    private readonly UpDownCounter<int> _activeRequestsCounter;

    public SensorMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("SensorDataIngestion.Api");

        // Contador de dados recebidos
        _sensorDataReceivedCounter = _meter.CreateCounter<long>(
            "sensor_data_received_total",
            description: "Número total de dados de sensores recebidos");

        // Contador de dados publicados com sucesso
        _sensorDataPublishedCounter = _meter.CreateCounter<long>(
            "sensor_data_published_total",
            description: "Número total de dados publicados na fila com sucesso");

        // Contador de falhas
        _sensorDataFailedCounter = _meter.CreateCounter<long>(
            "sensor_data_failed_total",
            description: "Número total de falhas ao processar dados de sensores");

        // Histograma de duração do processamento
        _sensorDataProcessingDuration = _meter.CreateHistogram<double>(
            "sensor_data_processing_duration_ms",
            unit: "ms",
            description: "Duração do processamento de dados de sensores em milissegundos");

        // Contador de tentativas de autenticação
        _authenticationAttemptsCounter = _meter.CreateCounter<long>(
            "authentication_attempts_total",
            description: "Número total de tentativas de autenticação");

        // Contador de falhas de autenticação
        _authenticationFailuresCounter = _meter.CreateCounter<long>(
            "authentication_failures_total",
            description: "Número total de falhas de autenticação");

        // Contador de requisições ativas
        _activeRequestsCounter = _meter.CreateUpDownCounter<int>(
            "active_requests",
            description: "Número de requisições sendo processadas atualmente");
    }

    public void RecordSensorDataReceived(string telemetryType, string talhaoId)
    {
        _sensorDataReceivedCounter.Add(1, 
            new KeyValuePair<string, object?>("telemetry_type", telemetryType),
            new KeyValuePair<string, object?>("talhao_id", talhaoId));
    }

    public void RecordSensorDataPublished(string telemetryType)
    {
        _sensorDataPublishedCounter.Add(1,
            new KeyValuePair<string, object?>("telemetry_type", telemetryType));
    }

    public void RecordSensorDataFailed(string telemetryType, string errorType)
    {
        _sensorDataFailedCounter.Add(1,
            new KeyValuePair<string, object?>("telemetry_type", telemetryType),
            new KeyValuePair<string, object?>("error_type", errorType));
    }

    public void RecordProcessingDuration(double durationMs, string telemetryType, bool success)
    {
        _sensorDataProcessingDuration.Record(durationMs,
            new KeyValuePair<string, object?>("telemetry_type", telemetryType),
            new KeyValuePair<string, object?>("success", success));
    }

    public void RecordAuthenticationAttempt(bool success)
    {
        _authenticationAttemptsCounter.Add(1,
            new KeyValuePair<string, object?>("success", success));

        if (!success)
        {
            _authenticationFailuresCounter.Add(1);
        }
    }

    public void IncrementActiveRequests()
    {
        _activeRequestsCounter.Add(1);
    }

    public void DecrementActiveRequests()
    {
        _activeRequestsCounter.Add(-1);
    }
}

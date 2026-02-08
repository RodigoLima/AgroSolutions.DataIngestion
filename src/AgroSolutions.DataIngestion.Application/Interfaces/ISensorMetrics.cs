namespace AgroSolutions.DataIngestion.Application.Interfaces;

public interface ISensorMetrics
{
    void RecordSensorDataReceived(string telemetryType, string talhaoId);
    void RecordSensorDataPublished(string telemetryType);
    void RecordSensorDataFailed(string telemetryType, string errorType);
    void RecordProcessingDuration(double durationMs, string telemetryType, bool success);
    void RecordAuthenticationAttempt(bool success);
    void IncrementActiveRequests();
    void DecrementActiveRequests();
}
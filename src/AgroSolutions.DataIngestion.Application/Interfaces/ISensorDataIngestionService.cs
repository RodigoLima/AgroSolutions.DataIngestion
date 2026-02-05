using AgroSolutions.DataIngestion.Application.DTOs;

namespace AgroSolutions.DataIngestion.Application.Interfaces;

public interface ISensorDataIngestionService
{
    Task IngestAsync(SensorDataRequest request, CancellationToken cancellationToken = default);
    Task IngestBatchAsync(IEnumerable<SensorDataRequest> requests, CancellationToken cancellationToken = default);
}


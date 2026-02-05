namespace AgroSolutions.DataIngestion.Application.DTOs;

public record SensorDataRequest(
    Guid TalhaoId,
    DateTime DataMedicao,
    TelemetryType Tipo,
    double Valor
);
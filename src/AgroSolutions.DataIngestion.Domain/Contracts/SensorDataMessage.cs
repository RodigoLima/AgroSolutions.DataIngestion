namespace AgroSolutions.Contracts;

public record SensorDataMessage(
    Guid TalhaoId,
    DateTime DataMedicao,
    TelemetryType Tipo,
    double Valor
);

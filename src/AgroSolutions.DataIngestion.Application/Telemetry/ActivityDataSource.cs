using System.Diagnostics;

namespace AgroSolutions.DataIngestion.Application.Telemetry;

/// <summary>
/// ActivitySource para criar traces customizados
/// </summary>
public static class SensorActivitySource
{
    public static readonly ActivitySource Source = new("AgroSolutions.DataIngestion.Api", "1.0.0");

    public const string SensorDataIngestion = "sensor.data.ingestion";
    public const string SensorDataValidation = "sensor.data.validation";
    public const string SensorDataPublishing = "sensor.data.publishing";
    public const string Authentication = "authentication";
}
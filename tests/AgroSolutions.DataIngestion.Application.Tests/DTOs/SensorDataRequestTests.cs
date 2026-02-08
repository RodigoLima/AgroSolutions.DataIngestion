using AgroSolutions.DataIngestion.Application.DTOs;
using Shouldly;

namespace AgroSolutions.DataIngestion.Application.Tests.DTOs;

public class SensorDataRequestTests
{
    [Fact]
    public void Should_Create_Request_With_All_Properties()
    {
        // Arrange
        var talhaoId = Guid.NewGuid();
        var dataMedicao = DateTime.UtcNow;
        var tipo = TelemetryType.Temperatura;
        var valor = 25.5;

        var request = new SensorDataRequest
        (
            talhaoId, 
            dataMedicao,
            tipo,
            valor
        );

        // Assert
        request.TalhaoId.ShouldBe(talhaoId);
        request.DataMedicao.ShouldBe(dataMedicao);
        request.Tipo.ShouldBe(tipo);
        request.Valor.ShouldBe(valor);
    }


    [Theory]
    [InlineData(TelemetryType.Temperatura)]
    [InlineData(TelemetryType.Umidade)]
    [InlineData(TelemetryType.Precipitacao)]
    public void Should_Support_All_Telemetry_Types(TelemetryType tipo)
    {
        // Arrange & Act
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            tipo,
            100.0
        );

        // Assert
        request.Tipo.ShouldBe(tipo);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(25.5)]
    [InlineData(-10.5)]
    [InlineData(100.0)]
    [InlineData(999999.99)]
    public void Should_Accept_Any_Double_Value(double valor)
    {
        // Arrange & Act
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            valor
        );

        // Assert
        request.Valor.ShouldBe(valor);
    }

    [Fact]
    public void Should_Accept_Past_DateTime()
    {
        // Arrange
        var pastDate = DateTime.UtcNow.AddDays(-7);

        // Act
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            pastDate,
            TelemetryType.Temperatura,
            25.5
        );

        // Assert
        request.DataMedicao.ShouldBe(pastDate);
        request.DataMedicao.ShouldBeLessThan(DateTime.UtcNow);
    }

    [Fact]
    public void Should_Accept_Future_DateTime()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);

        // Act
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            futureDate,
            TelemetryType.Temperatura,
            25.5
        );

        // Assert
        request.DataMedicao.ShouldBe(futureDate);
        request.DataMedicao.ShouldBeGreaterThan(DateTime.UtcNow);
    }

    [Fact]
    public void Should_Support_Collection_Of_Requests()
    {
        // Arrange & Act
        var requests = new List<SensorDataRequest>
        {
            new SensorDataRequest
            (
                Guid.NewGuid(),
                DateTime.UtcNow,
                TelemetryType.Temperatura,
                25.5
            ),
            new SensorDataRequest
            (
                Guid.NewGuid(),
                DateTime.UtcNow,
                TelemetryType.Umidade,
                65.0
            ),
            new SensorDataRequest
            (
                Guid.NewGuid(),
                DateTime.UtcNow,
                TelemetryType.Precipitacao,
                7.2
            )
        };

        // Assert
        requests.Count.ShouldBe(3);
        requests[0].Tipo.ShouldBe(TelemetryType.Temperatura);
        requests[1].Tipo.ShouldBe(TelemetryType.Umidade);
        requests[2].Tipo.ShouldBe(TelemetryType.Precipitacao);
    }
}

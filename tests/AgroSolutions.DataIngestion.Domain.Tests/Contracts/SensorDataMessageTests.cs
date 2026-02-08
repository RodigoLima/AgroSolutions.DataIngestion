using AgroSolutions.Contracts;
using Shouldly;

namespace AgroSolutions.DataIngestion.Domain.Tests.Contracts;

public class SensorDataMessageTests
{
    [Fact]
    public void Should_Create_Message_With_Valid_Properties()
    {
        // Arrange
        var talhaoId = Guid.NewGuid();
        var dataMedicao = DateTime.UtcNow;
        var tipo = TelemetryType.Temperatura;
        var valor = 25.5;

        // Act
        var message = new SensorDataMessage(talhaoId, dataMedicao, tipo, valor);

        // Assert
        message.TalhaoId.ShouldBe(talhaoId);
        message.DataMedicao.ShouldBe(dataMedicao);
        message.Tipo.ShouldBe(tipo);
        message.Valor.ShouldBe(valor);
    }

    [Fact]
    public void Should_Be_Record_Type()
    {
        // Arrange
        var talhaoId = Guid.NewGuid();
        var dataMedicao = DateTime.UtcNow;
        var tipo = TelemetryType.Temperatura;
        var valor = 25.5;

        var message1 = new SensorDataMessage(talhaoId, dataMedicao, tipo, valor);
        var message2 = new SensorDataMessage(talhaoId, dataMedicao, tipo, valor);

        // Act & Assert
        message1.ShouldBe(message2); // Records têm equality by value
        (message1 == message2).ShouldBeTrue();
    }

    [Fact]
    public void Should_Have_Different_Hash_Codes_For_Different_Values()
    {
        // Arrange
        var talhaoId1 = Guid.NewGuid();
        var talhaoId2 = Guid.NewGuid();
        var dataMedicao = DateTime.UtcNow;

        var message1 = new SensorDataMessage(talhaoId1, dataMedicao, TelemetryType.Temperatura, 25.5);
        var message2 = new SensorDataMessage(talhaoId2, dataMedicao, TelemetryType.Temperatura, 25.5);

        // Act & Assert
        message1.GetHashCode().ShouldNotBe(message2.GetHashCode());
    }

    [Fact]
    public void Should_Support_Deconstruction()
    {
        // Arrange
        var talhaoId = Guid.NewGuid();
        var dataMedicao = DateTime.UtcNow;
        var tipo = TelemetryType.Temperatura;
        var valor = 25.5;

        var message = new SensorDataMessage(talhaoId, dataMedicao, tipo, valor);

        // Act
        var (deconstructedTalhaoId, deconstructedDataMedicao, deconstructedTipo, deconstructedValor) = message;

        // Assert
        deconstructedTalhaoId.ShouldBe(talhaoId);
        deconstructedDataMedicao.ShouldBe(dataMedicao);
        deconstructedTipo.ShouldBe(tipo);
        deconstructedValor.ShouldBe(valor);
    }

    [Theory]
    [InlineData(TelemetryType.Temperatura, 25.5)]
    [InlineData(TelemetryType.Umidade, 65.0)]
    [InlineData(TelemetryType.Precipitacao, 7.2)]
    public void Should_Support_All_Telemetry_Types(TelemetryType tipo, double valor)
    {
        // Arrange
        var talhaoId = Guid.NewGuid();
        var dataMedicao = DateTime.UtcNow;

        // Act
        var message = new SensorDataMessage(talhaoId, dataMedicao, tipo, valor);

        // Assert
        message.Tipo.ShouldBe(tipo);
        message.Valor.ShouldBe(valor);
    }

    [Fact]
    public void Should_Support_With_Expression_For_Immutability()
    {
        // Arrange
        var talhaoId = Guid.NewGuid();
        var dataMedicao = DateTime.UtcNow;
        var message = new SensorDataMessage(talhaoId, dataMedicao, TelemetryType.Temperatura, 25.5);

        // Act
        var modifiedMessage = message with { Valor = 30.0 };

        // Assert
        message.Valor.ShouldBe(25.5); // Original não mudou
        modifiedMessage.Valor.ShouldBe(30.0); // Nova instância com valor diferente
        modifiedMessage.TalhaoId.ShouldBe(talhaoId); // Outros valores mantidos
    }

    [Fact]
    public void Should_Handle_Negative_Values()
    {
        // Arrange
        var talhaoId = Guid.NewGuid();
        var dataMedicao = DateTime.UtcNow;
        var valor = -10.5;

        // Act
        var message = new SensorDataMessage(talhaoId, dataMedicao, TelemetryType.Temperatura, valor);

        // Assert
        message.Valor.ShouldBe(valor);
    }

    [Fact]
    public void Should_Handle_Zero_Value()
    {
        // Arrange
        var talhaoId = Guid.NewGuid();
        var dataMedicao = DateTime.UtcNow;
        var valor = 0.0;

        // Act
        var message = new SensorDataMessage(talhaoId, dataMedicao, TelemetryType.Temperatura, valor);

        // Assert
        message.Valor.ShouldBe(0.0);
    }
}

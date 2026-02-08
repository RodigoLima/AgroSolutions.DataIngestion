using AgroSolutions.DataIngestion.Application.DTOs;
using AgroSolutions.DataIngestion.Application.Validators;
using FluentValidation.TestHelper;
using Shouldly;

namespace AgroSolutions.DataIngestion.Application.Tests.Validators;

public class SensorDataRequestValidatorTests
{
    private readonly SensorDataRequestValidator _validator;

    public SensorDataRequestValidatorTests()
    {
        _validator = new SensorDataRequestValidator();
    }

    [Fact]
    public void Should_Have_Error_When_TalhaoId_Is_Empty()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.Empty,
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            25.5
        );

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TalhaoId);
    }

    [Fact]
    public void Should_Not_Have_Error_When_TalhaoId_Is_Valid()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            25.5
        );

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.TalhaoId);
    }

    [Fact]
    public void Should_Have_Error_When_DataMedicao_Is_Default()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            default,
            TelemetryType.Temperatura,
            25.5
        );

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DataMedicao);
    }

    [Fact]
    public void Should_Have_Error_When_DataMedicao_Is_In_Future()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(2), // 2 horas no futuro
            TelemetryType.Temperatura,
            25.5
        );

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DataMedicao)
            .WithErrorMessage("DataMedicao não pode ser futura (tolerância de 1 hora)");
    }

    [Fact]
    public void Should_Not_Have_Error_When_DataMedicao_Is_Within_Tolerance()
    {
        // Arrange - Dentro da tolerância de 1 hora
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow.AddMinutes(30),
            TelemetryType.Temperatura,
            25.5
        );

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.DataMedicao);
    }

    [Fact]
    public void Should_Not_Have_Error_When_DataMedicao_Is_In_Past()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(-5),
            TelemetryType.Temperatura,
            25.5
        );

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.DataMedicao);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Should_Have_Error_When_Valor_Is_Invalid(double valorInvalido)
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            valorInvalido
        );

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Valor)
            .WithErrorMessage("Valor deve ser um número válido");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(25.5)]
    [InlineData(-10.0)]
    [InlineData(100.0)]
    public void Should_Not_Have_Error_When_Valor_Is_Valid(double valorValido)
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            valorValido
        );

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Valor);
    }

    [Fact]
    public void Should_Have_Error_When_Tipo_Is_Invalid()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            (TelemetryType)999, // Valor inválido
            25.5
        );

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Tipo);
    }

    [Theory]
    [InlineData(TelemetryType.Temperatura)]
    [InlineData(TelemetryType.Umidade)]
    [InlineData(TelemetryType.Precipitacao)]
    public void Should_Not_Have_Error_When_Tipo_Is_Valid(TelemetryType tipo)
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            tipo,
            25.5
        );

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Tipo);
    }

    [Fact]
    public void Should_Have_No_Errors_When_Request_Is_Completely_Valid()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow.AddMinutes(-5),
            TelemetryType.Temperatura,
            25.5
        );

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Multiple_Errors_When_Request_Is_Invalid()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.Empty, // Erro
            default, // Erro
            TelemetryType.Temperatura,
            double.NaN // Erro
        );

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TalhaoId);
        result.ShouldHaveValidationErrorFor(x => x.DataMedicao);
        result.ShouldHaveValidationErrorFor(x => x.Valor);
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(3);
    }
}

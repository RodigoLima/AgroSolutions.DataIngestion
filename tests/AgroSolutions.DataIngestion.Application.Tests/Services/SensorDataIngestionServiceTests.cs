using AgroSolutions.Contracts;
using AgroSolutions.DataIngestion.Application.DTOs;
using AgroSolutions.DataIngestion.Application.Interfaces;
using AgroSolutions.DataIngestion.Application.Services;
using AgroSolutions.DataIngestion.Application.Telemetry;
using FluentValidation;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace AgroSolutions.DataIngestion.Application.Tests.Services;

public class SensorDataIngestionServiceTests
{
    private readonly Mock<IPublishEndpoint> _publishEndpointMock;
    private readonly Mock<IValidator<SensorDataRequest>> _validatorMock;
    private readonly Mock<ILogger<SensorDataIngestionService>> _loggerMock;
    private readonly Mock<ISensorMetrics> _metricsMock;
    private readonly SensorDataIngestionService _service;

    public SensorDataIngestionServiceTests()
    {
        _publishEndpointMock = new Mock<IPublishEndpoint>();
        _validatorMock = new Mock<IValidator<SensorDataRequest>>();
        _loggerMock = new Mock<ILogger<SensorDataIngestionService>>();

        _metricsMock = new Mock<ISensorMetrics>();

        _service = new SensorDataIngestionService(
            _publishEndpointMock.Object,
            _validatorMock.Object,
            _loggerMock.Object,
            _metricsMock.Object);
    }

    [Fact]
    public async Task IngestAsync_Should_Publish_Message_When_Request_Is_Valid()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            25.5
        );

        _validatorMock
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());
            

        // Act
        await _service.IngestAsync(request);

        // Assert
        _publishEndpointMock.Verify(
            p => p.Publish(
                It.Is<SensorDataMessage>(m =>
                    m.TalhaoId == request.TalhaoId &&
                    m.DataMedicao == request.DataMedicao &&
                    m.Tipo == request.Tipo &&
                    m.Valor == request.Valor),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_Should_Throw_ValidationException_When_Request_Is_Invalid()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.Empty, // Inválido
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            25.5
        );

        var validationFailures = new List<FluentValidation.Results.ValidationFailure>
        {
            new FluentValidation.Results.ValidationFailure("TalhaoId", "TalhaoId não pode ser vazio")
        };

        _validatorMock
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult(validationFailures));

        // Act & Assert
        await Should.ThrowAsync<ValidationException>(async () =>
            await _service.IngestAsync(request));

        _publishEndpointMock.Verify(
            p => p.Publish(It.IsAny<SensorDataMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IngestAsync_Should_Record_Metric_When_Data_Is_Received()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            25.5
        );

        _validatorMock
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        // Act
        await _service.IngestAsync(request);

        // Assert
        _metricsMock.Verify(
            m => m.RecordSensorDataReceived(
                request.Tipo.ToString(),
                request.TalhaoId.ToString()),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_Should_Record_Metric_When_Data_Is_Published()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            TelemetryType.Umidade,
            65.0
        );

        _validatorMock
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        // Act
        await _service.IngestAsync(request);

        // Assert
        _metricsMock.Verify(
            m => m.RecordSensorDataPublished(request.Tipo.ToString()),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_Should_Record_Metric_When_Validation_Fails()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.Empty,
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            25.5
        );

        var validationFailures = new List<FluentValidation.Results.ValidationFailure>
        {
            new FluentValidation.Results.ValidationFailure("TalhaoId", "TalhaoId não pode ser vazio")
        };

        _validatorMock
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult(validationFailures));

        // Act & Assert
        await Should.ThrowAsync<ValidationException>(async () =>
            await _service.IngestAsync(request));

        _metricsMock.Verify(
            m => m.RecordSensorDataFailed(
                request.Tipo.ToString(),
                "validation_error"),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_Should_Record_Processing_Duration_On_Success()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            25.5
        );

        _validatorMock
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        // Act
        await _service.IngestAsync(request);

        // Assert
        _metricsMock.Verify(
            m => m.RecordProcessingDuration(
                It.IsAny<double>(),
                request.Tipo.ToString(),
                true), // success = true
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_Should_Record_Processing_Duration_On_Failure()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.Empty,
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            25.5
        );

        var validationFailures = new List<FluentValidation.Results.ValidationFailure>
        {
            new FluentValidation.Results.ValidationFailure("TalhaoId", "TalhaoId não pode ser vazio")
        };

        _validatorMock
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult(validationFailures));

        // Act & Assert
        await Should.ThrowAsync<ValidationException>(async () =>
            await _service.IngestAsync(request));

        _metricsMock.Verify(
            m => m.RecordProcessingDuration(
                It.IsAny<double>(),
                request.Tipo.ToString(),
                false), // success = false
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_Should_Map_Request_To_Message_Correctly()
    {
        // Arrange
        var talhaoId = Guid.NewGuid();
        var dataMedicao = DateTime.UtcNow;
        var tipo = TelemetryType.Umidade;
        var valor = 65.5;

        var request = new SensorDataRequest
        (
            talhaoId,
            dataMedicao,
            tipo,
            valor
        );

        _validatorMock
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        SensorDataMessage? capturedMessage = null;
        _publishEndpointMock
            .Setup(p => p.Publish(It.IsAny<SensorDataMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SensorDataMessage, CancellationToken>((msg, ct) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        // Act
        await _service.IngestAsync(request);

        // Assert
        capturedMessage.ShouldNotBeNull();
        capturedMessage.TalhaoId.ShouldBe(talhaoId);
        capturedMessage.DataMedicao.ShouldBe(dataMedicao);
        capturedMessage.Tipo.ShouldBe(tipo);
        capturedMessage.Valor.ShouldBe(valor);
    }

    [Fact]
    public async Task IngestAsync_Should_Log_Information_When_Starting()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            25.5
        );

        _validatorMock
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        // Act
        await _service.IngestAsync(request);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Iniciando ingestão")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_Should_Log_Warning_When_Validation_Fails()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.Empty,
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            25.5
        );

        var validationFailures = new List<FluentValidation.Results.ValidationFailure>
        {
            new FluentValidation.Results.ValidationFailure("TalhaoId", "TalhaoId não pode ser vazio")
        };

        _validatorMock
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult(validationFailures));

        // Act & Assert
        await Should.ThrowAsync<ValidationException>(async () =>
            await _service.IngestAsync(request));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Validação falhou")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_Should_Log_Success_When_Published()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            25.5
        );

        _validatorMock
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        // Act
        await _service.IngestAsync(request);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("publicados com sucesso")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(TelemetryType.Temperatura, 25.5)]
    [InlineData(TelemetryType.Umidade, 65.0)]
    [InlineData(TelemetryType.Precipitacao, 7.2)]
    public async Task IngestAsync_Should_Handle_All_Telemetry_Types(TelemetryType tipo, double valor)
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            tipo,
            valor
        );

        _validatorMock
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        // Act
        await _service.IngestAsync(request);

        // Assert
        _publishEndpointMock.Verify(
            p => p.Publish(
                It.Is<SensorDataMessage>(m => m.Tipo == tipo && m.Valor == valor),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_Should_Use_Provided_CancellationToken()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            25.5
        );

        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _validatorMock
            .Setup(v => v.ValidateAsync(request, cancellationToken))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        // Act
        await _service.IngestAsync(request, cancellationToken);

        // Assert
        _validatorMock.Verify(
            v => v.ValidateAsync(request, cancellationToken),
            Times.Once);

        _publishEndpointMock.Verify(
            p => p.Publish(It.IsAny<SensorDataMessage>(), cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task IngestBatchAsync_Should_Process_Multiple_Requests()
    {
        // Arrange
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

        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SensorDataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        // Act
        await _service.IngestBatchAsync(requests);

        // Assert
        _publishEndpointMock.Verify(
            p => p.Publish(It.IsAny<SensorDataMessage>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task IngestBatchAsync_Should_Log_Batch_Start_And_Complete()
    {
        // Arrange
        var requests = new List<SensorDataRequest>
        {
            new SensorDataRequest
            (
                Guid.NewGuid(),
                DateTime.UtcNow,
                TelemetryType.Temperatura,
                25.5
            )
        };

        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SensorDataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        // Act
        await _service.IngestBatchAsync(requests);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Iniciando ingestão em lote")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("concluída com sucesso")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_Should_Rethrow_ValidationException()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.Empty,
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            25.5
        );

        var validationFailures = new List<FluentValidation.Results.ValidationFailure>
        {
            new FluentValidation.Results.ValidationFailure("TalhaoId", "TalhaoId não pode ser vazio")
        };

        _validatorMock
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult(validationFailures));

        // Act & Assert
        var exception = await Should.ThrowAsync<ValidationException>(async () =>
            await _service.IngestAsync(request));

        exception.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task IngestAsync_Should_Rethrow_Exception_When_Publishing_Fails()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            25.5
        );

        _validatorMock
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        _publishEndpointMock
            .Setup(p => p.Publish(It.IsAny<SensorDataMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("RabbitMQ connection failed"));

        // Act & Assert
        var exception = await Should.ThrowAsync<Exception>(async () =>
            await _service.IngestAsync(request));

        exception.Message.ShouldBe("RabbitMQ connection failed");
    }

    [Fact]
    public async Task IngestAsync_Should_Record_Failed_Metric_When_Publishing_Fails()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            25.5
        );

        _validatorMock
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        _publishEndpointMock
            .Setup(p => p.Publish(It.IsAny<SensorDataMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("RabbitMQ connection failed"));

        // Act & Assert
        await Should.ThrowAsync<Exception>(async () =>
            await _service.IngestAsync(request));

        _metricsMock.Verify(
            m => m.RecordSensorDataFailed(
                request.Tipo.ToString(),
                "processing_error"),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_Should_Log_Error_When_Exception_Occurs()
    {
        // Arrange
        var request = new SensorDataRequest
        (
            Guid.NewGuid(),
            DateTime.UtcNow,
            TelemetryType.Temperatura,
            25.5
        );

        _validatorMock
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        var expectedException = new Exception("Test exception");
        _publishEndpointMock
            .Setup(p => p.Publish(It.IsAny<SensorDataMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        await Should.ThrowAsync<Exception>(async () =>
            await _service.IngestAsync(request));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erro ao processar")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestBatchAsync_Should_Record_Metrics_For_All_Requests()
    {
        // Arrange
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
            )
        };

        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SensorDataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        // Act
        await _service.IngestBatchAsync(requests);

        // Assert
        _metricsMock.Verify(
            m => m.RecordSensorDataReceived(It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(2));

        _metricsMock.Verify(
            m => m.RecordSensorDataPublished(It.IsAny<string>()),
            Times.Exactly(2));

        _metricsMock.Verify(
            m => m.RecordProcessingDuration(It.IsAny<double>(), It.IsAny<string>(), true),
            Times.Exactly(2));
    }
}
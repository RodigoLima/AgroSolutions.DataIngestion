using Shouldly;

namespace AgroSolutions.DataIngestion.Domain.Tests.Enums;

public class TelemetryTypeTests
{
    [Fact]
    public void Should_Have_Temperatura_With_Value_0()
    {
        // Arrange & Act
        var tipo = TelemetryType.Temperatura;

        // Assert
        ((int)tipo).ShouldBe(0);
    }

    [Fact]
    public void Should_Have_Umidade_With_Value_1()
    {
        // Arrange & Act
        var tipo = TelemetryType.Umidade;

        // Assert
        ((int)tipo).ShouldBe(1);
    }

    [Fact]
    public void Should_Have_Precipitacao_With_Value_2()
    {
        // Arrange & Act
        var tipo = TelemetryType.Precipitacao;

        // Assert
        ((int)tipo).ShouldBe(2);
    }

    [Fact]
    public void Should_Have_Exactly_3_Values()
    {
        // Arrange
        var allValues = Enum.GetValues<TelemetryType>();

        // Act & Assert
        allValues.Length.ShouldBe(3);
    }

    [Fact]
    public void Should_Convert_From_Int_To_Enum()
    {
        // Arrange & Act
        var tipo = (TelemetryType)0;

        // Assert
        tipo.ShouldBe(TelemetryType.Temperatura);
    }

    [Theory]
    [InlineData(0, TelemetryType.Temperatura)]
    [InlineData(1, TelemetryType.Umidade)]
    [InlineData(2, TelemetryType.Precipitacao)]
    public void Should_Map_Correctly_Between_Int_And_Enum(int intValue, TelemetryType expectedType)
    {
        // Act
        var tipo = (TelemetryType)intValue;

        // Assert
        tipo.ShouldBe(expectedType);
        ((int)tipo).ShouldBe(intValue);
    }

    [Theory]
    [InlineData("Temperatura", TelemetryType.Temperatura)]
    [InlineData("Umidade", TelemetryType.Umidade)]
    [InlineData("Precipitacao", TelemetryType.Precipitacao)]
    public void Should_TryParse_Return_True_For_Valid_String(string tipoString, TelemetryType expectedType)
    {
        // Act
        var success = Enum.TryParse<TelemetryType>(tipoString, out var tipo);

        // Assert
        success.ShouldBeTrue();
        tipo.ShouldBe(expectedType);
    }

    [Fact]
    public void Should_TryParse_Return_False_For_Invalid_String()
    {
        // Act
        var success = Enum.TryParse<TelemetryType>("InvalidType", out var tipo);

        // Assert
        success.ShouldBeFalse();
        tipo.ShouldBe(default);
    }

    [Fact]
    public void Should_IsDefined_Return_True_For_Valid_Values()
    {
        // Act & Assert
        for (int i = 1; i <= 2; i++)
        {
            Enum.IsDefined(typeof(TelemetryType), i).ShouldBeTrue();
        }
    }

    [Theory]
    [InlineData(9)]
    [InlineData(999)]
    [InlineData(-1)]
    public void Should_IsDefined_Return_False_For_Invalid_Values(int invalidValue)
    {
        // Act
        var isDefined = Enum.IsDefined(typeof(TelemetryType), invalidValue);

        // Assert
        isDefined.ShouldBeFalse();
    }

    [Fact]
    public void Should_Use_In_Switch_Expression()
    {
        // Arrange
        var tipo = TelemetryType.Temperatura;

        // Act
        string resultado = tipo switch
        {
            TelemetryType.Temperatura => "Medindo temperatura",
            TelemetryType.Umidade => "Medindo umidade",
            _ => "Outro tipo",
        };

        // Assert
        resultado.ShouldBe("Medindo temperatura");
    }
}

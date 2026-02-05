using AgroSolutions.DataIngestion.Application.DTOs;
using FluentValidation;

namespace AgroSolutions.DataIngestion.Application.Validators;

public class SensorDataRequestValidator : AbstractValidator<SensorDataRequest>
{
    public SensorDataRequestValidator()
    {
        RuleFor(x => x.TalhaoId)
            .NotEmpty()
            .WithMessage("TalhaoId é obrigatório");

        RuleFor(x => x.DataMedicao)
            .NotEmpty()
            .WithMessage("DataMedicao é obrigatória")
            .LessThanOrEqualTo(DateTime.UtcNow.AddHours(1))
            .WithMessage("DataMedicao não pode ser futura (tolerância de 1 hora)");

        RuleFor(x => x.Tipo)
            .IsInEnum()
            .WithMessage("Tipo de telemetria inválido");

        RuleFor(x => x.Valor)
            .Must(BeAValidNumber)
            .WithMessage("Valor deve ser um número válido");
    }

    private bool BeAValidNumber(double valor)
    {
        return !double.IsNaN(valor) && !double.IsInfinity(valor);
    }
}

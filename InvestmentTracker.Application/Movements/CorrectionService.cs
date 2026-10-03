using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Common.Validation;

namespace InvestmentTracker.Application.Movements
{
    public sealed class CorrectionService(ICorrectionRepository repository, TimeProvider clock) : ICorrectionService
    {
        private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
        private CorrectionInput Validate(CorrectionInput input)
        {
            if (input.Operation is null || input.Operation.Date < new DateOnly(1900, 1, 1) || input.Operation.Date > Today)
                throw new InputValidationException("Informe uma operação com data entre 01/01/1900 e hoje.");
            return input with { Reason = InputRules.RequiredText(input.Reason, "O motivo da correção", 400) };
        }
        public Task<CorrectionOperation?> DraftAsync(int portfolioId, int movementId, CancellationToken ct)
            => repository.DraftAsync(portfolioId, movementId, ct);
        public Task<CorrectionSimulationDto?> SimulateAsync(int portfolioId, CorrectionInput input, CancellationToken ct)
        {
            input = Validate(input);
            return repository.RunAsync(portfolioId, input, null, null, s => CorrectionCalculator.Calculate(s, input), ct);
        }
        public Task<CorrectionSimulationDto?> ApplyAsync(int portfolioId, ApplyCorrectionDto dto, CancellationToken ct)
        {
            if (dto.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(dto.Token)) throw new InputValidationException("Simule a correção antes de confirmar.");
            var input = Validate(dto.Input);
            return repository.RunAsync(portfolioId, input, dto.RequestId, dto.Token, s => CorrectionCalculator.Calculate(s, input), ct);
        }
    }
}

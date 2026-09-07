using InvestmentTracker.Application.Common.Exceptions;

namespace InvestmentTracker.Application.Common.Validation
{
    internal static class InputRules
    {
        public static string RequiredText(string? value, string field, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InputValidationException($"{field} é obrigatório.");
            }

            var normalized = value.Trim();
            if (normalized.Length > maxLength)
            {
                throw new InputValidationException($"{field} deve ter no máximo {maxLength} caracteres.");
            }

            return normalized;
        }

        public static string? OptionalText(string? value, string field, int maxLength)
        {
            return string.IsNullOrWhiteSpace(value) ? null : RequiredText(value, field, maxLength);
        }
    }
}

using InvestmentTracker.Application.Common.Exceptions;

namespace InvestmentTracker.Application.AssetTypes.Exceptions
{
    public sealed class AssetTypeValidationException(string message) : InputValidationException(message)
    {
    }
}

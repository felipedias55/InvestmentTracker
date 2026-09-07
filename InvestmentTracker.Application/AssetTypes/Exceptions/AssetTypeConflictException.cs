using InvestmentTracker.Application.Common.Exceptions;

namespace InvestmentTracker.Application.AssetTypes.Exceptions
{
    public sealed class AssetTypeConflictException(string message, Exception? innerException = null)
        : ResourceConflictException(message, innerException)
    {
    }
}

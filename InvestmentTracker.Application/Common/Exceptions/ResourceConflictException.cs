namespace InvestmentTracker.Application.Common.Exceptions
{
    public class ResourceConflictException(string message, Exception? innerException = null)
        : Exception(message, innerException)
    {
    }
}
